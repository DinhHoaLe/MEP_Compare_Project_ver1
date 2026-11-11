using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows;
using System.Windows.Interop;
using MEP_Compare_Project_ver1.Comparison;
using MEP_Compare_Project_ver1.Geometry;
using MEP_Compare_Project_ver1.Matching;
using MEP_Compare_Project_ver1.Models;
using MEP_Compare_Project_ver1.Readers;
using MEP_Compare_Project_ver1.UI;

namespace MEP_Compare_Project_ver1.Command;

[Transaction(TransactionMode.Manual)]
public sealed class CompareCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData,ref string message,ElementSet elements)
    {
        var ui=commandData.Application.ActiveUIDocument;
        if(ui is null || ui.Document.IsFamilyDocument)
        { TaskDialog.Show("Compare Project Ver1","Mở một project Revit trước khi chạy.");return Result.Cancelled; }
        try
        {
            if(ui.Document.Application.VersionNumber!="2025") throw new InvalidOperationException("DLL này dành cho Revit 2025.");
            var links=ModelReader.Links(ui.Document);
            if(!links.Any(l=>l.Loaded)) { TaskDialog.Show("Compare Project Ver1","Không có Revit/IFC link đang loaded.");return Result.Cancelled; }
            var initialSelection=ui.Selection.GetElementIds().ToList();
            RunOptions? options=null;bool configure=true;
            IReadOnlyList<ResultRow> rows=[];
            while(true)
            {
                if(configure)
                {
                    var window=new CompareWindow(links,options);Own(window,commandData.Application.MainWindowHandle);
                    if(window.ShowDialog()!=true) return Result.Cancelled;
                    options=window.Options!;
                    // All reads happen synchronously after the configuration modal closes,
                    // inside Execute. No transaction, background task, or document write.
                    var data=ModelReader.Read(ui,options,initialSelection);
                    if(data.Hosts.Count==0) { TaskDialog.Show("Compare Project Ver1","Không có Ducts/Pipes native trong phạm vi đã chọn.");continue; }
                    var scopedIds=data.Hosts.Select(h=>h.Id).ToHashSet();
                    var matchHosts=options.Scope==Scope.WholeModel ? data.Hosts : ModelReader.Read(ui,options with { Scope=Scope.WholeModel },initialSelection).Hosts;
                    rows=PairingService.Match(matchHosts,data.Links,options.Scope==Scope.WholeModel,options)
                        .Where(pair=>options.Scope==Scope.WholeModel || pair.Host is not null && scopedIds.Contains(pair.Host.Id))
                        .Select(pair=>ComparisonService.Compare(pair,options)).ToList();
                }
                configure=false;
                var result=new ResultsWindow(rows,options!,ui.Document.Title,links.First(l=>l.Id==options!.LinkId).Name);
                Own(result,commandData.Application.MainWindowHandle);result.ShowDialog();
                if(result.Action==ResultAction.Close) return Result.Succeeded;
                if(result.Action==ResultAction.Configure) { configure=true;continue; }
                try { Navigate(ui,options!.LinkId,result.Selected!,result.Action); }
                catch(Exception ex) { TaskDialog.Show("Zoom / Select",ex.Message); }
                // Let the user inspect the model before reopening this command.
                return Result.Succeeded;
            }
        }
        catch(Exception ex) { message=ex.ToString();TaskDialog.Show("Compare Project Ver1",ex.Message);return Result.Failed; }
    }
    private static void Own(Window window,IntPtr handle) => new WindowInteropHelper(window).Owner=handle;
    private static void Navigate(UIDocument ui,long linkId,ResultRow row,ResultAction action)
    {
        if(action==ResultAction.SelectLinked)
        {
            var link=(RevitLinkInstance)ui.Document.GetElement(new ElementId(linkId));
            var linked=link.GetLinkDocument()?.GetElement(new ElementId(row.LinkedId!.Value)) ?? throw new InvalidOperationException("Linked element không còn tồn tại.");
            ui.Selection.SetReferences(new List<Reference> { new Reference(linked).CreateLinkReference(link) });return;
        }
        if(row.HostId.HasValue) ui.Selection.SetElementIds(new List<ElementId> { new(row.HostId.Value) });
        if(action==ResultAction.SelectHost) return;
        // UI zoom only: never create/change a view, crop region or section box.
        var points=new List<XYZ>();
        if(row.HostId.HasValue) AddBounds(ui.Document.GetElement(new ElementId(row.HostId.Value)),Transform.Identity,points);
        var instance=ui.Document.GetElement(new ElementId(linkId)) as RevitLinkInstance;
        if(row.LinkedId.HasValue && instance?.GetLinkDocument() is Document linkedDoc)
            AddBounds(linkedDoc.GetElement(new ElementId(row.LinkedId.Value)),instance.GetTotalTransform(),points);
        if(points.Count==0) throw new InvalidOperationException("Không có bounding box để Zoom.");
        var view=ui.GetOpenUIViews().FirstOrDefault(v=>v.ViewId==ui.ActiveView.Id) ?? throw new InvalidOperationException("Không tìm thấy UI view đang mở.");
        var min=new XYZ(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z));
        var max=new XYZ(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z));
        var pad=new XYZ(300/GeometryReader.MmPerFoot,300/GeometryReader.MmPerFoot,300/GeometryReader.MmPerFoot);
        view.ZoomAndCenterRectangle(min-pad,max+pad);
    }
    private static void AddBounds(Element? e,Transform transform,List<XYZ> points)
    {
        var box=e?.get_BoundingBox(null);if(box is null) return;
        for(int x=0;x<2;x++) for(int y=0;y<2;y++) for(int z=0;z<2;z++)
            points.Add(transform.OfPoint(box.Transform.OfPoint(new XYZ(x==0?box.Min.X:box.Max.X,y==0?box.Min.Y:box.Max.Y,z==0?box.Min.Z:box.Max.Z))));
    }
}
