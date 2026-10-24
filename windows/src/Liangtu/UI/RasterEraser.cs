namespace Liangtu;
// Restore pixels from the unmarked base image, including their original alpha.
public sealed class RasterEraser
{
    readonly byte[] pixels, baseline; readonly int stride, width, height;
    public WriteableBitmap Image
    {
        get;
    }
    public RasterEraser(BitmapSource current, BitmapSource clean)
    {
        width = current.PixelWidth;
        height = current.PixelHeight;
        stride = width * 4;
        if (clean.PixelWidth != width || clean.PixelHeight != height)
            throw new ArgumentException("Base image size mismatch");
        pixels = new byte[stride * height];
        baseline = new byte[pixels.Length];
        new FormatConvertedBitmap(current, PixelFormats.Pbgra32, null, 0).CopyPixels(pixels, stride, 0);
        new FormatConvertedBitmap(clean, PixelFormats.Pbgra32, null, 0).CopyPixels(baseline, stride, 0);
        Image = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
        Image.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
    }
    public void Erase(Point from, Point to, double radius)
    {
        var steps = Math.Max(1, (int)Math.Ceiling((to - from).Length / Math.Max(1, radius / 2)));
        for (var n = 0; n <= steps; n++)
        {
            var p = from + (to - from) * (n / (double)steps);
            var x0 = Math.Max(0, (int)(p.X - radius));
            var y0 = Math.Max(0, (int)(p.Y - radius));
            var x1 = Math.Min(width - 1, (int)(p.X + radius));
            var y1 = Math.Min(height - 1, (int)(p.Y + radius));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if ((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y) <= radius * radius)
                        Buffer.BlockCopy(baseline, y * stride + x * 4, pixels, y * stride + x * 4, 4);
        }
        Image.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
    }
}
