namespace Liangtu;
public static class ToolbarChecks
{
    static IEnumerable<T> Children<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T value) yield return value;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in Children<T>(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    static void Layout(Window window)
    {
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(900, 650)); content.Arrange(new Rect(0, 0, 900, 650)); content.UpdateLayout();
    }
    static byte[] Pixels(FrameworkElement icon)
    {
        var image = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32); image.Render(icon);
        var pixels = new byte[32 * 32 * 4]; image.CopyPixels(pixels, 128, 0); return pixels;
    }
    public static void Run(App app, Action<string, Action> test)
    {
        var bitmap = BitmapSource.Create(100, 80, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)210, 100 * 80 * 4).ToArray(), 400);
        var file = Path.Combine(Settings.Root, "toolbar-image.png"); MediaTools.SavePng(bitmap, file);
        var picture = new Photo { Path = file, Image = bitmap }; var recording = new Photo { Path = Path.Combine(Settings.Root, "toolbar-video.mp4"), Image = bitmap };
        test("preview and editors contain no duplicate toolbar close buttons", () => {
            foreach (var window in new Window[] { new EditorWindow(app, picture), new PreviewWindow(app, picture), new PreviewWindow(app, recording), new TrimWindow(app, recording) })
            {
                try { Layout(window); if (Children<Button>(window.Content as DependencyObject ?? window).Any(b => (b.ToolTip as string)?.StartsWith("关闭") == true)) throw new Exception(window.GetType().Name + " has a duplicate close action"); }
                finally { window.Close(); }
            }
        });
        test("image annotation and video trim have distinct visual actions after navigation", () => {
            var before = app.Store.Items.ToArray(); app.Store.Items.Clear(); app.Store.Items.AddRange(new[] { picture, recording });
            var window = new PreviewWindow(app, picture);
            try
            {
                Layout(window);
                var edit = Children<Button>((DependencyObject)window.Content).Single(b => (b.ToolTip as string)?.Contains("标注") == true);
                var imageGlyph = Pixels((FrameworkElement)edit.Content);
                Children<Button>((DependencyObject)window.Content).Single(b => Equals(b.ToolTip, "下一项")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(window);
                if ((edit.ToolTip as string)?.Contains("裁剪") != true) throw new Exception("Video action label did not update");
                if (imageGlyph.SequenceEqual(Pixels((FrameworkElement)edit.Content))) throw new Exception("Annotation and trim use the same glyph");
            }
            finally { window.Close(); app.Store.Items.Clear(); app.Store.Items.AddRange(before); }
        });
    }
}
