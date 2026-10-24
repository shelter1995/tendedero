using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop;
namespace Liangtu;

/// Runs only as a separate test executable, in a private window station. It must
/// never fall back to the interactive station or read the user's clipboard.
internal static class ClipboardIntegrationChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Root);
        IntPtr desktop = IntPtr.Zero;
        if (!args.Contains("--prepare-only")) {
        var station = CreateWindowStation("LiangtuClipboardTest-" + Guid.NewGuid().ToString("N"), 0, 0x37F, IntPtr.Zero);
        if (station == IntPtr.Zero || !SetProcessWindowStation(station)) return Unavailable("private window station");
        desktop = CreateDesktop("default", IntPtr.Zero, IntPtr.Zero, 0, 0x1FF, IntPtr.Zero);
        if (desktop == IntPtr.Zero || !SetThreadDesktop(desktop)) return Unavailable("private desktop");
        }
        // Station/desktop handles live until this isolated process exits.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => {
            if (args.Contains("--latency")) {
                try { await MeasureLatency(!args.Contains("--prepare-only")); app.Shutdown(0); }
                catch (Exception e) { File.WriteAllText(Path.Combine(Settings.Root, "latency.txt"), "FAIL " + e); app.Shutdown(1); }
                return;
            }
            using var ready = new ManualResetEventSlim();
            Exception? holderError = null;
            var holder = new Thread(() => {
                try {
                    if (!SetThreadDesktop(desktop)) throw new System.ComponentModel.Win32Exception();
                    using var window = new HwndSource(new HwndSourceParameters("Clipboard contention fixture") { Width = 1, Height = 1, WindowStyle = unchecked((int)0x80000000) });
                    if (!OpenClipboard(window.Handle)) throw new System.ComponentModel.Win32Exception();
                    try { ready.Set(); Thread.Sleep(600); } finally { CloseClipboard(); }
                } catch (Exception e) { holderError = e; ready.Set(); }
            });
            holder.SetApartmentState(ApartmentState.STA);
            try {
                var image = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 5, 40, 230, 255, 220, 90, 10, 255 }, 8);
                var path = Path.Combine(Settings.Root, "native-copy.png"); MediaTools.SavePng(image, path);
                var store = new Store(new Settings()); int copied = 0, errors = 0, ticks = 0;
                store.Copied += _ => copied++; store.Error += _ => errors++;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) }; timer.Tick += (_, _) => ticks++; timer.Start();
                holder.Start(); if (!ready.Wait(3000)) throw new Exception("Clipboard lock fixture timed out");
                if (holderError != null) throw holderError;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                await store.Copy(new Photo { Path = path }); timer.Stop();
                if (copied != 1 || errors != 0 || ticks < 2 || watch.ElapsedMilliseconds < 400) throw new Exception($"Retry/dispatcher mismatch: copied={copied}, errors={errors}, ticks={ticks}, elapsed={watch.ElapsedMilliseconds}");
                var data = Clipboard.GetDataObject() ?? throw new Exception("No published data");
                if (!data.GetDataPresent("PNG", false) || !data.GetDataPresent(DataFormats.FileDrop) || !data.GetDataPresent(DataFormats.Bitmap) || !data.GetDataPresent("Liangtu.Internal", false)) throw new Exception("Published format missing");
                var actual = new byte[8]; new FormatConvertedBitmap(ClipboardImage.Read(data), PixelFormats.Bgra32, null, 0).CopyPixels(actual, 8, 0);
                if (actual[2] != 230 || actual[3] != 255 || actual[4] != 220) throw new Exception("Published pixels corrupted");
                var message = $"PASS isolated Windows clipboard: held 600ms, copy recovered, dispatcher ticks={ticks}, PNG/Bitmap/FileDrop/internal marker and exact pixels verified. Interactive clipboard untouched.";
                File.WriteAllText(Path.Combine(Settings.Root, "result.txt"), message); Console.WriteLine(message); app.Shutdown(0);
            } catch (Exception e) { File.WriteAllText(Path.Combine(Settings.Root, "result.txt"), "FAIL " + e); Console.WriteLine(e); app.Shutdown(1); }
            finally { if (holder.IsAlive) holder.Join(2000); }
        };
        return app.Run();
    }
    static int Unavailable(string stage)
    {
        var message = $"UNAVAILABLE {stage}: Win32 error {Marshal.GetLastWin32Error()}; no interactive clipboard access attempted.";
        File.WriteAllText(Path.Combine(Settings.Root, "result.txt"), message); Console.WriteLine(message); return 2;
    }
    static async Task MeasureLatency(bool native)
    {
        var log = new List<string> { $"SystemDoubleClickTime={Native.GetDoubleClickTime()}ms; {(native ? "private Windows clipboard" : "REAL image preparation + controlled writer (no native clipboard/flush measurement)")}; synthetic PNG; three copies per size." };
        foreach (var size in new[] { (1920, 1080), (3840, 2160) }) {
            var pixels = new byte[size.Item1 * size.Item2 * 4]; var random = new Random(7);
            for (int i = 0; i < pixels.Length; i += 4) { var v = random.Next(256); pixels[i] = (byte)v; pixels[i + 1] = (byte)(v / 2); pixels[i + 2] = (byte)(255 - v); pixels[i + 3] = 255; }
            var path = Path.Combine(Settings.Root, $"fixture-{size.Item1}.png");
            MediaTools.SavePng(BitmapSource.Create(size.Item1, size.Item2, 96, 96, PixelFormats.Bgra32, null, pixels, size.Item1 * 4), path);
            DataObject? published = null;
            var store = native ? new Store(new Settings()) : new Store(new Settings(), new ClipboardWriter(data => published = data, () => { }, _ => true)); var times = new List<double>();
            for (int i = 0; i < 3; i++) {
                var timer = System.Diagnostics.Stopwatch.StartNew(); await store.Copy(new Photo { Path = path }); times.Add(timer.Elapsed.TotalMilliseconds);
                if (native ? !Clipboard.ContainsImage() || !Clipboard.ContainsData("PNG") : published?.GetDataPresent("PNG", false) != true) throw new Exception("Published clipboard data missing");
            }
            log.Add($"{size.Item1}x{size.Item2}: CopyToSuccessMs={string.Join(",", times.Select(v => v.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)))}; MedianMs={times.OrderBy(v => v).ElementAt(1):F1}");
        }
        File.WriteAllLines(Path.Combine(Settings.Root, "latency.txt"), log); Console.WriteLine(string.Join(Environment.NewLine, log));
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateWindowStation(string name, uint flags, uint access, IntPtr security);
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetProcessWindowStation(IntPtr station);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode, uint flags, uint access, IntPtr security);
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetThreadDesktop(IntPtr desktop);
    [DllImport("user32.dll", SetLastError = true)] static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32.dll")] static extern bool CloseClipboard();
}
