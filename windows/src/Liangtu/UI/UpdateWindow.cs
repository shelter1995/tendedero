using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Liangtu.Updates;
namespace Liangtu;
internal sealed class UpdateWindow : Window
{
    readonly Func<PreparedUpdate,Task<bool>> handoff;
    readonly HttpClient client=UpdateService.CreateHttpClient();
    readonly CheckBox prerelease;
    readonly TextBlock status;
    readonly TextBox notes;
    readonly ProgressBar progress;
    readonly Button check,download,import,apply,cancel;
    CancellationTokenSource? operation;
    UpdateRelease? release;
    PreparedUpdate? prepared;
    bool busy;
    public UpdateWindow(Func<PreparedUpdate,Task<bool>> handoff,bool includePrerelease=false,Action<bool>? savePrerelease=null)
    {
        this.handoff=handoff;Title="晾图 · Windows 更新";Width=600;Height=535;MinWidth=500;MinHeight=440;Theme.WindowStyle(this);
        var panel=new Grid{Margin=new Thickness(24)};
        foreach(var height in new[]{GridLength.Auto,GridLength.Auto,GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto,GridLength.Auto})panel.RowDefinitions.Add(new RowDefinition{Height=height});
        Content=panel;
        var heading=Theme.Text("保持晾图更新",24);heading.FontWeight=FontWeights.SemiBold;panel.Children.Add(heading);
        var info=Theme.Text($"当前版本 {UpdateService.CurrentVersion} · {(UpdateService.Installed?"安装版":"便携版")} · Windows x64\n官方源：github.com/shelter1995/tendedero/releases",12,Theme.Muted);info.Margin=new Thickness(0,10,0,12);Grid.SetRow(info,1);panel.Children.Add(info);
        prerelease=new CheckBox{Content="包括 Windows 预发布版本",IsChecked=includePrerelease,Margin=new Thickness(0,0,0,12)};Grid.SetRow(prerelease,2);panel.Children.Add(prerelease);
        notes=new TextBox{Text="点击“检查更新”读取官方 Windows 发布记录。\n\n也可以导入可信来源提供的更新 ZIP 包。包内必须包含版本元数据和 SHA256 校验清单。",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(12),Background=Theme.ControlSurface,Foreground=Theme.Ink};Grid.SetRow(notes,3);panel.Children.Add(notes);
        var feedback=new StackPanel{Margin=new Thickness(0,12,0,8)};status=Theme.Text("尚未检查更新。",12,Theme.Muted);progress=new ProgressBar{Height=5,Minimum=0,Maximum=100,Margin=new Thickness(0,8,0,0),Visibility=Visibility.Collapsed};feedback.Children.Add(status);feedback.Children.Add(progress);Grid.SetRow(feedback,4);panel.Children.Add(feedback);
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};Grid.SetRow(actions,5);panel.Children.Add(actions);
        import=Theme.Button("导入更新包",()=>_=ImportAsync());check=Theme.Button("检查更新",()=>_=CheckAsync());download=Theme.Button("下载更新",()=>_=DownloadAsync());apply=Theme.Primary("退出并更新",()=>_=ApplyAsync());cancel=Theme.Button("取消",()=>operation?.Cancel());
        foreach(var button in new[]{import,check,download,apply,cancel})actions.Children.Add(button);download.IsEnabled=false;apply.IsEnabled=false;cancel.Visibility=Visibility.Collapsed;
                prerelease.Click+=(_,_)=>{savePrerelease?.Invoke(prerelease.IsChecked==true);release=null;download.IsEnabled=false;status.Text="发布渠道已改变，请重新检查。";};
        Closing+=(_,e)=>{if(busy&&operation==null)e.Cancel=true;else operation?.Cancel();};Closed+=(_,_)=>client.Dispose();
    }
    async Task RunAsync(Func<CancellationToken,Task> action)
    {
        if(busy)return;busy=true;operation=new CancellationTokenSource();check.IsEnabled=import.IsEnabled=download.IsEnabled=apply.IsEnabled=prerelease.IsEnabled=false;cancel.Visibility=Visibility.Visible;
        try{await action(operation.Token);}catch(OperationCanceledException){status.Text="操作已取消。";}catch(Exception ex){status.Text="操作失败："+ex.Message;}
        finally{busy=false;operation.Dispose();operation=null;check.IsEnabled=import.IsEnabled=prerelease.IsEnabled=true;download.IsEnabled=release!=null&&prepared==null;apply.IsEnabled=prepared!=null;cancel.Visibility=Visibility.Collapsed;progress.Visibility=Visibility.Collapsed;}
    }
    Task CheckAsync()=>RunAsync(async token=>{
        prepared=null;release=null;status.Text="正在读取官方 Windows 发布记录…";progress.IsIndeterminate=true;progress.Visibility=Visibility.Visible;
        release=await new UpdateService(client).CheckAsync(UpdateService.CurrentVersion,prerelease.IsChecked==true,UpdateService.Installed,token);
        if(release==null){status.Text="检查完成：所选渠道没有高于当前版本的可用 Windows 更新。";notes.Text="当前渠道："+(prerelease.IsChecked==true?"正式版与预发布版":"仅正式版")+"\n检查范围：官方仓库最近 100 条发布记录。";}
        else{status.Text=$"发现 Windows {release.Version} · {release.Asset.Size/1024d/1024d:F1} MB";notes.Text=release.Notes.Length==0?"此版本未提供发布说明。":release.Notes;}
    });
    Task DownloadAsync()=>RunAsync(async token=>{
        if(release==null)return;var selected=release;var work=PreparedUpdate.CreateWorkDirectory();var file=Path.Combine(work,selected.Asset.Name);
        progress.IsIndeterminate=false;progress.Value=0;progress.Visibility=Visibility.Visible;status.Text="正在下载并校验 SHA256…";
        await new UpdateService(client).DownloadAsync(selected.Asset,file,new Progress<double>(p=>{progress.Value=p;status.Text=$"正在下载 {p:F0}% · 校验完成后才允许安装";}),token);
        token.ThrowIfCancellationRequested();status.Text="正在检查更新包…";
        StagedPackage package;
        if(UpdateService.Installed){var stage=Path.Combine(work,"stage");Directory.CreateDirectory(stage);File.Move(file,Path.Combine(stage,selected.Asset.Name));package=new(stage,selected.Version,"installer",selected.Asset.Name);}
        else{package=await Task.Run(()=>UpdateService.StagePackage(file,Path.Combine(work,"stage"),UpdateService.CurrentVersion,false),token);if(package.Version.CompareTo(selected.Version)!=0)throw new InvalidDataException("更新包版本与发布记录不一致。");}
        token.ThrowIfCancellationRequested();prepared=new PreparedUpdate(package,work);status.Text=$"版本 {prepared.Version} 已验证。退出前会提示处理未保存编辑。";
    });
    async Task ImportAsync()
    {
        if(busy)return;
        var dialog=new Microsoft.Win32.OpenFileDialog{Title="导入晾图 Windows 更新包",Filter="晾图更新包 (*.zip)|*.zip",CheckFileExists=true};if(dialog.ShowDialog(this)!=true)return;
        if(MessageBox.Show(this,"仅导入你信任的晾图发布者提供的 ZIP 文件。包内 SHA256 可检查损坏，但不能证明本地包的发布者身份。继续检查此包？","导入本地更新",MessageBoxButton.OKCancel,MessageBoxImage.Information)!=MessageBoxResult.OK)return;
        await RunAsync(async token=>{prepared=null;status.Text="正在验证本地包版本、文件路径和校验和…";progress.IsIndeterminate=true;progress.Visibility=Visibility.Visible;var work=PreparedUpdate.CreateWorkDirectory();var package=await Task.Run(()=>UpdateService.StagePackage(dialog.FileName,Path.Combine(work,"stage"),UpdateService.CurrentVersion,UpdateService.Installed),token);token.ThrowIfCancellationRequested();prepared=new PreparedUpdate(package,work);notes.Text=$"本地更新包\n版本：{package.Version}\n已校验全部文件。";status.Text="本地包已验证，可以退出并更新。";});
    }
    async Task ApplyAsync()
    {
        if(prepared==null||busy)return;busy=true;check.IsEnabled=import.IsEnabled=download.IsEnabled=apply.IsEnabled=prerelease.IsEnabled=false;
        try{if(!await handoff(prepared))status.Text="已取消退出，更新包保留，可稍后重试。";}
        catch(Exception ex){status.Text="未启动更新："+ex.Message;}
        finally{busy=false;check.IsEnabled=import.IsEnabled=prerelease.IsEnabled=true;apply.IsEnabled=true;}
    }
}
