namespace Liangtu;

/// The material is supplied by a compositor window behind the transparent overlay.
/// These strokes are the exact layers used by upstream's glassFrame.
public sealed class GlassBorder : Border
{
    internal int ArtVariant { get; set; }
    static readonly List<WeakReference<GlassBorder>> instances = new();
    public GlassBorder()
    {
        instances.Add(new(this));
        Background = Brushes.Transparent;
    }
    public static IEnumerable<GlassBorder> In(Window window)
    {
        if (Theme.Current.Material != "canvas" || SystemParameters.HighContrast) yield break;
        for (var i = instances.Count - 1; i >= 0; i--)
        {
            if (!instances[i].TryGetTarget(out var glass))
            {
                instances.RemoveAt(i);
                continue;
            }
            if (glass.IsVisible && Window.GetWindow(glass) == window && glass.ActualWidth > 0 && glass.ActualHeight > 0)
            {
                double opacity = 1;
                bool clipped = false;
                for (DependencyObject? p = glass; p != null && p != window; p = VisualTreeHelper.GetParent(p))
                {
                    if (p is UIElement element) opacity *= element.Opacity;
                    if (p is Card card && !card.AllowsLiveGlassFor(glass)) clipped = true;
                }
                if (opacity > .02 && !clipped)
                    yield return glass;
            }
        }
    }
    protected override void OnRender(DrawingContext dc)
    {
        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        var radius = Math.Min(CornerRadius.TopLeft, Math.Min(ActualWidth, ActualHeight) / 2);
        DrawFrame(dc, Theme.Current, rect, radius, ArtVariant);
    }
    internal static void DrawFrame(DrawingContext dc, ThemeProfile profile, Rect rect, double radius, int variant)
    {
        if (profile.OriginalArtwork && !SystemParameters.HighContrast) { ThemeArtwork.Frame(dc, profile, rect, radius, variant); return; }
        dc.DrawRoundedRectangle(Theme.Token("FrameFill"), null, rect, radius, radius);
        dc.DrawRoundedRectangle(null, new Pen(Theme.Token("FrameEdge"), .9), rect, radius, radius);
        rect.Inflate(.5, .5);
        dc.DrawRoundedRectangle(null, new Pen(Theme.Brush("#1A000000"), .5), rect, radius + .5, radius + .5);
    }
    public Point[] ScreenOutline()
    {
        var w = ActualWidth;
        var h = ActualHeight;
        var r = Math.Min(CornerRadius.TopLeft, Math.Min(w, h) / 2);
        var points = new List<Point>();
        foreach (var corner in new[] { (w - r, r, -90d), (w - r, h - r, 0d), (r, h - r, 90d), (r, r, 180d) })
            for (int i = 0; i <= 9; i++)
            {
                var angle = (corner.Item3 + i * 10) * Math.PI / 180;
                points.Add(PointToScreen(new Point(corner.Item1 + r * Math.Cos(angle), corner.Item2 + r * Math.Sin(angle))));
            }
        return points.ToArray();
    }
}
