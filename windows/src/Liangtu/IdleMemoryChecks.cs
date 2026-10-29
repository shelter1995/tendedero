using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace Liangtu;

// Owned blank-window benchmark. Never reads the clipboard or registers hooks,
// hotkeys, file watchers or the normal application data directory.
internal static class IdleMemoryChecks
{
    [DllImport("psapi.dll", SetLastError = true)] static extern bool QueryWorkingSet(IntPtr process, IntPtr buffer, int size);
    static long PrivateWorkingSet(Process process)
    {
        for (var size = 256 * 1024; size <= 8 * 1024 * 1024; size *= 2) {
            var buffer = Marshal.AllocHGlobal(size);
            try {
                if (!QueryWorkingSet(process.Handle, buffer, size)) continue;
                var count = Marshal.ReadInt64(buffer); long pages = 0;
                for (long i = 0; i < count; i++) if ((Marshal.ReadInt64(buffer, checked((int)((i + 1) * 8))) & 0x100) == 0) pages++;
                return pages * Environment.SystemPageSize;
            } finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    [STAThread]
    public static int Main(string[] args)
    {
        var experiment = args.Length > 2 ? args[2] : "default";
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var snapshots = new List<object>(); var log = new List<string>();
        void Measure(string stage, double response = 0)
        {
            using var process = Process.GetCurrentProcess(); process.Refresh();
            var resident = PrivateWorkingSet(process); var gc = GC.GetGCMemoryInfo();
            var data = new { stage, privateWorkingSetMiB = resident / 1048576d, workingSetMiB = process.WorkingSet64 / 1048576d, privateCommitMiB = process.PrivateMemorySize64 / 1048576d, managedMiB = GC.GetTotalMemory(false) / 1048576d, gcCommittedMiB = gc.TotalCommittedBytes / 1048576d, responseMs = response };
            snapshots.Add(data);
            log.Add($"MEASURE {stage}: privateWS={data.privateWorkingSetMiB:F2}MiB WS={data.workingSetMiB:F2}MiB commit={data.privateCommitMiB:F2}MiB managed={data.managedMiB:F2}MiB response={response:F2}ms");
        }
        Measure("runtime");
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown }; Measure("application");
        app.Startup += async (_, _) => {
            LineWindow? line = null;
            try {
                var settings = new Settings { ThemeId = args.Length > 1 ? args[1] : "color-play", AutoHang = false, HoverReveal = false, NewReveal = false, Sounds = false, Welcomed = true, CollectionRevision = 1 };
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings);
                typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app, true);
                Theme.Select(settings.ThemeId); Measure("theme-selected");
                if (experiment == "preload-only") {
                    log.Add((RasterThemeArt.DecodedCount == 0 ? "PASS " : "FAIL ") + "blank theme selection does not decode unused artwork");
                    return;
                }
                var store = new Store(settings); typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, store); Measure("store-created");
                line = new LineWindow(app, new FullscreenProtection(() => Array.Empty<ScreenWindow>(), () => 0));
                typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, line);
                var target = System.Windows.Interop.HwndSource.FromHwnd(line.Hwnd).CompositionTarget;
                if (experiment == "verify") {
                    log.Add((target.RenderMode == System.Windows.Interop.RenderMode.Default ? "PASS " : "FAIL ") + "shelf retains hardware acceleration for dense content");
                    var initial = typeof(LineWindow).GetProperty("InitialSurfaceSize", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(line);
                    if (initial is System.Drawing.Size size) {
                        log.Add($"MEASURE initial native surface: {size.Width}x{size.Height}");
                    }
                }
                await Task.Delay(500);
                Measure("blank-hidden");
                if (experiment == "verify") {
                    Native.GetWindowRect(line.Hwnd, out var hiddenBounds);
                    log.Add((hiddenBounds.Right-hiddenBounds.Left <= 2 && hiddenBounds.Bottom-hiddenBounds.Top <= 2 ? "PASS " : "FAIL ") + "hidden shelf retains only a minimal hardware surface");
                    log.Add((line.RenderingCallbackCount == 0 ? "PASS " : "FAIL ") + "hidden shelf detaches frame rendering callbacks");
                }
                if (experiment == "verify") log.Add((RenderOptions.ProcessRenderMode == System.Windows.Interop.RenderMode.Default ? "PASS " : "FAIL ") + "process hardware rendering remains available after startup");
                using (var tray = ThemeIcons.Tray(Theme.Current, true)) { Measure("tray-art-created"); }
                await Task.Delay(2000); Measure("idle-before-use");
                var first = new TaskCompletionSource<bool>(); line.ContentRendered += (_, _) => first.TrySetResult(true);
                var watch = Stopwatch.StartNew(); line.Reveal(true, 3);
                await Task.WhenAny(first.Task, Task.Delay(2000)); watch.Stop();
                Measure("first-content-rendered", watch.Elapsed.TotalMilliseconds);
                if (experiment == "verify") log.Add((line.RenderingCallbackCount > 0 ? "PASS " : "FAIL ") + "visible shelf resumes frame rendering callbacks");
                await Task.Delay(1000); line.Retract(); await Task.Delay(600);
                Measure("hidden-after-use");
                if (experiment == "verify") {
                    Native.GetWindowRect(line.Hwnd, out var hiddenBounds);
                    log.Add((hiddenBounds.Right-hiddenBounds.Left <= 2 && hiddenBounds.Bottom-hiddenBounds.Top <= 2 ? "PASS " : "FAIL ") + "retracted shelf releases its large backing surface");
                    log.Add((line.RenderingCallbackCount == 0 ? "PASS " : "FAIL ") + "retracted shelf detaches frame rendering callbacks");
                }
                await Task.Delay(4000); Measure("steady-idle");
                watch.Restart(); line.Reveal(true, 3); watch.Stop(); Measure("warm-reveal", watch.Elapsed.TotalMilliseconds);
                if (experiment == "verify") {
                    line.SuspendForGameMode();
                    var tick = typeof(LineWindow).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<Action>(line);
                    tick(); var allocated = GC.GetAllocatedBytesForCurrentThread(); watch.Restart();
                    for (var i = 0; i < 1000; i++) tick();
                    watch.Stop(); allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    log.Add($"MEASURE 1000 idle polls: {allocated} allocated bytes, {watch.Elapsed.TotalMilliseconds:F2}ms");
                    log.Add((allocated <= 128_000 ? "PASS " : "FAIL ") + "idle pointer polling avoids per-tick screen and geometry allocation");
                    var editor = new Window(); Theme.WindowStyle(editor); new System.Windows.Interop.WindowInteropHelper(editor).EnsureHandle();
                    log.Add((System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(editor).Handle).CompositionTarget.RenderMode == System.Windows.Interop.RenderMode.Default && RenderOptions.ProcessRenderMode == System.Windows.Interop.RenderMode.Default ? "PASS " : "FAIL ") + "ordinary editor targets retain the default hardware preference");
                    editor.Close();
                }
                log.Add("PASS owned blank-window probe completed without forced GC or working-set trimming");
            } catch (Exception error) { log.Add("FAIL " + error); }
            finally {
                line?.Close();
                File.WriteAllLines(Path.Combine(Settings.Root, "results.txt"), log);
                File.WriteAllText(Path.Combine(Settings.Root, "measurements.json"), JsonSerializer.Serialize(snapshots, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine(string.Join(Environment.NewLine, log));
                app.Shutdown(log.Any(x => x.StartsWith("FAIL")) ? 1 : 0);
            }
        };
        return app.Run();
    }
}
