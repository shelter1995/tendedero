namespace Liangtu;

internal sealed class ImageViewState
{
    public double Zoom { get; private set; } = 1;
    public Point Offset { get; private set; }
    public Size ImageSize { get; set; }
    public Size ViewSize { get; set; }
    public bool Fitted { get; private set; } = true;
    public Point ToImage(Point view) => new((view.X - Offset.X) / Zoom, (view.Y - Offset.Y) / Zoom);
    public Point ToView(Point image) => new(image.X * Zoom + Offset.X, image.Y * Zoom + Offset.Y);
    public void Fit()
    {
        if (ImageSize.Width <= 0 || ImageSize.Height <= 0 || ViewSize.Width <= 0 || ViewSize.Height <= 0) return;
        Zoom = Math.Clamp(Math.Min(1, Math.Min(Math.Max(1, ViewSize.Width - 64) / ImageSize.Width, Math.Max(1, ViewSize.Height - 64) / ImageSize.Height)), .01, 16);
        Offset = new((ViewSize.Width - ImageSize.Width * Zoom) / 2, (ViewSize.Height - ImageSize.Height * Zoom) / 2);
        Fitted = true;
    }
    public void ZoomAt(double zoom, Point anchor)
    {
        var pixel = ToImage(anchor);
        Zoom = Math.Clamp(zoom, .01, 16);
        Offset = new(anchor.X - pixel.X * Zoom, anchor.Y - pixel.Y * Zoom);
        Fitted = false;
    }
    public void Pan(Vector delta) { Offset += delta; Fitted = false; }
}
