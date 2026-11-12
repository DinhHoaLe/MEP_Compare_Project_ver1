using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MEP_Compare_Project_ver1.Geometry;
using MEP_Compare_Project_ver1.Models;

namespace MEP_Compare_Project_ver1.Readers;

public static class ModelReader
{
    public static IReadOnlyList<LinkChoice> Links(Document document) => new FilteredElementCollector(document)
        .OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()
        .Select(l=>new LinkChoice(l.Id.Value,l.Name,l.GetLinkDocument()!=null,
            l.Name.Contains(".ifc",StringComparison.OrdinalIgnoreCase))).ToList();
    public static string ParameterText(Element e,string name)
    {
        if(string.IsNullOrWhiteSpace(name)) return "";
        Parameter? p;
        if(System.Guid.TryParse(name,out var guid)) p=e.get_Parameter(guid);
        else
        {
            var all=e.GetParameters(name);
            // Qualified labels from property groups are not necessarily Revit Definition.Name.
            // Prefer an exact name; accept only these explicit, known label aliases.
            if(all.Count==0)
            {
                var actual=name switch { "Revit.test"=>"test", "IFC.IfcGUID"=>"IfcGUID", "IFC.IfcTag"=>"IfcTag", _=>name };
                if(actual!=name) all=e.GetParameters(actual);
            }
            if(all.Count>1) throw new InvalidOperationException($"Parameter '{name}' trùng tên trên ID {e.Id.Value}; hãy nhập shared parameter GUID.");
            p=all.SingleOrDefault();
        }
        return p?.StorageType==StorageType.String?p.AsString()?.Trim() ?? "":"";
    }
    private static string? Category(Element e)
    {
        var id=e.Category?.Id.Value;
        if(id==(long)BuiltInCategory.OST_DuctCurves) return "Ducts";
        if(id==(long)BuiltInCategory.OST_PipeCurves) return "Pipes";
        var entity=ParameterText(e,"IFC.IfcExportAs");
        if(entity=="") entity=ParameterText(e,"IfcEntity");
        if(entity.StartsWith("IfcDuctSegment",StringComparison.OrdinalIgnoreCase)) return "Ducts";
        if(entity.StartsWith("IfcPipeSegment",StringComparison.OrdinalIgnoreCase)) return "Pipes";
        return null;
    }
    public static (List<ElementData> Hosts,List<ElementData> Links) Read(UIDocument ui,RunOptions options,ICollection<ElementId> initialSelection)
    {
        var doc=ui.Document;
        if(doc.GetElement(new ElementId(options.LinkId)) is not RevitLinkInstance link || link.GetLinkDocument() is not Document linkedDoc)
            throw new InvalidOperationException("Link đã unload hoặc không còn tồn tại.");
        var view=doc.ActiveView;
        if(options.Scope==Scope.ActiveView && !FilteredElementCollector.IsViewValidForElementIteration(doc,view.Id))
            throw new InvalidOperationException("View hiện tại không hỗ trợ thu thập phần tử.");
        IEnumerable<Element> native=options.Scope switch
        {
            Scope.Selection=>initialSelection.Select(doc.GetElement).Where(e=>e!=null),
            Scope.ActiveView=>new FilteredElementCollector(doc,view.Id).WhereElementIsNotElementType(),
            _=>new FilteredElementCollector(doc).WhereElementIsNotElementType()
        };
        bool Wanted(string? category)=>category=="Ducts" && options.Ducts || category=="Pipes" && options.Pipes;
        var hosts=new List<ElementData>();var links=new List<ElementData>();
        foreach(var e in native.Where(e=>e is MEPCurve))
        {
            var category=Category(e);
            if(Wanted(category)) hosts.Add(ReadOne(e,category!,options.SourceParameter,false,Transform.Identity));
        }
        var transform=link.GetTotalTransform();
        foreach(var e in new FilteredElementCollector(linkedDoc).WhereElementIsNotElementType())
        {
            var category=Category(e);
            if(Wanted(category)) links.Add(ReadOne(e,category!,"IFC.IfcGUID",true,transform));
        }
        return (hosts,links);
    }
    private static ElementData ReadOne(Element e,string category,string source,bool linked,Transform t)
    {
        var key=ParameterText(e,source);
        var tag=linked?ParameterText(e,"IFC.IfcTag"):"";
        AxisGeometry? geometry=null;string issue="";
        try { geometry=GeometryReader.Read(e,t); }
        catch(Exception ex) when(ex is not OutOfMemoryException) { issue=ex.Message; }
        return new(e.Id.Value,e.Name,category,key,tag,geometry,issue,linked) { UniqueId=e.UniqueId };
    }
}
