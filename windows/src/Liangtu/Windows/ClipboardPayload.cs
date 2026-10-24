namespace Liangtu;

internal sealed record ClipboardPayload(BitmapSource Image, byte[] Png)
{
    // Decode a single file snapshot off the UI thread. A PNG can be published
    // verbatim; decoding and re-encoding it on every click wastes most of the time.
    public static ClipboardPayload Load(string path)
    {
        byte[] bytes;
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
            bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes);
        }
        using var input = new MemoryStream(bytes, false);
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = input; image.EndInit();
        if ((long)image.PixelWidth * image.PixelHeight > 80_000_000) throw new IOException("图片尺寸超过限制");
        image.Freeze();
        if (bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return new(image, bytes);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = new MemoryStream(); encoder.Save(output);
        return new(image, output.ToArray());
    }
}
