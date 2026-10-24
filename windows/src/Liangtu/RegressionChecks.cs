using System.Diagnostics;
namespace Liangtu;
public static class RegressionChecks
{
    static MediaElement? FindMedia(DependencyObject node)
    {
        if (node is MediaElement m)
            return m;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var found = FindMedia(VisualTreeHelper.GetChild(node, i));
            if (found != null)
                return found;
        }
        return null;
    }
    public static async Task Run(App app, List<string> log)
    {
        async Task Check(string name, Func<Task<bool>> test)
        {
            try
            {
                log.Add((await test() ? "PASS " : "FAIL ") + name);
            }
            catch (Exception e) { log.Add("FAIL " + name + ": " + e.Message); }
        }
        var root = Path.Combine(Settings.Root, "regression");
        Directory.CreateDirectory(root);
        BitmapSource Solid(int w, int h, Color color)
        {
            var dv = new DrawingVisual();
            using (var c = dv.RenderOpen())
                c.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, w, h));
            var b = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            b.Render(dv);
            b.Freeze();
            return b;
        }

        await Check("single-click timer runs without rendering", async () => { var calls = 0; var clock = Stopwatch.StartNew(); using var timer = new GestureScheduler(a => { if (a == GestureAction.Copy) calls++; }, () => clock.Elapsed.TotalSeconds); timer.Down(0); timer.Up(.01, .9); await Task.Delay(1100); return calls == 1; });
        await Check("local eraser restores only its target", () => { var clean = Solid(100, 100, Colors.Blue); var marked = Solid(100, 100, Colors.Red); var eraser = new RasterEraser(marked, clean); eraser.Erase(new Point(20, 20), new Point(25, 20), 8); var pixels = new byte[40000]; eraser.Image.CopyPixels(pixels, 400, 0); return Task.FromResult(pixels[20 * 400 + 20 * 4] == 255 && pixels[80 * 400 + 80 * 4 + 2] == 255); });
        await Check("multi-frame images require a separate edited copy", () => { var path = Path.Combine(root, "animated.gif"); var encoder = new GifBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Solid(20, 20, Colors.Red))); encoder.Frames.Add(BitmapFrame.Create(Solid(20, 20, Colors.Blue))); using (var stream = File.Create(path)) encoder.Save(stream); return Task.FromResult(MediaTools.NeedsSeparateEdit(path)); });
        var videoSource = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "trim-test.mp4"));
        if (File.Exists(videoSource) && MediaTools.Ffmpeg != null)
        {
            await Check("non-keyframe trim starts with selected content", async () =>
            {
                var video = Path.Combine(root, "trim.mp4");
                File.Copy(videoSource, video, true);
                await MediaTools.Trim(video, 2.5, 4.5);
                var first = await MediaTools.Thumbnail(video);
                var b = new FormatConvertedBitmap(first, PixelFormats.Bgra32, null, 0);
                var pixel = new byte[4];
                b.CopyPixels(new Int32Rect(10, 10, 1, 1), pixel, 4, 0);
                return pixel[1] > 200 && pixel[2] < 20;
            });
            await Check("video preview opens and plays", async () =>
            {
                var photo = new Photo { Path = videoSource, Image = MediaTools.VideoPlaceholder() };
                var preview = new PreviewWindow(app, photo);
                preview.Show();
                await Task.Delay(1800);
                var media = FindMedia(preview);
                var loaded = media?.NaturalDuration.HasTimeSpan == true;
                preview.Close();
                return loaded;
            });
        }
        await Check("edited JPEG retains JPEG encoding", () => { var path = Path.Combine(root, "format.jpg"); MediaTools.SavePng(Solid(160, 80, Colors.Red), path); var b = File.ReadAllBytes(path); return Task.FromResult(b[0] == 0xff && b[1] == 0xd8); });
        await Check("drag bitmap uses full source dimensions", async () => { var path = Path.Combine(root, "large.png"); MediaTools.SavePng(Solid(1600, 800, Colors.Blue), path); var p = new Photo { Path = path, Image = await MediaTools.Thumbnail(path) }; return ((BitmapSource)Card.DragData(p).GetData(DataFormats.Bitmap)).PixelWidth == 1600; });
        await Check("watcher detects a folder created after enabling", async () => { var folder = Path.Combine(root, "later-" + Guid.NewGuid()); var settings = new Settings { ExtraFolders = new() { folder } }; var store = new Store(settings); using var watcher = new FolderWatch(store, settings); Directory.CreateDirectory(folder); MediaTools.SavePng(Solid(150, 100, Colors.Green), Path.Combine(folder, "late.png")); await Task.Delay(2200); return store.Items.Any(p => p.Path.EndsWith("late.png")); });
        await Check("watcher reloads an existing image without duplicates", async () => { var folder = Path.Combine(root, "edit-" + Guid.NewGuid()); Directory.CreateDirectory(folder); var settings = new Settings { ExtraFolders = new() { folder } }; var store = new Store(settings); using var watcher = new FolderWatch(store, settings); var path = Path.Combine(folder, "edited.png"); MediaTools.SavePng(Solid(100, 100, Colors.Red), path); await Task.Delay(1500); MediaTools.SavePng(Solid(200, 100, Colors.Blue), path); await Task.Delay(1800); return store.Items.Count == 1 && store.Items[0].Image.PixelWidth == 600 && store.Items[0].Image.PixelHeight == 300; });
    }
}
