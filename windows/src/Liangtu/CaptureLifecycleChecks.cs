namespace Liangtu;

// Deterministic barriers replace disk latency; only owned fixture files are used.
internal static class CaptureLifecycleChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var results = new List<string>();
        void Test(string name, Action body) { try { body(); results.Add("PASS " + name); } catch (Exception e) { results.Add("FAIL " + name + ": " + e); } }
        void Assert(bool okay, string message) { if (!okay) throw new Exception(message); }
        TaskCompletionSource<bool> Barrier() => new(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Startup += async (_, _) => {
            LineWindow? line = null;
            try {
                var settings = new Settings { Sounds = false, NewReveal = true, AutoHang = true, HoverReveal = false, Keep = 100 };
                var store = new Store(settings);
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings);
                typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, store);
                typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app, true);
                line = new LineWindow(app, new FullscreenProtection(() => Array.Empty<ScreenWindow>(), () => 0));
                typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, line);
                store.Added += app.OnPhotoAdded; store.Hanging += app.OnPhotoHanging;
                var bitmap = BitmapSource.Create(64, 48, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)180, 64 * 48 * 4).ToArray(), 256); bitmap.Freeze();
                var screen = line.Screen.Bounds; var origin = new Rect(screen.Left + 80, screen.Top + 300, 64, 48);
                foreach (var updating in new[] { false, true }) {
                    var allowSave = Barrier(); var saveStarted = Barrier(); var saved = false; var shutdown = false; var launched = false;
                    var before = store.Collection.Count;
                    var publication = app.PublishCapture(bitmap, origin, async (image, path) => {
                        saveStarted.SetResult(true); await allowSave.Task;
                        MediaTools.SavePng(image, path); saved = true;
                    });
                    await saveStarted.Task;
                    var label = updating ? "update" : "exit";
                    var drain = updating ? app.ApplyPreparedUpdateCore(() => { launched = true; return Task.CompletedTask; }, () => shutdown = true)
                        : app.ExitAfterPendingWork(() => shutdown = true);
                    line.SuspendForGameMode();
                    var rejected = await app.PublishCapture(bitmap, origin, (_, _) => throw new Exception("A blocked capture reached its saver"));
                    Test(label + " waits behind capture publication barrier and rejects new captures", () => {
                        Assert(app.WorkBlocked && app.PendingCaptureCount == 1 && !drain.IsCompleted && !publication.IsCompleted, "Shutdown did not wait for capture");
                        Assert(!rejected && !saved && !launched && !shutdown && store.Collection.Count == before, "Publication/launcher crossed barrier");
                    });
                    allowSave.SetResult(true);
                    var completed = await drain;
                    Test(label + " proceeds only after PNG and collection publication; no late popup", () => {
                        Assert(completed && awaitResult(publication) && saved && shutdown && (!updating || launched), "Shutdown/publication incomplete");
                        Assert(store.Collection.Count == before + 1 && File.Exists(store.Collection[^1].Path) && app.PendingCaptureCount == 0, "Capture not durably published");
                        Assert(Settings.Load().Collection.Contains(store.Collection[^1].Path), "Collection entry was not persisted before shutdown");
                        Assert(!line.IsVisible && !line.Revealed, "Pending capture reopened shelf during shutdown");
                    });
                    typeof(App).GetField("exiting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(app, false);
                    store.ResumeAfterCancelledExit();
                }
                var gate = Barrier(); var paths = new List<string>();
                Task Save(BitmapSource image, string path) { paths.Add(path); return SaveAfterGate(image, path); }
                async Task SaveAfterGate(BitmapSource image, string path) { await gate.Task; MediaTools.SavePng(image, path); }
                var first = app.PublishCapture(bitmap, origin, Save); var second = app.PublishCapture(bitmap, origin, Save);
                Test("pending screenshot filenames are reserved before either PNG exists", () => Assert(paths.Count == 2 && paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2 && app.PendingCaptureCount == 2, "Concurrent capture paths collided"));
                gate.SetResult(true); await Task.WhenAll(first, second);
                var reports = 0; var attempts = 0;
                var failures = new CapturePublications(_ => reports++);
                var failGate = Barrier();
                var failed = failures.Enqueue("owned-fixture", async () => { attempts++; await failGate.Task; if (attempts < 3) throw new IOException("Simulated disk full"); });
                var waiting = failures.Settle();
                Test("capture failure is awaited without a timer or busy retry", () => Assert(!waiting.IsCompleted && failures.Count == 1 && attempts == 1, "Unbounded retry or early settle"));
                failGate.SetResult(true);
                Assert(!await waiting && !await failed, "Failed capture was reported as published");
                Test("failed capture remains owned and reports exactly once", () => Assert(failures.Count == 1 && reports == 1 && attempts == 1, "Failure was discarded or repeated"));
                Assert(!await failures.Settle(), "Second simulated failure passed");
                Test("next explicit drain retries failed capture only once", () => Assert(failures.Count == 1 && attempts == 2 && reports == 2, "Unexpected retry loop"));
                Assert(await failures.Settle(), "Recovery did not publish");
                Test("successful retry releases retained capture only after publication", () => Assert(failures.Count == 0 && attempts == 3 && reports == 2, "Recovery retained/lost capture"));
                var appReports = 0; var saveAttempts = 0; var unblockFailure = Barrier(); var exited = false; var updateLaunched = false;
                var retained = new CapturePublications(_ => appReports++);
                typeof(App).GetField("capturePublications", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(app, retained);
                var unsuccessful = app.PublishCapture(bitmap, origin, async (image, path) => {
                    saveAttempts++; await unblockFailure.Task;
                    if (saveAttempts < 3) throw new IOException("Owned test disk-full failure");
                    MediaTools.SavePng(image, path);
                });
                var rejectedExit = app.ExitAfterPendingWork(() => exited = true);
                unblockFailure.SetResult(true);
                Assert(!await rejectedExit && !await unsuccessful, "Failed capture allowed exit");
                Test("actual exit refusal retains failed capture and unblocks user work", () => Assert(!exited && !app.WorkBlocked && retained.Count == 1 && appReports == 1, "Exit silently discarded failed save"));
                try { await app.ApplyPreparedUpdateCore(() => { updateLaunched = true; return Task.CompletedTask; }, () => exited = true); throw new Exception("Failed update proceeded"); }
                catch (InvalidOperationException error) when (error.Message.Contains("截图")) { }
                Test("actual update refuses failed retry before launching helper", () => Assert(!updateLaunched && !exited && !app.WorkBlocked && retained.Count == 1 && appReports == 2 && saveAttempts == 2, "Failed update lost capture or launched helper"));
                Assert(await app.ExitAfterPendingWork(() => exited = true), "Recovery exit did not complete");
                Test("actual exit publishes retained capture on successful retry", () => Assert(exited && retained.Count == 0 && saveAttempts == 3 && appReports == 2, "Recovery exit did not publish"));
            } catch (Exception e) { results.Add("FAIL harness: " + e); }
            finally { line?.Close(); File.WriteAllLines(Path.Combine(Settings.Root, "results.txt"), results); Console.WriteLine(string.Join(Environment.NewLine, results)); app.Shutdown(results.Any(s => s.StartsWith("FAIL")) ? 1 : 0); }
        };
        return app.Run();
    }
    static bool awaitResult(Task<bool> task) => task.IsCompletedSuccessfully && task.Result;
}
