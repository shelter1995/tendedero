namespace Liangtu;
public static class VideoDesignPreview
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => {
            var settings = new Settings { Keep = 100, Sounds = false, Welcomed = true };
            var store = new Store(settings);
            typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings); typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, store);
            var line = new LineWindow(app); typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, line);
            var path = Path.Combine(Settings.Root, "屏幕录制 · 视频界面测试.mp4"); if (!File.Exists(path)) File.Copy(Path.GetFullPath(args[1]), path);
            await store.Add(path); var photo = store.Items.Single();
            Window window = args.Contains("--preview") ? new PreviewWindow(app, photo) : new TrimWindow(app, photo);
            window.Closed += (_, _) => { if (args.Contains("--exit-on-close")) app.Shutdown(); };
            window.Show();
            if (window is TrimWindow trim)
            {
                for (var i = 0; i < 100 && !trim.Playback.Ready; i++) await Task.Delay(100);
                if (trim.Playback.Ready) { trim.Timeline.SetStart(2.5); trim.Timeline.SetEnd(Math.Min(9.5, trim.Playback.Duration)); }
            }
            await Task.Delay(3500);
            if (window.IsVisible) { window.UpdateLayout(); var b = new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); b.Render(window); MediaTools.SavePng(b, Path.Combine(Settings.Root, args.Contains("--preview") ? "preview.png" : "trim.png")); }
        };
        return app.Run();
    }
}
