namespace Liangtu;

// Owned fixtures and injected recycle transport only; never invokes the Shell.
internal static class DeleteConfirmationChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var log = new List<string>();
        void Test(string name, Action run) { try { run(); log.Add("PASS " + name); } catch (Exception e) { log.Add("FAIL " + name + ": " + e.Message); } }
        void Assert(bool okay, string message) { if (!okay) throw new Exception(message); }
        app.Startup += (_, _) => {
            Test("confirmation defaults on for existing settings", () => {
                var property = typeof(Settings).GetProperty("ConfirmBeforeDelete");
                if (property == null || !Equals(property.GetValue(new Settings()), true)) throw new Exception("Missing default-on deletion preference");
            });
            var photos = new[] { new Photo { Path = "owned-one.png" }, new Photo { Path = "owned-two.png" } };
            Test("legacy settings default on; opt-out survives restart; restore survives restart", () => {
                File.WriteAllText(Path.Combine(Settings.Root, "settings.json"), "{\"Sounds\":false}");
                var settings = Settings.Load(); Assert(settings.ConfirmBeforeDelete, "Old settings bypass confirmation");
                settings.ConfirmBeforeDelete = false; settings.Save(); Assert(!Settings.Load().ConfirmBeforeDelete, "Opt-out lost");
                settings.ConfirmBeforeDelete = true; settings.Save(); Assert(Settings.Load().ConfirmBeforeDelete, "Restore lost");
            });
            Test("cancel with checkbox checked neither deletes nor persists nor sounds", () => {
                var settings = new Settings(); int saves = 0, deletes = 0, cues = 0;
                var count = DeleteOperation.Execute(settings, photos, _ => new(false, true), _ => { deletes++; return true; }, () => saves++, () => cues++);
                Assert(count == 0 && settings.ConfirmBeforeDelete && saves == 0 && deletes == 0 && cues == 0, "Cancellation changed state");
            });
            Test("confirmed opt-out saves once, later batch skips prompt, each batch emits one cue", () => {
                var settings = new Settings(); int prompts = 0, saves = 0, deletes = 0, cues = 0;
                int Run() => DeleteOperation.Execute(settings, photos, _ => { prompts++; return new(true, true); }, _ => { deletes++; return true; }, () => saves++, () => cues++);
                Assert(Run() == 2 && Run() == 2 && prompts == 1 && saves == 1 && deletes == 4 && cues == 2 && !settings.ConfirmBeforeDelete, "Opt-out/batch behavior incorrect");
            });
            Test("unchecked confirm keeps prompting; all failures stay silent; partial success cues once", () => {
                var settings = new Settings(); int prompts = 0, saves = 0, cues = 0;
                DeleteDecision Ask(string _) { prompts++; return new(true, false); }
                Assert(DeleteOperation.Execute(settings, photos, Ask, _ => false, () => saves++, () => cues++) == 0, "Failed batch counted success");
                Assert(DeleteOperation.Execute(settings, photos, Ask, p => p == photos[1], () => saves++, () => cues++) == 1, "Partial result lost");
                Assert(prompts == 2 && saves == 0 && cues == 1 && settings.ConfirmBeforeDelete, "Unchecked/failure behavior incorrect");
            });
            Test("empty selection performs no decision, persistence, recycle, or cue", () => {
                void Unexpected() => throw new Exception("Empty selection triggered work");
                Assert(DeleteOperation.Execute(new Settings(), Array.Empty<Photo>(), _ => { Unexpected(); return default; }, _ => { Unexpected(); return false; }, Unexpected, Unexpected) == 0, "Nonzero result");
            });
            Test("recycle emits exactly one success event for mounted and basket-only photos", () => {
                var directory = Path.Combine(Settings.Root, "transport-fixtures"); Directory.CreateDirectory(directory);
                int transports = 0, cues = 0, removalCues = 0;
                var store = new Store(new Settings(), new ClipboardWriter(), path => { transports++; File.Move(path, path + ".recycled", true); });
                store.Trashed += _ => cues++; store.Removing += (_, quiet) => { if (!quiet) removalCues++; };
                foreach (var mounted in new[] { true, false }) {
                    var path = Path.Combine(directory, mounted + ".fixture"); File.WriteAllText(path, "owned recyclable fixture");
                    var photo = new Photo { Path = path }; store.Collection.Add(photo); if (mounted) store.Items.Add(photo);
                    Assert(store.Trash(photo), "Recycle failed"); Assert(!File.Exists(path) && File.ReadAllText(path + ".recycled") == "owned recyclable fixture", "Transport lost bytes");
                }
                Assert(transports == 2 && cues == 2 && removalCues == 0 && store.Collection.Count == 0 && store.Items.Count == 0, "Duplicate or missing deletion cue");
            });
            Test("recycle failure retains mounted photo and reports error without a cue", () => {
                var path = Path.Combine(Settings.Root, "failed-owned.fixture"); File.WriteAllText(path, "owned");
                var store = new Store(new Settings(), new ClipboardWriter(), _ => throw new IOException("injected recycle failure"));
                var photo = new Photo { Path = path }; store.Collection.Add(photo); store.Items.Add(photo); int errors = 0, cues = 0;
                store.Error += _ => errors++; store.Trashed += _ => cues++; store.Removing += (_, _) => cues++;
                Assert(!store.Trash(photo) && errors == 1 && cues == 0 && store.Collection.Contains(photo) && store.Items.Contains(photo) && File.Exists(path), "Failure mutated state");
                Assert(!store.Trash(photo, quiet: true) && errors == 1 && cues == 0, "Quiet failure leaked notification");
            });
            Test("batch suppresses per-photo cues while retaining errors and one batch success cue", () => {
                var store = new Store(new Settings(), new ClipboardWriter(), path => { if (path.EndsWith("2.fixture")) throw new IOException("injected"); File.Move(path, path + ".recycled", true); });
                var batch = Enumerable.Range(0, 3).Select(i => new Photo { Path = Path.Combine(Settings.Root, "batch-" + i + ".fixture") }).ToArray();
                foreach (var photo in batch) { File.WriteAllText(photo.Path, "owned"); store.Collection.Add(photo); store.Items.Add(photo); }
                int errors = 0, itemCues = 0, batchCues = 0;
                store.Error += _ => errors++; store.Trashed += _ => itemCues++; store.Removing += (_, quiet) => { if (!quiet) itemCues++; };
                var count = DeleteOperation.Execute(new Settings(), batch, _ => new(true, false), p => store.Trash(p, emitCue: false), () => { }, () => batchCues++);
                Assert(count == 2 && errors == 1 && itemCues == 0 && batchCues == 1 && store.Items.SequenceEqual(new[] { batch[2] }), "Batch duplicated cues or hid failure");
            });
            Test("dialog defaults to Cancel and places at monitor center regardless of owner geometry", () => {
                var screen = System.Windows.Forms.Screen.PrimaryScreen!;
                var strip = new Window { Width = 700, Height = 40, Left = 0, Top = 0, WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false };
                strip.Show();
                try {
                    foreach (var owner in new Window?[] { strip, null }) {
                        var dialog = new SilentConfirmation("Owned test confirmation", owner, screen);
                        var body = (StackPanel)dialog.Content;
                        var actions = body.Children.OfType<StackPanel>().Single().Children.OfType<Button>().ToArray();
                        Assert(actions[0].IsDefault && actions[0].IsCancel && !actions[1].IsDefault && !dialog.DoNotAskAgain, "Unsafe default");
                        Assert(dialog.WindowStartupLocation == WindowStartupLocation.Manual, "Theme overrode placement");
                        Exception? error = null;
                        dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(() => {
                            try {
                                Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(dialog).Handle, out var bounds);
                                var area = screen.WorkingArea;
                                Assert(Math.Abs((bounds.Left + bounds.Right) / 2d - (area.Left + area.Right) / 2d) <= 2 && Math.Abs((bounds.Top + bounds.Bottom) / 2d - (area.Top + area.Bottom) / 2d) <= 2, "Dialog not centered on monitor");
                            } catch (Exception e) { error = e; }
                            finally { actions[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
                        }, DispatcherPriority.ApplicationIdle);
                        Assert(dialog.ShowDialog() == false, "Cancel accepted deletion"); if (error != null) throw error;
                    }
                } finally { strip.Close(); }
            });
            SoundChecks.Run(Test);
            File.WriteAllLines(Path.Combine(Settings.Root, "results.txt"), log);
            Console.WriteLine(string.Join(Environment.NewLine, log)); app.Shutdown(log.Any(x => x.StartsWith("FAIL")) ? 1 : 0);
        };
        return app.Run();
    }
}
