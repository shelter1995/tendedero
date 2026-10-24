using System.Globalization;
namespace Liangtu;
public static class SelfTest
{
    public static async Task Run(App app, bool exit)
    {
        var lines = new List<string>();
        void Assert(bool ok, string name)
        {
            lines.Add((ok ? "PASS " : "FAIL ") + name);
            if (!ok)
                throw new Exception(name);
        }
        try
        {
            if (exit)
                await RegressionChecks.Run(app, lines);
            var folder = System.IO.Path.Combine(Settings.Root, "fixtures");
            Directory.CreateDirectory(folder);
            for (int i = 0; i < 3; i++)
            {
                var path = System.IO.Path.Combine(folder, $"sample-{i}.png");
                MediaTools.SavePng(Sample(i), path);
                await app.Store.Add(path);
            }
            var same = app.Store.Items[0].Path;
            await app.Store.Add(same);
            Assert(app.Store.Items.Count == 3, "duplicate files hang once");
            var external = app.Store.Items[0];
            app.Store.Discard(external);
            Assert(File.Exists(same), "taking down external image preserves file");
            await app.Store.Add(same);
            var broken = System.IO.Path.Combine(folder, "corrupt.png");
            File.WriteAllText(broken, "invalid");
            await app.Store.Add(broken);
            Assert(app.Store.Items.Count == 3, "corrupt image does not hang");
            app.Store.Persist();
            var settings = Settings.Load();
            Assert(settings.Items.Count == 3, "session saved");
            var rewritten = System.IO.Path.Combine(folder, "atomic.png");
            MediaTools.SavePng(Sample(0), rewritten);
            MediaTools.SavePng(Sample(1), rewritten);
            Assert(MediaTools.LoadImage(rewritten).PixelWidth == 640, "atomic image overwrite decodes");
            var originalImage = app.Store.Items[0].Image;
            Assert(originalImage.IsFrozen, "background-decoded image is safe for UI");
            app.Line.Refresh();
            app.Line.Reveal(true);
            await Task.Delay(1000);
            app.Line.UpdateLayout();
            var image = new RenderTargetBitmap((int)app.Line.Width, 300, 96, 96, PixelFormats.Pbgra32);
            image.Render(app.Line);
            MediaTools.SavePng(image, System.IO.Path.Combine(Settings.Root, "line-preview.png"));
            Assert(app.Line.IsVisible && app.Line.Revealed, "overlay revealed");
            Assert(app.Line.ShowActivated == false && app.Line.Topmost, "overlay does not activate and stays on top");
            lines.Add("PASS visual render saved");
            File.WriteAllLines(System.IO.Path.Combine(Settings.Root, "results.txt"), lines);
            if (exit)
            {
                var menu = app.CreateTrayMenu();
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
                menu.HorizontalOffset = 80;
                menu.VerticalOffset = 80;
                menu.IsOpen = true;
                await Task.Delay(200);
                menu.UpdateLayout();
                var menuImage = new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth), (int)Math.Ceiling(menu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                menuImage.Render(menu);
                MediaTools.SavePng(menuImage, Path.Combine(Settings.Root, "menu-preview.png"));
                Assert(menu.ActualWidth > 200 && menu.Items.Count >= 15, "complete tray menu renders");
                menu.IsOpen = false;
                var capture = new CaptureWindow((_, _) => { }, Sample(0), new System.Drawing.Rectangle(100, 100, 640, 400));
                capture.Show();
                await Task.Delay(150);
                Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(capture).Handle, out var captureBounds);
                Assert(captureBounds.Right - captureBounds.Left == 640 && captureBounds.Bottom - captureBounds.Top == 400, "capture selector uses exact physical bounds");
                capture.Close();
            }
            if (!exit)
            {
                app.Line.Busy = true;
                new EditorWindow(app, app.Store.Items[0]).Show();
            }
            if (exit)
            {
                app.Line.Retract();
                await Task.Delay(350);
                Assert(!app.Line.IsVisible, "hidden overlay removed");
                File.WriteAllLines(System.IO.Path.Combine(Settings.Root, "results.txt"), lines);
                app.Shutdown(lines.Any(l => l.StartsWith("FAIL ")) ? 1 : 0);
            }
        }
        catch (Exception e) { lines.Add("FAIL " + e); File.WriteAllLines(System.IO.Path.Combine(Settings.Root, "results.txt"), lines); if (exit) app.Shutdown(1); }
    }
    static BitmapSource Sample(int index)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var bg = new[] { Color.FromRgb(246, 242, 234), Color.FromRgb(29, 46, 56), Color.FromRgb(225, 234, 219) }[index];
            dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, 640, 400));
            var ink = index == 1 ? Colors.White : Color.FromRgb(32, 40, 37);
            void Text(string t, double size, double x, double y)
            {
                dc.DrawText(new FormattedText(t, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, new SolidColorBrush(ink), 1), new Point(x, y));
            }
            Text(new[] { "A small collection", "Ideas, in motion", "Keep what matters" }[index], 34, 36, 38);
            Text(new[] { "Notes from the everyday.", "Make room for a little curiosity.", "Less clutter. More clarity." }[index], 17, 36, 94);
            if (index == 0)
            {
                for (int i = 0; i < 4; i++)
                    dc.DrawRoundedRectangle(new SolidColorBrush(new[] { Colors.LightSalmon, Colors.Wheat, Colors.DarkSeaGreen, Colors.LightSlateGray }[i]), null, new Rect(36 + i * 146, 150, 126, 205), 12, 12);
            }
            else if (index == 1)
            {
                for (int i = 0; i < 7; i++)
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb((byte)(130 + i * 12), 190, 172)), null, new Rect(36 + i * 82, 330 - i * 23, 55, 25 + i * 23), 6, 6);
            }
            else
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(185, 201, 165)), null, new Point(450, 240), 130, 130);
                Text("01 / Inspiration", 24, 36, 185);
                Text("02 / A quieter desktop", 24, 36, 238);
                Text("03 / Within reach", 24, 36, 291);
            }
        }
        var b = new RenderTargetBitmap(640, 400, 96, 96, PixelFormats.Pbgra32);
        b.Render(dv);
        b.Freeze();
        return b;
    }
}
