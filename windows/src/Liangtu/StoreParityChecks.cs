using System.Diagnostics;
using System.Reflection;

namespace Liangtu;

public static class StoreParityChecks
{
    // Optional standalone entry point. The application project normally selects App.Main.
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length != 1)
            return 2;
        Settings.Root = Path.GetFullPath(args[0]);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            var log = new List<string>();
            try
            {
                await Run(app, log);
            }
            catch (Exception e) { log.Add("FAIL store parity runner: " + e); }
            Console.WriteLine(string.Join(Environment.NewLine, log));
            app.Shutdown(log.Any(line => line.StartsWith("FAIL ")) ? 1 : 0);
        };
        return app.Run();
    }

    public static async Task Run(App app, List<string> log)
    {
        app.Dispatcher.VerifyAccess();
        var workspace = new DirectoryInfo(Environment.CurrentDirectory);
        while (workspace != null && !File.Exists(Path.Combine(workspace.FullName, "src", "Liangtu", "Store.cs")))
            workspace = workspace.Parent;
        if (workspace == null || !FileRules.IsInside(Settings.Root, Path.Combine(workspace.FullName, "artifacts")))
            throw new InvalidOperationException("Store parity checks require a temporary Settings.Root below the workspace artifacts folder.");

        Directory.CreateDirectory(Settings.Root);
        var runId = "store-parity-" + Guid.NewGuid().ToString("N");
        var external = Path.Combine(Settings.Root, runId);
        var inbox = Path.Combine(Settings.Inbox, runId);
        Directory.CreateDirectory(external);
        Directory.CreateDirectory(inbox);
        var settingsFile = Path.Combine(Settings.Root, "settings.json");
        var savedSettings = File.Exists(settingsFile) ? await File.ReadAllBytesAsync(settingsFile) : null;
        var serial = 0;

        string Fixture(bool inInbox = false)
        {
            var path = Path.Combine(inInbox ? inbox : external, $"generated-{++serial}.png");
            var pixels = new byte[24 * 12 * 4];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 64;
                pixels[i + 1] = 128;
                pixels[i + 2] = 192;
                pixels[i + 3] = 255;
            }
            var bitmap = BitmapSource.Create(24, 12, 96, 96, PixelFormats.Bgra32, null, pixels, 24 * 4);
            bitmap.Freeze();
            MediaTools.SavePng(bitmap, path);
            return path;
        }

        async Task Check(string name, Func<Task<bool>> test)
        {
            try
            {
                log.Add((await test() ? "PASS " : "FAIL ") + name);
            }
            catch (Exception e) { log.Add("FAIL " + name + ": " + (e.InnerException ?? e)); }
        }

        async Task Seed(Store store, IEnumerable<string> paths)
        {
            foreach (var path in paths)
                await store.Add(path, restoring: true);
        }

        // Binding the proposed contract lets the original implementation fail assertions,
        // rather than failing to compile before its behavior can be exercised.
        var removingEvent = typeof(Store).GetEvent("Removing");
        var copiedEvent = typeof(Store).GetEvent("Copied");
        var clear = typeof(Store).GetMethod("Clear", Type.EmptyTypes);
        var side = typeof(Store).GetMethod("TakeDownSide", new[] { typeof(Photo), typeof(bool) });
        var applyLimit = typeof(Store).GetMethod("ApplyKeepLimit", Type.EmptyTypes);
        var letGo = typeof(Store).GetMethod("LetGo", new[] { typeof(Photo) });
        async Task Invoke(Store store, MethodInfo method, params object[] args)
        {
            if (method.Invoke(store, args) is Task task)
                await task;
        }

        try
        {
            await Check("store parity: restore retains every valid photo above current capacity", async () =>
            {
                var paths = Enumerable.Range(0, 5).Select(_ => Fixture()).ToList();
                var settings = new Settings { Items = paths };
                var store = new Store(settings) { Capacity = 2 };
                var added = 0;
                store.Added += _ => added++;
                await store.Restore();
                return store.Items.Select(p => p.Path).SequenceEqual(paths) && added == 0;
            });

            await Check("store parity: one new photo evicts only one oldest photo on a narrower screen", async () =>
            {
                var paths = Enumerable.Range(0, 5).Select(_ => Fixture()).ToArray();
                var store = new Store(new Settings()) { Capacity = 2 };
                await Seed(store, paths);
                var newest = Fixture();
                await store.Add(newest);
                return store.Items.Select(p => p.Path).SequenceEqual(paths.Skip(1).Append(newest)) && paths.All(File.Exists);
            });

            await Check("store collection: automatic inbox eviction preserves the file", async () =>
            {
                var store = new Store(new Settings()) { Capacity = 1 };
                var oldest = Fixture(inInbox: true);
                await store.Add(oldest);
                await store.Add(Fixture());
                return File.Exists(oldest) && store.Items.Count == 1 && store.Collection.Count == 2;
            });

            await Check("store parity: removal notification arrives before the item leaves", async () =>
            {
                if (removingEvent == null)
                    return false;
                var settings = new Settings();
                var store = new Store(settings);
                await store.Add(Fixture());
                var photo = store.Items[0];
                var called = 0;
                var beforeRemoval = false;
                removingEvent.AddEventHandler(store, (Action<Photo, bool>)((p, quiet) =>
                {
                    called++;
                    beforeRemoval = p == photo && !quiet && store.Items.Contains(p);
                }));
                store.TakeDown(photo);
                store.TakeDown(photo);
                return called == 1 && beforeRemoval && File.Exists(photo.Path) && settings.Items.Count == 0;
            });

            await Check("store collection: locked inbox file can be taken down without deletion", async () =>
            {
                if (letGo == null || removingEvent == null)
                    return false;
                var store = new Store(new Settings());
                var path = Fixture(inInbox: true);
                await store.Add(path);
                var photo = store.Items[0];
                var errors = 0;
                var quietRemoval = false;
                store.Error += _ => errors++;
                removingEvent.AddEventHandler(store, (Action<Photo, bool>)((p, quiet) => quietRemoval = p == photo && quiet));
                using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    await Invoke(store, letGo, photo);
                return store.Items.Count == 0 && File.Exists(path) && quietRemoval && errors == 0;
            });

            await Check("store parity: clear cascades every 60ms and only its first removal is audible", async () =>
            {
                if (clear == null || removingEvent == null)
                    return false;
                var store = new Store(new Settings());
                var paths = Enumerable.Range(0, 3).Select(_ => Fixture()).ToArray();
                await Seed(store, paths);
                var clock = Stopwatch.StartNew();
                var events = new List<(string path, bool quiet, double ms)>();
                removingEvent.AddEventHandler(store, (Action<Photo, bool>)((p, quiet) => events.Add((p.Path, quiet, clock.Elapsed.TotalMilliseconds))));
                await Invoke(store, clear);
                return store.Items.Count == 0 && paths.All(File.Exists) && events.Select(e => e.path).SequenceEqual(paths)
                    && events.Select(e => e.quiet).SequenceEqual(new[] { false, true, true })
                    && events[1].ms - events[0].ms >= 45 && events[2].ms - events[1].ms >= 45;
            });

            await Check("store parity: take down each side preserves the selected photo and external files", async () =>
            {
                if (side == null)
                    return false;
                var store = new Store(new Settings());
                var paths = Enumerable.Range(0, 5).Select(_ => Fixture()).ToArray();
                await Seed(store, paths);
                var selected = store.Items[2];
                await Invoke(store, side, selected, true);
                if (!store.Items.Select(p => p.Path).SequenceEqual(paths.Skip(2)))
                    return false;
                await Invoke(store, side, selected, false);
                return store.Items.Count == 1 && store.Items[0] == selected && paths.All(File.Exists);
            });

            await Check("store parity: lower keep limit quietly removes oldest inbox photos", async () =>
            {
                if (applyLimit == null || removingEvent == null)
                    return false;
                var settings = new Settings { Keep = 3 };
                var store = new Store(settings) { Capacity = 2 };
                var paths = Enumerable.Range(0, 5).Select(_ => Fixture(inInbox: true)).ToArray();
                await Seed(store, paths);
                var quiet = new List<bool>();
                removingEvent.AddEventHandler(store, (Action<Photo, bool>)((_, value) => quiet.Add(value)));
                await Invoke(store, applyLimit);
                return store.Items.Select(p => p.Path).SequenceEqual(paths.Skip(2))
                    && paths.All(File.Exists) && store.Collection.Count == paths.Length
                    && quiet.Count == 2 && quiet.All(value => value) && settings.Items.SequenceEqual(paths.Skip(2));
            });

            await Check("store parity: successful copies share one notification and failed copies notify nobody", async () =>
            {
                if (copiedEvent == null)
                    return false;
                var store = new Store(new Settings());
                await store.Add(Fixture());
                var photo = store.Items[0];
                var notified = new List<Photo>();
                copiedEvent.AddEventHandler(store, (Action<Photo>)(p => notified.Add(p)));
                var original = Clipboard.GetDataObject();
                try
                {
                    await store.Copy(photo);
                    try
                    {
                        await store.Copy(new Photo { Path = Path.Combine(external, "missing.png"), Image = photo.Image });
                    }
                    catch (IOException) { }
                    return notified.Count == 1 && notified[0] == photo;
                }
                finally
                {
                    if (original != null)
                        Clipboard.SetDataObject(original, true);
                    else
                        Clipboard.Clear();
                }
            });
        }
        finally
        {
            if (savedSettings != null)
                await File.WriteAllBytesAsync(settingsFile, savedSettings);
            else if (File.Exists(settingsFile))
                File.Delete(settingsFile);
        }
    }
}
