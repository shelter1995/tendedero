using System.Reflection;
using System.Windows.Media.Animation;
namespace Liangtu;

internal static class BasketChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox); Directory.CreateDirectory(Path.Combine(Settings.Root, "ui"));
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => {
            var log = new List<string>();
            void Check(string name, Action run) { try { run(); log.Add("PASS " + name); } catch (Exception e) { log.Add("FAIL " + name + ": " + e); } }
            void Assert(bool value, string message) { if (!value) throw new Exception(message); }
            try {
                var settings = new Settings { Sounds = false, Welcomed = true, CollectionRevision = 1 };
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings);
                typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app, true);
                var store = new Store(settings); typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, store);
                var line = new LineWindow(app); typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, line);
                var path = Path.Combine(Settings.Inbox, "界面参考.png"); CreateSample(path); await store.Add(path);
                var path2 = Path.Combine(Settings.Inbox, "操作说明.png"); File.Copy(path, path2, true); await store.Add(path2);
                var photo = store.Items[0]; store.TakeDown(photo);
                var basket = new BasketWindow(app);
                Check("basket filters and preserves multiple selection on store changes", () => {
                    Assert(basket.MediaList.Items.Count == 2, "All count");
                    basket.MediaList.SelectAll(); store.Persist(); basket.Refresh(); Assert(basket.MediaList.SelectedItems.Count == 2, "Selection lost");
                    basket.PendingFilter.IsChecked = true; Assert(basket.MediaList.Items.Count == 1, "Pending count");
                    basket.SearchBox.Text = "not-found"; Assert(basket.MediaList.Items.Count == 0, "Search"); basket.SearchBox.Text = "";
                    basket.MediaList.SelectedIndex = 0;
                });
                await Task.Delay(150);
                Check("basket renders at desktop and compact sizes", () => {
                    Render(basket, "basket.png", 1000, 640); Render(basket, "basket-compact.png", 640, 460);
                });
                Check("pin single instance and close keeps media; return remounts", () => {
                    app.Pin(photo); app.Pin(photo); Assert(app.PinCount == 1 && !store.Items.Contains(photo), "Pin duplicated or still mounted");
                    var pinned = app.Windows.OfType<PinnedImageWindow>().Single(); Assert(pinned.Topmost, "Not topmost");
                    pinned.Zoom(1.12); Assert(double.IsFinite(pinned.Width) && pinned.Width > 0, "Invalid zoom");
                    Render(pinned, "pin.png", 420, 340);
                    app.SetGameDoNotDisturb(true); Assert(!pinned.IsVisible, "DND pin remains visible");
                    app.SetGameDoNotDisturb(false); Assert(pinned.IsVisible, "DND pin not restored");
                    pinned.Close(); Assert(app.PinCount == 0 && store.Collection.Contains(photo) && store.Items.Contains(photo) && File.Exists(photo.Path), "Closing must hang the photo back on the rope");
                    app.Pin(photo); app.ReturnToLine(photo); Assert(app.PinCount == 0 && store.Items.Contains(photo), "Return failed");
                });
                Check("explicit mounting closes a corresponding pin", () => { app.Pin(photo); store.Mount(photo); Assert(app.PinCount == 0, "Pin survived remount"); });
                Check("reveal reverses retract without jumping to hidden origin", () => {
                    var slide = new TranslateTransform(0, 0); var retract = LineMotion.Retract(slide, -220);
                    retract.Controller!.SeekAlignedToLastTick(TimeSpan.FromSeconds(.12), TimeSeekOrigin.BeginTime); var before = slide.Y;
                    var reveal = LineMotion.Reveal(slide, -220, false); reveal.Controller!.SeekAlignedToLastTick(TimeSpan.Zero, TimeSeekOrigin.BeginTime);
                    Assert(Math.Abs(slide.Y - before) < .01, "Reversal jumped");
                    reveal.Controller!.SeekAlignedToLastTick(TimeSpan.FromSeconds(1), TimeSeekOrigin.BeginTime); Assert(Math.Abs(slide.Y) < .01, "Did not settle");
                });
                await ShelfVisualChecks.Run(app, photo, Check);
                await ChoreographyChecks.Run(app, photo, Check);
                TextChecks.Run(app, photo, Check);
                var editor = new EditorWindow(app, photo); editor.SelectTool("文字"); editor.BeginStroke(new Point(90, 100));
                var input = Descendants(editor.Content as DependencyObject).OfType<TextBox>().Single(t => t.AcceptsReturn); input.Text = "在这里输入文字\n拖动移动柄调整位置"; editor.CommitText();
                Check("inline text uses dashed selection frame", () => { Assert(Descendants(editor.Content as DependencyObject).OfType<TextSelectionFrame>().Any(), "Missing frame"); Render(editor, "editor.png", 1120, 740); });
                for (int i = 0; i < 30; i++) editor.Undo(); editor.Close(); basket.Close(); line.Close();
            } catch (Exception e) { log.Add("FAIL runner: " + e); }
            await File.WriteAllLinesAsync(Path.Combine(Settings.Root, "results.txt"), log);
            foreach (Window w in app.Windows.Cast<Window>().ToArray()) {
                File.AppendAllText(Path.Combine(Settings.Root, "cleanup.txt"), "Closing " + w.GetType().Name + ": " + w.Title + Environment.NewLine);
                if (w is EditorWindow remaining) for (int i = 0; i < 100; i++) remaining.Undo();
                w.Close();
                File.AppendAllText(Path.Combine(Settings.Root, "cleanup.txt"), "Closed" + Environment.NewLine);
            }
            app.Shutdown(log.Any(l => l.StartsWith("FAIL")) ? 1 : 0);
        };
        return app.Run();
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject? root) {
        if (root == null) yield break;
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    static void Render(Window window, string name, double width, double height) {
        var root = (FrameworkElement)window.Content; root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(root); MediaTools.SavePng(bitmap, Path.Combine(Settings.Root, name));
    }
    static void CreateSample(string path) {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) {
            dc.DrawRectangle(Theme.Brush("#EEF1F5"), null, new Rect(0, 0, 900, 560));
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(50, 44, 800, 472), 14, 14);
            dc.DrawRectangle(Theme.Brush("#30455F"), null, new Rect(50, 44, 800, 64));
            var text = new FormattedText("项目参考 / 工作台", System.Globalization.CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 24, Brushes.White, 1); dc.DrawText(text, new Point(76, 62));
            for (int i = 0; i < 3; i++) { dc.DrawRoundedRectangle(Theme.Brush("#E4EAF2"), null, new Rect(78 + i * 252, 150, 230, 170), 6, 6); dc.DrawRectangle(Theme.Brush("#C5D2E3"), null, new Rect(78 + i * 252, 340, 172, 12)); }
            dc.DrawRoundedRectangle(Theme.Brush("#4779C7"), null, new Rect(78, 418, 150, 42), 5, 5);
        }
        var bmp = new RenderTargetBitmap(900, 560, 96, 96, PixelFormats.Pbgra32); bmp.Render(visual); MediaTools.SavePng(bmp, path);
    }
}
