using System.Diagnostics;
using System.Text.Json;
namespace Liangtu;

// Interactive acceptance fixture: real hooks and clipboard notifications, with
// an isolated collection. UI input is supplied by the desktop test operator.
internal static class CaptureAcceptanceChecks
{
    [STAThread] public static int Main(string[] args)
    {
        Settings.Root=Path.GetFullPath(args[0]);Directory.CreateDirectory(Settings.Inbox);
        var app=new App { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.Startup+=(_,_)=>{
            var settings=new Settings { ClipboardImages=true, HandleScreenshots=true, AutoHang=true, NewReveal=true, HoverReveal=true, Sounds=false, Welcomed=true, CollectionRevision=1, ThemeId="linen" };
            typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app,settings);typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app,true);
            Theme.Select("linen");var store=new Store(settings);typeof(App).GetProperty(nameof(App.Store))!.SetValue(app,store);
            var line=new LineWindow(app);typeof(App).GetProperty(nameof(App.Line))!.SetValue(app,line);
            var observer=new WindowsSnipOrigin(()=>true,message=>Settings.Log("Capture origin "+message));app.ConfigureWindowsSnipIntake(observer);
            store.Hanging+=app.OnPhotoHanging;store.Added+=app.OnPhotoAdded;Native.AddClipboardFormatListener(line.Hwnd);
            var root=new DockPanel { Margin=new Thickness(28) };
            var status=Theme.Text("等待截图：请框选下方彩色区域",18);DockPanel.SetDock(status,Dock.Top);root.Children.Add(status);
            var buttons=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,14,0,14) };DockPanel.SetDock(buttons,Dock.Top);root.Children.Add(buttons);
            buttons.Children.Add(Theme.Button("显示 / 收起晾衣绳",line.Toggle));buttons.Children.Add(Theme.Button("图片篮子",app.ShowBasket));
            var fixture=new Grid();fixture.ColumnDefinitions.Add(new ColumnDefinition());fixture.ColumnDefinitions.Add(new ColumnDefinition());fixture.RowDefinitions.Add(new RowDefinition());fixture.RowDefinitions.Add(new RowDefinition());
            var colors=new[]{"#A8BAA0","#CC7757","#93ACBE","#D8BE81"};
            for(var i=0;i<4;i++) {
                var cell=new Border { Background=Theme.Brush(colors[i]),Margin=new Thickness(6),Padding=new Thickness(20),Child=Theme.Text("截图验收 "+(i+1)+"\n事件锚点 / 已测选区 → 带框飞入\n0.10.5",24) };
                Grid.SetColumn(cell,i%2);Grid.SetRow(cell,i/2);fixture.Children.Add(cell);
            }
            root.Children.Add(fixture);
            var window=new Window { Title="晾图 0.10.5 截图验收 · 独立测试数据",Width=1100,Height=760,Content=root };Theme.WindowStyle(window);
            store.Added+=photo=>{ status.Text="已接收 "+store.Collection.Count+" 张 · 选区已验证："+photo.ValidatedWindowsCapture;Settings.Log("ACCEPT "+photo.Path+" validated="+photo.ValidatedWindowsCapture); };
            var watch=Stopwatch.StartNew();var frames=new List<object>();
            EventHandler sample=(_,_)=>{
                foreach(var card in ((Canvas)line.Content).Children.OfType<Canvas>().Single().Children.OfType<Card>())
                    if(card.ArrivalFlightBounds is Rect bounds) frames.Add(new { time=watch.Elapsed.TotalMilliseconds,path=Path.GetFileName(card.Photo.Path),bounds=new[]{bounds.X,bounds.Y,bounds.Width,bounds.Height} });
            };
            CompositionTarget.Rendering+=sample;
            var flush=new DispatcherTimer { Interval=TimeSpan.FromSeconds(2) };flush.Tick+=(_,_)=>File.WriteAllText(Path.Combine(Settings.Root,"flight-frames.json"),JsonSerializer.Serialize(frames));flush.Start();
            window.Closed+=(_,_)=>{flush.Stop();CompositionTarget.Rendering-=sample;observer.Dispose();Native.RemoveClipboardFormatListener(line.Hwnd);line.Close();app.Shutdown();};
            window.Show();
        };
        return app.Run();
    }
}
