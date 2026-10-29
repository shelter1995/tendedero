namespace Liangtu;

internal static class ChoreographyChecks
{
    public static async Task Run(App app, Photo photo, Action<string, Action> test)
    {
        void Assert(bool ok, string reason) { if (!ok) throw new Exception(reason); }
        test("hidden wall-clock time never consumes first visible arrival", () => {
            var clock = new VisibleArrivalClock(); Assert(clock.Sample(1, false) == -1 && clock.Sample(100, false) == -1 && !clock.Started, "Hidden clock started");
            Assert(clock.Sample(500, true) == 0 && clock.Sample(500.5, true) > .49, "First visible time lost");
        });
        test("peg arrives and opens before picture; closing starts only after picture stops", () => {
            var first = ArrivalPhase.At(.18); Assert(first.PegOffset == 0 && first.PegOpen > .7 && !first.PictureVisible, "Peg not first");
            foreach (var t in new[] { .3, .6, 1.0 }) { var phase = ArrivalPhase.At(t); Assert(phase.PegOpen == 1 && phase.Picture < 1, "Clamp closed on moving picture"); }
            var clamp = ArrivalPhase.At(1.22); Assert(clamp.Picture == 1 && clamp.PegOpen > 0 && clamp.PegOpen < 1, "Closing phase incorrect");
            Assert(ArrivalPhase.At(1.4).Finished && ArrivalPhase.At(1.4).PegOpen == 0, "Clamp did not finish");
        });
        test("1 to100 photos reserve basket area and remain readable across viewport sizes", () => {
            foreach (var width in new[] { 640d, 1024, 1536, 1920, 2560 }) foreach (var count in new[] { 1, 6, 12, 25, 50, 100 }) {
                var layout = ShelfLayout.Create(width, count, 1.35); Assert(layout.Scale >= .55 && layout.Scale <= 1.35, "Unreadable scale");
                foreach (var offset in new[] { 0d, layout.MaxScroll }) {
                    var inView = Enumerable.Range(0, count).Select(i => layout.Center(i, offset)).Where(x => x >= layout.Left && x <= layout.Right).ToArray();
                    foreach (var x in inView) Assert(x + (150 * layout.Scale - 6) / 2 < width - 100, "Photo intrudes into hanging basket");
                }
            }
        });
        test("Bezier timing stays continuous and monotonic", () => {
            var values = Enumerable.Range(0, 101).Select(i => MotionCurve.Ease(i / 100d)).ToArray();
            Assert(values[0] == 0 && values[^1] == 1 && values.Zip(values.Skip(1)).All(p => p.First <= p.Second), "Timing reverses or misses endpoints");
        });
        app.Line.SuspendForGameMode(); app.Store.TakeDown(photo);
        var bounds = app.Line.Screen.Bounds;
        app.Store.Mount(photo, new Rect(bounds.Left + 140, bounds.Top + 270, 320, 200));
        await Task.Delay(1450);
        var surface = ((Canvas)app.Line.Content).Children.OfType<Canvas>().Single();
        var card = surface.Children.OfType<Card>().Last(c => c.Photo == photo);
        test("real hidden card still awaits first visible frame after1.45seconds", () => Assert(card.AwaitingFirstArrival, "Hidden card animation expired"));
        app.Line.Reveal(true, 3); app.Line.UpdateLayout();
        var dir = Path.Combine(Settings.Root, "arrival-frames"); Directory.CreateDirectory(dir);
        var picturesMoved = new List<Rect>();
        for (int i = 0; i < 42; i++) {
            await Task.Delay(40); app.Line.UpdateLayout();
            var flying = card.ArrivalRenderedBounds;
            picturesMoved.Add(flying ?? card.FrameBounds(surface));
            var canvas = (FrameworkElement)app.Line.Content;
            var composite = new DrawingVisual();
            using (var dc = composite.RenderOpen()) {
                dc.DrawRectangle(new VisualBrush(canvas), null, new Rect(0, 0, canvas.ActualWidth, canvas.ActualHeight));
                // The click-through flight owns a separate native surface. Compose
                // its measured screen rectangle into this fixture-only recording.
                card.DrawArrivalSnapshot(dc,app.Line);
            }
            var bmp = new RenderTargetBitmap((int)Math.Ceiling(app.Line.Width), 540, 96, 96, PixelFormats.Pbgra32); bmp.Render(composite);
            MediaTools.SavePng(bmp, Path.Combine(dir, i.ToString("D2") + ".png"));
        }
        test("real screenshot arrival moves through intermediate frames and settles", () => {
            Assert(picturesMoved.Select(r => Math.Round(r.Y)).Distinct().Count() > 8, "No visible path");
            Assert(Math.Abs(picturesMoved[^1].Y - picturesMoved[^2].Y) < 2, "Arrival never settles");
        });
        test("peg and image do not share flight transforms and actions occupy opposite corners", () => {
            var group = (Grid)card.Children[0]; var frame = (Border)group.Children[0]; var peg = (Border)group.Children[1]; var left = (Border)group.Children[2]; var right = (Border)group.Children[3];
            Assert(frame.RenderTransform is TransformGroup && peg.RenderTransform is TranslateTransform, "Transforms coupled");
            Assert(left.Margin.Left + left.Width < right.Margin.Left, "Action targets overlap");
            Assert(right.ToolTip?.ToString()?.Contains("回收站") == true, "Missing explicit recycle action");
        });
        test("native live glass never extends beyond the photo lane", () => {
            var themeBefore = Theme.SelectedId; Theme.Select("sky"); app.Line.Refresh(); app.Line.UpdateLayout();
            card.FinishArrival();
            var frame = (GlassBorder)((Grid)card.Children[0]).Children[0]; var rect = card.FrameBounds((Visual)app.Line.Content);
            var layout = ShelfLayout.Create(app.Line.Width, app.Store.Items.Count, app.Settings.Scale);
            try {
                Assert(GlassBorder.In(app.Line).Contains(frame), "Fixture is not contributing live glass");
                card.SetLane(rect.Left + rect.Width / 2, layout.Right, app.Line.Height);
                Assert(!GlassBorder.In(app.Line).Contains(frame), "Native glass ignores clipping");
            } finally { card.SetLane(layout.Left, layout.Right, app.Line.Height); Theme.Select(themeBefore); app.Line.Refresh(); }
        });
        app.Line.SuspendForGameMode();
    }
}
