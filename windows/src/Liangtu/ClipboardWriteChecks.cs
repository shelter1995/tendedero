using System.Runtime.InteropServices;
using System.Threading;
namespace Liangtu;

internal static class ClipboardWriteChecks
{
    const int Busy = unchecked((int)0x800401D0);
    public static async Task<List<string>> Run()
    {
        var log = new List<string>();
        void Assert(bool value) { if (!value) throw new InvalidOperationException("Clipboard write regression"); }
        async Task Check(string name, Func<Task> body)
        {
            try { await body(); log.Add("PASS " + name); }
            catch (Exception e) { log.Add("FAIL " + name + ": " + e); }
        }
        await Check("clipboard write recovers from temporary OpenClipboard failure on STA", async () => {
            int attempts = 0, flushes = 0, yielded = 0, thread = Environment.CurrentManagedThreadId;
            var writer = new ClipboardWriter(_ => {
                Assert(Environment.CurrentManagedThreadId == thread && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA);
                if (++attempts < 3) throw new COMException("OpenClipboard", Busy);
            }, () => flushes++, _ => true, async (_, token) => { await Task.Yield(); token.ThrowIfCancellationRequested(); yielded++; });
            Assert(await writer.WriteAsync(new DataObject(), default));
            Assert(attempts == 3 && flushes == 1 && yielded >= 2);
        });
        await Check("clipboard flush retries without publishing image again", async () => {
            int sets = 0, flushes = 0;
            var writer = new ClipboardWriter(_ => sets++, () => { if (++flushes < 3) throw new ExternalException("Flush busy", Busy); }, _ => true, (_, _) => Task.CompletedTask);
            Assert(await writer.WriteAsync(new DataObject(), default)); Assert(sets == 1 && flushes == 3);
        });
        await Check("clipboard write has a bounded busy timeout", async () => {
            int attempts = 0;
            var writer = new ClipboardWriter(_ => { attempts++; throw new COMException("Busy", Busy); }, () => throw new Exception("Must not flush"), _ => true, (_, _) => Task.CompletedTask);
            bool failed = false;
            try { await writer.WriteAsync(new DataObject(), default); } catch (ExternalException e) when (e.HResult == Busy) { failed = true; }
            Assert(failed && attempts > 1 && attempts <= 25);
        });
        await Check("clipboard write does not retry unrelated access errors", async () => {
            int attempts = 0;
            var writer = new ClipboardWriter(_ => { attempts++; throw new UnauthorizedAccessException(); }, () => { }, _ => true, (_, _) => throw new Exception("Unexpected retry"));
            bool failed = false; try { await writer.WriteAsync(new DataObject(), default); } catch (UnauthorizedAccessException) { failed = true; }
            Assert(failed && attempts == 1);
        });
        await Check("clipboard copy cancelled before retry cannot overwrite newer request", async () => {
            int attempts = 0;
            using var cancellation = new CancellationTokenSource();
            var writer = new ClipboardWriter(_ => { attempts++; throw new COMException("Busy", Busy); }, () => { }, _ => true, (_, _) => { cancellation.Cancel(); return Task.CompletedTask; });
            bool cancelled = false; try { await writer.WriteAsync(new DataObject(), cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
            Assert(cancelled && attempts == 1);
        });
        await Check("clipboard replaced by another application is not flushed or republished", async () => {
            int sets = 0, flushes = 0;
            var writer = new ClipboardWriter(_ => sets++, () => flushes++, _ => false, (_, _) => Task.CompletedTask);
            Assert(!await writer.WriteAsync(new DataObject(), default)); Assert(sets == 1 && flushes == 0);
        });
        var path = Path.Combine(Settings.Root, "copy-fixture.png");
        var pixels = new byte[] { 20, 80, 240, 255, 220, 100, 10, 255 };
        MediaTools.SavePng(BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 8), path);
        var photo = new Photo { Path = path };
        await Check("store copy reports success once after retry and preserves all image formats", async () => {
            int attempts = 0, notified = 0, errors = 0;
            var writer = new ClipboardWriter(data => {
                if (++attempts < 3) throw new COMException("Busy", Busy);
                Assert(((string[])data.GetData(DataFormats.FileDrop))[0] == path && data.GetDataPresent("Liangtu.Internal", false));
                Assert(data.GetDataPresent(DataFormats.Bitmap) && data.GetDataPresent("PNG", false));
                var actual = new byte[8]; new FormatConvertedBitmap(ClipboardImage.Read(data), PixelFormats.Bgra32, null, 0).CopyPixels(actual, 8, 0);
                Assert(actual.SequenceEqual(pixels));
            }, () => Assert(notified == 0), _ => true, (_, _) => Task.CompletedTask);
            var store = new Store(new Settings(), writer); store.Copied += p => { Assert(p == photo); notified++; }; store.Error += _ => errors++;
            await store.Copy(photo); Assert(attempts == 3 && notified == 1 && errors == 0);
        });
        await Check("store clipboard busy timeout reports one friendly error and keeps screenshot", async () => {
            int notifications = 0; var errors = new List<string>();
            var writer = new ClipboardWriter(_ => throw new COMException("Busy", Busy), () => { }, _ => true, (_, _) => Task.CompletedTask);
            var store = new Store(new Settings(), writer); store.Copied += _ => notifications++; store.Error += errors.Add;
            try { await store.Copy(photo); } catch (ExternalException e) when (e.HResult == Busy) { }
            Assert(notifications == 0 && errors.Count == 1 && errors[0].Contains("暂时被其他程序占用") && File.Exists(path));
        });
        await Check("store newest copy cancels an older pending image without stale success", async () => {
            var otherPath = Path.Combine(Settings.Root, "copy-fixture-new.png"); File.Copy(path, otherPath, true);
            var other = new Photo { Path = otherPath };
            var gate = new TaskCompletionSource(); int oldAttempts = 0, errors = 0; var successes = new List<Photo>();
            var writer = new ClipboardWriter(data => {
                if (((string[])data.GetData(DataFormats.FileDrop))[0] == path) { oldAttempts++; throw new COMException("Busy", Busy); }
            }, () => { }, _ => true, (ms, _) => ms == 150 ? gate.Task : Task.CompletedTask);
            var store = new Store(new Settings(), writer); store.Copied += successes.Add; store.Error += _ => errors++;
            var older = store.Copy(photo);
            Assert(!older.IsCompleted);
            await store.Copy(other); gate.SetResult(); await older;
            Assert(oldAttempts == 1 && errors == 0 && successes.SequenceEqual(new[] { other }));
        });
        return log;
    }
}
