using System.Drawing;
using Liangtu.Core;

public static class DpiChecks
{
    public static void Run(Action<string, Action> test)
    {
        foreach (var size in new[] { new Size(3840, 2160), new Size(1920, 1080) })
        foreach (var scale in new[] { 1d, 1.25, 1.5, 2 })
        {
            var localSize = size;
            var localScale = scale;
            test($"DPI simulated {size.Width}x{size.Height} at {scale * 100}%", () =>
            {
                var display = Screen("session", new Rectangle(-3840, -2160, localSize.Width, localSize.Height), localScale);
                Near(localSize.Width / localScale, display.LocalBounds.Width);
                Near(localSize.Height / localScale, display.LocalBounds.Height);
                Eq(new DipPoint(0, 0), display.PhysicalToLocal(display.PhysicalBounds.Location));
                Eq(display.PhysicalBounds, display.LocalToPhysical(display.LocalBounds));
                var pixel = new Point(display.PhysicalBounds.Left + 333, display.PhysicalBounds.Top + 177);
                Eq(pixel, display.LocalToPhysical(display.PhysicalToLocal(pixel)));
            });
        }

        test("DPI negative origin is subtracted before scaling", () =>
        {
            var display = Screen("left", new Rectangle(-2560, -300, 2560, 1440), 1.25);
            Eq(new DipPoint(400, 200), display.PhysicalToLocal(new Point(-2060, -50)));
            Eq(new Point(-2060, -50), display.LocalToPhysical(new DipPoint(400, 200)));
            Eq(new DipRect(80, 80, 800, 400), display.PhysicalToLocal(new Rectangle(-2460, -200, 1000, 500)));
        });

        foreach (var bounds in new[]
        {
            new Rectangle(-2560, 0, 2560, 1440), new Rectangle(1920, -360, 2560, 1440),
            new Rectangle(-320, -1440, 2560, 1440), new Rectangle(500, 1080, 2560, 1440)
        })
        {
            var localBounds = bounds;
            test($"DPI adjacent screen at ({bounds.Left},{bounds.Top})", () =>
            {
                var primary = Screen("primary", new Rectangle(0, 0, 1920, 1080), 1, true);
                var adjacent = Screen("adjacent", localBounds, 1.5);
                var point = new Point(localBounds.Left + 150, localBounds.Top + 150);
                Eq(new DipPoint(100, 100), adjacent.PhysicalToLocal(point));
                Eq(adjacent, DisplayGeometry.ResolveDisplay(new[] { primary, adjacent }, null, point));
            });
        }

        test("DPI monitor seams are half open", () =>
        {
            var left = Screen("left", new Rectangle(-1920, 0, 1920, 1080), 1);
            var right = Screen("right", new Rectangle(0, 0, 2560, 1440), 2);
            Eq(false, left.ContainsTopEdge(new Point(0, 0)));
            Eq(true, right.ContainsTopEdge(new Point(0, 0)));
            Eq(right, DisplayGeometry.ResolveDisplay(new[] { left, right }, null, new Point(0, 0)));
            Eq(false, right.ContainsTopEdge(new Point(2560, 0)));
        });

        test("DPI top edge uses physical pixels on an offset monitor", () =>
        {
            var display = Screen("upper", new Rectangle(-100, -2160, 3840, 2160), 2);
            Eq(true, display.ContainsTopEdge(new Point(-100, -2160)));
            Eq(true, display.ContainsTopEdge(new Point(10, -2157)));
            Eq(false, display.ContainsTopEdge(new Point(10, -2156)));
            Eq(false, display.ContainsTopEdge(new Point(10, -2161)));
            Eq(false, display.ContainsTopEdge(new Point(-101, -2160)));
            Eq(false, display.ContainsTopEdge(new Point(10, -2160), 0));
        });

        test("DPI panel hit band uses local DIPs and stays on its monitor", () =>
        {
            var display = Screen("left", new Rectangle(-2560, -100, 2560, 1440), 1.25);
            Eq(true, display.ContainsTopBand(new Point(-2500, 174), 220));
            Eq(false, display.ContainsTopBand(new Point(-2500, 175), 220));
            Eq(false, display.ContainsTopBand(new Point(-2500, -101), 220));
            Eq(false, display.ContainsTopBand(new Point(0, 0), 220));
            Eq(false, display.ContainsTopBand(new Point(-2500, 1340), 10000));
        });

        test("DPI point rounding and rectangle outward rounding preserve coverage", () =>
        {
            var display = Screen("negative", new Rectangle(-2000, -1000, 2000, 1000), 1.25);
            Eq(new Point(-1987, -987), display.LocalToPhysical(new DipPoint(10.4, 10.4)));
            Eq(new Rectangle(-2000, -1000, 3, 3), display.LocalToPhysical(new DipRect(.2, .2, 2, 2)));
            Eq(new Rectangle(-2001, -1001, 2, 2), display.LocalToPhysical(new DipRect(-.2, -.2, .4, .4)));
        });

        test("DPI rectangle round trips do not grow by a pixel", () =>
        {
            foreach (var scale in new[] { 1d, 1.25, 1.5, 2 })
            {
                var display = Screen("roundtrip", new Rectangle(-3840, -2160, 3840, 2160), scale);
                for (var x = 0; x < 600; x += 7)
                {
                    var pixels = new Rectangle(-3840 + x, -2160 + x, 11, 19);
                    Eq(pixels, display.LocalToPhysical(display.PhysicalToLocal(pixels)));
                }
            }
        });

        test("DPI cross screen selection is split in physical pixels", () =>
        {
            var left = Screen("left", new Rectangle(-2560, 0, 2560, 1440), 1.25);
            var right = Screen("right", new Rectangle(0, 0, 3840, 2160), 2);
            var slices = DisplayGeometry.SplitSelection(new Rectangle(-100, 100, 300, 200), new[] { left, right });
            Eq(2, slices.Count);
            Eq(left, slices[0].Display);
            Eq(new Rectangle(-100, 100, 100, 200), slices[0].PhysicalBounds);
            Eq(new DipRect(1968, 80, 80, 160), slices[0].LocalBounds);
            Eq(new Rectangle(0, 100, 200, 200), slices[1].PhysicalBounds);
            Eq(new DipRect(0, 50, 100, 100), slices[1].LocalBounds);
            Eq(60000, slices.Sum(s => s.PhysicalBounds.Width * s.PhysicalBounds.Height));
        });

        test("DPI vertical cross screen selection uses each monitor origin", () =>
        {
            var upper = Screen("upper", new Rectangle(-500, -1440, 2560, 1440), 1.5);
            var lower = Screen("lower", new Rectangle(0, 0, 1920, 1080), 1);
            var slices = DisplayGeometry.SplitSelection(new Rectangle(100, -150, 300, 300), new[] { upper, lower });
            Eq(2, slices.Count);
            Eq(new DipRect(400, 860, 200, 100), slices[0].LocalBounds);
            Eq(new DipRect(100, 0, 300, 150), slices[1].LocalBounds);
        });

        test("DPI selection gaps and outside pixels do not become monitor content", () =>
        {
            var left = Screen("left", new Rectangle(-200, 0, 100, 100), 1);
            var right = Screen("right", new Rectangle(0, 0, 100, 100), 2);
            var slices = DisplayGeometry.SplitSelection(new Rectangle(-250, -50, 400, 200), new[] { left, right });
            Eq(2, slices.Count);
            Eq(20000, slices.Sum(s => s.PhysicalBounds.Width * s.PhysicalBounds.Height));
            Eq(0, DisplayGeometry.SplitSelection(new Rectangle(-100, 0, 100, 100), new[] { left, right }).Count);
            Eq(0, DisplayGeometry.SplitSelection(Rectangle.Empty, new[] { left, right }).Count);
        });

        test("DPI hot unplug falls back to monitor containing pointer", () =>
        {
            var primary = Screen("primary", new Rectangle(0, 0, 1920, 1080), 1, true);
            var right = Screen("right", new Rectangle(1920, 0, 2560, 1440), 1.5);
            Eq(right, DisplayGeometry.ResolveDisplay(new[] { primary, right }, "unplugged", new Point(2200, 200)));
        });

        test("DPI hot unplug chooses nearest screen for stale pointer", () =>
        {
            var primary = Screen("primary", new Rectangle(0, 0, 1920, 1080), 1, true);
            var right = Screen("right", new Rectangle(1920, 0, 2560, 1440), 1.5);
            Eq(right, DisplayGeometry.ResolveDisplay(new[] { primary, right }, "unplugged", new Point(6000, 200)));
        });

        test("DPI empty topology returns no display during reconnect", () =>
            Eq<DisplayGeometry?>(null, DisplayGeometry.ResolveDisplay(Array.Empty<DisplayGeometry>(), "old", new Point(10, 10))));

        test("DPI fallback without pointer uses primary then first", () =>
        {
            var first = Screen("first", new Rectangle(-1920, 0, 1920, 1080), 1);
            var primary = Screen("primary", new Rectangle(0, 0, 1920, 1080), 1.25, true);
            Eq(primary, DisplayGeometry.ResolveDisplay(new[] { first, primary }, "old"));
            Eq(first, DisplayGeometry.ResolveDisplay(new[] { first }, "old"));
        });

        test("DPI same device after remote reconnect uses new session bounds and scale", () =>
        {
            var physical = Screen("session", new Rectangle(0, 0, 3840, 2160), 1.5, true);
            var remote = Screen("SESSION", new Rectangle(0, 0, 1920, 1080), 1, true);
            var resolved = DisplayGeometry.ResolveDisplay(new[] { remote }, physical.DeviceName, new Point(3000, 1000));
            Eq(remote, resolved);
            Eq(new DipRect(0, 0, 1920, 1080), resolved!.LocalBounds);
            var resized = Screen("session", new Rectangle(0, 0, 2560, 1440), 1.25, true);
            Eq(resized, DisplayGeometry.ResolveDisplay(new[] { resized }, remote.DeviceName));
            Eq(new DipRect(0, 0, 2048, 1152), resized.LocalBounds);
        });

        test("DPI nearest distance ties prefer primary consistently", () =>
        {
            var left = Screen("left", new Rectangle(-200, 0, 100, 100), 1);
            var right = Screen("right", new Rectangle(0, 0, 100, 100), 1, true);
            Eq(right, DisplayGeometry.ResolveDisplay(new[] { left, right }, null, new Point(-50, 50)));
        });

        test("DPI invalid bounds and scale are rejected", () =>
        {
            Throws<ArgumentOutOfRangeException>(() => Screen("bad", Rectangle.Empty, 1));
            Throws<ArgumentOutOfRangeException>(() => Screen("bad", new Rectangle(int.MaxValue - 4, 0, 10, 100), 1));
            foreach (var scale in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
                Throws<ArgumentOutOfRangeException>(() => Screen("bad", new Rectangle(0, 0, 100, 100), scale));
            Throws<ArgumentException>(() => Screen("", new Rectangle(0, 0, 100, 100), 1));
        });

        test("DPI invalid or empty conversion inputs have explicit behavior", () =>
        {
            var display = Screen("valid", new Rectangle(0, 0, 1920, 1080), 1.25);
            Eq(Rectangle.Empty, display.LocalToPhysical(new DipRect(1, 2, 0, 10)));
            Throws<ArgumentOutOfRangeException>(() => display.LocalToPhysical(new DipPoint(double.NaN, 0)));
            Throws<ArgumentOutOfRangeException>(() => display.LocalToPhysical(new DipRect(0, 0, -1, 1)));
            Throws<ArgumentOutOfRangeException>(() => display.ContainsTopEdge(new Point(0, 0), -1));
            Throws<ArgumentOutOfRangeException>(() => display.ContainsTopBand(new Point(0, 0), double.PositiveInfinity));
            Throws<ArgumentOutOfRangeException>(() => DisplayGeometry.SplitSelection(new Rectangle(0, 0, -1, 1), new[] { display }));
        });
    }

    static DisplayGeometry Screen(string name, Rectangle bounds, double scale, bool primary = false) => new(name, bounds, scale, primary);
    static void Eq<T>(T expected, T actual)
    {
        if (!Equals(expected, actual)) throw new Exception($"expected {expected}, got {actual}");
    }
    static void Near(double expected, double actual)
    {
        if (!double.IsFinite(actual) || Math.Abs(expected - actual) > 0.000001) throw new Exception($"expected {expected}, got {actual}");
    }
    static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception($"expected {typeof(T).Name}");
    }
}
