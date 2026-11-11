using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MEP_Compare_Project_ver1.Models;

namespace MEP_Compare_Project_ver1.UI;

public sealed class CompareWindow : Window
{
    private readonly ComboBox link=new() { DisplayMemberPath="Display",MinWidth=450 };
    private readonly CheckBox ducts=new() { Content="Ducts",IsChecked=true,Margin=new Thickness(0,0,24,0) };
    private readonly CheckBox pipes=new() { Content="Pipes",IsChecked=true };
    private readonly ComboBox scope=new() { ItemsSource=new[]{"Toàn model host","View hiện tại (host)","Selection ban đầu (host)"},SelectedIndex=0 };
    private readonly TextBox source=new() { Text="test" };
    private readonly TextBox position=new() { Text="1" };
    private readonly TextBox size=new() { Text="1" };
    private readonly TextBox length=new() { Text="1" };
    private readonly TextBox angle=new() { Text="0.1" };
    private readonly CheckBox geometryFallback=new() { Content="Ghép hình học khi không có GUID dùng chung",IsChecked=true,Margin=new Thickness(0,10,0,4) };
    private readonly TextBox searchDistance=new() { Text="1000" };
    private readonly TextBox searchSize=new() { Text="10" };
    private readonly TextBox searchLength=new() { Text="100" };
    private readonly TextBox searchAngle=new() { Text="5" };
    public RunOptions? Options { get; private set; }
    public CompareWindow(IReadOnlyList<LinkChoice> links,RunOptions? previous=null)
    {
        Title="Compare Project Ver1 — Revit 2025";Width=740;SizeToContent=SizeToContent.Height;
        ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        MaxHeight=SystemParameters.WorkArea.Height-40;
        var panel=new StackPanel { Margin=new Thickness(22) };
        Content=new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text="So sánh native Ducts/Pipes ↔ IFC link",FontSize=21,Margin=new Thickness(0,0,0,12) });
        link.ItemsSource=links;link.SelectedItem=links.FirstOrDefault(x=>x.Loaded && x.IsIfc) ?? links.FirstOrDefault(x=>x.Loaded);
        Add(panel,"IFC link instance (tọa độ theo GetTotalTransform)",link);
        var categories=new StackPanel { Orientation=Orientation.Horizontal };categories.Children.Add(ducts);categories.Children.Add(pipes);Add(panel,"Phần tử",categories);
        Add(panel,"Phạm vi native",scope);
        Add(panel,"Parameter chứa GUID nguồn: tên hoặc shared parameter GUID",source);
        Add(panel,"Dung sai vị trí / max endpoint (mm)",position);Add(panel,"Dung sai tiết diện (mm)",size);
        Add(panel,"Dung sai chiều dài (mm)",length);Add(panel,"Dung sai hướng / xoay tiết diện (độ)",angle);
        panel.Children.Add(geometryFallback);
        var searchPanel=new StackPanel();
        Add(searchPanel,"Khoảng cách tâm tối đa để tìm cặp (mm)",searchDistance);
        Add(searchPanel,"Chênh tiết diện tối đa để tìm cặp (mm)",searchSize);
        Add(searchPanel,"Chênh chiều dài tối đa để tìm cặp (mm)",searchLength);
        Add(searchPanel,"Góc trục tối đa để tìm cặp (độ)",searchAngle);
        searchPanel.Children.Add(new TextBlock { Text="Đây là giới hạn tìm ứng viên, độc lập với dung sai kết luận lệch. Chỉ tự ghép khi duy nhất ở cả hai phía; nhiều ứng viên sẽ báo Cần xác nhận cặp.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0) });
        panel.Children.Add(new Expander { Header="Giới hạn tìm cặp hình học (mặc định: 1000 / 10 / 100 mm; 5°)",Content=searchPanel,Margin=new Thickness(0,5,0,5) });
        geometryFallback.Checked+=(_,_)=>searchPanel.IsEnabled=true;
        geometryFallback.Unchecked+=(_,_)=>searchPanel.IsEnabled=false;
        panel.Children.Add(new TextBlock { Text="Ưu tiên GUID chính xác (phân biệt hoa/thường); không ghép hình học để bỏ qua GUID trùng.\nView/selection chỉ giới hạn báo cáo; xét cả host ngoài phạm vi để tránh ghép nhầm.\nVer1: đoạn thẳng, tròn/chữ nhật; IFC chữ nhật suy ra trục dài nhất. Chưa ghép một–nhiều.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,12) });
        var button=new Button { Content="Compare",Padding=new Thickness(22,7,22,7),HorizontalAlignment=HorizontalAlignment.Right,IsDefault=true };panel.Children.Add(button);
        button.Click+=(_,_)=>Accept();
        if(previous is not null)
        {
            link.SelectedItem=links.FirstOrDefault(x=>x.Id==previous.LinkId);ducts.IsChecked=previous.Ducts;pipes.IsChecked=previous.Pipes;
            scope.SelectedIndex=(int)previous.Scope;source.Text=previous.SourceParameter;
            position.Text=previous.PositionMm.ToString(CultureInfo.CurrentCulture);size.Text=previous.SizeMm.ToString(CultureInfo.CurrentCulture);
            length.Text=previous.LengthMm.ToString(CultureInfo.CurrentCulture);angle.Text=previous.AngleDegrees.ToString(CultureInfo.CurrentCulture);
            geometryFallback.IsChecked=previous.GeometryFallback;
            searchDistance.Text=previous.SearchDistanceMm.ToString(CultureInfo.CurrentCulture);
            searchSize.Text=previous.SearchSizeMm.ToString(CultureInfo.CurrentCulture);
            searchLength.Text=previous.SearchLengthMm.ToString(CultureInfo.CurrentCulture);
            searchAngle.Text=previous.SearchAngleDegrees.ToString(CultureInfo.CurrentCulture);
        }
    }
    private static void Add(Panel panel,string label,UIElement control)
    {
        panel.Children.Add(new TextBlock { Text=label,Margin=new Thickness(0,9,0,4) });panel.Children.Add(control);
    }
    private static double Number(TextBox box,string label)
    {
        if((!double.TryParse(box.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out var value) && !double.TryParse(box.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)) || !double.IsFinite(value) || value<0)
            throw new InvalidOperationException($"{label}: nhập số hữu hạn ≥ 0.");
        return value;
    }
    private void Accept()
    {
        try
        {
            if(link.SelectedItem is not LinkChoice selected || !selected.Loaded) throw new InvalidOperationException("Chọn link đang loaded.");
            if(ducts.IsChecked!=true && pipes.IsChecked!=true) throw new InvalidOperationException("Chọn Ducts hoặc Pipes.");
            if(string.IsNullOrWhiteSpace(source.Text) && geometryFallback.IsChecked!=true) throw new InvalidOperationException("Nhập parameter nguồn hoặc bật ghép hình học.");
            var degrees=Number(angle,"Dung sai góc");if(degrees>90) throw new InvalidOperationException("Dung sai góc phải ≤ 90°.");
            Options=new(selected.Id,ducts.IsChecked==true,pipes.IsChecked==true,(Scope)scope.SelectedIndex,source.Text.Trim(),Number(position,"Vị trí"),Number(size,"Tiết diện"),Number(length,"Chiều dài"),degrees);
            if(geometryFallback.IsChecked==true)
            {
                double searchDegrees=Number(searchAngle,"Góc tìm cặp");
                if(searchDegrees>90) throw new InvalidOperationException("Góc tìm cặp phải ≤ 90°.");
                Options=Options with { GeometryFallback=true,SearchDistanceMm=Number(searchDistance,"Khoảng cách tìm cặp"),SearchSizeMm=Number(searchSize,"Tiết diện tìm cặp"),SearchLengthMm=Number(searchLength,"Chiều dài tìm cặp"),SearchAngleDegrees=searchDegrees };
            }
            else Options=Options with { GeometryFallback=false };
            DialogResult=true;
        }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,"Kiểm tra đầu vào",MessageBoxButton.OK,MessageBoxImage.Warning); }
    }
}
