using System.Threading;
using System.Runtime.InteropServices;
namespace Liangtu;

internal sealed class ClipboardWriter
{
    readonly Action<DataObject> set;
    readonly Action flush;
    readonly Func<DataObject, bool> isCurrent;
    readonly Func<int, CancellationToken, Task> delay;
    DataObject? owned;
    internal async Task FinishPersistenceAsync()
    {
        var data = owned;
        if (data == null) return;
        await Retry(() => {
            if (isCurrent(data)) flush();
            if (ReferenceEquals(owned, data)) owned = null;
            return true;
        }, CancellationToken.None);
    }
    internal ClipboardWriter() : this(
        data => System.Windows.Forms.Clipboard.SetDataObject(data, copy: false, retryTimes: 0, retryDelay: 0),
        () => Marshal.ThrowExceptionForHR(OleFlushClipboard()),
        data => { var result = OleIsCurrentClipboard((System.Runtime.InteropServices.ComTypes.IDataObject)data); Marshal.ThrowExceptionForHR(result); return result == 0; }) { }
    internal ClipboardWriter(Action<DataObject> set, Action flush, Func<DataObject, bool> isCurrent, Func<int, CancellationToken, Task>? delay = null)
    {
        this.set = set; this.flush = flush; this.isCurrent = isCurrent; this.delay = delay ?? Task.Delay;
    }
    internal async Task<bool> WriteAsync(DataObject data, CancellationToken cancellation, Action? published = null)
    {
        // Single native attempts; WPF's SetDataObject/Flush retry by sleeping on
        // the UI thread. Keep this STA dispatcher free between attempts instead.
        await Retry(() => { set(data); owned = data; return true; }, cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (!await Retry(() => isCurrent(data), cancellation)) { if (ReferenceEquals(owned, data)) owned = null; return false; }
        // OLE ownership is already published and pasteable. Persistence must not
        // hold back feedback while clipboard listeners briefly lock the service.
        published?.Invoke();
        // Give clipboard listeners (including remote-session sync) a message turn.
        await delay(40, cancellation);
        return await Retry(() => {
            if (!isCurrent(data)) { if (ReferenceEquals(owned, data)) owned = null; return false; }
            flush(); if (ReferenceEquals(owned, data)) owned = null; return true;
        }, cancellation);
    }
    async Task<bool> Retry(Func<bool> operation, CancellationToken cancellation)
    {
        for (int attempt = 0; ; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            try { return operation(); }
            catch (ExternalException e) when (IsBusy(e) && attempt < 20)
            {
                var retryDelay = attempt switch { 0 => 15, 1 => 30, 2 => 60, 3 => 100, _ => 150 };
                await delay(retryDelay, cancellation);
            }
        }
    }
    internal static bool IsBusy(Exception error) => error.HResult == unchecked((int)0x800401D0);
    [DllImport("ole32.dll")] static extern int OleFlushClipboard();
    [DllImport("ole32.dll")] static extern int OleIsCurrentClipboard([MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject data);
}
