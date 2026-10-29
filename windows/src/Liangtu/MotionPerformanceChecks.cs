using System.Diagnostics;
namespace Liangtu;

// Owned synthetic fixtures only: no clipboard, capture, user files or recycle calls.
internal static class MotionPerformanceChecks
{
    sealed class CountingCanvas : Canvas
    {
        public int Arranges;
        protected override Size ArrangeOverride(Size size) { Arranges++; return base.ArrangeOverride(size); }
    }
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var log = new List<string>();
        void Test(string name, Action body) { try { body(); log.Add("PASS " + name); } catch (Exception e) { log.Add("FAIL " + name + ": " + e); } }
        void Assert(bool okay, string error) { if (!okay) throw new Exception(error); }
        app.Startup += async (_, _) => {
            LineWindow? line = null;
            try {
                var settings = new Settings { AutoHang = false, NewReveal = false, HoverReveal = false, Keep = 100, Sounds = false };
                var store = new Store(settings);
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings);
                typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, store);
                typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app, true);
                line = new LineWindow(app, new FullscreenProtection(() => Array.Empty<ScreenWindow>(), () => 0));
                typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, line);
                store.Hanging += app.OnPhotoHanging; store.Added += app.OnPhotoAdded;
                var bitmap = BitmapSource.Create(320, 180, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)180, 320 * 180 * 4).ToArray(), 320 * 4); bitmap.Freeze();
                var external = Path.Combine(Settings.Inbox, "external.png"); MediaTools.SavePng(bitmap, external);
                await store.Add(external);
                Test("background import respects AutoHang/NewReveal and keeps unknown origin unknown", () => Assert(store.Items.Count == 0 && !line.IsVisible && store.Collection[0].Origin == null, "Background intake overrode preferences"));
                var screen = line.Screen.Bounds; var origin = new Rect(screen.Left + 180, screen.Top + screen.Height * .52, 320, 180);
                app.RevealCaptureDestination(origin);
                Test("completed built-in capture reveals before image encoding and thumbnail load", () => Assert(line.IsVisible && line.Revealed && store.Items.Count == 0, "Reveal was deferred"));
                Test("shelf layered window excludes unused desktop raster area", () => {
                    Native.GetWindowRect(line.Hwnd, out var actual);
                    var dpi = Native.GetDpiForWindow(line.Hwnd) / 96d;
                    var expected = Math.Min(screen.Height, Math.Ceiling((line.PanelHeight + 140) * dpi));
                    log.Add($"MEASURE shelf HWND={actual.Right - actual.Left}x{actual.Bottom - actual.Top}px, full monitor={screen.Width}x{screen.Height}px, retainedArea={(actual.Bottom - actual.Top) / (double)screen.Height:P1}, renderTier={RenderCapability.Tier >> 16}, dpi={dpi:F2}, animations={SystemParameters.ClientAreaAnimation}");
                    Assert(Math.Abs(actual.Bottom - actual.Top - expected) <= 1 && !line.ExtendedTransferSurface, "Layered window remains full-monitor");
                });
                var capture = Path.Combine(Settings.Inbox, "capture.png"); MediaTools.SavePng(bitmap, capture);
                await store.Add(capture, origin: origin, userInitiated: true);
                Test("explicit capture hangs with automatic intake disabled", () => Assert(store.Items.Count == 1 && store.Items[0].UserInitiatedCapture, "Explicit capture lost"));
                var surface = ((Canvas)line.Content).Children.OfType<Canvas>().Single();
                var card = surface.Children.OfType<Card>().Single();
                var flights = new List<Rect>(); var rendered = new List<Rect>(); var frameTimes = new List<double>();
                System.Windows.Interop.RenderMode? flightMode = null;
                var watch = Stopwatch.StartNew(); double last = 0;
                EventHandler sampling = (_, _) => {
                    var now = watch.Elapsed.TotalMilliseconds; if (last > 0) frameTimes.Add(now - last); last = now;
                    if (card.ArrivalFlightBounds is Rect r) {
                        flights.Add(r); rendered.Add(card.ArrivalRenderedBounds!.Value);
                        if (flightMode == null && app.Windows.OfType<ArrivalFlight>().FirstOrDefault(w => w.IsVisible) is ArrivalFlight flight)
                            flightMode = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(flight).Handle).CompositionTarget.RenderMode;
                    }
                };
                CompositionTarget.Rendering += sampling;
                await Task.Delay(2100);
                CompositionTarget.Rendering -= sampling;
                Test("large animated flight keeps the default acceleration preference", () => Assert(!SystemParameters.ClientAreaAnimation || flightMode == System.Windows.Interop.RenderMode.Default, "Animated desktop canvas was forced into software rendering"));
                foreach (var pair in flights.Zip(rendered).Where(p => (p.First.TopLeft - p.Second.TopLeft).Length >= 1 || (p.First.BottomRight - p.Second.BottomRight).Length >= 1).Take(3)) log.Add($"MEASURE flight expected={pair.First} actual={pair.Second}");
                Test("hidden-to-visible capture flies from actual physical screen region", () => {
                    if (!SystemParameters.ClientAreaAnimation) { Assert(!card.AwaitingFirstArrival, "Reduced motion left pending photo"); return; }
                    Assert(flights.Count > 8, "Missing visible flight");
                    Assert((flights[0].TopLeft - origin.TopLeft).Length < 65 && Math.Abs(flights[0].Width - origin.Width) < 30, "Flight source is not captured desktop bounds");
                    Assert(flights.Select(r => Math.Round(r.Y)).Distinct().Count() > 8, "Flight did not move");
                    Assert(flights.Zip(rendered).All(p => (p.First.TopLeft - p.Second.TopLeft).Length < 1 && (p.First.BottomRight - p.Second.BottomRight).Length < 1), "Window DPI transform distorted desktop flight");
                    Assert(card.ArrivalFlightBounds == null && !card.AwaitingFirstArrival, "Flight did not retire");
                });
                Test("Bezier endpoints preserve negative monitor coordinates and scale", () => {
                    foreach (var dpi in new[] { 1d, 1.25, 1.5, 2 }) {
                        var from = new Rect(-3600, -600, 800 * dpi, 450 * dpi); var to = new Rect(320, -1100, 140 * dpi, 90 * dpi);
                        Assert(MediaTransferVisual.Travel(from, to, 0) == from && MediaTransferVisual.Travel(from, to, 1) == to, "Incorrect desktop coordinates");
                    }
                });
                if (frameTimes.Count > 0) { var sorted = frameTimes.OrderBy(x => x).ToArray(); log.Add($"MEASURE arrival frames={sorted.Length}, median={sorted[sorted.Length / 2]:F2}ms, p95={sorted[(int)(sorted.Length * .95)]:F2}ms, max={sorted[^1]:F2}ms, over33ms={sorted.Count(x => x > 33)}"); }
                Test("reflow commits layout once and interrupts continuously", () => {
                    var item = new Border(); Canvas.SetLeft(item, 100); Canvas.SetTop(item, 20);
                    LineMotion.Place(item, 300, 30, true);
                    Assert(Canvas.GetLeft(item) == 300 && !item.HasAnimatedProperties, "Canvas layout is animated");
                    var current = Canvas.GetLeft(item) + LineMotion.Reflow(item).X;
                    LineMotion.Place(item, 500, 40, true);
                    Assert(Math.Abs(Canvas.GetLeft(item) + LineMotion.Reflow(item).X - current) < .01 || !SystemParameters.ClientAreaAnimation, "Interrupted reflow jumps");
                    LineMotion.Place(item, 800, 60, false);
                    Assert(!LineMotion.Reflow(item).HasAnimatedProperties && LineMotion.Reflow(item).X == 0, "Display reset retained clock");
                });
                Test("render-only reflow eliminates per-frame arrange passes", () => {
                    var panel = new CountingCanvas { Width = 1200, Height = 260 };
                    var children = Enumerable.Range(0, 24).Select(_ => new Border { Width = 90, Height = 100 }).ToArray();
                    foreach (var child in children) { panel.Children.Add(child); Canvas.SetTop(child, 20); }
                    panel.Measure(new Size(1200, 260)); panel.Arrange(new Rect(0, 0, 1200, 260));
                    long Run(bool renderOnly) {
                        panel.Arranges = 0; var timer = Stopwatch.StartNew();
                        for (int f = 0; f < 180; f++) { for (int i = 0; i < children.Length; i++) { if (renderOnly) LineMotion.Reflow(children[i]).X = i * 40 + f * .1; else Canvas.SetLeft(children[i], i * 40 + f * .1); } panel.UpdateLayout(); }
                        timer.Stop(); log.Add($"MEASURE {(renderOnly ? "transform" : "legacy Canvas")}: 24cards/180frames, arranges={panel.Arranges}, ui={timer.Elapsed.TotalMilliseconds:F2}ms"); return panel.Arranges;
                    }
                    var old = Run(false); var improved = Run(true); Assert(old >= 179 && improved <= 1, "Layout work not eliminated");
                });
                Theme.Select("sky"); line.Refresh(); line.Reveal(true, 4); await Task.Delay(2900);
                var count = line.NativeGlassUpdateCount; await Task.Delay(300);
                Test("idle live glass does not resubmit unchanged native geometry", () => Assert(line.NativeGlassUpdateCount - count <= 1, "Repeated native region reconstruction"));
                foreach (var theme in new[] { "linen", "color-play" }) {
                    line.SuspendForGameMode(); Theme.Select(theme); line.Refresh();
                    Test(theme + " basket artwork remains cached across 120 animation frames", () => {
                        var basket = (BasketVisual)surface.Children.OfType<Button>().Single().Content;
                        basket.Measure(new Size(72, 86)); basket.Arrange(new Rect(0, 0, 72, 86));
                        var render = new RenderTargetBitmap(72, 86, 96, 96, PixelFormats.Pbgra32); render.Render(basket);
                        var before = basket.ArtworkBuildCount; var timer = Stopwatch.StartNew();
                        for (int f = 0; f < 120; f++) { basket.Sway = Math.Sin(f / 10d); basket.InvalidateVisual(); basket.UpdateLayout(); render.Render(basket); }
                        log.Add($"MEASURE {theme} basket120frames={timer.Elapsed.TotalMilliseconds:F2}ms, artworkRebuilds={basket.ArtworkBuildCount - before}");
                        Assert(basket.ArtworkBuildCount == before, "Animation rebuilt bitmap/vector artwork");
                    });
                    var file = Path.Combine(Settings.Inbox, theme + "-capture.png"); MediaTools.SavePng(bitmap, file);
                    app.RevealCaptureDestination(origin); await store.Add(file, origin: origin, userInitiated: true);
                    frameTimes.Clear(); watch.Restart(); last = 0; flights.Clear(); rendered.Clear();
                    card = surface.Children.OfType<Card>().Last(c => c.Photo.Path == file);
                    CompositionTarget.Rendering += sampling; await Task.Delay(1900); CompositionTarget.Rendering -= sampling;
                    Test(theme + " hidden screenshot arrival completes on physical desktop path", () => Assert(!SystemParameters.ClientAreaAnimation || flights.Count > 8 && card.ArrivalFlightBounds == null && flights.Zip(rendered).All(p => (p.First.TopLeft - p.Second.TopLeft).Length < 1), "Missing or distorted themed arrival"));
                    if (frameTimes.Count > 0) { var sorted = frameTimes.OrderBy(x => x).ToArray(); log.Add($"MEASURE {theme} arrival frames={sorted.Length}, median={sorted[sorted.Length / 2]:F2}ms, p95={sorted[(int)(sorted.Length * .95)]:F2}ms, max={sorted[^1]:F2}ms, over33ms={sorted.Count(x => x > 33)}"); }
                }
                line.Reveal(true, 5);
                var bottom = new Rect(screen.Left + 250, screen.Bottom - 220, 260, 180);
                line.ReceiveFromScreen(card.Photo, bottom);
                await Task.Delay(80);
                Test("bottom-of-desktop basket return expands native surface without clipping", () => {
                    if (!SystemParameters.ClientAreaAnimation) return;
                    Native.GetWindowRect(line.Hwnd, out var actual);
                    var transfer = surface.Children.OfType<MediaTransferVisual>().Single();
                    Assert(line.ExtendedTransferSurface && actual.Bottom == screen.Bottom && transfer.CurrentBounds.Bottom <= line.Height + 1, "Return flight clipped by compact window");
                });
                await Task.Delay(850);
                Test("basket return retires flight and restores compact surface", () => Assert(!line.ExtendedTransferSurface && line.ActiveTransferCount == 0, "Transfer retained full desktop surface"));
                var pinned = false;
                var pinPrepared = line.PreparePinTransfer(card.Photo, bottom, () => pinned = true);
                if (pinPrepared) store.TakeDown(card.Photo, quiet: true);
                Test("desktop pin travels below the shelf on its own bounded surface", () => {
                    if(!SystemParameters.ClientAreaAnimation)return;
                    Native.GetWindowRect(line.Hwnd,out var actual);
                    var desktop=app.Windows.OfType<ArrivalFlight>().Single();
                    Assert(pinPrepared&&!line.ExtendedTransferSurface&&actual.Bottom<screen.Bottom&&desktop.SurfaceBounds.Bottom>=bottom.Bottom,"Pin resized the shelf or clipped its independent surface");
                });
                await Task.Delay(1050);
                Test("pin completion retires its desktop surface and keeps compact shelf geometry", () => Assert(!SystemParameters.ClientAreaAnimation || pinned && !line.ExtendedTransferSurface && line.ActiveTransferCount == 0&&!app.Windows.OfType<ArrivalFlight>().Any(), "Pin completion retained a desktop surface"));
                settings.GameDoNotDisturb = true; line.SuspendForGameMode(); app.RevealCaptureDestination(origin);
                Test("capture reveal obeys game do-not-disturb", () => Assert(!line.IsVisible && !line.Revealed && card.ArrivalFlightBounds == null, "Game mode leaked overlay"));
                settings.GameDoNotDisturb = false; Theme.Select("color-play");
                for (var i = 0; i < 12; i++) store.Items.Add(new Photo { Path = "owned-stress-" + i + ".png", Image = card.Photo.Image });
                line.Refresh(); line.Reveal(true, 5); await Task.Delay(400);
                frameTimes.Clear(); watch.Restart(); last = 0;
                EventHandler shelfSampling = (_, _) => { var now = watch.Elapsed.TotalMilliseconds; if (last > 0) frameTimes.Add(now-last); last=now; };
                CompositionTarget.Rendering += shelfSampling; await Task.Delay(1400); CompositionTarget.Rendering -= shelfSampling;
                Test("twelve visible cards keep smooth shelf animation", () => {
                    if (!SystemParameters.ClientAreaAnimation) return;
                    var sorted = frameTimes.OrderBy(x => x).ToArray();
                    Assert(sorted.Length > 35, "Too few shelf frames");
                    var median = sorted[sorted.Length/2]; var p95 = sorted[(int)(sorted.Length*.95)];
                    log.Add($"MEASURE 12-card shelf frames={sorted.Length}, median={median:F2}ms, p95={p95:F2}ms, max={sorted[^1]:F2}ms");
                    Assert(median < 22, "Shelf animation fell below its desktop frame cadence");
                });
                store.Items.Clear(); line.Refresh(); await Task.Delay(1100);
                var lifecyclePhoto = store.Collection[0]; store.Mount(lifecyclePhoto); line.Reveal(true, 30); await Task.Delay(1650);
                var decodesBeforeDeparture = RasterThemeArt.DecodeOperations;
                store.TakeDown(lifecyclePhoto); await Task.Delay(1100);
                Test("final take-down keeps peg assets hot until its animation finishes", () => Assert(RasterThemeArt.DecodeOperations == decodesBeforeDeparture, "Departure decoded retired peg assets"));
                Test("final take-down releases peg roots after completion", () => Assert(!RasterThemeArt.HasDecodedPegs(Theme.Current.Id), "Unused peg assets remain rooted"));
                store.Mount(lifecyclePhoto); line.Reveal(true, 30); await Task.Delay(1650);
                var decodesBeforePin = RasterThemeArt.DecodeOperations; var pinDone = false;
                var pinStarted = line.PreparePinTransfer(lifecyclePhoto, bottom, () => pinDone = true);
                store.TakeDown(lifecyclePhoto); await Task.Delay(1100);
                Test("final desktop-pin flight keeps peg assets hot and releases them on completion", () => Assert((!SystemParameters.ClientAreaAnimation || pinStarted && pinDone) && RasterThemeArt.DecodeOperations == decodesBeforePin && !RasterThemeArt.HasDecodedPegs(Theme.Current.Id), "Pin decoded or retained unused peg assets"));
                SoundChecks.Run(Test);
                Test("recycle confirmation is silent and defaults to cancellation", () => {
                    var dialog = new SilentConfirmation("Owned fixture; do not delete", null);
                    var buttons = ((StackPanel)dialog.Content).Children.OfType<StackPanel>().Single().Children.OfType<Button>().ToArray();
                    Assert(buttons[0].IsDefault && buttons[0].IsCancel && !buttons[1].IsDefault, "Unsafe confirmation default"); dialog.Close();
                });
            } catch (Exception e) { log.Add("FAIL harness " + e); }
            finally { line?.Close(); File.WriteAllLines(Path.Combine(Settings.Root, "results.txt"), log); Console.WriteLine(string.Join(Environment.NewLine, log)); app.Shutdown(log.Any(x => x.StartsWith("FAIL")) ? 1 : 0); }
        };
        return app.Run();
    }
}
