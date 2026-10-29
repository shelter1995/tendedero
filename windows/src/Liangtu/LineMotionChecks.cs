namespace Liangtu;

internal static class LineMotionChecks
{
    public static void Run(App app, LineWindow window, BitmapSource bitmap, Action<string, Action> test)
    {
        var photo = new Photo { Path = Path.Combine(Settings.Root, "line-motion.png"), Image = bitmap };
        app.Store.Items.Add(photo); window.Refresh();
        var layer = (Canvas)window.Content;
        var line = layer.Children.OfType<Canvas>().Single();
        var card = line.Children.OfType<Card>().Single();
        var displays = new[] {
            new DisplayGeometry("wide-left", new System.Drawing.Rectangle(-3840, -300, 3840, 2160), 1.5),
            new DisplayGeometry("narrow-right", new System.Drawing.Rectangle(0, 0, 1920, 1080), 1.25),
            new DisplayGeometry("upper", new System.Drawing.Rectangle(200, -1440, 2560, 1440), 2),
            new DisplayGeometry("wide-left", new System.Drawing.Rectangle(-3840, -300, 3840, 2160), 1.5),
            new DisplayGeometry("wide-left", new System.Drawing.Rectangle(-3840, -300, 3840, 2160), 2) };
        foreach (var display in displays)
        {
            var target = display;
            test("line display switch snaps layout without diagonal animation: " + target.DeviceName + " @ " + target.Scale, () => {
                window.ApplyDisplayLayout(target);
                var expected = ShelfLayout.Create(target.LocalBounds.Width, 1, app.Settings.Scale).Center(0, 0) - 125;
                if (Math.Abs(Canvas.GetLeft(card) - expected) > .001 || System.Windows.DependencyPropertyHelper.GetValueSource(card, Canvas.LeftProperty).IsAnimated || System.Windows.DependencyPropertyHelper.GetValueSource(card, Canvas.TopProperty).IsAnimated)
                    throw new Exception($"Old-screen motion survived: expected X={expected}, actual X={Canvas.GetLeft(card)}");
            });
        }
        test("line reveal clears old held animation before assigning hidden start", () => {
            var slide = new TranslateTransform(140, 0);
            slide.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 0, TimeSpan.FromSeconds(1)));
            LineMotion.PrepareHidden(slide, -222);
            if (slide.X != 0 || slide.Y != -222 || slide.HasAnimatedProperties) throw new Exception("Stale slide clock survived reset");
        });
        test("line fresh reveal stays vertical through animation and settles at zero", () => {
            var slide = new TranslateTransform(400, 0);
            var clock = LineMotion.Reveal(slide, -222, restart: true);
            foreach (var time in new[] { 0.0, .1, .3, .6, 1.0 }) {
                clock.Controller!.SeekAlignedToLastTick(TimeSpan.FromSeconds(time), System.Windows.Media.Animation.TimeSeekOrigin.BeginTime);
                if (slide.X != 0 || !double.IsFinite(slide.Y) || time == 0 && Math.Abs(slide.Y + 222) > .001 || time == 1 && Math.Abs(slide.Y) > .001) throw new Exception($"Unexpected slide at t={time}: {slide.X},{slide.Y}");
            }
        });
        test("line same-screen reorder remains animated but display relayout cancels it", () => {
            var item = new Border(); Canvas.SetLeft(item, 100); Canvas.SetTop(item, 20);
            LineMotion.Place(item, 300, 30, animate: true);
            // Offscreen clocks are attached before the first rendering tick.
            if (SystemParameters.ClientAreaAnimation && !LineMotion.Reflow(item).HasAnimatedProperties) throw new Exception("Normal reorder lost animation");
            if (item.HasAnimatedProperties) throw new Exception("Reorder invalidates Canvas layout every frame");
            LineMotion.Place(item, 800, 40, animate: false);
            if (Canvas.GetLeft(item) != 800 || Canvas.GetTop(item) != 40 || item.HasAnimatedProperties || LineMotion.Reflow(item).HasAnimatedProperties) throw new Exception("Reorder still affects new display");
        });
        app.Store.Items.Clear();
    }
}
