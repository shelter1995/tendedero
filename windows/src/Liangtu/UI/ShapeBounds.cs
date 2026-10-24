namespace Liangtu;

internal static class ShapeBounds
{
    public static Point LineEnd(Point anchor, Point pointer, Size canvas, bool constrained)
    {
        if (!constrained) return new(Math.Clamp(pointer.X, 0, canvas.Width), Math.Clamp(pointer.Y, 0, canvas.Height));
        var delta = pointer - anchor;
        var sector = ((int)Math.Round(Math.Atan2(delta.Y, delta.X) / (Math.PI / 4), MidpointRounding.AwayFromZero) + 8) % 8;
        var direction = sector switch {
            0 => new Vector(1, 0), 1 => new Vector(1, 1), 2 => new Vector(0, 1), 3 => new Vector(-1, 1),
            4 => new Vector(-1, 0), 5 => new Vector(-1, -1), 6 => new Vector(0, -1), _ => new Vector(1, -1)
        };
        // Project onto the nearest direction, then shorten along that ray at
        // image edges. Clamping X/Y separately would break a diagonal's angle.
        var distance = Math.Max(0, Vector.Multiply(delta, direction) / direction.LengthSquared);
        if (direction.X != 0) distance = Math.Min(distance, direction.X > 0 ? canvas.Width - anchor.X : anchor.X);
        if (direction.Y != 0) distance = Math.Min(distance, direction.Y > 0 ? canvas.Height - anchor.Y : anchor.Y);
        return anchor + direction * distance;
    }
    public static Rect Calculate(Point anchor, Point pointer, Size canvas, bool square)
    {
        if (square)
        {
            double dx = pointer.X - anchor.X, dy = pointer.Y - anchor.Y;
            double xDirection = dx < 0 ? -1 : 1, yDirection = dy < 0 ? -1 : 1;
            double availableX = dx < 0 ? anchor.X : canvas.Width - anchor.X;
            double availableY = dy < 0 ? anchor.Y : canvas.Height - anchor.Y;
            double side = Math.Min(Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Min(availableX, availableY));
            return new Rect(anchor, anchor + new Vector(xDirection * side, yDirection * side));
        }
        var end = new Point(Math.Clamp(pointer.X, 0, canvas.Width), Math.Clamp(pointer.Y, 0, canvas.Height));
        return new Rect(anchor, end);
    }
}
