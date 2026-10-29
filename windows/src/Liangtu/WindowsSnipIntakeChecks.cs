namespace Liangtu;

// No native hooks, clipboard reads/writes, input injection, or user files.
internal static class WindowsSnipIntakeChecks
{
    sealed class OncePng : IDataObject
    {
        readonly byte[] bytes;
        readonly Action? materialize;
        internal int Reads;
        internal OncePng(byte[] bytes, Action? materialize = null) { this.bytes = bytes; this.materialize = materialize; }
        public object GetData(string format, bool autoConvert) { if (format != "PNG") return null!; if (++Reads > 1) throw new InvalidOperationException("PNG COM snapshot read twice"); materialize?.Invoke(); return new MemoryStream(bytes); }
        public object GetData(string format) => GetData(format, true);
        public object GetData(Type format) => GetData(format.FullName!);
        public bool GetDataPresent(string format, bool autoConvert) => format == "PNG";
        public bool GetDataPresent(string format) => GetDataPresent(format, true);
        public bool GetDataPresent(Type format) => GetDataPresent(format.FullName!);
        public string[] GetFormats(bool autoConvert) => new[] { "PNG" };
        public string[] GetFormats() => GetFormats(true);
        public void SetData(string format, object data, bool autoConvert) => throw new NotSupportedException();
        public void SetData(string format, object data) => throw new NotSupportedException();
        public void SetData(Type format, object data) => throw new NotSupportedException();
        public void SetData(object data) => throw new NotSupportedException();
    }
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var log = new List<string>(); LineWindow? line = null;
        void Assert(bool okay, string reason) { if (!okay) throw new Exception(reason); }
        void Test(string name, Action body) { try { body(); log.Add("PASS " + name); } catch (Exception e) { log.Add("FAIL " + name + ": " + e); } }
        var settings = new Settings(); Store store = null!;
        void Reset(bool autoHang = true, bool generalClipboard = false, bool game = false, bool fullscreen = false) {
            line?.Close();
            settings = new Settings { HandleScreenshots = true, AutoHang = autoHang, NewReveal = false, HoverReveal = false, Sounds = false, Keep = 100, ClipboardImages = generalClipboard, GameDoNotDisturb = game };
            store = new Store(settings);
            typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings); typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, store); typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app, true);
            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            line = new LineWindow(app, new FullscreenProtection(() => fullscreen ? new[] { new ScreenWindow(bounds) } : Array.Empty<ScreenWindow>(), () => 0));
            typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, line);
            store.Added += app.OnPhotoAdded; store.Hanging += app.OnPhotoHanging; app.ConfigureWindowsSnipIntake(null);
        }
        byte[] Png(byte value) {
            var image = BitmapSource.Create(320, 180, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat(value, 320 * 180 * 4).ToArray(), 1280); image.Freeze();
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var memory = new MemoryStream(); encoder.Save(memory); return memory.ToArray();
        }
        DataObject Data(byte[] png) { var data = new DataObject(); data.SetData("PNG", new MemoryStream(png)); data.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4])); return data; }
        Func<string, uint, int, int, Rect?> Matcher(Rect origin, uint sequence) {
            var session = new SnipOriginSession(); session.Begin(origin.TopLeft, sequence - 1, 10); session.End(origin.BottomRight, 20);
            return (owner, current, width, height) => session.Match(ClipboardImage.IsWindowsScreenshotOwner(owner), current, width, height, 30);
        }
        Card CardFor(Photo photo) => ((Canvas)line!.Content).Children.OfType<Canvas>().Single().Children.OfType<Card>().Single(card => card.Photo == photo);
        app.Startup += async (_, _) => {
            try {
                var eventStore = new Store(new Settings { HandleScreenshots = true, AutoHang = false });
                var order = new List<string>(); var ownerAtEvent = "SnippingTool"; uint eventSequence = 100; var ownerReads = 0;
                var beforeRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var delayedPng = new OncePng(Png(179), () => { order.Add("materialize-png"); ownerAtEvent = ""; });
                var eventCapture = eventStore.CaptureClipboardEvent(
                    () => { ownerReads++; return ownerAtEvent; }, () => eventSequence,
                    () => { order.Add("read-object"); return delayedPng; },
                    milliseconds => { order.Add("delay:" + milliseconds); return beforeRead.Task; },
                    (owner, sequence) => order.Add($"observe:{owner}:{sequence}"));
                Test("production clipboard event observes trusted source before its first await", () => Assert(!eventCapture.IsCompleted && order.SequenceEqual(new[] { "observe:SnippingTool:100", "delay:100" }) && delayedPng.Reads == 0, "Source observation was delayed until after COM access"));
                ownerAtEvent = "ScreenClippingHost"; eventSequence = 101; beforeRead.SetResult(true); await eventCapture;
                Test("production clipboard event refreshes source before PNG materialization can flush ownership", () => Assert(ownerReads == 2 && order.SequenceEqual(new[] { "observe:SnippingTool:100", "delay:100", "observe:ScreenClippingHost:101", "read-object", "materialize-png" }) && ownerAtEvent == "" && eventStore.Collection.Count == 1 && delayedPng.Reads == 1, "Fresh observation did not precede delayed-render ownership loss"));
                var rejectedObservations = 0;
                await eventStore.CaptureClipboardEvent(() => "ordinary-app", () => 102, () => throw new Exception("Rejected clipboard was read"), _ => throw new Exception("Rejected clipboard was delayed"), (_, _) => rejectedObservations++);
                Test("clipboard event source observation obeys intake eligibility before delay/read", () => Assert(rejectedObservations == 0, "Excluded source was observed"));
                foreach (var scenario in new[] { "empty-same-sequence", "empty-new-sequence", "nonempty-replacement" }) {
                    var memoStore = new Store(new Settings { HandleScreenshots = true, AutoHang = false });
                    var trusted = new Dictionary<uint, string>(); var freshOwner = "SnippingTool"; uint freshSequence = 200; var reads = 0; var memoLookups = 0;
                    var releaseRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var memoCapture = memoStore.CaptureClipboardEvent(() => freshOwner, () => freshSequence,
                        () => { reads++; return Data(Png(178)); }, _ => releaseRead.Task,
                        (source, sequence) => { if (freshOwner == source && ClipboardImage.IsWindowsScreenshotOwner(source)) trusted[sequence] = source; },
                        sequence => { memoLookups++; return trusted.TryGetValue(sequence, out var source) ? source : null; });
                    freshOwner = scenario == "nonempty-replacement" ? "ordinary-app" : "";
                    if (scenario == "empty-new-sequence") freshSequence++;
                    releaseRead.SetResult(true); await memoCapture;
                    Test("production clipboard source memo: " + scenario, () => {
                        if (scenario == "empty-same-sequence") Assert(reads == 1 && memoStore.Collection.Count == 1 && memoLookups == 1, "Trusted same-sequence empty owner was discarded");
                        else Assert(reads == 0 && memoStore.Collection.Count == 0 && (scenario != "nonempty-replacement" || memoLookups == 0), "Stale memo overrode a new sequence or nonempty application owner");
                    });
                }
                var entryMemoStore = new Store(new Settings { HandleScreenshots = true, AutoHang = false }); var entryMemoReads = 0;
                await entryMemoStore.CaptureClipboardEvent(() => "", () => 300,
                    () => { entryMemoReads++; return Data(Png(177)); }, _ => Task.CompletedTask,
                    observedOwner: sequence => sequence == 300 ? "SnippingTool" : null);
                Test("production event entry can reuse existing trusted proof only for its current sequence", () => Assert(entryMemoReads == 1 && entryMemoStore.Collection.Count == 1, "Empty-owner event entry ignored same-sequence proof"));
                Reset(); var origin = new Rect(line!.Screen.Bounds.Left + 120, line.Screen.Bounds.Top + 700, 320, 180); var png = Png(180); var once = new OncePng(png);
                var acceptedBeforeAdded = false; var trackedBeforeReveal = false;
                store.WindowsCaptureAccepted += _ => { acceptedBeforeAdded = line.IsVisible && store.Collection.Count == 0; trackedBeforeReveal = app.PendingCaptureCount == 1; };
                await store.CaptureClipboardData(once, "SnippingTool", 10, () => 10, Matcher(origin, 10));
                var photo = store.Collection.Single(); var card = CardFor(photo);
                Test("validated Windows snip reveals hidden shelf despite NewReveal off", () => Assert(line.Revealed && line.IsVisible && photo.ValidatedWindowsCapture && !photo.UserInitiatedCapture && store.Items.Contains(photo), "Validated explicit capture did not reveal/hang"));
                Test("validated snip registers lifecycle and reveals before PNG/thumbnail publication", () => Assert(acceptedBeforeAdded && trackedBeforeReveal, "Early reveal or tracked boundary missing"));
                Test("actual rectangle reaches existing Bezier arrival without coordinate guessing", () => Assert(card.PendingArrivalOrigin == origin, "Origin was lost before Card construction"));
                Test("Windows PNG is read once and original encoded bytes are preserved", () => Assert(once.Reads == 1 && File.ReadAllBytes(photo.Path).SequenceEqual(png), "PNG was reread or re-encoded"));
                card.FinishArrival(); var beforeOrigin = card.PendingArrivalOrigin;
                var laterOrigin = new Rect(origin.X + 100, origin.Y + 50, origin.Width, origin.Height);
                await store.CaptureClipboardData(Data(png), "SnippingTool", 11, () => 11, Matcher(laterOrigin, 11));
                Test("finished duplicate arrival never adopts a new origin or replays", () => Assert(store.Collection.Count == 1 && photo.ArrivalStarted && card.PendingArrivalOrigin == beforeOrigin && card.ArrivalFlightBounds == null, "Completed photo replayed"));
                var windowsOriginal = Path.Combine(Settings.Root, "windows-original-after-clipboard.png"); File.WriteAllBytes(windowsOriginal, png); await store.Add(windowsOriginal, screenshot: true);
                Test("clipboard-first duplicate preserves Windows original file and single photo", () => Assert(File.Exists(windowsOriginal) && store.Collection.Count == 1, "Original removed or duplicate hung"));
                store.TakeDown(photo, quiet: true); store.Collection.Remove(photo); line.SuspendForGameMode(); settings.ClipboardImages = true;
                var unknownPng = Png(180); var unknownSnapshot = Data(unknownPng);
                // Clear only the legacy byte-hash fast path so the pixel-origin
                // handoff remains under test for this identical ordinary import.
                typeof(Store).GetField("lastHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(store, null);
                await store.CaptureClipboardData(unknownSnapshot, "ordinary-app", 12, () => 12, (_, _, _, _) => null);
                Test("completed Windows origin cannot leak to pixel-identical ordinary clipboard import", () => Assert(store.Collection.Count == 1 && !store.Collection[0].ValidatedWindowsCapture && CardFor(store.Collection[0]).PendingArrivalOrigin == null && !line.IsVisible, "Completed origin escaped its publication lifetime"));

                Reset(); png = Png(181); windowsOriginal = Path.Combine(Settings.Root, "windows-original-before-clipboard.png"); File.WriteAllBytes(windowsOriginal, png); await store.Add(windowsOriginal, screenshot: true);
                photo = store.Collection.Single(); card = CardFor(photo);
                Test("file-first photo stays pending while automatic reveal is disabled", () => Assert(!line!.IsVisible && card.AwaitingFirstArrival && !photo.ArrivalStarted && card.PendingArrivalOrigin == null, "Fixture was already presented"));
                var leadIn = (VisibleArrivalClock)typeof(Card).GetField("arrivalClock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(card)!;
                leadIn.Sample(0, true, 100);
                await store.CaptureClipboardData(Data(png), "SnippingTool", 20, () => 20, Matcher(origin, 20));
                Test("file-first duplicate adopts validated origin during peg lead-in before visible picture", () => Assert(line!.Revealed && store.Collection.Count == 1 && store.Collection[0] == photo && card.PendingArrivalOrigin == origin && File.Exists(windowsOriginal), "Pending original lost origin or duplicated"));

                Reset(); png = Png(182); windowsOriginal = Path.Combine(Settings.Root, "windows-original-racing.png"); File.WriteAllBytes(windowsOriginal, png); Task? fileArrival = null;
                store.WindowsCaptureAccepted += _ => fileArrival = store.Add(windowsOriginal, screenshot: true);
                await store.CaptureClipboardData(Data(png), "SnippingTool", 30, () => 30, Matcher(origin, 30)); if (fileArrival != null) await fileArrival;
                photo = store.Collection.Single();
                Test("overlapping file/clipboard intake keeps one photo with the verified origin", () => Assert(CardFor(photo).PendingArrivalOrigin == origin && photo.ValidatedWindowsCapture && File.Exists(windowsOriginal), "Concurrent intake lost source or original file"));

                Reset(autoHang: false); png = Png(183); await store.CaptureClipboardData(Data(png), "SnippingTool", 40, () => 40, Matcher(origin, 40));
                Test("validated Windows capture respects AutoHang off and remains in basket", () => Assert(store.Collection.Count == 1 && store.Items.Count == 0 && !line!.IsVisible && !store.Collection[0].UserInitiatedCapture, "AutoHang preference overridden"));

                Reset(generalClipboard: true); png = Png(184); var calls = 0;
                await store.CaptureClipboardData(Data(png), "other-app", 50, () => 50, (_, _, _, _) => { calls++; return null; });
                Test("ordinary copied images with no validated selection cannot force explicit reveal", () => Assert(calls == 1 && !line!.IsVisible && !store.Collection[0].ValidatedWindowsCapture && CardFor(store.Collection[0]).PendingArrivalOrigin == null, "Untrusted source acquired origin"));

                Reset(); png = Png(185); calls = 0; var excluded = Data(png); excluded.SetData("ExcludeClipboardContentFromMonitorProcessing", true);
                await store.CaptureClipboardData(excluded, "SnippingTool", 60, () => 60, (_, _, _, _) => { calls++; return origin; });
                var retry = await store.CaptureClipboardData(Data(png), "SnippingTool", 60, () => 61, (_, _, _, _) => { calls++; return origin; });
                Test("privacy filters and changed post-read sequence reject before candidate consumption", () => Assert(calls == 0 && !retry && store.Collection.Count == 0 && !line!.IsVisible, "Excluded or stale clipboard consumed candidate"));
                calls = 0;
                await store.CaptureClipboardData(Data(png), "SnippingTool", 62, () => ++calls < 3 ? 62u : 63u, Matcher(origin, 62));
                Test("sequence change during publication cannot save stale clipboard image", () => Assert(store.Collection.Count == 0 && app.PendingCaptureCount == 0, "Stale snapshot published"));
                windowsOriginal = Path.Combine(Settings.Root, "after-rejected-sequence.png"); File.WriteAllBytes(windowsOriginal, png); await store.Add(windowsOriginal, screenshot: true);
                Test("rejected publication cannot leak an origin to a later file import", () => Assert(!store.Collection[0].ValidatedWindowsCapture && CardFor(store.Collection[0]).PendingArrivalOrigin == null, "Rejected origin leaked"));

                Reset(game: true); png = Png(186); calls = 0;
                await store.CaptureClipboardData(Data(png), "SnippingTool", 70, () => 70, (_, _, _, _) => { calls++; return origin; });
                Test("game mode still accepts files quietly but never consumes origin or reveals", () => Assert(calls == 0 && store.Collection.Count == 1 && !line!.IsVisible && !store.Collection[0].ValidatedWindowsCapture, "Game mode leaked presentation"));
                Reset(fullscreen: true); png = Png(187); await store.CaptureClipboardData(Data(png), "SnippingTool", 80, () => 80, Matcher(origin, 80));
                Test("validated Windows snip does not bypass protected fullscreen", () => Assert(store.Collection[0].ValidatedWindowsCapture && !line!.IsVisible && !line.Revealed, "Fullscreen guard bypassed"));

                Reset(); png = Png(188);
                var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var publisher = store.PublishValidatedWindowsCapture!;
                store.PublishValidatedWindowsCapture = (path, publish) => publisher(path, async () => { await barrier.Task; await publish(); });
                var intake = store.CaptureClipboardData(Data(png), "SnippingTool", 90, () => 90, Matcher(origin, 90));
                var shutdown = false; var exiting = app.ExitAfterPendingWork(() => shutdown = true);
                Test("exit waits for a validated Windows clipboard publication", () => Assert(!exiting.IsCompleted && !intake.IsCompleted && app.PendingCaptureCount == 1 && !shutdown, "Windows capture was untracked"));
                barrier.SetResult(true); await intake; await exiting;
                Test("waiting Windows capture publishes durably without a late reveal during exit", () => Assert(shutdown && store.Collection.Count == 1 && File.Exists(store.Collection[0].Path) && Settings.Load().Collection.Contains(store.Collection[0].Path) && !line!.IsVisible && app.PendingCaptureCount == 0, "Exit lost capture or reopened shelf"));
            } catch (Exception e) { log.Add("FAIL runner: " + e); }
            finally { line?.Close(); File.WriteAllLines(Path.Combine(Settings.Root, "results.txt"), log); Console.WriteLine(string.Join(Environment.NewLine, log)); app.Shutdown(log.Any(item => item.StartsWith("FAIL")) ? 1 : 0); }
        };
        return app.Run();
    }
}
