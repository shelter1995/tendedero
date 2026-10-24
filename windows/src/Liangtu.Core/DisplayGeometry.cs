using System.Drawing;

namespace Liangtu.Core;

public readonly record struct DipPoint(double X, double Y);
public readonly record struct DipRect(double X, double Y, double Width, double Height);
public sealed record DisplaySelection(DisplayGeometry Display, Rectangle PhysicalBounds, DipRect LocalBounds);

/// <summary>
/// One display in the current desktop session. Physical bounds use global screen
/// pixels; DIP coordinates are local to this display's top-left corner. There is
/// no single global DIP space across displays with different scale factors.
/// </summary>
public sealed class DisplayGeometry
{
    public string DeviceName { get; }
    public Rectangle PhysicalBounds { get; }
    public double Scale { get; }
    public bool IsPrimary { get; }
    public DipRect LocalBounds => new(0, 0, PhysicalBounds.Width / Scale, PhysicalBounds.Height / Scale);

    public DisplayGeometry(string deviceName, Rectangle physicalBounds, double scale, bool isPrimary = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ValidateRectangle(physicalBounds, nameof(physicalBounds));
        if (physicalBounds.Width == 0 || physicalBounds.Height == 0)
            throw new ArgumentOutOfRangeException(nameof(physicalBounds));
        if (!double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale));
        DeviceName = deviceName;
        PhysicalBounds = physicalBounds;
        Scale = scale;
        IsPrimary = isPrimary;
    }

    public DipPoint PhysicalToLocal(Point point) => new(
        ((double)point.X - PhysicalBounds.Left) / Scale,
        ((double)point.Y - PhysicalBounds.Top) / Scale);

    public DipRect PhysicalToLocal(Rectangle rectangle)
    {
        ValidateRectangle(rectangle, nameof(rectangle));
        var origin = PhysicalToLocal(rectangle.Location);
        return new(origin.X, origin.Y, rectangle.Width / Scale, rectangle.Height / Scale);
    }

    /// <summary>Converts a local point to its nearest physical pixel.</summary>
    public Point LocalToPhysical(DipPoint point)
    {
        RequireFinite(point.X, nameof(point));
        RequireFinite(point.Y, nameof(point));
        return new(
            Pixel(Math.Round(PhysicalBounds.Left + point.X * Scale, MidpointRounding.AwayFromZero)),
            Pixel(Math.Round(PhysicalBounds.Top + point.Y * Scale, MidpointRounding.AwayFromZero)));
    }

    /// <summary>
    /// Rounds outward to cover all selected pixels. Coordinates may extend outside
    /// this display; use SplitSelection to clip against the current topology.
    /// Zero-area rectangles return Rectangle.Empty.
    /// </summary>
    public Rectangle LocalToPhysical(DipRect rectangle)
    {
        RequireFinite(rectangle.X, nameof(rectangle));
        RequireFinite(rectangle.Y, nameof(rectangle));
        RequireFinite(rectangle.Width, nameof(rectangle));
        RequireFinite(rectangle.Height, nameof(rectangle));
        if (rectangle.Width < 0 || rectangle.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(rectangle));
        if (rectangle.Width == 0 || rectangle.Height == 0)
            return Rectangle.Empty;
        var left = Pixel(Math.Floor(SnapToPixel(PhysicalBounds.Left + rectangle.X * Scale)));
        var top = Pixel(Math.Floor(SnapToPixel(PhysicalBounds.Top + rectangle.Y * Scale)));
        var right = Pixel(Math.Ceiling(SnapToPixel(PhysicalBounds.Left + (rectangle.X + rectangle.Width) * Scale)));
        var bottom = Pixel(Math.Ceiling(SnapToPixel(PhysicalBounds.Top + (rectangle.Y + rectangle.Height) * Scale)));
        return new(left, top, checked(right - left), checked(bottom - top));
    }

    /// <summary>The hover target keeps a physical-pixel thickness at every DPI.</summary>
    public bool ContainsTopEdge(Point point, int thicknessPixels = 4)
    {
        if (thicknessPixels < 0)
            throw new ArgumentOutOfRangeException(nameof(thicknessPixels));
        return PhysicalBounds.Contains(point) && (long)point.Y - PhysicalBounds.Top < thicknessPixels;
    }

    /// <summary>The visible panel's hit band has a local-DIP height.</summary>
    public bool ContainsTopBand(Point point, double heightDip)
    {
        RequireFinite(heightDip, nameof(heightDip));
        if (heightDip < 0)
            throw new ArgumentOutOfRangeException(nameof(heightDip));
        return PhysicalBounds.Contains(point) && PhysicalToLocal(point).Y < heightDip;
    }

    /// <summary>
    /// Clips a physical-pixel selection separately to each display, preserving
    /// its local scale. Gaps in the virtual desktop produce no slice.
    /// </summary>
    public static IReadOnlyList<DisplaySelection> SplitSelection(Rectangle selection, IReadOnlyList<DisplayGeometry> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        ValidateRectangle(selection, nameof(selection));
        var result = new List<DisplaySelection>();
        foreach (var display in displays)
        {
            var clipped = Rectangle.Intersect(selection, display.PhysicalBounds);
            if (clipped.Width > 0 && clipped.Height > 0)
                result.Add(new(display, clipped, display.PhysicalToLocal(clipped)));
        }
        return result;
    }

    /// <summary>
    /// Returns an instance from the latest topology: preferred device, pointer's
    /// display, nearest display, then primary/first. An empty reconnect snapshot
    /// returns null. A remembered device name never preserves stale bounds or DPI.
    /// Omit preferredDeviceName when selecting a display for a new pointer action.
    /// </summary>
    public static DisplayGeometry? ResolveDisplay(IReadOnlyList<DisplayGeometry> displays, string? preferredDeviceName = null, Point? pointer = null)
    {
        ArgumentNullException.ThrowIfNull(displays);
        if (displays.Count == 0)
            return null;
        if (preferredDeviceName is not null)
        {
            var preferred = displays.FirstOrDefault(d => string.Equals(d.DeviceName, preferredDeviceName, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
                return preferred;
        }
        if (pointer is Point position)
        {
            var containing = displays.FirstOrDefault(d => d.PhysicalBounds.Contains(position));
            if (containing is not null)
                return containing;
            return displays.OrderBy(d => DistanceSquared(d.PhysicalBounds, position))
                .ThenByDescending(d => d.IsPrimary).First();
        }
        return displays.FirstOrDefault(d => d.IsPrimary) ?? displays[0];
    }

    static double DistanceSquared(Rectangle bounds, Point point)
    {
        var dx = Math.Max(Math.Max((double)bounds.Left - point.X, point.X - (double)bounds.Right), 0);
        var dy = Math.Max(Math.Max((double)bounds.Top - point.Y, point.Y - (double)bounds.Bottom), 0);
        return dx * dx + dy * dy;
    }

    static double SnapToPixel(double value)
    {
        var nearest = Math.Round(value);
        // Cancel only floating-point round-trip noise near an exact pixel edge.
        return Math.Abs(value - nearest) < 0.0000001 ? nearest : value;
    }

    static int Pixel(double value)
    {
        if (!double.IsFinite(value) || value < int.MinValue || value > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value));
        return (int)value;
    }

    static void RequireFinite(double value, string parameter)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameter);
    }

    static void ValidateRectangle(Rectangle rectangle, string parameter)
    {
        if (rectangle.Width < 0 || rectangle.Height < 0 ||
            (long)rectangle.Left + rectangle.Width > int.MaxValue ||
            (long)rectangle.Top + rectangle.Height > int.MaxValue)
            throw new ArgumentOutOfRangeException(parameter);
    }
}
