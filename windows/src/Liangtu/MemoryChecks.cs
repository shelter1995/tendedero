using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
namespace Liangtu;

// Synthetic, owned fixtures only. No clipboard, global shortcuts, watchers or user data.
internal static class MemoryChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var log = new List<string>(); var measurements = new List<object>();
        void Check(string name, bool okay) => log.Add((okay ? "PASS " : "FAIL ") + name);
        long ImageBytes(BitmapSource image) => (long)image.PixelWidth * image.PixelHeight * Math.Max(4, (image.Format.BitsPerPixel + 7) / 8);
        long Pixels(Store store) => store.Collection.Select(p => p.Image).Distinct().Sum(ImageBytes);
        void Measure(string stage, Store store, double milliseconds = 0)
        {
            using var process = Process.GetCurrentProcess(); process.Refresh();
            var bytes = Pixels(store);
            measurements.Add(new { stage, workingSetMB = process.WorkingSet64 / 1048576d, privateMB = process.PrivateMemorySize64 / 1048576d, managedMB = GC.GetTotalMemory(false) / 1048576d, decodedMB = bytes / 1048576d, milliseconds });
            log.Add($"MEASURE {stage}: workingSet={process.WorkingSet64/1048576d:F2}MiB private={process.PrivateMemorySize64/1048576d:F2}MiB decoded={bytes/1048576d:F2}MiB time={milliseconds:F2}ms");
        }
        app.Startup += async (_, _) => {
            LineWindow? line = null; BasketWindow? basket = null;
            try {
                var settings = new Settings { AutoHang = false, NewReveal = false, HoverReveal = false, Sounds = false, Welcomed = true, CollectionRevision = 1, ThemeId = "color-play", Keep = 100 };
                var store = new Store(settings);
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings);
                typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, store);
                typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app, true);
                Theme.Select(settings.ThemeId);
                line = new LineWindow(app, new FullscreenProtection(() => Array.Empty<ScreenWindow>(), () => 0));
                typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, line);
                await Task.Delay(400); Measure("cold-idle", store);
                Check("opaque theme avoids initializing unused native glass", !line.LiveGlassAvailable);
                var fixture = Path.Combine(Settings.Inbox, "fixture.png");
                var pixels = new byte[1920 * 1080 * 4];
                for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = (byte)((i / 4) % 251); pixels[i+1] = 140; pixels[i+2] = 210; pixels[i+3] = 255; }
                var bitmap = BitmapSource.Create(1920, 1080, 96, 96, PixelFormats.Bgra32, null, pixels, 1920 * 4); bitmap.Freeze();
                MediaTools.SavePng(bitmap, fixture);
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < 96; i++) { var file = Path.Combine(Settings.Inbox, $"history-{i:000}.png"); File.Copy(fixture, file, true); await store.Add(file); }
                watch.Stop(); await Settle(); Measure("history-96", store, watch.Elapsed.TotalMilliseconds);
                Check("history thumbnail retention is bounded to 16MiB", Pixels(store) <= 17L * 1024 * 1024);
                Check("offscreen cache eviction preserves all 96 files and records", store.Collection.Count == 96 && store.Collection.All(p => File.Exists(p.Path)));
                var old = store.Collection[0];
                Check("evicted photo retains a tiny preview with the original aspect ratio", old.Image.PixelWidth <= 96 && Math.Abs((double)old.Image.PixelWidth/old.Image.PixelHeight - 1920d/1080) < .03);
                watch.Restart(); await store.EnsureThumbnail(old); watch.Stop();
                Check("old thumbnail reloads at original display quality", old.Image.PixelWidth == 600 && MediaTools.LoadImage(old.Path).PixelWidth == 1920);
                Measure("reload-old", store, watch.Elapsed.TotalMilliseconds);
                store.Mount(old); await store.EnsureThumbnail(old);
                var hot = old.Image;
                for (var i = 0; i < store.Collection.Count; i++) await store.EnsureThumbnail(store.Collection[i]);
                Check("mounted image stays hot during history browsing", ReferenceEquals(hot, old.Image) && !old.NeedsThumbnail);
                await Settle(); Measure("mounted-and-browsed", store);
                basket = new BasketWindow(app); basket.Show();
                await Task.Delay(400);
                var realized = Descendants<ListBoxItem>(basket.MediaList).Where(c => c.IsVisible).Select(c => (Photo)c.Content.GetType().GetProperty("Photo")!.GetValue(c.Content)!).ToArray();
                var realizedImages = realized.Select(p => p.Image).ToArray();
                foreach (var photo in store.Collection) await store.EnsureThumbnail(photo);
                Check("visible basket thumbnails stay hot under offscreen cache pressure", realized.Zip(realizedImages).All(pair => !pair.First.NeedsThumbnail && ReferenceEquals(pair.First.Image, pair.Second)));
                var scrollTimes = new List<double>();
                foreach (var index in new[] { 0, 40, 80, 10, 95, 0 }) {
                    watch.Restart(); basket.MediaList.ScrollIntoView(basket.MediaList.Items[index]); basket.MediaList.UpdateLayout();
                    await Task.Delay(160); watch.Stop(); scrollTimes.Add(watch.Elapsed.TotalMilliseconds - 160);
                    var visible = Descendants<ListBoxItem>(basket.MediaList).Where(c => c.IsVisible).ToArray();
                    Check("visible basket rows have decoded thumbnails at scroll " + index, visible.Length > 0 && visible.All(c => !((Photo)c.Content.GetType().GetProperty("Photo")!.GetValue(c.Content)!).NeedsThumbnail));
                }
                await Settle(); Measure("basket-scrolled", store, scrollTimes.Max());
                basket.Close(); basket = null;
                var references = new List<WeakReference>();
                for (var i = 0; i < 4; i++) references.Add(ClosedEditor(app, old));
                await Settle();
                Check("closed editor windows can be collected", references.All(r => !r.IsAlive));
                Measure("windows-closed", store);
                Check("mounted image aspect and pixels survive cache pressure", old.Image.PixelWidth == hot.PixelWidth && old.Image.PixelHeight == hot.PixelHeight && ReferenceEquals(hot, old.Image));
                line.Reveal(true, 3); await Task.Delay(500);
                var times = new List<double>();
                for (var i = 0; i < 20; i++) { watch.Restart(); line.Refresh(); line.UpdateLayout(); watch.Stop(); times.Add(watch.Elapsed.TotalMilliseconds); }
                Measure("hot-shelf-refresh", store, times.OrderBy(x => x).ElementAt(18));
                line.SuspendForGameMode();
                Theme.Select("sky"); Check("switching to glass theme initializes its live material", line.LiveGlassAvailable);
                Theme.Select("linen"); Check("switching to opaque theme releases unused native material", !line.LiveGlassAvailable);
                Theme.Select("sky"); Check("live material can be initialized again after theme changes", line.LiveGlassAvailable);
                Check("inactive theme bitmap caches release their strong references", RasterThemeArt.DecodedCount == 0);
                Theme.Select(settings.ThemeId);
                for (var i = 96; i < 320; i++) { var file = Path.Combine(Settings.Inbox, $"history-{i:000}.png"); File.Copy(fixture, file, true); await store.Add(file); }
                Check("tiny historical previews also have a bounded memory budget", store.Collection.Where(p => p.NeedsThumbnail).Select(p => p.Image).Distinct().Sum(ImageBytes) <= 2L * 1024 * 1024 + 4096);
                await Settle(); Measure("history-320", store);
            } catch (Exception e) { log.Add("FAIL harness " + e); }
            finally {
                basket?.Close(); line?.Close();
                File.WriteAllLines(Path.Combine(Settings.Root, "results.txt"), log);
                File.WriteAllText(Path.Combine(Settings.Root, "measurements.json"), JsonSerializer.Serialize(measurements, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine(string.Join(Environment.NewLine, log));
                app.Shutdown(log.Any(s => s.StartsWith("FAIL")) ? 1 : 0);
            }
        };
        return app.Run();
    }
    static async Task Settle()
    {
        await Task.Delay(180);
        // Measurement only: compare retained resources after the same collection cycle.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        await Task.Delay(180);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference ClosedEditor(App app, Photo photo)
    {
        var editor = new EditorWindow(app, photo); editor.Close(); return new WeakReference(editor);
    }
    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match;
            foreach (var item in Descendants<T>(child)) yield return item;
        }
    }
}
