using System.Diagnostics;
using System.Reflection;
namespace Liangtu;

// Real filesystem, FFmpeg and WPF integration checks; always use isolated data.
public static class VideoFlowChecks
{
    static readonly List<string> log = new();
    static bool historyOnly;
    static void Assert(bool ok, string detail) { if (!ok) throw new Exception(detail); }
    static async Task Until(Func<bool> ready, int timeout = 6000, string state = "expected state")
    {
        var clock = Stopwatch.StartNew();
        while (!ready() && clock.ElapsedMilliseconds < timeout) await Task.Delay(50);
        Assert(ready(), "Timed out waiting for " + state);
    }
    static IEnumerable<T> Children<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T item) yield return item;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in Children<T>(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    static async Task Check(string name, Func<Task> body)
    {
        if (historyOnly && !name.Contains("history")) return;
        try { await body(); log.Add("PASS " + name); }
        catch (Exception e) { log.Add("FAIL " + name + ": " + e.Message); }
        File.WriteAllLines(Path.Combine(Settings.Root, "results.txt"), log);
    }
    [STAThread]
    public static int Main(string[] args)
    {
        historyOnly = args.Contains("--history-only");
        Settings.Root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(Settings.Inbox);
        var source = Path.GetFullPath(args[1]);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            var settings = new Settings { Keep = 100 };
            typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings);
            typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, new Store(settings));
            await Check("MP4 thumbnail contains decoded frame", async () =>
            {
                var thumb = await MediaTools.Thumbnail(source);
                Assert(thumb.PixelWidth == 600 && thumb.PixelHeight == 338 && thumb.IsFrozen, $"Unexpected thumbnail {thumb.PixelWidth}x{thumb.PixelHeight}");
                MediaTools.SavePng(thumb, Path.Combine(Settings.Root, "thumbnail.png"));
            });
            await Check("completed recording is collected once", async () =>
            {
                var folder = Path.Combine(Settings.Root, "completed"); Directory.CreateDirectory(folder);
                var s = new Settings { ExtraFolders = new() { folder } }; var store = new Store(s);
                using var watch = new FolderWatch(store, s);
                File.Copy(source, Path.Combine(folder, "recording.mp4"), true);
                await Until(() => store.Items.Count == 1);
                await Task.Delay(800);
                Assert(store.Items.Count == 1 && store.Items[0].Image.PixelWidth == 600, "Duplicate or placeholder card");
            });
            await Check("recording locked for 12 seconds is collected after writer closes", async () =>
            {
                var folder = Path.Combine(Settings.Root, "recording"); Directory.CreateDirectory(folder);
                var s = new Settings { ExtraFolders = new() { folder } }; var store = new Store(s);
                using var watch = new FolderWatch(store, s);
                var path = Path.Combine(folder, "long-recording.mp4");
                using (var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    var bytes = await File.ReadAllBytesAsync(source); await writer.WriteAsync(bytes); await writer.FlushAsync();
                    await Task.Delay(12000);
                    Assert(store.Items.Count == 0, "Imported an unfinished recording");
                }
                await Until(() => store.Items.Count == 1);
                Assert(store.Items[0].Image.PixelWidth == 600, "Recording has placeholder thumbnail");
            });
            var photo = new Photo { Path = source, Image = await MediaTools.Thumbnail(source) };
            await Check("preview advances, pauses, resumes and replays after EOF", async () =>
            {
                var preview = new PreviewWindow(app, photo); preview.Show();
                try
                {
                    var media = Children<MediaElement>(preview).Single();
                    await Until(() => media.NaturalDuration.HasTimeSpan && media.Position.TotalSeconds > .2, state: "preview playback to advance");
                    void Click() => media.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
                    Click(); var paused = media.Position.TotalSeconds; await Task.Delay(350);
                    Assert(Math.Abs(media.Position.TotalSeconds - paused) < .15, "Pause does not stop playback");
                    Click(); await Until(() => media.Position.TotalSeconds > paused + .2, state: "preview resume");
                    var ended = false; media.MediaEnded += (_, _) => ended = true;
                    media.Position = media.NaturalDuration.TimeSpan - TimeSpan.FromSeconds(.3);
                    await Until(() => ended, state: "natural MediaEnded event");
                    Click(); await Task.Delay(650);
                    Assert(media.Position.TotalSeconds > .1 && media.Position.TotalSeconds < 2, $"Replay stuck at {media.Position.TotalSeconds:F2}s");
                }
                finally { preview.Close(); }
            });
            await Check("trim window loads, previews selection and saves", async () =>
            {
                var path = Path.Combine(Settings.Root, "trim-ui.mp4"); File.Copy(source, path, true);
                var p = new Photo { Path = path, Image = await MediaTools.Thumbnail(path) };
                var window = new TrimWindow(app, p); window.Show();
                try
                {
                    var media = Children<MediaElement>(window).Single();
                    var timeline = Children<VideoTimeline>(window).Single();
                    await Until(() => media.NaturalDuration.HasTimeSpan && timeline.Range.End > 11);
                    timeline.SetStart(2.5); timeline.SetEnd(4.5);
                    var buttons = Children<Button>(window).ToArray();
                    buttons.Single(b => (b.ToolTip as string)?.StartsWith("播放选区") == true).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Until(() => media.Position.TotalSeconds >= 4.5);
                    var position = media.Position.TotalSeconds; await Task.Delay(350);
                    Assert(Math.Abs(media.Position.TotalSeconds - position) < .15, "Selection did not pause at end");
                    buttons.Single(b => Equals(b.Content, "保存片段")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Until(() => !window.IsVisible, 15000);
                    Assert(!Directory.EnumerateFiles(Settings.Root, ".trimming-*").Any(), "Temporary file leaked");
                }
                finally { if (window.IsVisible && window.IsEnabled) window.Close(); }
            });
            await Check("trim cancellation preserves original and releases media", async () =>
            {
                var path = Path.Combine(Settings.Root, "cancel.mp4"); File.Copy(source, path, true); var before = File.ReadAllBytes(path);
                var window = new TrimWindow(app, new Photo { Path = path, Image = photo.Image }); window.Show();
                await Until(() => window.Playback.Ready); window.Timeline.SetStart(2); window.Close(); await Task.Delay(250);
                Assert(before.SequenceEqual(File.ReadAllBytes(path)), "Cancel changed original");
                using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            });
            await Check("timeline accessibility, keyboard and exact input stay synchronized", async () =>
            {
                var window = new TrimWindow(app, photo); window.Show();
                try
                {
                    await Until(() => window.Playback.Ready);
                    var handles = Children<TimelineThumb>(window).ToArray();
                    var startPeer = new TimelineThumbPeer(handles[0]); startPeer.SetValue(2.5);
                    var endPeer = new TimelineThumbPeer(handles[1]); endPeer.SetValue(4.5);
                    Assert(Math.Abs(window.Timeline.Range.Start - 2.5) < .001 && Math.Abs(window.Playback.End - 4.5) < .001, "Handle did not set selection");
                    Assert(!window.Playback.Playing, "Dragging selection resumed playback");
                    var inputs = Children<TextBox>(window).ToArray();
                    Assert(inputs[0].Text == "00:02.500" && inputs[1].Text == "00:04.500", "Time labels not synchronized");
                    window.Width = 700; window.Height = 480; window.UpdateLayout();
                    foreach (var control in inputs.Cast<FrameworkElement>().Concat(Children<Button>(window)))
                    {
                        var box = control.TransformToAncestor(window).TransformBounds(new Rect(control.RenderSize));
                        Assert(box.Left >= 0 && box.Right <= window.ActualWidth + 1 && box.Bottom <= window.ActualHeight + 1, "Control outside compact window");
                    }
                    var picture = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); picture.Render(window); MediaTools.SavePng(picture, Path.Combine(Settings.Root,"trim-compact.png"));
                }
                finally { window.Close(); }
            });
            await Check("failed save restores playback and preserves the selected range", async () =>
            {
                var path = Path.Combine(Settings.Root, "save-failure.mp4"); File.Copy(source, path, true); var before = File.ReadAllBytes(path);
                var window = new TrimWindow(app, new Photo { Path = path, Image = photo.Image }); window.Show();
                var originalPath = Environment.GetEnvironmentVariable("PATH");
                try
                {
                    await Until(() => window.Playback.Ready); window.Timeline.SetStart(2.5); window.Timeline.SetEnd(4.5);
                    Environment.SetEnvironmentVariable("PATH", "");
                    Children<Button>(window).Single(b => Equals(b.Content,"保存片段")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Until(() => window.Playback.Ready && window.IsEnabled);
                    Assert(window.Timeline.Range.Start == 2.5 && window.Timeline.Range.End == 4.5,"Selection lost after save failure");
                    Assert(before.SequenceEqual(File.ReadAllBytes(path)), "Failure changed original");
                }
                finally { Environment.SetEnvironmentVariable("PATH", originalPath); window.Close(); }
            });
            await Check("video selection history groups a drag and supports undo redo reset", async () =>
            {
                var window = new TrimWindow(app, photo); window.Show();
                try
                {
                    await Until(() => window.Playback.Ready);
                    var buttons = Children<Button>(window).ToArray();
                    var undo = buttons.Single(b => (b.ToolTip as string)?.StartsWith("撤销") == true);
                    var redo = buttons.Single(b => (b.ToolTip as string)?.StartsWith("重做") == true);
                    Assert(!undo.IsEnabled && !redo.IsEnabled,"New session has history");
                    var thumb = Children<TimelineThumb>(window).First();
                    thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0,0));
                    for(var i=0;i<120;i++) window.Timeline.SetStart(.5+i*.01);
                    thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(0,0,false));
                    Assert(undo.IsEnabled,"Drag not recorded"); undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(window.Timeline.Range.Start == 0 && !undo.IsEnabled && redo.IsEnabled,"Drag must undo as a single step");
                    redo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(Math.Abs(window.Timeline.Range.Start-1.69)<.001,"Redo lost drag");
                    window.Timeline.SetEnd(4.5);
                    buttons.Single(b=>Equals(b.Content,"重置选区")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(window.Timeline.Range.Start==0 && window.Timeline.Range.End==window.Playback.Duration,"Reset did not select full video");
                    undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(Math.Abs(window.Timeline.Range.Start-1.69)<.001 && window.Timeline.Range.End==4.5,"Undo reset lost either endpoint");
                    window.Timeline.SetStart(2); Assert(!redo.IsEnabled,"New edit retained stale redo");
                }
                finally { window.Close(); }
            });
            foreach (var ext in new[] { "mp4", "mov", "m4v", "avi", "wmv" })
            {
                var fixture = Path.Combine(Path.GetDirectoryName(source)!, "source." + ext);
                if (!File.Exists(fixture)) { log.Add("FAIL missing fixture " + ext); continue; }
                await Check(ext + " trim supports advertised format and preserves audio", async () =>
                {
                    var target = Path.Combine(Settings.Root, "trim." + ext); File.Copy(fixture, target, true);
                    await MediaTools.Trim(target, 2.5, 4.5);
                    var psi = new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(MediaTools.Ffmpeg)!, "ffprobe.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    foreach (var a in new[] { "-v", "error", "-show_entries", "format=duration:stream=codec_type", "-of", "json", target }) psi.ArgumentList.Add(a);
                    using var proc = Process.Start(psi)!; var output = proc.StandardOutput.ReadToEndAsync(); var err = proc.StandardError.ReadToEndAsync(); await proc.WaitForExitAsync();
                    var json = await output; File.WriteAllText(Path.Combine(Settings.Root, ext + "-probe.json"), json);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var duration = double.Parse(doc.RootElement.GetProperty("format").GetProperty("duration").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                    Assert(Math.Abs(duration - 2) < .2 && json.Contains("audio") && json.Contains("video"), $"Wrong duration/audio: {json} {await err}");
                });
            }
            await Check("failed trim preserves source bytes", async () =>
            {
                var target = Path.Combine(Settings.Root, "invalid.mp4"); var bytes = new byte[] { 1, 2, 3, 4 }; File.WriteAllBytes(target, bytes);
                try { await MediaTools.Trim(target, 1, 2); throw new Exception("Invalid media accepted"); }
                catch (IOException) { Assert(File.ReadAllBytes(target).SequenceEqual(bytes), "Source overwritten on failure"); }
                Assert(!Directory.EnumerateFiles(Settings.Root, ".trimming-*").Any(), "Temporary file leaked");
            });
            File.WriteAllLines(Path.Combine(Settings.Root, "results.txt"), log);
            app.Shutdown(log.Any(l => l.StartsWith("FAIL")) ? 1 : 0);
        };
        return app.Run();
    }
}
