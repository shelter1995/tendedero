using System.Drawing;
namespace Liangtu.Core;

/// Physical desktop pixels only. Monitor DPI never changes capture/export size.
public static class ScreenPixels
{
    public static Rectangle Selection(Point first, Point last, Rectangle desktop)
    {
        int left = Math.Clamp(Math.Min(first.X, last.X), desktop.Left, desktop.Right);
        int top = Math.Clamp(Math.Min(first.Y, last.Y), desktop.Top, desktop.Bottom);
        int right = Math.Clamp(Math.Max(first.X, last.X), desktop.Left, desktop.Right);
        int bottom = Math.Clamp(Math.Max(first.Y, last.Y), desktop.Top, desktop.Bottom);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }
    public static Rectangle WindowBounds(Rectangle workArea, double widthDip, double heightDip, double scale)
    {
        if (scale <= 0 || !double.IsFinite(scale)) throw new ArgumentOutOfRangeException(nameof(scale));
        int margin = Math.Min(16, Math.Min(workArea.Width, workArea.Height) / 8);
        int width = Math.Clamp((int)Math.Round(widthDip * scale), 1, Math.Max(1, workArea.Width - margin * 2));
        int height = Math.Clamp((int)Math.Round(heightDip * scale), 1, Math.Max(1, workArea.Height - margin * 2));
        return new Rectangle(workArea.Left + (workArea.Width - width) / 2, workArea.Top + (workArea.Height - height) / 2, width, height);
    }
}
