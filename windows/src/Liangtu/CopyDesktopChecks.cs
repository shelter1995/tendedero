using System.Diagnostics;
using System.Runtime.InteropServices;
using ComData = System.Runtime.InteropServices.ComTypes.IDataObject;
namespace Liangtu;

// Explicit desktop diagnostic: synthetic image only; retain the original OLE
// data object for this short test and restore it unless another app copied anew.
internal static class CopyDesktopChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Root);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => {
            var log = new List<string>(); ComData? previous = null; uint ownSequence = 0; bool wrote = false;
            var watch = new Stopwatch();
            void Stamp(string stage) => log.Add($"{watch.Elapsed.TotalMilliseconds:F1}ms {stage}");
            using var sounds = new OperationSounds();
            try {
                Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
                Marshal.ThrowExceptionForHR(OleGetClipboard(out previous));
                var settings = new Settings { Sounds = false, Welcomed = true };
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings);
                var writer = new ClipboardWriter(data => {
                    Stamp("set begin"); System.Windows.Forms.Clipboard.SetDataObject(data, false, 0, 0); wrote = true; ownSequence = GetClipboardSequenceNumber(); Stamp("set complete");
                }, () => { Stamp("flush begin"); Marshal.ThrowExceptionForHR(OleFlushClipboard()); ownSequence = GetClipboardSequenceNumber(); Stamp("flush complete"); }, data => {
                    int result = OleIsCurrentClipboard((ComData)data); Marshal.ThrowExceptionForHR(result); return result == 0;
                });
                var store = new Store(settings, writer); typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, store);
                var photo = new Photo { Path = Path.GetFullPath(args[1]), Image = MediaTools.LoadImage(args[1], 600) };
                var card = new Card(photo, app); card.FinishArrival();
                var host = new Window { Title = "晾图 · 复制耗时诊断", Width = 420, Height = 300, Content = new Border { Padding = new Thickness(50), Background = Theme.WindowSurface, Child = card } };
                Theme.WindowStyle(host); host.Show();
                await Task.Delay(200);
                for (int run = 0; run < 3; run++) {
                    bool renderPending = false, processingPending = true; var rendered = new TaskCompletionSource();
                    EventHandler render = (_, _) => { if (processingPending) { processingPending = false; Stamp("processing rendering"); } if (renderPending) { renderPending = false; Stamp("feedback rendering"); rendered.TrySetResult(); } };
                    void Copied(Photo _) { Stamp("copied event"); card.SetCopied(true); renderPending = true; Stamp("sound begin"); sounds.Play(OperationCue.Copy, true); Stamp("sound returned"); }
                    store.Copied += Copied; CompositionTarget.Rendering += render;
                    try {
                        log.Add($"Run {run + 1}"); watch.Restart(); card.SetCopying(); await Dispatcher.Yield(DispatcherPriority.Render); await store.Copy(photo); Stamp("copy returned");
                        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(4));
                        await Task.Delay(300);
                    } finally { store.Copied -= Copied; CompositionTarget.Rendering -= render; }
                }
                host.Close();
            } catch (Exception e) { log.Add("FAIL " + e); }
            finally {
                if (wrote && GetClipboardSequenceNumber() == ownSequence) {
                    try { await RestoreClipboard(previous, ownSequence, log); }
                    catch (Exception e) { log.Add("RESTORE FAILED " + e.Message); }
                } else log.Add("Clipboard not restored: no test write or a newer external copy exists.");
                if (previous != null && Marshal.IsComObject(previous)) Marshal.ReleaseComObject(previous);
                File.WriteAllLines(Path.Combine(Settings.Root, "timings.txt"), log);
                Console.WriteLine(string.Join(Environment.NewLine, log)); app.Shutdown(log.Any(s => s.StartsWith("FAIL") || s.StartsWith("RESTORE FAILED")) ? 1 : 0);
            }
        };
        return app.Run();
    }
    static async Task RestoreClipboard(ComData? original, uint sequence, List<string> log)
    {
        for (int stage = 0; stage < 2; stage++) {
            for (int attempt = 0; ; attempt++) {
                if (GetClipboardSequenceNumber() != sequence) { log.Add("Restoration stopped for a newer external clipboard update."); return; }
                var result = stage == 0 ? OleSetClipboard(original) : OleFlushClipboard();
                if (result >= 0) { sequence = GetClipboardSequenceNumber(); break; }
                if (result != unchecked((int)0x800401D0) || attempt == 39) Marshal.ThrowExceptionForHR(result);
                await Task.Delay(50);
            }
        }
        log.Add("Original clipboard restored; contents not logged.");
    }
    [DllImport("ole32.dll")] static extern int OleInitialize(IntPtr reserved);
    [DllImport("ole32.dll")] static extern int OleGetClipboard(out ComData? data);
    [DllImport("ole32.dll")] static extern int OleSetClipboard(ComData? data);
    [DllImport("ole32.dll")] static extern int OleFlushClipboard();
    [DllImport("ole32.dll")] static extern int OleIsCurrentClipboard(ComData data);
    [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
}
