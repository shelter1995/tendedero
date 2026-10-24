using System.Windows.Interop;
using System.Diagnostics;
using System.Threading;
namespace Liangtu;
public sealed partial class App : Application
{
    public Settings Settings { get; private set; } = null!;
    public Store Store { get; private set; } = null!;
    public LineWindow Line { get; private set; } = null!;
    FolderWatch? watcher; System.Windows.Forms.NotifyIcon? tray; ActivationGate? activation; DispatcherTimer? prune;
    public bool HotKeyRegistered
    {
        get; private set;
    }
    public bool CaptureKeyRegistered
    {
        get; private set;
    }
    int trayIconState = -1;
    public bool TestMode
    {
        get; private set;
    }
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--verify-package")) return PackageVerifier.Run();
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { Settings.Log(e.Exception.ToString()); if (app.TestMode) File.WriteAllText(Path.Combine(Settings.Root, "failure.txt"), e.Exception.ToString()); else MessageBox.Show(e.Exception.Message, "晾图"); e.Handled = true; };
        app.Startup += async (_, _) => await app.Start(args);
        return app.Run();
    }
    async Task Start(string[] args)
    {
        TestMode = args.Contains("--demo") || args.Contains("--self-test");
        if (args.Contains("--data"))
        {
            var i = Array.FindIndex(args, a => a == "--data");
            Settings.Root = i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : Path.Combine(AppContext.BaseDirectory, "test-data");
        }
        activation = new ActivationGate(Settings.Root, () => Dispatcher.BeginInvoke(() => { if (Line != null) Line.ShowFromPointer(); }));
        if (!activation.IsFirst)
        {
            activation.Dispose();
            Shutdown();
            return;
        }
        Settings = Settings.Load();
        Store = new Store(Settings);
        Store.Error += message => MessageBox.Show(message, "晾图");
        Line = new LineWindow(this);
        MainWindow = Line;
        Exit += (_, _) => { Microsoft.Win32.SystemEvents.UserPreferenceChanged -= TrayThemeChanged; watcher?.Dispose(); prune?.Stop(); Native.RemoveClipboardFormatListener(Line.Hwnd); Native.UnregisterHotKey(Line.Hwnd, 1); Native.UnregisterHotKey(Line.Hwnd, 2); tray?.Dispose(); Store.Persist(); activation?.Dispose(); };
        Store.Added += p => { if (Settings.NewReveal) Line.Reveal(false, 2.5); UpdateTray(); };
        Store.Changed += _ => UpdateTray();
        Store.Hanging += p => { Native.GetCursorPos(out var point); if (p.Origin is Rect origin) { point.X = (int)(origin.X + origin.Width / 2); point.Y = (int)(origin.Y + origin.Height / 2); } Line.MoveTo(System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y))); Line.ResetScroll(); };
        Store.Removing += (_, quiet) => { if (!quiet) Sound(); };
        Store.Added += _ => Sound();
        await Store.Restore();
        if (TestMode)
        {
            if (args.Contains("--demo"))
                CreateTray();
            await SelfTest.Run(this, args.Contains("--self-test"));
            return;
        }
        CreateTray();
        watcher = new FolderWatch(Store, Settings);
        Native.AddClipboardFormatListener(Line.Hwnd);
        if (!RegisterShortcut())
            tray?.ShowBalloonTip(6000, "晾图快捷键不可用", "显示快捷键被其他程序占用。可点击托盘图标显示，并在快捷键菜单更改。", System.Windows.Forms.ToolTipIcon.Warning);
        if (!RegisterShortcut(true))
            tray?.ShowBalloonTip(6000, "晾图截图快捷键不可用", "组合键被其他程序占用。请在托盘 → 快捷键中更改框选截图快捷键。", System.Windows.Forms.ToolTipIcon.Warning);
        Settings.Log($"Started pid={Environment.ProcessId}, hwnd={Line.Hwnd}, items={Store.Items.Count}, hotkey={HotKeyRegistered}, captureKey={CaptureKeyRegistered}");
        prune = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        prune.Tick += (_, _) => Store.Prune();
        prune.Start();
        if (!Settings.Welcomed)
        {
            Settings.Welcomed = true;
            Settings.Save();
            ShowWelcome();
        }
        if (args.Contains("--show"))
            Line.ShowFromPointer();
        else if (Store.Items.Count > 0)
            Line.Reveal(false, 2.5);
        if (args.Contains("--drag-harness"))
            new DragHarness(this).Show();
    }
    void CreateTray()
    {
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += TrayThemeChanged;
        tray = new System.Windows.Forms.NotifyIcon { Icon = MakeIcon(), Visible = true, Text = "晾图" };
        tray.MouseClick += (_, e) => Dispatcher.Invoke(() => { if (e.Button == System.Windows.Forms.MouseButtons.Left) Line.Toggle(); else OpenTrayMenu(); });
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(Line.Toggle);
        UpdateTray();
    }
    static System.Drawing.Icon MakeIcon(bool filled = false, bool active = true)
    {
        bool light = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 1) is int v && v != 0;
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Liangtu;component/Assets/tray-" + (light ? "light" : "dark") + ".ico")).Stream;
        using var icon = new System.Drawing.Icon(stream, new System.Drawing.Size(32, 32));
        return (System.Drawing.Icon)icon.Clone();
    }
    void TrayThemeChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(() => { trayIconState = -1; UpdateTray(); });
    void UpdateTray()
    {
        if (tray != null)
        {
            var state = (Settings.ClipboardImages ? 1 : 0) + (Store.Items.Count > 0 ? 2 : 0);
            if (state != trayIconState)
            {
                var old = tray.Icon;
                tray.Icon = MakeIcon(Settings.ClipboardImages, Store.Items.Count > 0);
                old?.Dispose();
                trayIconState = state;
            }
            tray.Text = "晾图 · " + Localization.Text(Store.Items.Count == 0 ? "The line is empty" : "%d on the line").Replace("%d", Store.Items.Count.ToString());
        }
    }
    public void SaveSettings(bool restartWatch = false)
    {
        Settings.Save();
        if (restartWatch)
            watcher?.Restart();
        Line.Refresh();
        UpdateTray();
    }
    public bool RegisterShortcut(bool capture = false)
    {
        int id = capture ? 2 : 1;
        var key = capture ? Settings.CaptureHotkeyKey : Settings.HotkeyKey;
        var modifiers = capture ? Settings.CaptureHotkeyModifiers : Settings.HotkeyModifiers;
        Native.UnregisterHotKey(Line.Hwnd, id);
        bool registered = key == 0 || Native.RegisterHotKey(Line.Hwnd, id, modifiers | 0x4000, key);
        if (capture) CaptureKeyRegistered = registered; else HotKeyRegistered = registered;
        Settings.Log($"Hotkey id={id}, registered={registered}, key={key}, modifiers={modifiers}, error={(registered ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error())}");
        return registered;
    }
    public void Sound()
    {
        if (Settings.Sounds && !TestMode)
            System.Media.SystemSounds.Asterisk.Play();
    }
    public void Open(Photo p) => Theme.Shell(p.Path);
    public void Preview(Photo p)
    {
        var screen = Line.Screen;
        Line.Retract();
        WindowPresenter.Show(new PreviewWindow(this, p), screen);
    }
    public void Edit(Photo p)
    {
        var screen = Line.Screen;
        Line.Retract();
        if (p.Video)
            WindowPresenter.Show(new TrimWindow(this, p), screen);
        else
            WindowPresenter.Show(new EditorWindow(this, p), screen);
    }
    public void Discard(Card c)
    {
        Store.Discard(c.Photo);
    }
    public void Capture()
    {
        if (Line.Busy)
            return;
        Line.Retract();
        _ = CaptureLater();
    }
    async Task CaptureLater()
    {
        await Task.Delay(320);
        new CaptureWindow(async (image, origin) => { var path = FileRules.UniquePath(Settings.Inbox, "Screenshot " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + ".png"); MediaTools.SavePng(image, path); await Store.Add(path, origin: origin); }).Show();
    }
    void ShowWelcome()
    {
        var w = new Window { Title = "欢迎使用晾图", Width = 500, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
        Theme.WindowStyle(w);
        var s = new StackPanel { Margin = new Thickness(28) };
        s.Children.Add(new TextBlock { Text = "截图，挂起来晾着。", FontSize = 26, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        s.Children.Add(new TextBlock { Text = "把鼠标停在屏幕顶边，晾衣绳就会滑下来。\n单击复制 · 双击打开 · 长按标注 · 拖拽分享\n\nCtrl+Alt+T  显示 / 隐藏\nCtrl+Alt+S  框选截图\n两组快捷键都可在托盘菜单中修改或关闭。\n\nWin+Shift+S 继续使用系统截图工具。开启下方选项后，自动接收系统截图和截图目录的新文件。Snipaste 等软件的复制图片，可在托盘开启“挂上复制的图片”。", TextWrapping = TextWrapping.Wrap, LineHeight = 24, Foreground = Theme.Muted });
        var check = new CheckBox { Content = "接收 Windows 系统截图和录屏", Margin = new Thickness(0, 20, 0, 14), IsChecked = true };
        s.Children.Add(check);
        s.Children.Add(Theme.Primary("开始使用", () => { Settings.HandleScreenshots = check.IsChecked == true; SaveSettings(true); w.Close(); Line.Reveal(true, 5); }));
        w.Content = s;
        w.Show();
    }
}
