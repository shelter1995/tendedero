namespace Liangtu;

internal static class ShelfVisualChecks
{
    public static async Task Run(App app, Photo photo, Action<string, Action> test)
    {
        void Assert(bool ok, string error) { if (!ok) throw new Exception(error); }
        test("crowded shelf shrinks cards before they can reach the basket", () => {
            var before = app.Store.Items.ToArray();
            try {
                app.Store.Items.Clear(); app.Store.Items.Add(photo); app.Line.Refresh();
                var canvas = ((Canvas)app.Line.Content).Children.OfType<Canvas>().Single();
                var one = canvas.Children.OfType<Card>().Last(c => c.Photo == photo).CardWidth;
                for (int i = 0; i < 24; i++) app.Store.Items.Add(new Photo { Path = "layout-" + i, Image = photo.Image });
                app.Line.Refresh();
                var crowded = canvas.Children.OfType<Card>().Last(c => c.Photo == photo).CardWidth;
                Assert(crowded < one, "25 photos still use the single-photo size");
                var root = (FrameworkElement)app.Line.Content;
                root.Measure(new Size(app.Line.Width, app.Line.Height)); root.Arrange(new Rect(0, 0, app.Line.Width, app.Line.Height)); root.UpdateLayout();
                var slide = (TranslateTransform)canvas.RenderTransform; var hiddenY = slide.Y;
                try {
                    slide.BeginAnimation(TranslateTransform.YProperty, null); slide.Y = 0;
                    foreach (var c in canvas.Children.OfType<Card>()) c.FinishArrival();
                    var dense = new RenderTargetBitmap((int)app.Line.Width, 240, 96, 96, PixelFormats.Pbgra32); dense.Render(root); MediaTools.SavePng(dense, Path.Combine(Settings.Root, "dense-layout.png"));
                } finally { slide.Y = hiddenY; }
            } finally { app.Store.Items.Clear(); app.Store.Items.AddRange(before); app.Line.Refresh(); }
        });
        test("peg requires 450ms; release movement and repeated ticks cannot activate twice", () => {
            var hold = new PegHold(); hold.Begin(10); Assert(!hold.Complete(10.44), "Premature activation");
            hold.Cancel(); Assert(!hold.Complete(11), "Release did not cancel"); hold.Begin(12); hold.Move(7); Assert(!hold.Complete(13), "Movement did not cancel");
            hold.Begin(14); Assert(hold.Complete(14.46) && !hold.Complete(15), "Repeated activation");
        });
        test("explicit pin basket route and internal deletion never return to line", () => {
            app.Pin(photo); app.PutPinInBasket(photo); Assert(app.PinCount == 0 && !app.Store.Items.Contains(photo), "Basket route remounted");
            app.Pin(photo); app.Store.Collection.Remove(photo); app.Store.Prune(); app.Store.TakeDown(photo);
            // A normal collection update closes the pin without creating an orphan mount.
            app.Store.Persist(); var pin = app.Windows.OfType<PinnedImageWindow>().FirstOrDefault(); pin?.CloseWithoutReturn();
            Assert(app.PinCount == 0 && !app.Store.Items.Contains(photo), "Internal close remounted"); app.Store.Collection.Add(photo); app.Store.Mount(photo);
        });
        test("card consumes screenshot origin only once", () => {
            var sample = new Photo { Path = photo.Path, Image = photo.Image, Origin = new Rect(50, 60, 320, 180) };
            var card = new Card(sample, app); Assert(sample.Origin == null, "Screenshot source persists across mounts"); card.CancelInput();
        });
        test("flight starts and ends at requested rectangles with an upward arc", () => {
            var from = new Rect(100, 90, 150, 100); var to = new Rect(920, 62, 34, 22);
            Assert(MediaTransferVisual.Travel(from, to, 0) == from && MediaTransferVisual.Travel(from, to, 1) == to, "Endpoints drift");
            var middle = MediaTransferVisual.Travel(from, to, .5); Assert(middle.Y < 76 && middle.Width > 34 && middle.Width < 150, "Arc/size");
        });
        test("basket clip and corner controls render against light dark and busy images", () => RenderGallery(app));
        app.Line.Reveal(true, 2); app.Line.UpdateLayout();
        var animatePin = app.Line.CanAnimateTransfer;
        app.Pin(photo);
        test("moving into a pin keeps its invisible target out of hit testing", () => {
            if (!animatePin) return;
            var window = app.Windows.OfType<PinnedImageWindow>().Single();
            Assert(app.Line.ActiveTransferCount == 1 && !window.IsHitTestVisible && window.Opacity == 0, "Pin did not start a protected transfer");
        });
        await Task.Delay(1100);
        test("pin flight completes and reveals a usable desktop picture", () => {
            var window = app.Windows.OfType<PinnedImageWindow>().Single(); Assert(window.Opacity > .99 && app.Line.ActiveTransferCount == 0, "Incomplete flight");
        });
        app.ReturnToLine(photo);
        app.Line.Reveal(true, 2); app.Line.UpdateLayout(); app.Store.TakeDown(photo);
        await Task.Delay(120);
        app.SetGameDoNotDisturb(true);
        test("game mode cancels in-flight visuals without deleting or hanging the media", () => Assert(app.Line.ActiveTransferCount == 0 && !app.Line.IsVisible && app.Store.Collection.Contains(photo) && !app.Store.Items.Contains(photo), "Flight survived game mode"));
        app.SetGameDoNotDisturb(false); app.Store.Mount(photo); app.Line.SuspendForGameMode();
    }
    static void RenderGallery(App app)
    {
        var root = new Canvas { Width = 1060, Height = 270, Background = Theme.Brush("#242831") };
        root.Children.Add(new Rope { Width = 1060, Height = 75 });
        for (int i = 0; i < 5; i++) {
            var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) {
                dc.DrawRectangle(i == 0 ? Brushes.White : i == 1 ? Brushes.Black : Theme.Brush("#5B91AC"), null, new Rect(0, 0, 180, 120));
                if (i >= 2) for (int n = 0; n < 16; n++) dc.DrawLine(new Pen(n % 2 == 0 ? Brushes.White : Theme.Brush("#EF7960"), 6), new Point(n * 14, 0), new Point(n * 14 - 30, 120));
            }
            var bmp = new RenderTargetBitmap(180, 120, 96, 96, PixelFormats.Pbgra32); bmp.Render(visual);
            var card = new Card(new Photo { Path = "visual-" + i, Image = bmp }, app); card.FinishArrival(); Canvas.SetLeft(card, 15 + i * 174 - 50); Canvas.SetTop(card, 29); root.Children.Add(card);
        }
        var basket = new BasketVisual(); Canvas.SetLeft(basket, 970); Canvas.SetTop(basket, 20); root.Children.Add(basket);
        Save(root, "shelf-controls.png");
        var sheet = new Canvas { Width = 1060, Height = 850, Background = Theme.Brush("#EDF2F7") };
        for (int i = 0; i < 4; i++) {
            var panel = new Canvas { Width = 1060, Height = 200 }; var target = new Rect(976, 51, 34, 22);
            var flight = new MediaTransferVisual(app.Store.Collection[0].Image, new Rect(90, 90, 150, 100), target, true, 63) { Width = 1060, Height = 200, Progress = new[] { .1, .4, .7, .92 }[i] };
            panel.Children.Add(flight); var art = new BasketVisual(); Canvas.SetLeft(art, 950); Canvas.SetTop(art, 20); panel.Children.Add(art); Canvas.SetTop(panel, i * 210); sheet.Children.Add(panel);
        }
        Save(sheet, "basket-flight-frames.png");
    }
    static void Save(FrameworkElement root, string name)
    {
        root.Measure(new Size(root.Width, root.Height)); root.Arrange(new Rect(0, 0, root.Width, root.Height)); root.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)root.Width, (int)root.Height, 96, 96, PixelFormats.Pbgra32); bmp.Render(root); MediaTools.SavePng(bmp, Path.Combine(Settings.Root, name));
    }
}
