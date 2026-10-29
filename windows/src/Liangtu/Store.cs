using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace Liangtu;
public sealed class Photo : System.ComponentModel.INotifyPropertyChanged
{
    public string Path { get; init; } = "";
    BitmapSource image = null!;
    public BitmapSource Image { get => image; set { image = value; ThumbnailWidth = value.PixelWidth; ThumbnailHeight = value.PixelHeight; PropertyChanged?.Invoke(this, new(nameof(Image))); } }
    internal int ThumbnailWidth { get; private set; } = 32;
    internal int ThumbnailHeight { get; private set; } = 24;
    internal int ThumbnailGeneration { get; set; }
    internal void ReleaseThumbnail()
    {
        // A detached tiny preview prevents blank flashes while an old photo is
        // reloaded. TransformedBitmap alone would keep its full source alive.
        var scale = Math.Min(1, 96d / Math.Max(image.PixelWidth, image.PixelHeight));
        var small = new TransformedBitmap(image, new ScaleTransform(scale, scale));
        var stride = (small.PixelWidth * small.Format.BitsPerPixel + 7) / 8;
        var pixels = new byte[stride * small.PixelHeight]; small.CopyPixels(pixels, stride, 0);
        var preview = BitmapSource.Create(small.PixelWidth, small.PixelHeight, 96, 96, small.Format, small.Palette, pixels, stride); preview.Freeze();
        UseDeferredThumbnail(preview);
    }
    internal void UseDeferredThumbnail(BitmapSource preview)
    {
        image = preview;
        NeedsThumbnail = true; ThumbnailAttempt = default;
        PropertyChanged?.Invoke(this, new(nameof(Image)));
    }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    internal bool NeedsThumbnail { get; set; }
    internal DateTime ThumbnailAttempt { get; set; }
    internal int MountRevision { get; set; }
    internal bool ArrivalPending { get; set; }
    internal bool UserInitiatedCapture { get; set; }
    internal bool ValidatedWindowsCapture { get; set; }
    internal bool ArrivalStarted { get; set; } = true;
    internal PinAppearance? ReturnAppearance { get; set; }
    public Rect? Origin
    {
        get; set;
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
    readonly Action<string> recycleFile;
    readonly Dictionary<Photo, Task> thumbnailLoads = new();
    readonly ThumbnailCache thumbnails;
    static BitmapSource? placeholder;
    static BitmapSource Placeholder
    {
        get
        {
            if (placeholder == null) { placeholder = BitmapSource.Create(32, 24, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)160, 32 * 24 * 4).ToArray(), 128); placeholder.Freeze(); }
            return placeholder;
        }
    }
    System.Threading.CancellationTokenSource? copyCancellation;
    TaskCompletionSource<bool>? copyCompletion;
    bool exitRequested;
    internal void ResumeAfterCancelledExit() => exitRequested = false;
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
    public List<Photo> Collection { get; } = new();
    public event Action<Photo?>? Changed;
    public event Action<Photo>? Added;
    public event Action<Photo>? Hanging;
    internal event Action<Photo, Rect>? OriginAdopted;
    internal event Action<Rect>? WindowsCaptureAccepted;
    internal WindowsSnipOrigin? WindowsSnips { get; set; }
    internal Func<string, Func<Task>, Task<bool>>? PublishValidatedWindowsCapture { get; set; }
    public event Action<Photo>? Copied;
    public event Action<Photo, bool>? Removing;
    public event Action<Photo>? Trashed;
    public event Action<string>? Error;
    public int Capacity { get; set; } = 9;
    public Store(Settings settings) : this(settings, new ClipboardWriter()) { }
    internal Store(Settings settings, ClipboardWriter clipboardWriter, Action<string>? recycleFile = null)
    {
        this.settings = settings;
        thumbnails = new ThumbnailCache(photo => Items.Contains(photo), Placeholder);
        this.clipboardWriter = clipboardWriter;
        this.recycleFile = recycleFile ?? WindowsRecycle.Send;
    }
    public async Task Restore()
    {
        var mounted = settings.Items.ToArray();
        var paths = settings.Collection.Concat(mounted).ToList();
        if (settings.CollectionRevision < 1)
        {
            var original = Path.Combine(Settings.Root, "settings.json");
            var backup = Path.Combine(Settings.Root, "settings.before-basket.json");
            if (File.Exists(original) && !File.Exists(backup)) File.Copy(original, backup);
            if (Directory.Exists(Settings.Inbox)) paths.AddRange(Directory.EnumerateFiles(Settings.Inbox, "*", SearchOption.AllDirectories).Where(FileRules.IsMedia));
        }
        var known = Collection.Select(p => p.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try {
                var full = Path.GetFullPath(path);
                if (File.Exists(full) && FileRules.IsMedia(full) && known.Add(full))
                    Collection.Add(new Photo { Path = full, Image = Placeholder, NeedsThumbnail = true });
            } catch (Exception e) { Settings.Log("Restore path " + e.Message); }
        }
        Items.Clear();
        foreach (var path in mounted)
        {
            var photo = Collection.FirstOrDefault(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
            if (photo != null && !Items.Contains(photo)) { photo.MountRevision++; Items.Add(photo); await EnsureThumbnail(photo, notify: false); }
        }
        settings.CollectionRevision = 1;
        Persist();
        Changed?.Invoke(null);
    }
    public Task Add(string path, bool restoring = false, Rect? origin = null, bool screenshot = false, bool userInitiated = false) =>
        AddPhoto(path, restoring, origin, screenshot, userInitiated);
    async Task<Photo?> AddPhoto(string path, bool restoring = false, Rect? origin = null, bool screenshot = false, bool userInitiated = false, bool validatedWindowsCapture = false)
    {
        path = System.IO.Path.GetFullPath(path);
        if (!FileRules.IsMedia(path) || Collection.Any(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase)) || !loading.Add(path))
            return Collection.FirstOrDefault(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
        try
        {
            if (!File.Exists(path))
                return null;
            if (!MediaTools.IsVideo(path) && new FileInfo(path).Length > 64L * 1024 * 1024)
                return null;
            var image = await MediaTools.Thumbnail(path);
            // Windows can publish the same capture through the clipboard and disk.
            // Deduplicate only this short-lived screenshot intake, never general files.
            if (screenshot && !restoring && !MediaTools.IsVideo(path))
            {
                var fingerprint = await Task.Run(() => ImageFingerprint(MediaTools.LoadImage(path)));
                recentCaptures.RemoveAll(p => DateTime.UtcNow - p.time > TimeSpan.FromSeconds(12));
                PrunePendingWindowsOrigins();
                if (origin == null && !pendingClipboardPaths.Contains(path) && pendingWindowsOrigins.TryGetValue(fingerprint, out var pendingOrigin)) {
                    origin = pendingOrigin.origin; validatedWindowsCapture = pendingOrigin.measured;
                }
                var existing = FindRecentCapture(fingerprint);
                if (existing != null)
                {
                    if (origin is Rect captured) AdoptWindowsOrigin(existing, captured, validatedWindowsCapture);
                    // A suppressed clipboard import is our own temporary file.
                    // A Windows/third-party screenshot file must remain untouched.
                    if (pendingClipboardPaths.Remove(path)) File.Delete(path);
                    return existing;
                }
                recentCaptures.Add((fingerprint, path, DateTime.UtcNow));
            }
            var item = new Photo { Path = path, Image = image, Origin = origin, ArrivalPending = !restoring, ArrivalStarted = restoring, UserInitiatedCapture = userInitiated, ValidatedWindowsCapture = validatedWindowsCapture && origin != null };
            var hang = settings.AutoHang || userInitiated;
            Collection.Add(item);
            if (!restoring && hang)
                Hanging?.Invoke(item);
            if (restoring || hang) Items.Add(item);
            thumbnails.Touch(item);
            if (!restoring && hang)
            {
                // A line restored from a wider screen loses only one old photo
                // per new arrival. Lowering Keep is the separate bulk operation.
                if (Items.Count > Math.Max(Capacity, settings.Keep))
                    LetGo(Items[0]);
            }
            if (!restoring)
                Persist();
            Changed?.Invoke(item);
            if (!restoring && hang)
                Added?.Invoke(item);
            return item;
        }
        catch (Exception e) { Settings.Log("载入失败 " + e.Message); return null; }
        finally { loading.Remove(path); pendingClipboardPaths.Remove(path); }
    }
    public async Task Reload(Photo p)
    {
        var generation = ++p.ThumbnailGeneration;
        var tracked = Collection.Contains(p);
        try
        {
            var bitmap = await MediaTools.Thumbnail(p.Path);
            if ((tracked && !Collection.Contains(p)) || p.ThumbnailGeneration != generation) return;
            p.Image = bitmap;
            p.NeedsThumbnail = false;
            if (Collection.Contains(p)) thumbnails.Touch(p);
            Changed?.Invoke(null);
        }
        catch (Exception e) { Error?.Invoke(e.Message); }
    }
    public Task EnsureThumbnail(Photo photo, bool notify = true)
    {
        if (!Collection.Contains(photo)) return Task.CompletedTask;
        if (!photo.NeedsThumbnail) { thumbnails.Touch(photo); return Task.CompletedTask; }
        if (thumbnailLoads.TryGetValue(photo, out var loadingTask)) return loadingTask;
        if (DateTime.UtcNow - photo.ThumbnailAttempt < TimeSpan.FromSeconds(3)) return Task.CompletedTask;
        photo.ThumbnailAttempt = DateTime.UtcNow;
        var generation = photo.ThumbnailGeneration;
        var task = Load(); thumbnailLoads[photo] = task; return task;
        async Task Load()
        {
            // Yield before recording the in-flight task; missing files can fail synchronously.
            await Task.Yield();
            try {
                var decodeWidth = Math.Max(240, photo.ThumbnailWidth);
                var bitmap = photo.Video ? await MediaTools.Thumbnail(photo.Path) : await Task.Run(() => MediaTools.LoadImage(photo.Path, decodeWidth));
                if (!Collection.Contains(photo) || photo.ThumbnailGeneration != generation) return;
                photo.Image = bitmap; photo.NeedsThumbnail = false;
                thumbnails.Touch(photo);
                if (notify) Changed?.Invoke(photo);
            } catch (Exception e) { Settings.Log("Thumbnail deferred " + e.Message); }
            finally { thumbnailLoads.Remove(photo); }
        }
    }
    internal IDisposable RetainThumbnail(Photo photo) => thumbnails.Retain(photo);
    public void Prune()
    {
        var missing = Collection.Concat(Items).Distinct().Where(p => !File.Exists(p.Path)).ToArray();
        foreach (var photo in missing)
        {
            Collection.Remove(photo); thumbnails.Forget(photo); TakeDown(photo, quiet: true);
        }
        if (missing.Length > 0) { Persist(); Changed?.Invoke(null); }
    }
    public void Mount(Photo photo, Rect? origin = null)
    {
        if (!Collection.Contains(photo) || Items.Contains(photo) || !File.Exists(photo.Path)) return;
        photo.Origin = origin;
        photo.ArrivalPending = true;
        photo.ArrivalStarted = false;
        Hanging?.Invoke(photo); photo.MountRevision++; Items.Add(photo);
        _ = EnsureThumbnail(photo);
        if (Items.Count > Math.Max(Capacity, settings.Keep)) LetGo(Items[0]);
        Persist(); Changed?.Invoke(photo); Added?.Invoke(photo);
    }
    public void TakeDown(Photo p, bool quiet = false)
    {
        if (!Items.Contains(p))
            return;
        Removing?.Invoke(p, quiet);
        Items.Remove(p);
        thumbnails.Trim();
        Persist();
        Changed?.Invoke(null);
    }
    public bool Discard(Photo p, bool trashExternal = false, bool quiet = false)
    {
        if (trashExternal) return Trash(p, quiet);
        TakeDown(p, quiet); return true;
    }
    public bool Trash(Photo p, bool quiet = false, bool emitCue = true)
    {
        if (!Collection.Contains(p) && !Items.Contains(p)) return false;
        try
        {
            if (File.Exists(p.Path)) recycleFile(p.Path);
            Collection.Remove(p);
            thumbnails.Forget(p);
            TakeDown(p, quiet: true);
            Persist(); Changed?.Invoke(null);
            if (!quiet && emitCue) Trashed?.Invoke(p);
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
    /// Presentation capacity never deletes collected files.
    public void LetGo(Photo p)
    {
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
        var batch = photos.Select(p => (photo: p, revision: p.MountRevision)).ToArray();
        for (var index = 0; index < batch.Length; index++)
        {
            if (index > 0)
                await Task.Delay(60);
            if (batch[index].photo.MountRevision == batch[index].revision) TakeDown(batch[index].photo, quiet: index > 0);
        }
    }
    public async Task ApplyKeepLimit()
    {
        var excess = Math.Max(0, Items.Count - Math.Max(Capacity, settings.Keep));
        var oldest = Items.Take(excess).Select(p => (photo: p, revision: p.MountRevision)).ToArray();
        for (var index = 0; index < oldest.Length; index++)
        {
            if (index > 0)
                await Task.Delay(60);
            if (oldest[index].photo.MountRevision == oldest[index].revision) LetGo(oldest[index].photo);
        }
    }
    public void SaveDesktop(Photo p)
    {
        try
        {
            var dest = FileRules.UniquePath(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), System.IO.Path.GetFileName(p.Path));
            File.Copy(p.Path, dest);
        }
        catch (Exception e) { Error?.Invoke("保存失败：" + e.Message); }
    }
    public void Persist()
    {
        settings.Items = Items.Select(p => p.Path).ToList();
        settings.Collection = Collection.Select(p => p.Path).ToList();
        try
        {
            settings.Save();
        }
        catch (Exception e) { Settings.Log(e.Message); }
    }
    public Task CaptureClipboard() => CaptureClipboardEvent(ClipboardImage.OwnerProcess, Native.GetClipboardSequenceNumber,
        Clipboard.GetDataObject, milliseconds => Task.Delay(milliseconds));
    // The production event path and deterministic tests share this ordering.
    // Source observation must precede both delayed rendering and PNG COM access.
    internal async Task CaptureClipboardEvent(Func<string> ownerProcess, Func<uint> currentSequence,
        Func<IDataObject?> readData, Func<int, Task> delay, Action<string, uint>? observed = null, Func<uint, string?>? observedOwner = null,
        Func<Point?>? eventPointer = null, Func<Point,System.Drawing.Rectangle>? eventScreen = null)
    {
        var generation = ++clipboardGeneration;
        if (copyCancellation != null) return;
        var owner = ownerProcess();
        var eventSequence = currentSequence();
        owner = ResolveOwner(owner, eventSequence);
        if (!ClipboardImage.ShouldCapture(settings, owner)) { if(owner.Length>0)WindowsSnips?.RejectClipboard(currentSequence());return; }
        Observe(owner, eventSequence);
        // Freeze PicRope's fallback point before any clipboard retries/decoding.
        // Windows selections retain their separate measured-origin contract.
        var anchor=settings.ClipboardImages&&!settings.GameDoNotDisturb&&!ClipboardImage.IsWindowsScreenshotOwner(owner)
            ? ClipboardFlightAnchor.Capture(eventPointer,eventScreen) : null;
        for (int attempt = 0; attempt < 8; attempt++)
        {
        await delay(attempt == 0 ? 100 : 100 + attempt * 50);
        if (generation != clipboardGeneration || !ClipboardImage.ShouldCapture(settings, owner)) return;
        try
        {
            var sequence = currentSequence();
            owner = ResolveOwner(ownerProcess(), sequence);
            if (!ClipboardImage.ShouldCapture(settings, owner)) { if(owner.Length>0)WindowsSnips?.RejectClipboard(sequence);return; }
            Observe(owner, sequence);
            var data = readData();
            if (data == null)
                continue;
            if (await CaptureClipboardData(data, owner, sequence, currentSequence,eventAnchor:sequence==eventSequence?anchor:null)) return;
        }
        catch (System.Runtime.InteropServices.COMException) { }
        catch (Exception e) { Settings.Log("Clipboard " + e.Message); return; }
        }
        Settings.Log("Clipboard image unavailable after bounded retries; owner=" + owner);
        void Observe(string source, uint sequence)
        {
            WindowsSnips?.ObserveClipboardSource(source, sequence);
            observed?.Invoke(source, sequence);
        }
        string ResolveOwner(string current, uint sequence)
        {
            // OLE may release its owner HWND after another reader materializes
            // PNG. Only the tracker's trusted proof for this exact sequence can
            // bridge that empty owner; a nonempty replacement is never replaced.
            if (current.Length != 0 || !settings.HandleScreenshots || settings.GameDoNotDisturb) return current;
            var previous = observedOwner != null ? observedOwner(sequence) : WindowsSnips?.ObservedClipboardOwner(sequence);
            return previous ?? current;
        }
    }
    // Test seam accepts owned IDataObject fixtures without reading/replacing the
    // user's clipboard. False means the caller should retry a changed sequence.
    internal async Task<bool> CaptureClipboardData(IDataObject data, string owner, uint sequence, Func<uint> currentSequence,
        Func<string, uint, int, int, Rect?>? matchOrigin = null, ClipboardFlightAnchor? eventAnchor = null)
    {
        if (!ClipboardImage.ShouldCapture(settings, owner) || ClipboardImage.Excluded(data)) { if(sequence==currentSequence())WindowsSnips?.RejectClipboard(sequence);return true; }
        var snapshot = ClipboardImage.ReadSnapshot(data);
        var image = snapshot?.Image;
        if (image == null || (long)image.PixelWidth * image.PixelHeight > 80_000_000) return false;
        if (sequence != currentSequence()) return false;
        // A match is consumed only after filters and the post-read sequence check.
        var origin = settings.HandleScreenshots && !settings.GameDoNotDisturb && owner.Length > 0
            ? (matchOrigin != null ? matchOrigin(owner, sequence, image.PixelWidth, image.PixelHeight) : WindowsSnips?.TryMatch(owner, sequence, image.PixelWidth, image.PixelHeight)) : null;
        var measured=origin!=null;
        if(origin==null&&settings.ClipboardImages&&!settings.GameDoNotDisturb&&!ClipboardImage.IsWindowsScreenshotOwner(owner)&&eventAnchor is ClipboardFlightAnchor entry) {
            origin=entry.ForImage(image.PixelWidth,image.PixelHeight);
            if(origin is Rect reference) Settings.Log($"Clipboard flight source=reference-pointer seq={sequence} point={entry.Pointer} rect={reference}");
        }
        var tracked = origin != null && PublishValidatedWindowsCapture != null;
        var name = "Clipboard " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");
        var path = Path.Combine(Settings.Inbox, name + ".png");
        for (var suffix = 2; File.Exists(path) || pendingClipboardPaths.Contains(path); suffix++) path = Path.Combine(Settings.Inbox, name + " " + suffix + ".png");
        pendingClipboardPaths.Add(path);
        byte[]? bytes = null;
        string? hash = null;
        var accepted = false; var written = false; var terminal = false;
        string? registeredFingerprint = null;
        Photo? alreadyPublished = null;
        void Complete()
        {
            terminal = true; pendingClipboardPaths.Remove(path);
            if (registeredFingerprint != null && pendingWindowsOrigins.TryGetValue(registeredFingerprint, out var candidate) && candidate.sequence == sequence) pendingWindowsOrigins.Remove(registeredFingerprint);
        }
        async Task Publish()
        {
            if (alreadyPublished != null) { settings.Save(); Complete(); return; }
            if (!accepted) {
                if (sequence != currentSequence()) { Complete(); return; }
                if (origin is Rect captured) {
                    var fingerprint = ImageFingerprint(image);
                    PrunePendingWindowsOrigins();
                    pendingWindowsOrigins[fingerprint] = (captured, measured, DateTime.UtcNow, sequence);
                    registeredFingerprint = fingerprint;
                    var existing = FindRecentCapture(fingerprint);
                    if (existing != null) {
                        alreadyPublished = existing;
                        AdoptWindowsOrigin(existing, captured, measured);
                        if(measured) WindowsCaptureAccepted?.Invoke(captured);
                        settings.Save(); Complete(); return;
                    }
                    if(measured) WindowsCaptureAccepted?.Invoke(captured);
                }
                byte[] Encode() {
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                    using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
                }
                // Windows already provides PNG: preserve the bytes read with this
                // snapshot instead of a second COM read or UI-thread re-encoding.
                bytes = origin != null && snapshot!.Png is byte[] originalPng ? originalPng : tracked ? await Task.Run(Encode) : Encode();
                if (bytes.Length > 64L * 1024 * 1024) { Complete(); return; }
                hash = Convert.ToHexString(SHA256.HashData(bytes));
                if (!measured && lastHash == hash && lastPath != null && File.Exists(lastPath)) { Complete(); return; }
                // Keep final sequence validation and acceptance in one dispatcher
                // turn; retries of an accepted save retain that frozen snapshot.
                if (sequence != currentSequence()) { Complete(); return; }
                accepted = true;
            }
            if (!written) {
                File.WriteAllBytes(path, bytes!); written = true;
                lastHash = hash; lastPath = path;
            }
            var photo = await AddPhoto(path, origin: origin, screenshot: true, validatedWindowsCapture: measured);
            if (origin != null) {
                if (photo == null) throw new IOException("截图未能加入收藏。文件位置：" + path);
                settings.Save();
            }
            Complete();
        }
        try {
            if (tracked) await PublishValidatedWindowsCapture!(path, Publish);
            else await Publish();
            return tracked || terminal || accepted;
        }
        finally {
            if (terminal || !tracked) pendingClipboardPaths.Remove(path);
        }
    }
    readonly Dictionary<string, (Rect origin, bool measured, DateTime time, uint sequence)> pendingWindowsOrigins = new(StringComparer.Ordinal);
    void PrunePendingWindowsOrigins()
    {
        foreach (var key in pendingWindowsOrigins.Where(entry => DateTime.UtcNow - entry.Value.time > TimeSpan.FromSeconds(12)).Select(entry => entry.Key).ToArray()) pendingWindowsOrigins.Remove(key);
    }
    Photo? FindRecentCapture(string fingerprint) => recentCaptures.Where(capture => capture.hash == fingerprint && DateTime.UtcNow - capture.time <= TimeSpan.FromSeconds(12))
        .Select(capture => Collection.FirstOrDefault(photo => string.Equals(photo.Path, capture.path, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(photo => photo != null);
    void AdoptWindowsOrigin(Photo photo, Rect origin, bool measured = true)
    {
        if (photo.ArrivalStarted || photo.ValidatedWindowsCapture || !Items.Contains(photo) || !settings.AutoHang) return;
        photo.Origin = origin; photo.ValidatedWindowsCapture = measured;
        OriginAdopted?.Invoke(photo, origin);
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
