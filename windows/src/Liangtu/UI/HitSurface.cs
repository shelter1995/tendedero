namespace Liangtu;

internal static class HitSurface
{
    public static bool Contains(FrameworkElement target, Visual ancestor, Point point, double radius)
    {
        try {
            var inverse = target.TransformToAncestor(ancestor).Inverse;
            if (inverse == null || !inverse.TryTransform(point, out var local)) return false;
            var rect = new Rect(0, 0, target.ActualWidth, target.ActualHeight);
            return new RectangleGeometry(rect, radius, radius).FillContains(local);
        } catch (InvalidOperationException) { return false; }
    }
}
