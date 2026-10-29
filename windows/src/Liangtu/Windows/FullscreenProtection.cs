using System.Drawing;
namespace Liangtu;

internal sealed record ScreenWindow(Rectangle Bounds, bool Transient = false);
internal static class FullscreenPolicy
{
    public static bool Covers(Rectangle window, Rectangle screen) => window.Width > 0 && window.Height > 0 &&
        window.Left <= screen.Left + 4 && window.Top <= screen.Top + 4 && window.Right >= screen.Right - 4 && window.Bottom >= screen.Bottom - 4;
    // Windows are ordered front to back. Small overlays must not conceal the
    // fullscreen window from detection; a substantial normal window is Alt-Tab.
    public static bool Occupied(IReadOnlyList<ScreenWindow> windows, Rectangle screen)
    {
        foreach (var window in windows)
        {
            if (Covers(window.Bounds, screen)) return true;
            var intersection = Rectangle.Intersect(window.Bounds, screen);
            if (!window.Transient && (long)intersection.Width * intersection.Height >= (long)screen.Width * screen.Height / 4) return false;
        }
        return false;
    }
}
internal sealed class FullscreenProtection
{
    readonly Func<IReadOnlyList<ScreenWindow>> snapshot;
    readonly Func<double> now;
    readonly Dictionary<string, (Rectangle bounds, double until)> protectedUntil = new(StringComparer.OrdinalIgnoreCase);
    IReadOnlyList<ScreenWindow> windows = Array.Empty<ScreenWindow>();
    double sampled = double.NegativeInfinity;
    public FullscreenProtection() : this(FullscreenWindows.Read, () => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency) { }
    public FullscreenProtection(Func<IReadOnlyList<ScreenWindow>> snapshot, Func<double> now) { this.snapshot = snapshot; this.now = now; }
    public bool IsBlocked(string display, Rectangle bounds, bool fresh = false)
    {
        var time = now();
        if (fresh || time - sampled >= .2) { windows = snapshot(); sampled = time; }
        if (FullscreenPolicy.Occupied(windows, bounds)) { protectedUntil[display] = (bounds, time + 1.5); return true; }
        if (protectedUntil.TryGetValue(display, out var hold)) {
            if (hold.bounds == bounds && time < hold.until) return true;
            protectedUntil.Remove(display);
        }
        return false;
    }
}
