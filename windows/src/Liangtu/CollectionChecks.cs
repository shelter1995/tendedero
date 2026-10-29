namespace Liangtu;

public static class CollectionChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int failures = 0; var log = new List<string>();
        app.Startup += async (_, _) => {
            async Task Check(string name, Func<Task<bool>> run) {
                try { if (!await run()) throw new Exception("Assertion failed"); log.Add("PASS " + name); }
                catch (Exception e) { failures++; log.Add("FAIL " + name + ": " + e.Message); }
            }
            string Image(string name) {
                var path = Path.Combine(Settings.Inbox, name + ".png");
                MediaTools.SavePng(BitmapSource.Create(24, 18, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)180, 24 * 18 * 4).ToArray(), 96), path); return path;
            }
            await Check("bulk take-down preserves inbox originals", async () => {
                var store = new Store(new Settings()); var path = Image("bulk"); await store.Add(path);
                await store.Clear(); return File.Exists(path) && store.Items.Count == 0;
            });
            await Check("capacity overflow preserves inbox originals", async () => {
                var store = new Store(new Settings()) { Capacity = 1 }; var first = Image("overflow-first");
                await store.Add(first); await store.Add(Image("overflow-second")); return File.Exists(first) && store.Items.Count == 1;
            });
            await Check("auto hang disabled collects without appearing; manual remount uses same record", async () => {
                var store = new Store(new Settings { AutoHang = false }); var path = Image("manual"); await store.Add(path);
                if (store.Items.Count != 0 || store.Collection.Count != 1) return false;
                var photo = store.Collection[0]; store.Mount(photo); store.TakeDown(photo); store.Mount(photo); store.Mount(photo);
                return store.Items.Count == 1 && store.Collection.Count == 1 && store.Items[0] == photo && File.Exists(path);
            });
            await Check("migration keeps mounted order and discovers inbox without mounting old files", async () => {
                var one = Image("migration-one"); var two = Image("migration-two"); var orphan = Image("migration-orphan");
                var settings = new Settings { Items = new() { two, one } }; settings.Save();
                var store = new Store(settings); await store.Restore();
                return store.Items.Select(p => p.Path).SequenceEqual(new[] { two, one }) && store.Collection.Any(p => p.Path == orphan)
                    && File.Exists(Path.Combine(Settings.Root, "settings.before-basket.json")) && Settings.Load().CollectionRevision == 1;
            });
            await Check("restart restores basket independently of line", async () => {
                var settings = new Settings { CollectionRevision = 1 }; var store = new Store(settings);
                var one = Image("restart-one"); var two = Image("restart-two"); await store.Add(one); await store.Add(two); store.TakeDown(store.Items[0]);
                var restored = new Store(Settings.Load()); await restored.Restore();
                return restored.Collection.Count == 2 && restored.Items.Count == 1 && restored.Items[0].Path == two;
            });
            await Check("missing file removed from basket and line", async () => {
                var store = new Store(new Settings()); var path = Image("missing"); await store.Add(path); File.Delete(path); store.Prune();
                return store.Items.Count == 0 && store.Collection.Count == 0;
            });
            await Check("explicit deletion moves collected file to recycle bin", async () => {
                var store = new Store(new Settings { AutoHang = false }); var path = Image("explicit-trash"); await store.Add(path);
                return store.Trash(store.Collection[0]) && !File.Exists(path) && store.Collection.Count == 0;
            });
            await Check("failed deletion preserves collection and reports error", async () => {
                var store = new Store(new Settings()); var path = Image("locked-trash"); await store.Add(path); var errors = 0; store.Error += _ => errors++;
                using var held = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return !store.Trash(store.Collection[0]) && store.Collection.Count == 1 && store.Items.Count == 1 && errors == 1;
            });
            await Check("restore keeps temporarily unreadable collection records", async () => {
                var path = Image("locked-restore"); var settings = new Settings { CollectionRevision = 1, Collection = new() { path } }; settings.Save();
                using var held = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                var restored = new Store(Settings.Load()); await restored.Restore();
                return restored.Collection.Any(p => p.Path == path) && Settings.Load().Collection.Contains(path);
            });
            await Check("restore publishes once without transiently hanging history", async () => {
                var paths = Enumerable.Range(0, 20).Select(i => Image("history-" + i)).ToList();
                var store = new Store(new Settings { CollectionRevision = 1, Collection = paths, Items = new() { paths[0] } });
                int notifications = 0, maximumMounted = 0; store.Changed += _ => { notifications++; maximumMounted = Math.Max(maximumMounted, store.Items.Count); };
                await store.Restore(); return notifications == 1 && maximumMounted == 1 && store.Collection.Count == 20;
            });
            await Check("bulk take-down does not remove a newer explicit remount", async () => {
                var store = new Store(new Settings { Keep = 100 }); foreach(var i in Enumerable.Range(0, 4)) await store.Add(Image("batch-" + i));
                var last = store.Items.Last(); var clear = store.Clear(); store.TakeDown(last); store.Mount(last); await clear;
                return store.Items.Contains(last) && File.Exists(last.Path);
            });
            await File.WriteAllLinesAsync(Path.Combine(Settings.Root, "results.txt"), log);
            app.Shutdown(failures == 0 ? 0 : 1);
        };
        return app.Run();
    }
}
