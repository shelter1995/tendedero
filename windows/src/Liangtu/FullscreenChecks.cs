using System.Drawing;
namespace Liangtu;

internal static class FullscreenChecks
{
    public static void Run(Action<string, Action> test)
    {
        void Assert(bool ok) { if (!ok) throw new Exception("Fullscreen protection regression"); }
        var screen = new Rectangle(0, 0, 2560, 1440);
        test("borderless fullscreen tolerates small physical edge discrepancies", () => {
            Assert(FullscreenPolicy.Covers(new Rectangle(2, 1, 2556, 1437), screen));
            Assert(!FullscreenPolicy.Covers(new Rectangle(0, 0, 2560, 1392), screen));
        });
        test("small foreground overlay does not uncover fullscreen game", () => {
            Assert(FullscreenPolicy.Occupied(new[] { new ScreenWindow(new Rectangle(900, 20, 700, 180)), new ScreenWindow(screen) }, screen));
        });
        test("ordinary foreground window occludes game while other monitor stays independent", () => {
            Assert(!FullscreenPolicy.Occupied(new[] { new ScreenWindow(new Rectangle(200, 100, 2000, 1200)), new ScreenWindow(screen) }, screen));
            var second = new Rectangle(-1920, -200, 1920, 1080);
            Assert(!FullscreenPolicy.Occupied(new[] { new ScreenWindow(screen) }, second));
        });
        test("short foreground detection gaps hold protection then release", () => {
            double now = 0; IReadOnlyList<ScreenWindow> windows = new[] { new ScreenWindow(screen) };
            var protection = new FullscreenProtection(() => windows, () => now);
            Assert(protection.IsBlocked("game", screen));
            windows = Array.Empty<ScreenWindow>(); now = .2; Assert(protection.IsBlocked("game", screen, true));
            Assert(!protection.IsBlocked("second", new Rectangle(-1920, 0, 1920, 1080), true));
            now = 2; Assert(!protection.IsBlocked("game", screen, true));
        });
        test("fullscreen upgrade resets old allow-over preference once and preserves later explicit opt-in", () => {
            var legacy = new Settings { OverFullscreen = true, FullscreenProtectionRevision = 0, HotkeyKey = 0x71 };
            legacy.Save(); var migrated = Settings.Load();
            Assert(!migrated.OverFullscreen && migrated.FullscreenProtectionRevision == 1 && migrated.HotkeyKey == 0x71);
            migrated.OverFullscreen = true; migrated.Save(); Assert(Settings.Load().OverFullscreen);
        });
        test("fullscreen protection excludes screenshot publishers without excluding games", () => {
            Assert(FullscreenWindows.IsScreenshotProcess("SnippingTool")); Assert(FullscreenWindows.IsScreenshotProcess("ScreenClippingHost"));
            Assert(!FullscreenWindows.IsScreenshotProcess("League of Legends"));
        });
        test("fresh display preflight bypasses snapshot cache when game just appeared", () => {
            double time = 0; IReadOnlyList<ScreenWindow> windows = Array.Empty<ScreenWindow>();
            var protection = new FullscreenProtection(() => windows, () => time);
            Assert(!protection.IsBlocked("main", screen)); windows = new[] { new ScreenWindow(screen) };
            time = .01; Assert(protection.IsBlocked("main", screen, true));
        });
        test("tool overlay and negative-origin monitor preserve fullscreen protection", () => {
            var monitor = new Rectangle(-2560, -200, 2560, 1440);
            Assert(FullscreenPolicy.Occupied(new[] { new ScreenWindow(new Rectangle(-2200, -100, 1600, 800), true), new ScreenWindow(monitor) }, monitor));
        });
    }
    public static void RunUi(App app, Action<string, Action> test)
    {
        var previous = app.Line;
        var settings = app.Settings;
        bool previousAllowed = settings.OverFullscreen; settings.OverFullscreen = false;
        var screen = System.Windows.Forms.Screen.PrimaryScreen!;
        var protection = new FullscreenProtection(() => new[] { new ScreenWindow(screen.Bounds) }, () => 0);
        var line = new LineWindow(app, protection);
        typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, line);
        try {
            test("fullscreen preflight prevents automatic reveal including new screenshot peek", () => {
                line.Reveal(false, 2.5);
                if (line.Revealed || line.IsVisible) throw new Exception("Automatic show reached protected desktop");
            });
            test("fullscreen check retires previous revealed state before moving window", () => {
                typeof(LineWindow).GetProperty(nameof(LineWindow.Revealed))!.SetValue(line, true);
                line.MoveTo(screen);
                if (line.Revealed || line.IsVisible) throw new Exception("Visible state survived move preflight");
            });
            test("entering fullscreen dismisses previously pinned line even when input was busy", () => {
                typeof(LineWindow).GetProperty(nameof(LineWindow.Revealed))!.SetValue(line, true);
                typeof(LineWindow).GetField("openedOnPurpose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(line, true);
                line.Busy = true;
                if (!line.EnforceFullscreenProtection() || line.Revealed || line.IsVisible) throw new Exception("Old manual/busy state bypasses protection");
                line.Busy = false;
            });
            test("explicit fullscreen setting still permits automatic behavior after opt-in", () => {
                settings.OverFullscreen = true;
                if (line.BlocksAutomaticDisplay(screen, true)) throw new Exception("Explicit opt-in ignored");
                settings.OverFullscreen = false;
            });
        } finally { line.Close(); settings.OverFullscreen = previousAllowed; typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, previous); }
    }
}
