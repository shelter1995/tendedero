namespace Liangtu;

internal static class GameModeChecks
{
    public static void Run(Action<string, Action> test)
    {
        void Assert(bool ok) { if (!ok) throw new Exception("Game do-not-disturb regression"); }
        test("game mode only allows the show hotkey despite all automatic preferences being enabled", () => {
            var settings = new Settings { GameDoNotDisturb = true, HoverReveal = true, NewReveal = true, OverFullscreen = true };
            Assert(!PresentationPolicy.CanReveal(settings, RevealTrigger.Automatic));
            Assert(!PresentationPolicy.CanReveal(settings, RevealTrigger.UserAction));
            Assert(PresentationPolicy.CanReveal(settings, RevealTrigger.Hotkey));
            Assert(!PresentationPolicy.CanCapture(settings));
        });
        test("game mode silences operation sounds and background notices", () => {
            var settings = new Settings { GameDoNotDisturb = true, Sounds = true };
            Assert(!PresentationPolicy.CanPlaySound(settings, false) && !PresentationPolicy.CanNotify(settings));
            settings.GameDoNotDisturb = false;
            Assert(PresentationPolicy.CanPlaySound(settings, false) && PresentationPolicy.CanNotify(settings));
            settings.Sounds = false; Assert(!PresentationPolicy.CanPlaySound(settings, false));
        });
        test("game mode persists without changing reveal sound or clipboard preferences", () => {
            var settings = new Settings { GameDoNotDisturb = true, Sounds = false, HoverReveal = true, NewReveal = false, OverFullscreen = true, FullscreenProtectionRevision = 1, ClipboardImages = true };
            settings.Save(); var restored = Settings.Load();
            Assert(restored.GameDoNotDisturb && !restored.Sounds && restored.HoverReveal && !restored.NewReveal && restored.OverFullscreen && restored.ClipboardImages);
            restored.GameDoNotDisturb = false;
            Assert(!PresentationPolicy.CanPlaySound(restored, false) && PresentationPolicy.CanReveal(restored, RevealTrigger.UserAction));
        });
    }
    public static async Task RunUi(App app, Action<string, Action> test)
    {
        var previousLine = app.Line; var previousStore = app.Store; var previousSettings = app.Settings; var previousTestMode = app.TestMode;
        var settings = new Settings { Sounds = true, HoverReveal = true, NewReveal = true, OverFullscreen = true, FullscreenProtectionRevision = 1, Keep = 100 };
        var store = new Store(settings);
        typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings);
        typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, store);
        typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app, true);
        var line = new LineWindow(app, new FullscreenProtection(() => Array.Empty<ScreenWindow>(), () => 0));
        typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, line);
        store.Hanging += app.OnPhotoHanging; store.Added += app.OnPhotoAdded;
        try {
            test("entering game mode immediately clears previous visible and pinned state", () => {
                typeof(LineWindow).GetProperty(nameof(LineWindow.Revealed))!.SetValue(line, true);
                line.Busy = true; app.SetGameDoNotDisturb(true); line.Busy = false;
                if (line.Revealed || line.IsVisible || !Settings.Load().GameDoNotDisturb) throw new Exception("Entry did not hide or persist");
                if (!settings.Sounds || !settings.HoverReveal || !settings.NewReveal || !settings.OverFullscreen) throw new Exception("Original preferences changed");
            });
            test("game mode blocks startup duplicate activation tray toggle and automatic peek", () => {
                line.Reveal(); line.Reveal(true); line.ShowFromPointer(); line.Toggle();
                if (line.Revealed || line.IsVisible) throw new Exception("Non-hotkey show bypassed game mode");
            });
            var pixels = Enumerable.Repeat((byte)255, 20 * 16 * 4).ToArray();
            var image = BitmapSource.Create(20, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 80);
            var path = Path.Combine(Settings.Root, "game-mode-intake.png"); MediaTools.SavePng(image, path);
            await store.Add(path);
            test("game mode accepts and persists new screenshots without revealing", () => {
                if (store.Items.Count != 1 || !File.Exists(path) || !Settings.Load().Items.Contains(path) || line.Revealed || line.IsVisible) throw new Exception("Intake interrupted or triggered display");
            });
            test("game mode does not move or dismiss an explicitly hotkey-opened line on intake", () => {
                typeof(LineWindow).GetProperty(nameof(LineWindow.Revealed))!.SetValue(line, true);
                typeof(LineWindow).GetField("fullscreenManualOverride", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(line, true);
                Native.GetWindowRect(line.Hwnd, out var before);
                app.OnPhotoHanging(store.Items[0]); app.OnPhotoAdded(store.Items[0]);
                Native.GetWindowRect(line.Hwnd, out var after);
                if (!line.Revealed || line.EnforceFullscreenProtection() || before.Left != after.Left || before.Top != after.Top) throw new Exception("Intake disturbed manual display");
                line.SuspendForGameMode();
            });
            test("game mode tray exposes checked toggle and disabled show capture sound controls", () => {
                var menu = app.CreateTrayMenu(); var items = menu.Items.OfType<MenuItem>().ToArray();
                if (!items[0].IsChecked || !items[0].IsEnabled || items.Single(i => i.Header?.ToString()?.StartsWith("显示晾衣绳") == true).IsEnabled) throw new Exception("Mode controls incorrect");
                if (items.Single(i => i.Header?.ToString() == "框选截图").IsEnabled || items.Single(i => i.Header?.ToString() == "声音（勿扰中暂停）").IsEnabled) throw new Exception("Paused controls still enabled");
            });
            test("leaving game mode restores preferences without replaying old screenshot popups", () => {
                app.SetGameDoNotDisturb(false);
                if (settings.GameDoNotDisturb || !settings.Sounds || !settings.HoverReveal || !settings.NewReveal || !settings.OverFullscreen || line.IsVisible || line.Revealed) throw new Exception("Exit changed preferences or reopened line");
                if (!PresentationPolicy.CanCapture(settings) || !PresentationPolicy.CanNotify(settings)) throw new Exception("Exit did not restore normal policy");
            });
        } finally {
            store.Hanging -= app.OnPhotoHanging; store.Added -= app.OnPhotoAdded; line.Close();
            typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, previousLine);
            typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, previousStore);
            typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, previousSettings);
            typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app, previousTestMode);
        }
    }
}
