using System.IO;
using System.Text.Json;
using MEP_Compare_Project_ver1.Models;

namespace MEP_Compare_Project_ver1.Matching;

public record MappingContext(string HostDocument, string LinkInstance, string LinkedDocument);
public record ManualPair(string HostUniqueId, string LinkedUniqueId);
public record MappingFile(int Version, MappingContext Context, List<ManualPair> Pairs);

// Independent of Revit: persistent identity never relies on numeric ElementId or IFC GUID alone.
public sealed class MappingSession
{
    public MappingContext Context { get; }
    public IReadOnlyList<ElementData> Hosts { get; private set; }
    public IReadOnlyList<ElementData> Links { get; private set; }
    private List<ManualPair> pairs=[];
    public int Count => pairs.Count;
    public bool Dirty { get; private set; }
    public MappingSession(MappingContext context, IReadOnlyList<ElementData> hosts, IReadOnlyList<ElementData> links)
    { Context=context;Hosts=hosts;Links=links; }
    public void UpdateData(IReadOnlyList<ElementData> hosts,IReadOnlyList<ElementData> links)
    {
        var oldHosts=Hosts;var oldLinks=Links;
        Hosts=hosts;Links=links;
        try { Validate(pairs); }
        catch { Hosts=oldHosts;Links=oldLinks;throw; }
    }
    private void Validate(IReadOnlyList<ManualPair> values)
    {
        if(values.Any(p=>p is null || string.IsNullOrWhiteSpace(p.HostUniqueId) || string.IsNullOrWhiteSpace(p.LinkedUniqueId)))
            throw new InvalidOperationException("Mapping thiếu UniqueId.");
        if(values.Select(p=>p.HostUniqueId).Distinct().Count()!=values.Count || values.Select(p=>p.LinkedUniqueId).Distinct().Count()!=values.Count)
            throw new InvalidOperationException("Mapping phải là một–một; có Host hoặc IFC bị dùng nhiều lần.");
        foreach(var p in values)
        {
            var h=Hosts.SingleOrDefault(e=>e.UniqueId==p.HostUniqueId);
            var l=Links.SingleOrDefault(e=>e.UniqueId==p.LinkedUniqueId);
            if(h is null || l is null) throw new InvalidOperationException("Mapping có phần tử đã mất, được tạo lại hoặc ngoài nhóm Duct/Pipe đang chọn. File chưa được áp dụng.");
            if(h.Category!=l.Category) throw new InvalidOperationException("Không thể ghép Duct với Pipe.");
        }
    }
    public void Confirm(long hostId,long linkedId)
    {
        var h=Hosts.Single(e=>e.Id==hostId);var l=Links.Single(e=>e.Id==linkedId);
        if(pairs.Any(p=>p.LinkedUniqueId==l.UniqueId && p.HostUniqueId!=h.UniqueId))
            throw new InvalidOperationException("IFC này đã được xác nhận cho host khác. Hãy bỏ cặp đó trước.");
        var next=pairs.Where(p=>p.HostUniqueId!=h.UniqueId).Append(new ManualPair(h.UniqueId,l.UniqueId)).ToList();
        Validate(next);pairs=next;Dirty=true;
    }
    public void Remove(long hostId)
    {
        var h=Hosts.Single(e=>e.Id==hostId);
        if(pairs.RemoveAll(p=>p.HostUniqueId==h.UniqueId)>0) Dirty=true;
    }
    public IReadOnlyList<MatchItem> Match(bool includeLinkOnly,RunOptions options)
    {
        Validate(pairs);
        var manual=pairs.Select(p=>new MatchItem(Hosts.Single(h=>h.UniqueId==p.HostUniqueId),Links.Single(l=>l.UniqueId==p.LinkedUniqueId),"Paired","Cặp được người dùng xác nhận.") { Method="Manual" }).ToList();
        var hostIds=manual.Select(p=>p.Host!.Id).ToHashSet();var linkIds=manual.Select(p=>p.Link!.Id).ToHashSet();
        // Reserve explicit pairs before GUID and geometry, across the full model scope.
        return manual.Concat(PairingService.Match(Hosts.Where(h=>!hostIds.Contains(h.Id)).ToList(),Links.Where(l=>!linkIds.Contains(l.Id)).ToList(),includeLinkOnly,options)).ToList();
    }
    public void Save(string path)
    {
        Validate(pairs);
        var full=Path.GetFullPath(path);var temp=full+"."+System.Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temp,JsonSerializer.Serialize(new MappingFile(1,Context,pairs),new JsonSerializerOptions { WriteIndented=true }));
            File.Move(temp,full,true);Dirty=false;
        }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    public void Load(string path)
    {
        var file=JsonSerializer.Deserialize<MappingFile>(File.ReadAllText(path)) ?? throw new InvalidOperationException("File mapping rỗng.");
        if(file.Version!=1 || file.Context!=Context) throw new InvalidOperationException("Mapping khác phiên bản, model hoặc link instance. File chưa được áp dụng.");
        if(file.Pairs is null) throw new InvalidOperationException("File thiếu danh sách cặp.");
        Validate(file.Pairs);pairs=file.Pairs.ToList();Dirty=false;
    }
}
