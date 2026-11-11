using MEP_Compare_Project_ver1.Models;

namespace MEP_Compare_Project_ver1.Matching;

public record MatchItem(ElementData? Host, ElementData? Link, string Status, string Notes)
{
    public string Method { get; init; } = "GUID";
    public string CandidateLinkedIds { get; init; } = "";
}
public static class GuidMatcher
{
    // IFC compressed identifiers are case sensitive. Never lowercase or fuzzy match.
    public static string Key(string value) => value.Trim();
    public static IReadOnlyList<MatchItem> Match(IReadOnlyList<ElementData> hosts, IReadOnlyList<ElementData> links, bool includeLinkOnly)
    {
        var result = new List<MatchItem>();
        var h = hosts.Where(x=>Key(x.Guid)!="").GroupBy(x=>Key(x.Guid),StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.ToList(),StringComparer.Ordinal);
        var l = links.Where(x=>Key(x.Guid)!="").GroupBy(x=>Key(x.Guid),StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.ToList(),StringComparer.Ordinal);
        foreach(var item in hosts)
        {
            var key=Key(item.Guid);
            if(key=="") { result.Add(new(item,null,"MissingSourceGuid","Parameter nguồn trống hoặc không có.")); continue; }
            l.TryGetValue(key,out var targets);
            if(h[key].Count>1 || targets?.Count>1)
            {
                result.Add(new(item,null,"DuplicateGuid",$"Host IDs: {string.Join(",",h[key].Select(x=>x.Id))}; Linked IDs: {string.Join(",",targets?.Select(x=>x.Id) ?? [])}"));
                continue;
            }
            result.Add(targets?.Count==1 ? new(item,targets[0],"Paired","") : new(item,null,"MissingInIfc","Không tìm thấy IFC.IfcGUID tương ứng."));
        }
        if(includeLinkOnly)
            foreach(var item in links)
            {
                var key=Key(item.Guid);
                if(key=="") result.Add(new(null,item,"MissingIfcGuid","IFC.IfcGUID trống."));
                else if(!h.ContainsKey(key)) result.Add(new(null,item,l[key].Count>1 ? "DuplicateGuid" : "OnlyInIfc","Không có host trong phạm vi so sánh."));
            }
        return result;
    }
}
