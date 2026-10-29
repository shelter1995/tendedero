namespace Liangtu.Core;

public readonly record struct ShelfLayout(double Left, double Right, double Scale, double Spacing, int Visible, int Count)
{
    public const double MinimumScale = .55;
    public double MaxScroll => Math.Max(0, Count - Visible) * Spacing;
    public static int Capacity(double width, double basketWidth = 80) => Math.Max(1, (int)(Math.Max(80, width - 120 - Math.Max(80,basketWidth)) / (150 * MinimumScale + 18)));
    public static ShelfLayout Create(double width, int count, double preferred, double basketWidth = 80)
    {
        var left = 52d; var right = Math.Max(left + 80, width - 68 - Math.Max(80,basketWidth)); var area = right - left;
        var scale = Math.Clamp((area / Math.Max(1, count) - 18) / 150, MinimumScale, Math.Max(MinimumScale, preferred));
        var spacing = 150 * scale + 18;
        var visible = Math.Max(1, Math.Min(Math.Max(1, count), (int)(area / spacing)));
        return new(left, right, scale, spacing, visible, Math.Max(0, count));
    }
    public double Center(int index, double scroll)
    {
        var center = (Left + Right) / 2;
        return Count <= Visible ? center + (index - (Count - 1) / 2d) * Spacing
            : center + (Visible - 1) * Spacing / 2 - (Count - 1 - index) * Spacing + Math.Clamp(scroll, 0, MaxScroll);
    }
}
