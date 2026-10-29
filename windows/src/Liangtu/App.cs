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
    WindowsSnipOrigin? windowsSnips;
    public bool HotKeyRegistered
    {
        get; private set;
    }
    public bool CaptureKeyRegistered
    {
        get; private set;
    }
    int trayIconState = -1;
    readonly OperationSounds sounds = new();
    int captureGeneration;
    bool exiting;
    bool updateHandoff;
    CapturePublications? capturePublications;
    CapturePublications Captures => capturePublications ??= new CapturePublications(error => ReportBackgroundError("截图保存或入库未完成，任务已保留。请检查保存位置或磁盘空间；退出或更新时会重试。\n" + error.Message));
    internal int PendingCaptureCount => capturePublications?.Count ?? 0;
    internal bool WorkBlocked => exiting || updateHandoff;
    async void ExitAfterCopy() => await ExitAfterPendingWork(Shutdown);
    internal async Task<bool> ExitAfterPendingWork(Action shutdown)
    {
        if (WorkBlocked) return false;
        exiting = true; ++captureGeneration; Line.Retract();
        if (!await Captures.Settle()) {
            exiting = false;
            Notify("截图尚未保存", "已暂缓退出，截图仍保留在内存中。请检查保存位置或磁盘空间后重试。", System.Windows.Forms.ToolTipIcon.Warning);
            return false;
        }
        if (await Store.FinishCopyBeforeExit()) { shutdown(); return true; }
        exiting = false; Store.ResumeAfterCancelledExit();
        Notify("剪贴板仍被占用", "已暂缓退出，请完成粘贴或重新复制后再退出。", System.Windows.Forms.ToolTipIcon.Info);
        return false;
    }
    public bool TestMode
    {
        get; private set;
    }
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--verify-package")) return PackageVerifier.Run();
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { Settings.Log(e.Exception.ToString()); if (app.TestMode) File.WriteAllText(Path.Combine(Settings.Root, "failure.txt"), e.Exception.ToString()); else app.ReportBackgroundError(e.Exception.Message); e.Handled = true; };
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
        Theme.Select(Settings.ThemeId);
        SessionEnding += (_, _) => exiting = true;
        Store = new Store(Settings);
        Store.Error += ReportBackgroundError;
        Line = new LineWindow(this);
        MainWindow = Line;
        Exit += (_, _) => { Microsoft.Win32.SystemEvents.UserPreferenceChanged -= TrayThemeChanged; windowsSnips?.Dispose(); watcher?.Dispose(); prune?.Stop(); Native.RemoveClipboardFormatListener(Line.Hwnd); Native.UnregisterHotKey(Line.Hwnd, 1); Native.UnregisterHotKey(Line.Hwnd, 2); tray?.Dispose(); Store.Persist(); activation?.Dispose(); };
        Store.Added += OnPhotoAdded;
        Store.Changed += _ => UpdateTray();
        Store.Hanging += OnPhotoHanging;
        await Store.Restore();
        Store.Removing += (_, quiet) => { if (!quiet) PlaySound(OperationCue.TakeDown); };
        Store.Trashed += _ => PlaySound(OperationCue.TakeDown);
        Store.Copied += _ => PlaySound(OperationCue.Copy);
        Exit += (_, _) => sounds.Dispose();
        if (TestMode)
        {
            if (args.Contains("--demo"))
                CreateTray();
            await SelfTest.Run(this, args.Contains("--self-test"));
            return;
        }
        windowsSnips = new WindowsSnipOrigin(() => Settings.HandleScreenshots && !WorkBlocked && !Settings.GameDoNotDisturb,
            message => Settings.Log("Capture origin " + message));
        ConfigureWindowsSnipIntake(windowsSnips);
        CreateTray();
        watcher = new FolderWatch(Store, Settings);
        Native.AddClipboardFormatListener(Line.Hwnd);
        if (!RegisterShortcut())
            Notify("晾图快捷键不可用", "显示快捷键被其他程序占用。可在快捷键菜单更改。", System.Windows.Forms.ToolTipIcon.Warning);
        if (!RegisterShortcut(true))
            Notify("晾图截图快捷键不可用", "组合键被其他程序占用。请在托盘 → 快捷键中更改框选截图快捷键。", System.Windows.Forms.ToolTipIcon.Warning);
        Settings.Log($"Started version={typeof(App).Assembly.GetName().Version} pid={Environment.ProcessId}, hwnd={Line.Hwnd}, items={Store.Items.Count}, hotkey={HotKeyRegistered}, captureKey={CaptureKeyRegistered}, allowOverFullscreen={Settings.OverFullscreen}, gameDoNotDisturb={Settings.GameDoNotDisturb}");
        prune = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        prune.Tick += (_, _) => Store.Prune();
        prune.Start();
        if (!Settings.Welcomed && !Settings.GameDoNotDisturb)
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
        if(SystemParameters.HighContrast) light=SystemColors.WindowColor.R>128;
        return ThemeIcons.Tray(Theme.Current,light);
    }
    void TrayThemeChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(() => { Theme.Select(Settings.ThemeId); Line.Refresh(); trayIconState = -1; UpdateTray(); });
    void UpdateTray()
    {
        if (tray != null)
        {
            var state = (Settings.ClipboardImages ? 1 : 0) + (Store.Items.Count > 0 ? 2 : 0) + 4*ThemeProfiles.All.ToList().IndexOf(Theme.Current);
            if (state != trayIconState)
            {
                var old = tray.Icon;
                tray.Icon = MakeIcon(Settings.ClipboardImages, Store.Items.Count > 0);
                old?.Dispose();
                trayIconState = state;
            }
            tray.Text = "晾图" + (Settings.GameDoNotDisturb ? " · 游戏勿扰" : "") + " · " + Localization.Text(Store.Items.Count == 0 ? "The line is empty" : "%d on the line").Replace("%d", Store.Items.Count.ToString());
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
        if (capture && !PresentationPolicy.CanCapture(Settings)) { CaptureKeyRegistered = false; return true; }
        bool registered = key == 0 || Native.RegisterHotKey(Line.Hwnd, id, modifiers | 0x4000, key);
        if (capture) CaptureKeyRegistered = registered; else HotKeyRegistered = registered;
        Settings.Log($"Hotkey id={id}, registered={registered}, key={key}, modifiers={modifiers}, error={(registered ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error())}");
        return registered;
    }
    public void Open(Photo p) { if (!WorkBlocked) Theme.Shell(p.Path); }
    public void Preview(Photo p)
    {
        if (WorkBlocked) return;
        var screen = Line.Screen;
        Line.Retract();
        WindowPresenter.Show(new PreviewWindow(this, p), screen);
    }
    public void Edit(Photo p)
    {
        if (WorkBlocked) return;
        var screen = Line.Screen;
        Line.Retract();
        if (p.Video)
            WindowPresenter.Show(new TrimWindow(this, p), screen);
        else
            WindowPresenter.Show(new EditorWindow(this, p), screen);
        PlaySound(OperationCue.Edit);
    }
    public void Discard(Card c)
    {
        Store.TakeDown(c.Photo);
    }
    public void Capture()
    {
        if (WorkBlocked || !PresentationPolicy.CanCapture(Settings) || Line.Busy)
            return;
        Line.Retract();
        _ = CaptureLater(++captureGeneration);
    }
    async Task CaptureLater(int generation)
    {
        await Task.Delay(320);
        if (WorkBlocked || !PresentationPolicy.CanCapture(Settings) || generation != captureGeneration) return;
        new CaptureWindow((image, origin) => { _ = PublishCapture(image, origin); }).Show();
    }
    internal Task<bool> PublishCapture(BitmapSource image, Rect origin, Func<BitmapSource, string, Task>? save = null)
    {
        if (WorkBlocked || !PresentationPolicy.CanCapture(Settings)) return Task.FromResult(false);
        var name = "Screenshot " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");
        var path = Path.Combine(Settings.Inbox, name + ".png");
        for (var suffix = 2; File.Exists(path) || Captures.ContainsPath(path); suffix++) path = Path.Combine(Settings.Inbox, name + " " + suffix + ".png");
        var encoded = false;
        return Captures.Enqueue(path, async () => {
            if (!WorkBlocked) RevealCaptureDestination(origin);
            if (!encoded) {
                if (save != null) await save(image, path);
                else await Task.Run(() => MediaTools.SavePng(image, path));
                encoded = true;
            }
            await Store.Add(path, origin: origin, userInitiated: true);
            if (!Store.Collection.Any(photo => string.Equals(photo.Path, path, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("截图未能加入收藏。文件位置：" + path);
            // Store.Persist logs settings IO failures for background imports. A
            // completed capture needs a strict durable-publication boundary.
            Settings.Save();
        });
    }
    internal void RevealCaptureDestination(Rect origin)
    {
        if (WorkBlocked || !PresentationPolicy.CanCapture(Settings)) return;
        var center = new System.Drawing.Point((int)(origin.X + origin.Width / 2), (int)(origin.Y + origin.Height / 2));
        Line.MoveTo(System.Windows.Forms.Screen.FromPoint(center));
        // A completed built-in capture is explicit intent, but may never break
        // fullscreen protection or game do-not-disturb.
        Line.Reveal(false, 3.0);
    }
    internal void PlaySound(OperationCue cue) => sounds.Play(cue, PresentationPolicy.CanPlaySound(Settings, TestMode));
    internal void ConfigureWindowsSnipIntake(WindowsSnipOrigin? tracker)
    {
        Store.WindowsSnips = tracker;
        Store.PublishValidatedWindowsCapture = (path, publish) => Captures.Enqueue(path, publish);
        Store.OriginAdopted += Line.AdoptPendingOrigin;
        Store.WindowsCaptureAccepted += origin => {
            if (Settings.AutoHang && !WorkBlocked) RevealCaptureDestination(origin);
        };
    }
    internal void OnPhotoAdded(Photo photo)
    {
        if (WorkBlocked) return;
        if (manualMounts.Contains(photo)) Line.Reveal(true,1.8);
        else if (Settings.NewReveal || photo.UserInitiatedCapture || photo.ValidatedWindowsCapture) Line.Reveal(false, 2.5);
        UpdateTray(); PlaySound(OperationCue.Hang);
    }
    internal void OnPhotoHanging(Photo photo)
    {
        if (WorkBlocked || Settings.GameDoNotDisturb) return;
        Native.GetCursorPos(out var point);
        if (photo.Origin is Rect origin) { point.X = (int)(origin.X + origin.Width / 2); point.Y = (int)(origin.Y + origin.Height / 2); }
        Line.MoveTo(System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(point.X, point.Y)),manual:manualMounts.Contains(photo)); Line.ResetScroll();
    }
    internal void SetGameDoNotDisturb(bool enabled)
    {
        if (Settings.GameDoNotDisturb == enabled) return;
        Settings.GameDoNotDisturb = enabled;
        UpdatePinVisibility();
        if (enabled) { ++captureGeneration; sounds.Stop(); Line.SuspendForGameMode(); }
        SaveSettings();
        if (!TestMode && !RegisterShortcut(true)) Notify("晾图截图快捷键不可用", "退出游戏勿扰后，截图快捷键已被其他程序占用，请在快捷键菜单更改。", System.Windows.Forms.ToolTipIcon.Warning);
    }
    internal void ReportBackgroundError(string message)
    {
        Settings.Log("Application notice: " + message);
        if (Settings == null || PresentationPolicy.CanNotify(Settings)) MessageBox.Show(message, "晾图");
    }
    void Notify(string title, string message, System.Windows.Forms.ToolTipIcon icon)
    {
        Settings.Log(title + ": " + message);
        if (PresentationPolicy.CanNotify(Settings)) tray?.ShowBalloonTip(6000, title, message, icon);
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


