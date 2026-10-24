using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace Liangtu;
public sealed class Photo
{
    public string Path { get; init; } = "";
    public BitmapSource Image { get; set; } = null!;
    public Rect? Origin
    {
        get; init;
    }
    public bool Video => MediaTools.IsVideo(Path);
    public double Tilt { get; } = Random.Shared.NextDouble() * 5 - 2.5;
    public DateTime Created { get; } = DateTime.UtcNow;
    public bool InInbox => FileRules.IsInside(Path, Settings.Inbox);
}
public sealed class Store
{
    readonly Settings settings; readonly HashSet<string> loading = new(StringComparer.OrdinalIgnoreCase);
    readonly ClipboardWriter clipboardWriter;
    System.Threading.CancellationTokenSource? copyCancellation;
    TaskCompletionSource<bool>? copyCompletion;
    bool exitRequested;
    internal async Task<bool> FinishCopyBeforeExit()
    {
        exitRequested = true;
        if (copyCompletion != null) await copyCompletion.Task;
        var safe = true;
        // The latest request may have failed before replacing an older published
        // object. Always persist whichever object we still own before quitting.
        try { await clipboardWriter.FinishPersistenceAsync(); } catch (ExternalException) { safe = false; }
        if (!safe) exitRequested = false;
        return safe;
    }
    public List<Photo> Items { get; } = new();
    public event Action<Photo?>? Changed;
    public event Action<Photo>? Added;
    public event Action<Photo>? Hanging;
    public event Action<Photo>? Copied;
    public event Action<Photo, bool>? Removing;
    public event Action<string>? Error;
    public int Capacity { get; set; } = 9;
    public Store(Settings settings) : this(settings, new ClipboardWriter()) { }
    internal Store(Settings settings, ClipboardWriter clipboardWriter)
    {
        this.settings = settings;
        this.clipboardWriter = clipboardWriter;
    }
    public async Task Restore()
    {
        foreach (var p in settings.Items.ToArray())
            await Add(p, true);
        Persist();
    }
    public async Task Add(string path, bool restoring = false, Rect? origin = null, bool screenshot = false)
    {
        path = System.IO.Path.GetFullPath(path);
        if (!FileRules.IsMedia(path) || Items.Any(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase)) || !loading.Add(path))
            return;
        try
        {
            if (!File.Exists(path))
                return;
            if (!MediaTools.IsVideo(path) && new FileInfo(path).Length > 64L * 1024 * 1024)
                return;
            var image = await MediaTools.Thumbnail(path);
            // Windows can publish the same capture through the clipboard and disk.
            // Deduplicate only this short-lived screenshot intake, never general files.
            if (screenshot && !restoring && !MediaTools.IsVideo(path))
            {
                var fingerprint = await Task.Run(() => ImageFingerprint(MediaTools.LoadImage(path)));
                recentCaptures.RemoveAll(p => DateTime.UtcNow - p.time > TimeSpan.FromSeconds(12));
                if (recentCaptures.Any(p => p.hash == fingerprint && Items.Any(item => item.Path == p.path)))
                {
                    // A suppressed clipboard import is our own temporary file.
                    // A Windows/third-party screenshot file must remain untouched.
                    if (pendingClipboardPaths.Remove(path)) File.Delete(path);
                    return;
                }
                recentCaptures.Add((fingerprint, path, DateTime.UtcNow));
            }
            var item = new Photo { Path = path, Image = image, Origin = origin };
            if (!restoring)
                Hanging?.Invoke(item);
            Items.Add(item);
            if (!restoring)
            {
                // A line restored from a wider screen loses only one old photo
                // per new arrival. Lowering Keep is the separate bulk operation.
                if (Items.Count > Math.Max(Capacity, settings.Keep))
                    LetGo(Items[0]);
            }
            if (!restoring)
                Persist();
            Changed?.Invoke(item);
            if (!restoring)
                Added?.Invoke(item);
        }
        catch (Exception e) { Settings.Log("载入失败 " + e.Message); }
        finally { loading.Remove(path); pendingClipboardPaths.Remove(path); }
    }
    public async Task Reload(Photo p)
    {
        try
        {
            p.Image = await MediaTools.Thumbnail(p.Path);
            Changed?.Invoke(null);
        }
        catch (Exception e) { Error?.Invoke(e.Message); }
    }
    public void Prune()
    {
        foreach (var photo in Items.Where(p => !File.Exists(p.Path)).ToArray())
            TakeDown(photo, quiet: true);
    }
    public void TakeDown(Photo p, bool quiet = false)
    {
        if (!Items.Contains(p))
            return;
        Removing?.Invoke(p, quiet);
        Items.Remove(p);
        Persist();
        Changed?.Invoke(null);
    }
    public bool Discard(Photo p, bool trashExternal = false, bool quiet = false)
    {
        if (!Items.Contains(p))
            return false;
        try
        {
            if (p.InInbox || trashExternal)
            {
                if (File.Exists(p.Path))
                    WindowsRecycle.Send(p.Path);
            }
            TakeDown(p, quiet);
            return true;
        }
        catch (Exception e)
        {
            Settings.Log("Recycle " + e.Message);
            if (!quiet)
                Error?.Invoke("无法移到回收站：" + e.Message);
            return false;
        }
    }
    /// Automatic overflow follows the corner button's file ownership rule.
    /// If the recycle bin refuses an inbox file, it still leaves the line.
    public void LetGo(Photo p)
    {
        if (p.InInbox && Discard(p, quiet: true))
            return;
        TakeDown(p, quiet: true);
    }
    public Task Clear() => TakeDownBatch(Items.ToArray());
    public Task TakeDownSide(Photo selected, bool toTheLeft)
    {
        var index = Items.IndexOf(selected);
        if (index < 0)
            return Task.CompletedTask;
        var photos = toTheLeft ? Items.Take(index).ToArray() : Items.Skip(index + 1).ToArray();
        return TakeDownBatch(photos);
    }
    async Task TakeDownBatch(Photo[] photos)
    {
        for (var index = 0; index < photos.Length; index++)
        {
            if (index > 0)
                await Task.Delay(60);
            Discard(photos[index], quiet: index > 0);
        }
    }
    public async Task ApplyKeepLimit()
    {
        var excess = Math.Max(0, Items.Count - Math.Max(Capacity, settings.Keep));
        var oldest = Items.Take(excess).ToArray();
        for (var index = 0; index < oldest.Length; index++)
        {
            if (index > 0)
                await Task.Delay(60);
            LetGo(oldest[index]);
        }
    }
    public void SaveDesktop(Photo p)
    {
        try
        {
            var dest = FileRules.UniquePath(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), System.IO.Path.GetFileName(p.Path));
            File.Move(p.Path, dest);
            TakeDown(p, quiet: true);
        }
        catch (Exception e) { Error?.Invoke("保存失败：" + e.Message); }
    }
    public void Persist()
    {
        settings.Items = Items.Select(p => p.Path).ToList();
        try
        {
            settings.Save();
        }
        catch (Exception e) { Settings.Log(e.Message); }
    }
    public async Task CaptureClipboard()
    {
        var generation = ++clipboardGeneration;
        if (copyCancellation != null) return;
        var owner = ClipboardImage.OwnerProcess();
        if (!ClipboardImage.ShouldCapture(settings, owner)) return;
        for (int attempt = 0; attempt < 8; attempt++)
        {
        await Task.Delay(attempt == 0 ? 100 : 100 + attempt * 50);
        if (generation != clipboardGeneration || !ClipboardImage.ShouldCapture(settings, owner)) return;
        try
        {
            var sequence = Native.GetClipboardSequenceNumber();
            var data = Clipboard.GetDataObject();
            if (data == null)
                continue;
            if (ClipboardImage.Excluded(data))
                return;
            var img = ClipboardImage.Read(data);
            if (img == null || (long)img.PixelWidth * img.PixelHeight > 80_000_000)
                continue;
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(img));
            using var m = new MemoryStream();
            enc.Save(m);
            if (m.Length > 64L * 1024 * 1024)
                return;
            var bytes = m.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (lastHash == hash && lastPath != null && File.Exists(lastPath))
                return;
            if (sequence != Native.GetClipboardSequenceNumber())
                continue;
            var path = FileRules.UniquePath(Settings.Inbox, "Clipboard " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + ".png");
            // Keep the sequence check and publication in the same dispatcher turn.
            File.WriteAllBytes(path, bytes);
            lastHash = hash;
            lastPath = path;
            pendingClipboardPaths.Add(path);
            await Add(path, screenshot: true);
            return;
        }
        catch (System.Runtime.InteropServices.COMException) { }
        catch (Exception e) { Settings.Log("Clipboard " + e.Message); return; }
        }
        Settings.Log("Clipboard image unavailable after bounded retries; owner=" + owner);
    }
    int clipboardGeneration;
    readonly HashSet<string> pendingClipboardPaths = new(StringComparer.OrdinalIgnoreCase);
    readonly List<(string hash, string path, DateTime time)> recentCaptures = new();
    static string ImageFingerprint(BitmapSource bitmap)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = checked(bitmap.PixelWidth * 4);
        var pixels = new byte[checked(stride * bitmap.PixelHeight)];
        converted.CopyPixels(pixels, stride, 0);
        return $"{bitmap.PixelWidth}x{bitmap.PixelHeight}:" + Convert.ToHexString(SHA256.HashData(pixels));
    }
    string? lastHash, lastPath;
    public async Task Copy(Photo p)
    {
        if (exitRequested) return;
        copyCancellation?.Cancel();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        copyCompletion = completion;
        using var request = new System.Threading.CancellationTokenSource();
        copyCancellation = request;
        ++clipboardGeneration;
        bool published = false;
        bool safeToExit = true;
        try
        {
            // Register requests before yielding so an older completion cannot
            // clear the new pending feedback. Let the UI paint before native IO.
            await Dispatcher.Yield(DispatcherPriority.Background);
            request.Token.ThrowIfCancellationRequested();
            var payload = p.Video ? null : await Task.Run(() => ClipboardPayload.Load(p.Path), request.Token);
            request.Token.ThrowIfCancellationRequested();
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { p.Path });
            data.SetData("Liangtu.Internal", true);
            if (payload != null)
            {
                data.SetImage(payload.Image);
                data.SetData("PNG", new MemoryStream(payload.Png, false));
            }
            var persisted = await clipboardWriter.WriteAsync(data, request.Token, () => {
                request.Token.ThrowIfCancellationRequested();
                published = true;
                Copied?.Invoke(p);
            });
            if (!persisted && !published && !request.IsCancellationRequested)
                Error?.Invoke("复制期间剪贴板被其他应用更新，请再复制一次。");
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception e)
        {
            if (request.IsCancellationRequested) return;
            safeToExit = !published;
            Settings.Log($"Clipboard copy failed hr=0x{e.HResult:X8}: {e}");
            Error?.Invoke(published ? "图片已复制，但剪贴板持久化失败。请保持晾图运行并完成粘贴。" : ClipboardWriter.IsBusy(e) ? "剪贴板暂时被其他程序占用，自动重试后仍无法复制。请稍后再试；截图仍保留在晾衣绳上。" : "复制失败：" + e.Message);
            throw;
        }
        finally { if (ReferenceEquals(copyCancellation, request)) copyCancellation = null; completion.TrySetResult(safeToExit); }
    }
}

