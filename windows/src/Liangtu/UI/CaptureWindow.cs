using System.Windows.Shapes;
using System.Windows.Interop;
namespace Liangtu;

public sealed class CaptureWindow : Window
{
    readonly Canvas canvas = new();
    readonly Rectangle selection = new() { Stroke = Brushes.White, StrokeThickness = 1, Fill = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)) };
    readonly Rectangle shade = new() { Fill = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)) };
    readonly BitmapSource screenshot;
    readonly System.Drawing.Rectangle bounds;
    readonly Action<BitmapSource, Rect> done;
    System.Drawing.Point? start;
    bool placing;
    public CaptureWindow(Action<BitmapSource, Rect> done, BitmapSource? testImage = null, System.Drawing.Rectangle? testBounds = null)
    {
        this.done = done;
        bounds = testBounds ?? System.Windows.Forms.SystemInformation.VirtualScreen;
        screenshot = testImage ?? Native.Capture(bounds);
        if (screenshot.PixelWidth != bounds.Width || screenshot.PixelHeight != bounds.Height) throw new ArgumentException("Capture must match physical desktop pixels");
        var initialDpi = Native.Dpi(System.Windows.Forms.Screen.FromRectangle(bounds));
        Width = bounds.Width / initialDpi; Height = bounds.Height / initialDpi;
        Title = "晾图 · 框选截图"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        Topmost = true; ShowInTaskbar = false; Cursor = Cursors.Cross;
        Background = new ImageBrush(screenshot) { Stretch = Stretch.Fill }; Content = canvas;
        canvas.Children.Add(shade); canvas.Children.Add(selection);
        var help = new TextBlock { Text = "拖动选择截图区域 · Esc 取消", FontFamily = Theme.Font, Foreground = Brushes.White, Background = Theme.Brush("#D0202125"), Padding = new Thickness(16, 9, 16, 9), FontSize = 14 };
        canvas.Children.Add(help); Canvas.SetLeft(help, 40); Canvas.SetTop(help, 40);
        SourceInitialized += (_, _) => Place(); Loaded += (_, _) => { Place(); Activate(); };
        DpiChanged += (_, _) => { if (!placing) Dispatcher.BeginInvoke(Place, DispatcherPriority.Loaded); };
        SizeChanged += (_, _) => { shade.Width = ActualWidth; shade.Height = ActualHeight; };
        MouseLeftButtonDown += (_, e) => { start = Pointer(); canvas.CaptureMouse(); e.Handled = true; };
        MouseMove += (_, _) => {
            if (start is not System.Drawing.Point first) return;
            var physical = ScreenPixels.Selection(first, Pointer(), bounds);
            var a = PointFromScreen(new Point(physical.Left, physical.Top)); var b = PointFromScreen(new Point(physical.Right, physical.Bottom));
            Canvas.SetLeft(selection, a.X); Canvas.SetTop(selection, a.Y); selection.Width = Math.Max(0, b.X - a.X); selection.Height = Math.Max(0, b.Y - a.Y);
        };
        MouseLeftButtonUp += (_, e) => {
            if (start is not System.Drawing.Point first) return;
            var physical = ScreenPixels.Selection(first, Pointer(), bounds); start = null; canvas.ReleaseMouseCapture();
            var image = Crop(screenshot, bounds, physical); Close();
            if (image != null) done(image, new Rect(physical.Left, physical.Top, physical.Width, physical.Height));
            e.Handled = true;
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Closed += (_, _) => { start = null; if (Mouse.Captured == canvas) canvas.ReleaseMouseCapture(); };
    }
    void Place()
    {
        if (placing) return;
        placing = true;
        try { Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x10); }
        finally { placing = false; }
    }
    static System.Drawing.Point Pointer() { Native.GetCursorPos(out var point); return new(point.X, point.Y); }
    internal static BitmapSource? Crop(BitmapSource source, System.Drawing.Rectangle desktop, System.Drawing.Rectangle selection)
    {
        var rect = System.Drawing.Rectangle.Intersect(desktop, selection);
        if (rect.Width < 3 || rect.Height < 3) return null;
        var bitmap = new CroppedBitmap(source, new Int32Rect(rect.X - desktop.X, rect.Y - desktop.Y, rect.Width, rect.Height)); bitmap.Freeze(); return bitmap;
    }
}
