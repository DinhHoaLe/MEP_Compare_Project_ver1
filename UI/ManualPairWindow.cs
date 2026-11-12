using System.Windows;
using System.Windows.Controls;
using MEP_Compare_Project_ver1.Matching;
using MEP_Compare_Project_ver1.Models;

namespace MEP_Compare_Project_ver1.UI;

public sealed class ManualPairWindow : Window
{
    public long LinkedId { get; private set; }
    public ManualPairWindow(MappingSession session,ResultRow row)
    {
        Title="Xác nhận cặp thủ công";Width=850;Height=440;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel { Margin=new Thickness(18) };Content=panel;
        panel.Children.Add(new TextBlock { Text=$"Host {row.HostId} | {row.Category}\nỨng viên hình học: {row.CandidateLinkedIds}",TextWrapping=TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text="Tìm IFC theo ID / Tag / GUID / tên (cùng loại Duct/Pipe):",Margin=new Thickness(0,12,0,4) });
        var search=new TextBox();panel.Children.Add(search);
        var list=new ListBox { Height=190,DisplayMemberPath="Display",Margin=new Thickness(0,8,0,8) };panel.Children.Add(list);
        var candidates=row.CandidateLinkedIds.Split(',').ToHashSet();
        var available=session.Links.Where(l=>l.Category==row.Category).OrderByDescending(l=>candidates.Contains(l.Id.ToString())).ThenBy(l=>l.Id).ToList();
        void Filter() => list.ItemsSource=available.Where(l=>l.Display.Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)).ToList();
        search.TextChanged+=(_,_)=>Filter();Filter();
        list.SelectedItem=available.FirstOrDefault(l=>l.Id==row.LinkedId);
        panel.Children.Add(new TextBlock { Text="Xác nhận này thay thế cách ghép tự động của host. Kết quả so sánh vẫn dùng dung sai hiện tại; xác nhận cặp không có nghĩa là khớp hình học.",TextWrapping=TextWrapping.Wrap });
        var confirm=new Button { Content="Xác nhận cặp",HorizontalAlignment=HorizontalAlignment.Right,Padding=new Thickness(15,7,15,7),Margin=new Thickness(0,12,0,0) };panel.Children.Add(confirm);
        confirm.Click+=(_,_)=>
        {
            if(list.SelectedItem is not ElementData selected) { MessageBox.Show(this,"Chọn phần tử IFC.");return; }
            try { session.Confirm(row.HostId!.Value,selected.Id);LinkedId=selected.Id;DialogResult=true; }
            catch(Exception ex) { MessageBox.Show(this,ex.Message,"Không xác nhận được"); }
        };
    }
}
