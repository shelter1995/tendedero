namespace Liangtu;
public sealed class FolderWatch : IDisposable
{
    readonly Store store;
    readonly Settings settings;
    readonly Dispatcher dispatcher;
    readonly List<FileSystemWatcher> watchers = new();
    readonly Dictionary<string, int> generations = new(StringComparer.OrdinalIgnoreCase);
    List<string> targets = new();
    int lifecycle;
    public FolderWatch(Store store, Settings settings)
    {
        this.store = store;
        this.settings = settings;
        dispatcher = Dispatcher.CurrentDispatcher;
        Restart();
    }
    public static IEnumerable<string> ScreenshotFolders()
    {
        var pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        var paths = new List<string> { Path.Combine(pics, "Screenshots"), Path.Combine(pics, "屏幕截图"), Path.Combine(videos, "Captures"), Path.Combine(videos, "Screen Recordings"), Path.Combine(videos, "屏幕录制") };
        // FOLDERID_Screenshots follows redirection (including OneDrive/custom locations).
        var id = new Guid("b7bede81-df94-4682-a7d8-57a52620b86f");
        if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var pointer) == 0)
        {
            try { var path = System.Runtime.InteropServices.Marshal.PtrToStringUni(pointer); if (!string.IsNullOrEmpty(path)) paths.Add(path); }
            finally { System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pointer); }
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase);
    }
    [System.Runtime.InteropServices.DllImport("shell32.dll")] static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);
    public void Restart()
    {
        Dispose();
        targets = new[] { Settings.Inbox }.Concat(settings.HandleScreenshots ? ScreenshotFolders() : Array.Empty<string>()).Concat(settings.ExtraFolders).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var target in targets)
        {
            var root = target;
            while (!Directory.Exists(root) && Directory.GetParent(root) is { } parent)
                root = parent.FullName;
            if (!Directory.Exists(root))
                continue;
            try
            {
                var watcher = new FileSystemWatcher(root) { NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size, IncludeSubdirectories = !string.Equals(root, target, StringComparison.OrdinalIgnoreCase) };
                watcher.Created += (_, e) => Discover(e.FullPath);
                watcher.Changed += (_, e) => Queue(e.FullPath);
                watcher.Renamed += (_, e) => { Discover(e.FullPath); dispatcher.BeginInvoke(() => store.Prune()); };
                watcher.Deleted += (_, _) => dispatcher.BeginInvoke(() => store.Prune());
                watcher.Error += (_, e) => Settings.Log("File watcher " + e.GetException().Message);
                watcher.EnableRaisingEvents = true;
                watchers.Add(watcher);
            }
            catch (Exception e) { Settings.Log("Watch " + e.Message); }
        }
    }
    void Discover(string path)
    {
        Queue(path);
        if (!Directory.Exists(path))
            return;
        foreach (var folder in targets.Where(t => string.Equals(t, path, StringComparison.OrdinalIgnoreCase) || FileRules.IsInside(t, path)))
        {
            if (!Directory.Exists(folder))
                continue;
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder))
                    Queue(file);
            }
            catch (IOException) { }
        }
    }
    void Queue(string path)
    {
        if (!FileRules.IsMedia(path) || !targets.Any(t => string.Equals(Path.GetDirectoryName(path), t, StringComparison.OrdinalIgnoreCase)))
            return;
        var version = lifecycle;
        dispatcher.BeginInvoke(async () =>
        {
            if (version != lifecycle)
                return;
            generations.TryGetValue(path, out var generation);
            generations[path] = ++generation;
            await Task.Delay(450);
            if (version != lifecycle || !generations.TryGetValue(path, out var current) || current != generation)
                return;
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    var length = new FileInfo(path).Length;
                    await Task.Delay(250);
                    if (version != lifecycle || !File.Exists(path))
                        return;
                    if (new FileInfo(path).Length != length)
                        continue;
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                    }
                    var existing = store.Collection.FirstOrDefault(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                        await store.Add(path, screenshot: settings.HandleScreenshots && ScreenshotFolders().Contains(Path.GetDirectoryName(path), StringComparer.OrdinalIgnoreCase));
                    else
                        await store.Reload(existing);
                    break;
                }
                catch (IOException) { await Task.Delay(250); }
                catch (UnauthorizedAccessException) { break; }
            }
            if (generations.TryGetValue(path, out current) && current == generation)
                generations.Remove(path);
        });
    }
    public void Dispose()
    {
        lifecycle++;
        foreach (var w in watchers)
            w.Dispose();
        watchers.Clear();
        generations.Clear();
    }
}
