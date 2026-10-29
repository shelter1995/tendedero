using System.Diagnostics;
using System.Windows.Controls.Primitives;
namespace Liangtu;

public static class VideoScrubChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => {
            var log = new List<string>();
            var settings = new Settings { Sounds = false, Keep = 100 };
            typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings);
            typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, new Store(settings));
            var photo = new Photo { Path = Path.GetFullPath(args[1]), Image = await MediaTools.Thumbnail(Path.GetFullPath(args[1])) };
            var window = new TrimWindow(app, photo); window.Show();
            try
            {
                for (var i = 0; i < 100 && !window.Playback.Ready; i++) await Task.Delay(100);
                if (!window.Playback.Ready) throw new Exception("Media did not open");
                await Task.Delay(3500);
                var changed = 0; var stale = 0; var expected = -1.0;
                window.Playback.Changed += () => { changed++; if (expected >= 0 && Math.Abs(window.Timeline.Position - expected) > .001) stale++; };
                var clock = Stopwatch.StartNew();
                for (var i = 0; i < 240; i++) { expected = 1 + (i % 120) * .03; window.Timeline.SetStart(expected); }
                clock.Stop(); expected = -1;
                log.Add($"240 selection changes: {clock.Elapsed.TotalMilliseconds:F2} ms; notifications={changed}; stale-position notifications={stale}");
                log.Add((stale == 0 ? "PASS" : "FAIL") + " seek never publishes the previous position");
                changed = 0; await Task.Delay(600);
                log.Add($"Paused idle 600ms: notifications={changed}");
                log.Add((changed == 0 ? "PASS" : "FAIL") + " paused player does not invalidate idle timeline");
                var timeline = window.Timeline; timeline.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)timeline.ActualWidth, (int)timeline.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                var allocation = GC.GetAllocatedBytesForCurrentThread(); clock.Restart();
                for (var i = 0; i < 120; i++) { timeline.SetPosition(i / 120.0 * timeline.Range.Duration); timeline.UpdateLayout(); bitmap.Render(timeline); }
                clock.Stop();
                log.Add($"120 timeline render/position updates: {clock.Elapsed.TotalMilliseconds:F2} ms; allocated={GC.GetAllocatedBytesForCurrentThread() - allocation} bytes");
                window.Playback.Seek(1); var nativeBefore = window.Playback.Media.Position.TotalSeconds;
                window.Playback.BeginScrub();
                for (var i = 0; i < 120; i++) window.Playback.Seek(3 + i * .01);
                var latest = 4.19;
                log.Add((Math.Abs(window.Playback.Position - latest) < .001 && Math.Abs(window.Playback.Media.Position.TotalSeconds - nativeBefore) < .001 ? "PASS" : "FAIL") + " drag burst updates UI immediately without issuing every native seek");
                window.Playback.EndScrub();
                log.Add((Math.Abs(window.Playback.Media.Position.TotalSeconds - latest) < .001 ? "PASS" : "FAIL") + " releasing drag applies final position immediately");
                window.Playback.BeginScrub(); window.Playback.Seek(5); window.Playback.Seek(6); await Task.Delay(120);
                log.Add($"Sustained drag at 120ms: requested={window.Playback.Position:F6}s, native={window.Playback.Media.Position.TotalSeconds:F6}s");
                log.Add((Math.Abs(window.Playback.Media.Position.TotalSeconds - 6) < .001 ? "PASS" : "FAIL") + " sustained drag decodes latest pending position");
                window.Playback.EndScrub();
            }
            catch(Exception e) { log.Add("FAIL " + e); }
            finally { window.Close(); File.WriteAllLines(Path.Combine(Settings.Root,"results.txt"),log); app.Shutdown(log.Any(l=>l.StartsWith("FAIL")) ? 1 : 0); }
        };
        return app.Run();
    }
}