internal static class WindowsRecycle
{
    public static void Send(string path)
    {
        var type = Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09"), throwOnError: true)!;
        var operation = (IFileOperation)Activator.CreateInstance(type)!;
        var item = IntPtr.Zero;
        try
        {
            // Windows 8+ requires recycling rather than silently substituting a
            // permanent delete. Failures return to the caller without Shell UI.
            // https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags
            const uint flags = 0x00080000 | 0x00100000 | 0x0040 | 0x0004 | 0x0010 | 0x0400;
            Marshal.ThrowExceptionForHR(operation.SetOperationFlags(flags));
            var iid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(Path.GetFullPath(path), IntPtr.Zero, ref iid, out item));
            Marshal.ThrowExceptionForHR(operation.DeleteItem(item, IntPtr.Zero));
            Marshal.ThrowExceptionForHR(operation.PerformOperations());
            Marshal.ThrowExceptionForHR(operation.GetAnyOperationsAborted(out var aborted));
            if (aborted || File.Exists(path))
                throw new IOException("Windows 未能将文件移到回收站。");
        }
        finally
        {
            if (item != IntPtr.Zero)
                Marshal.Release(item);
            Marshal.FinalReleaseComObject(operation);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid iid, out IntPtr item);

    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IFileOperation
    {
        [PreserveSig] int Advise(IntPtr sink, out uint cookie);
        [PreserveSig] int Unadvise(uint cookie);
        [PreserveSig] int SetOperationFlags(uint flags);
        [PreserveSig] int SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        [PreserveSig] int SetProgressDialog(IntPtr dialog);
        [PreserveSig] int SetProperties(IntPtr changes);
        [PreserveSig] int SetOwnerWindow(IntPtr owner);
        [PreserveSig] int ApplyPropertiesToItem(IntPtr item);
        [PreserveSig] int ApplyPropertiesToItems(IntPtr items);
        [PreserveSig] int RenameItem(IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr sink);
        [PreserveSig] int RenameItems(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int MoveItem(IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, IntPtr sink);
        [PreserveSig] int MoveItems(IntPtr items, IntPtr destination);
        [PreserveSig] int CopyItem(IntPtr item, IntPtr destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, IntPtr sink);
        [PreserveSig] int CopyItems(IntPtr items, IntPtr destination);
        [PreserveSig] int DeleteItem(IntPtr item, IntPtr sink);
        [PreserveSig] int DeleteItems(IntPtr items);
        [PreserveSig] int NewItem(IntPtr destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string? template, IntPtr sink);
        [PreserveSig] int PerformOperations();
        [PreserveSig] int GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
}
