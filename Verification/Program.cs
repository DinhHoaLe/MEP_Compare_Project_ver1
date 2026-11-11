using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MEP_Compare_Project_ver1.Models;
using MEP_Compare_Project_ver1.Matching;
using MEP_Compare_Project_ver1.Comparison;
using MEP_Compare_Project_ver1.Reporting;
using MEP_Compare_Project_ver1.UI;

internal static class Program
{
    static int checks;
    static void Check(bool condition,string description) { if(!condition) throw new Exception(description);checks++;Console.WriteLine("PASS "+description); }
    static ElementData Element(long id,string guid,AxisGeometry? geometry,bool linked=false) => new(id,"Synthetic","Ducts",guid,"test-tag",geometry,geometry is null?"synthetic unsupported":"",linked);
    static readonly RunOptions Options=new(1,true,true,Scope.WholeModel,"Revit.test",1,1,1,0.1);
    static ResultRow Compare(AxisGeometry a,AxisGeometry b) => ComparisonService.Compare(new(Element(1,"A",a),Element(2,"A",b,true),"Paired",""),Options);
    [STAThread]
    static void Main(string[] args)
    {
        var axis=new AxisGeometry(new(0,0,0),new(2000,0,0),Shape.Rectangular,400,200,new(0,1,0),"Synthetic verification");
        Check(Compare(axis,axis).Status=="Match","identical geometry");
        Check(Compare(axis,axis with { Start=axis.End,End=axis.Start }).Status=="Match","reversed endpoints");
        var offset=new Vec(618.5,0,0);
        var moved=Compare(axis,axis with { Start=axis.Start+offset,End=axis.End+offset });
        Check(moved.Status.StartsWith("Different") && Math.Abs(moved.DeltaXmm!.Value-618.5)<1e-8 && moved.DeltaYmm==0,"signed IFC minus host translation (synthetic, not model evidence)");
        Check(Compare(axis,axis with { End=new(2200,0,0) }).Status.Contains("Length"),"length error");
        Check(Compare(axis,axis with { WidthMm=410 }).Status.Contains("Size"),"section size error");
        Check(Compare(axis,axis with { WidthMm=200,HeightMm=400,SectionX=new(0,0,1) }).Status=="Match","swapped rectangular section axes");
        Check(Compare(axis,axis with { SectionX=new(0,Math.Cos(Math.PI/6),Math.Sin(Math.PI/6)) }).Status.Contains("Roll"),"rectangular roll error");
        Check(Compare(axis,axis with { Shape=Shape.Round }).Status=="SectionMismatch","section shape mismatch");
        Check(Compare(axis,axis with { End=new(2000,200,0) }).Status.Contains("Direction"),"direction error");
        var empty=ComparisonService.Compare(new(Element(1,"A",null),Element(2,"A",axis,true),"Paired",""),Options);
        Check(empty.Status=="GeometryUnsupported" && empty.DeltaXmm is null,"unsupported is not match or zero error");
        Check(GuidMatcher.Match([Element(1," A ",axis)],[Element(2,"A",axis,true)],true).Single().Status=="Paired","trim GUID whitespace");
        Check(GuidMatcher.Match([Element(1,"A",axis)],[Element(2,"a",axis,true)],true).Count==2,"case-sensitive IFC identifier");
        Check(GuidMatcher.Match([Element(1,"A",axis),Element(3,"A",axis)],[Element(2,"A",axis,true)],true).All(x=>x.Status=="DuplicateGuid"),"duplicate source never arbitrarily paired");
        Check(GuidMatcher.Match([Element(1,"A",axis)],[Element(2,"A",axis,true),Element(4,"A",axis,true)],true).Single().Status=="DuplicateGuid","duplicate linked GUID never arbitrarily paired");
        Check(GuidMatcher.Match([Element(1,"",axis)],[],true).Single().Status=="MissingSourceGuid","missing source GUID");
        Check(GuidMatcher.Match([], [Element(2,"",axis,true)],true).Single().Status=="MissingIfcGuid","missing IFC GUID");
        Check(GuidMatcher.Match([], [Element(2,"A",axis,true)],false).Count==0,"scoped mode suppresses unrelated linked-only rows");
        PairingChecks.Run(Check);
        var output=Path.Combine(Directory.GetCurrentDirectory(),"Verification","artifacts");Directory.CreateDirectory(output);
        var csv=Path.Combine(output,"synthetic.csv");
        var sample=new ResultRow { Guid="=1+1",Notes="comma, quote\"\nand newline",Status="Match" };
        CsvReport.Write(csv,[sample],Options,"Synthetic model","Synthetic IFC");
        var bytes=File.ReadAllBytes(csv);var content=File.ReadAllText(csv);
        Check(bytes.Take(3).SequenceEqual(new byte[]{239,187,191}) && content.Contains("\"'=1+1\"") && content.Contains("quote\"\"\nand"),"UTF-8 BOM, CSV quoting and formula escaping");
        Check(content.Contains("CandidateLinkedIds") && content.Contains("SearchDistance_mm"),"CSV records pairing method, candidates and separate search limits");
        if(args.Contains("--logic-only")) { Console.WriteLine($"{checks} checks passed; no windows opened.");return; }
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        if(args.Contains("--headless-ui"))
        {
            var setup=new CompareWindow([new(1,"Synthetic IFC.ifc",true,true)]);
            RenderWithoutWindow(setup,Path.Combine(output,"configuration-fallback.png"),740,820);
            var expander=Descendants((DependencyObject)setup.Content).OfType<Expander>().Single();
            expander.IsExpanded=true;
            RenderWithoutWindow(setup,Path.Combine(output,"configuration-fallback-expanded.png"),740,820);
            Check(Descendants((DependencyObject)setup.Content).OfType<CheckBox>().Any(c=>Equals(c.Content,"Ghép hình học khi không có GUID dùng chung") && c.IsChecked==true),"headless WPF geometry fallback enabled");
            var pending=ComparisonService.Compare(new MatchItem(Element(7,"",axis),null,PairingService.ConfirmationStatus,"Synthetic ambiguous candidates") { Method="Geometry",CandidateLinkedIds="8,9" },Options);
            var surface=new ResultsWindow([Compare(axis,axis),pending],Options,"Synthetic model","Synthetic IFC");
            RenderWithoutWindow(surface,Path.Combine(output,"results-fallback.png"),1350,750);
            var resultGrid=Descendants((DependencyObject)surface.Content).OfType<DataGrid>().Single();
            Check(resultGrid.Columns.Any(c=>Equals(c.Header,"Cách ghép")) && resultGrid.Columns.Any(c=>Equals(c.Header,"Linked ứng viên")),"headless WPF shows method and candidate ID columns");
            Descendants((DependencyObject)surface.Content).OfType<TextBox>().First(c=>c.Width==240).Text="8,9";
            Check(resultGrid.Items.Count==1,"headless WPF can filter ambiguous candidate IDs");
            Check(((ResultRow)resultGrid.Items[0]).LinkedId is null,"headless WPF unresolved row has no selected IFC");
            setup.Close();surface.Close();app.Shutdown();
            Console.WriteLine($"{checks} checks passed; WPF rendered in memory without opening windows.");return;
        }
        var configure=new CompareWindow([new(1,"Synthetic IFC.ifc",true,true)]);
        RenderModal(configure,Path.Combine(output,"configuration.png"),()=>
        {
            var compare=Descendants(configure).OfType<Button>().Single(b=>Equals(b.Content,"Compare"));compare.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        Check(configure.Options is { SourceParameter:"test",LinkId:1 },"WPF configuration modal accepts default options");
        var results=new ResultsWindow([Compare(axis,axis),moved,empty],Options,"Synthetic model","Synthetic IFC");
        RenderModal(results,Path.Combine(output,"results.png"),()=>
        {
            var grid=Descendants(results).OfType<DataGrid>().Single();
            Check(grid.Items.Count==3,"WPF results render all rows");
            Descendants(results).OfType<TextBox>().First(t=>t.Width==240).Text="GeometryUnsupported";
            Check(grid.Items.Count==1,"WPF text filter");
            grid.SelectedIndex=0;
            Descendants(results).OfType<Button>().Single(b=>Equals(b.Content,"Select host")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        Check(results.Action==ResultAction.SelectHost && results.Selected?.HostId==1,"navigation request captured after modal closure");
        app.Shutdown();Console.WriteLine($"{checks} checks passed. Synthetic fixtures only; Revit runtime not exercised.");
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) { var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child)) yield return nested; }
    }
    static void RenderWithoutWindow(Window window,string path,int width,int height)
    {
        var root=(FrameworkElement)window.Content;
        root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
        var background=new DrawingVisual();
        using(var drawing=background.RenderOpen()) drawing.DrawRectangle(Brushes.White,null,new Rect(0,0,width,height));
        bitmap.Render(background);bitmap.Render(root);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var file=File.Create(path)) encoder.Save(file);
    }
    static void RenderModal(Window window,string path,Action verify)
    {
        Exception? error=null;
        window.ContentRendered+=(_,_)=>window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>
        {
            try
            {
                window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(path)) encoder.Save(file);
                verify();
            }
            catch(Exception ex) { error=ex; }
            finally { if(window.IsVisible) window.Close(); }
        }));
        window.ShowDialog();if(error is not null) throw error;
    }
}
