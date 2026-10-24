namespace Liangtu;

internal static class MultiDisplayChecks
{
    public static void Run(App app, LineWindow window, BitmapSource bitmap, Action<string, Action> test)
    {
        var photo = new Photo { Path = Path.Combine(Settings.Root, "multi-display.png"), Image = bitmap };
        app.Store.Items.Add(photo); window.Refresh();
        var layer = (Canvas)window.Content; var line = layer.Children.OfType<Canvas>().Single(); var card = line.Children.OfType<Card>().Last();
        var group = (Grid)card.Children[0]; var frame = (Border)group.Children[0];
        test("card animation resumes after repeated unload/load without duplicate subscriptions", () => {
            for (int i = 0; i < 4; i++) {
                card.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                if (card.RenderingAttached) throw new Exception("Still attached while unloaded");
                card.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); card.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                if (!card.RenderingAttached) throw new Exception("Animation never reattached on next reveal");
            }
        });
        test("rotated card corner outside the visible frame is click-through", () => {
            window.ApplyDisplayLayout(new Liangtu.Core.DisplayGeometry("test", new System.Drawing.Rectangle(-2560, -200, 2560, 1440), 1.25));
            layer.Measure(new Size(window.Width, window.Height)); layer.Arrange(new Rect(0, 0, window.Width, window.Height)); layer.UpdateLayout();
            var transforms = (TransformGroup)group.RenderTransform; transforms.Children.OfType<RotateTransform>().Single().Angle = 30;
            var transform = frame.TransformToAncestor(layer); var bounds = transform.TransformBounds(new Rect(frame.RenderSize));
            var corner = new Point(bounds.Left + 1, bounds.Top + 1);
            if (card.Contains(corner)) throw new Exception("Bounding box accepts a transparent corner");
            if (!card.Contains(transform.Transform(new Point(frame.ActualWidth / 2, frame.ActualHeight / 2)))) throw new Exception("Visible center not clickable");
        });
        foreach (var scale in new[] { 1d, 1.25, 1.5, 1.75, 2, 2.5, 3 })
        {
            var factor = scale;
            test($"mixed DPI {scale * 100}%: transformed card and physical mouse agree", () => {
                var display = new Liangtu.Core.DisplayGeometry("mixed", new System.Drawing.Rectangle(-3840, -400, 3840, 2160), factor);
                window.ApplyDisplayLayout(display);
                var slide = (TranslateTransform)line.RenderTransform; slide.BeginAnimation(TranslateTransform.YProperty, null); slide.Y = 0;
                card.FollowRope(9.5);
                ((TransformGroup)group.RenderTransform).Children.OfType<RotateTransform>().Single().Angle = 27;
                ((ScaleTransform)frame.RenderTransform).ScaleX = ((ScaleTransform)frame.RenderTransform).ScaleY = 1.035;
                layer.Measure(new Size(window.Width, window.Height)); layer.Arrange(new Rect(0, 0, window.Width, window.Height)); layer.UpdateLayout();
                var transform = frame.TransformToAncestor(layer);
                var center = transform.Transform(new Point(frame.ActualWidth / 2, frame.ActualHeight / 2));
                var pixel = display.LocalToPhysical(new DipPoint(center.X, center.Y)); var local = display.PhysicalToLocal(pixel);
                if (!card.Contains(new Point(local.X, local.Y))) throw new Exception("Visible image center misses after pixel/DIP conversion");
                var outside = transform.Transform(new Point(-8, -8)); var outsidePixel = display.LocalToPhysical(new DipPoint(outside.X, outside.Y)); var outsideLocal = display.PhysicalToLocal(outsidePixel);
                if (card.Contains(new Point(outsideLocal.X, outsideLocal.Y))) throw new Exception("Outside point intercepts click");
                var expected = Layout.Photo(bitmap.PixelWidth, bitmap.PixelHeight, app.Settings.Scale);
                if (Math.Abs(card.CardWidth - expected.width - 8) > .001 || photo.Image.PixelWidth != bitmap.PixelWidth) throw new Exception("Display scale altered image/source size");
            });
            test($"editor placement fits target work area at {scale * 100}%", () => {
                var work = new System.Drawing.Rectangle(-2560, -400, 2560, 1400);
                var rect = ScreenPixels.WindowBounds(work, 1120, 800, factor);
                if (!work.Contains(rect) || Math.Abs((rect.Left + rect.Width / 2d) - (work.Left + work.Width / 2d)) > 1) throw new Exception("Window spills outside target monitor");
            });
        }
        test("rope bounce repeats and every card follows the same parabola", () => {
            var rope = line.Children.OfType<Rope>().Single();
            for (int repeat = 0; repeat < 4; repeat++) {
                var animation = window.StartRopeBounce(); animation.Controller!.SeekAlignedToLastTick(TimeSpan.FromSeconds(.14), System.Windows.Media.Animation.TimeSeekOrigin.BeginTime);
                window.UpdateRopeMotion(null, EventArgs.Empty);
                var delta = ((TranslateTransform)card.RenderTransform).Y;
                if (Math.Abs(rope.Deflection) < 1 || Math.Abs(delta - Rope.Offset(Canvas.GetLeft(card) + 125, window.Width, rope.Deflection)) > .001) throw new Exception("Card detached from rope motion");
                animation.Controller.SeekAlignedToLastTick(TimeSpan.FromSeconds(1.3), System.Windows.Media.Animation.TimeSeekOrigin.BeginTime); window.UpdateRopeMotion(null, EventArgs.Empty);
                if (Math.Abs(rope.Deflection) > .001 || Math.Abs(((TranslateTransform)card.RenderTransform).Y) > .001) throw new Exception("Bounce did not settle");
            }
        });
        test("physical crop crosses monitor seam and preserves exact source pixels", () => {
            var desktop = new System.Drawing.Rectangle(-1600, -100, 3200, 100);
            var pixels = new byte[3200 * 100 * 4];
            for (int y = 0; y < 100; y++) for (int x = 0; x < 3200; x++) { var i = (y * 3200 + x) * 4; pixels[i] = 20; pixels[i + 1] = (byte)y; pixels[i + 2] = (byte)x; pixels[i + 3] = 255; }
            var source = BitmapSource.Create(3200, 100, 96, 96, PixelFormats.Bgra32, null, pixels, 12800);
            foreach (var scale in new[] { 1d, 1.25, 1.5, 2, 3 }) {
                var display = new DisplayGeometry("capture", desktop, scale);
                var wanted = new System.Drawing.Rectangle(-50, -90, 120, 30);
                var actual = display.LocalToPhysical(display.PhysicalToLocal(wanted));
                var selection = ScreenPixels.Selection(new System.Drawing.Point(actual.Right, actual.Bottom), actual.Location, desktop);
                var cropped = CaptureWindow.Crop(source, desktop, selection) ?? throw new Exception("Missing crop");
                var output = new byte[120 * 30 * 4]; cropped.CopyPixels(output, 480, 0);
                if (cropped.PixelWidth != 120 || cropped.PixelHeight != 30 || output[1] != 10 || output[2] != (1550 % 256)) throw new Exception("DPI scaled capture pixels or origin");
            }
            var clipped = ScreenPixels.Selection(new System.Drawing.Point(-1700, -120), new System.Drawing.Point(-1500, -70), desktop);
            if (clipped != new System.Drawing.Rectangle(-1600, -100, 100, 30)) throw new Exception("Out-of-bounds crop not clipped");
        });
        test("cancelled drag return interpolates physical size instead of reusing other monitor DPI", () => {
            var from = new Rect(-3000, -200, 270, 180); var to = new Rect(1100, 40, 135, 90);
            if (ReturnFlight.Interpolate(from, to, 0) != from || ReturnFlight.Interpolate(from, to, 1) != to || ReturnFlight.Interpolate(from, to, .5).Width != 202.5) throw new Exception("Mixed units in drag return");
        });
        card.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        app.Store.Items.Clear(); window.Refresh();
    }
}
