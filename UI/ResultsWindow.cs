using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using MEP_Compare_Project_ver1.Models;
using MEP_Compare_Project_ver1.Reporting;

namespace MEP_Compare_Project_ver1.UI;

public enum ResultAction { Close, Configure, SelectHost, SelectLinked, ZoomPair }
public sealed class ResultsWindow : Window
{
    private readonly DataGrid grid=new() { IsReadOnly=true,AutoGenerateColumns=false,SelectionMode=DataGridSelectionMode.Single,CanUserAddRows=false,EnableRowVirtualization=true,EnableColumnVirtualization=true };
    private readonly TextBox search=new() { Width=240,Margin=new Thickness(8,0,12,0) };
    private readonly ComboBox status=new() { MinWidth=170 };
    private readonly TextBlock count=new() { Margin=new Thickness(10,5,0,5) };
    private readonly ICollectionView view;
    public ResultAction Action { get; private set; }
    public ResultRow? Selected { get; private set; }
    public ResultsWindow(IReadOnlyList<ResultRow> rows,RunOptions options,string document,string link)
    {
        Title="Compare Project Ver1 — Kết quả";Width=1350;Height=750;MinWidth=850;MinHeight=480;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        view=CollectionViewSource.GetDefaultView(rows);
        var dock=new DockPanel { Margin=new Thickness(12) };Content=dock;
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);dock.Children.Add(top);
        top.Children.Add(new TextBlock { Text=$"{document}  ↔  {link}",FontSize=18,TextWrapping=TextWrapping.Wrap });
        top.Children.Add(new TextBlock { Text=$"{options.Scope} | {options.SourceParameter} ↔ IFC.IfcGUID | Hình học dự phòng: {(options.GeometryFallback?"Bật":"Tắt")} | Δ = IFC − Host (internal host, mm).",Margin=new Thickness(0,6,0,8),TextWrapping=TextWrapping.Wrap });
        var filter=new StackPanel { Orientation=Orientation.Horizontal };top.Children.Add(filter);
        filter.Children.Add(new TextBlock { Text="Tìm ID / GUID / Tag",VerticalAlignment=VerticalAlignment.Center });filter.Children.Add(search);
        status.ItemsSource=new[]{"Tất cả"}.Concat(rows.Select(r=>r.Status).Distinct().OrderBy(s=>s)).ToList();status.SelectedIndex=0;filter.Children.Add(status);filter.Children.Add(count);
        var bottom=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,0) };DockPanel.SetDock(bottom,Dock.Bottom);dock.Children.Add(bottom);
        Button Add(string text,Action click) { var b=new Button { Content=text,Margin=new Thickness(0,0,8,0),Padding=new Thickness(10,6,10,6) };b.Click+=(_,_)=>click();bottom.Children.Add(b);return b; }
        Add("Select host",()=>Finish(ResultAction.SelectHost));Add("Select linked",()=>Finish(ResultAction.SelectLinked));Add("Zoom cặp",()=>Finish(ResultAction.ZoomPair));
        Add("Xuất CSV (dòng đang lọc)",()=>
        {
            var dialog=new SaveFileDialog { Filter="CSV (*.csv)|*.csv",FileName="CompareProject.csv",DefaultExt=".csv",AddExtension=true };
            if(dialog.ShowDialog(this)!=true) return;
            try { CsvReport.Write(dialog.FileName,view.Cast<ResultRow>(),options,document,link);MessageBox.Show(this,"Đã xuất CSV.","Compare Project"); }
            catch(Exception ex) { MessageBox.Show(this,ex.Message,"Không xuất được CSV"); }
        });
        Add("Đổi cấu hình",()=> { Action=ResultAction.Configure;Close(); });Add("Đóng",Close);
        dock.Children.Add(grid);
        void Col(string title,string property,string? format=null)
        {
            grid.Columns.Add(new DataGridTextColumn { Header=title,Binding=new Binding(property) { StringFormat=format },Width=new DataGridLength(property=="Notes"?500:property=="Status"?230:property=="Guid"?185:110) });
        }
        Col("Host ID","HostId");Col("Linked ID","LinkedId");Col("IfcTag","IfcTag");Col("Loại","Category");Col("Cách ghép","MatchMethod");Col("Trạng thái","Status");Col("Linked ứng viên","CandidateLinkedIds");Col("GUID nguồn","Guid");
        Col("ΔX mm","DeltaXmm","F2");Col("ΔY mm","DeltaYmm","F2");Col("ΔZ mm","DeltaZmm","F2");Col("Endpoint mm","EndpointMm","F2");
        Col("ΔLength mm","LengthMm","F2");Col("Góc °","AngleDeg","F3");Col("ΔSize mm","SizeMm","F2");Col("Roll °","RollDeg","F3");Col("Ghi chú","Notes");
        grid.ItemsSource=view;
        void Filter()
        {
            string text=search.Text.Trim();var selected=status.SelectedItem as string;
            view.Filter=o=> o is ResultRow r && (selected=="Tất cả" || selected==r.Status) && (text=="" || $"{r.HostId} {r.LinkedId} {r.Guid} {r.IfcTag} {r.Status} {r.MatchMethod} {r.CandidateLinkedIds} {r.Notes}".Contains(text,StringComparison.OrdinalIgnoreCase));
            count.Text=$"{view.Cast<object>().Count()} / {rows.Count} dòng; {rows.Count(r=>r.Status=="Match")} khớp; {rows.Count(r=>r.HostId.HasValue && r.Status==Matching.PairingService.ConfirmationStatus)} host cần xác nhận";
        }
        search.TextChanged+=(_,_)=>Filter();status.SelectionChanged+=(_,_)=>Filter();Filter();
    }
    private void Finish(ResultAction action)
    {
        if(grid.SelectedItem is not ResultRow row) { MessageBox.Show(this,"Chọn một dòng kết quả.");return; }
        if(action==ResultAction.SelectHost && row.HostId is null || action==ResultAction.SelectLinked && row.LinkedId is null)
        { MessageBox.Show(this,"Dòng này không có phần tử tương ứng.");return; }
        Selected=row;Action=action;Close();
    }
}
