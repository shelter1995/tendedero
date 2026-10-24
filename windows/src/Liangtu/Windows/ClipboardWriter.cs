using System.Threading;
using System.Runtime.InteropServices;
namespace Liangtu;

internal sealed class ClipboardWriter
{
    readonly Action<DataObject> set;
    readonly Action flush;
    readonly Func<DataObject, bool> isCurrent;
    readonly Func<int, CancellationToken, Task> delay;
    internal ClipboardWriter() : this(
        data => System.Windows.Forms.Clipboard.SetDataObject(data, copy: false, retryTimes: 0, retryDelay: 0),
        () => Marshal.ThrowExceptionForHR(OleFlushClipboard()),
        data => { var result = OleIsCurrentClipboard((System.Runtime.InteropServices.ComTypes.IDataObject)data); Marshal.ThrowExceptionForHR(result); return result == 0; }) { }
    internal ClipboardWriter(Action<DataObject> set, Action flush, Func<DataObject, bool> isCurrent, Func<int, CancellationToken, Task>? delay = null)
    {
        this.set = set; this.flush = flush; this.isCurrent = isCurrent; this.delay = delay ?? Task.Delay;
    }
    internal async Task<bool> WriteAsync(DataObject data, CancellationToken cancellation)
    {
        // Single native attempts; WPF's SetDataObject/Flush retry by sleeping on
        // the UI thread. Keep this STA dispatcher free between attempts instead.
        await Retry(() => { set(data); return true; }, cancellation);
        // Give clipboard listeners (including remote-session sync) a message turn.
        await delay(40, cancellation);
        return await Retry(() => {
            if (!isCurrent(data)) return false;
            flush(); return true;
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
                await delay(150, cancellation);
            }
        }
    }
    internal static bool IsBusy(Exception error) => error.HResult == unchecked((int)0x800401D0);
    [DllImport("ole32.dll")] static extern int OleFlushClipboard();
    [DllImport("ole32.dll")] static extern int OleIsCurrentClipboard([MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject data);
}
