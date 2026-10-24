namespace Liangtu;

internal static class ClipboardImage
{
    public static bool ShouldCapture(Settings settings, string owner) => settings.ClipboardImages ||
        settings.HandleScreenshots && (owner.Equals("SnippingTool", StringComparison.OrdinalIgnoreCase) || owner.Equals("ScreenClippingHost", StringComparison.OrdinalIgnoreCase));

    public static bool Excluded(IDataObject data) => new[] { "UnicodeText", "Text", "FileDrop", "ExcludeClipboardContentFromMonitorProcessing", "org.nspasteboard.ConcealedType", "org.nspasteboard.TransientType", "Liangtu.Internal" }.Any(f => data.GetDataPresent(f, false));

    public static BitmapSource? Read(IDataObject data)
    {
        foreach (var format in new[] { "PNG", "image/png" })
        {
            if (!data.GetDataPresent(format, false)) continue;
            try
            {
                var value = data.GetData(format, false);
                var bytes = value switch { MemoryStream m => m.ToArray(), byte[] b => b, _ => null };
                if (bytes == null || bytes.Length > 64 * 1024 * 1024) continue;
                using var stream = new MemoryStream(bytes);
                var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                if ((long)frame.PixelWidth * frame.PixelHeight > 80_000_000) return null;
                frame.Freeze();
                return frame;
            }
            catch (Exception e) when (e is IOException or FileFormatException or NotSupportedException or ArgumentException) { Settings.Log("Clipboard PNG " + e.Message); }
        }
        if (data.GetDataPresent(DataFormats.Dib, false) && data.GetData(DataFormats.Dib, false) is MemoryStream dib)
        {
            try { return DecodeDib(dib.ToArray()); }
            catch (Exception e) when (e is IOException or ArgumentException or OverflowException or NotSupportedException) { Settings.Log("Clipboard DIB " + e.Message); }
        }
        // CF_BITMAP carries RGB data; its spare byte is not a PNG alpha channel.
        // Do not apply this conversion to PNG, where transparency is intentional.
        if (data.GetData(DataFormats.Bitmap) is not BitmapSource bitmap) return null;
        if ((long)bitmap.PixelWidth * bitmap.PixelHeight > 80_000_000) return null;
        var result = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr32, null, 0);
        result.Freeze();
        return result;
    }

    public static BitmapSource DecodeDib(byte[] bytes)
    {
        if (bytes.Length < 40) throw new IOException("Truncated DIB");
        int header = BitConverter.ToInt32(bytes, 0), width = BitConverter.ToInt32(bytes, 4), height = BitConverter.ToInt32(bytes, 8);
        int bits = BitConverter.ToUInt16(bytes, 14), compression = BitConverter.ToInt32(bytes, 16);
        if (header < 40 || header > bytes.Length || width <= 0 || height == 0 || height == int.MinValue || (long)width * Math.Abs(height) > 80_000_000) throw new IOException("Invalid DIB dimensions");
        if (compression == 0 && bits is 24 or 32)
        {
            int stride = checked((int)(((long)width * bits + 31) / 32 * 4));
            int rows = Math.Abs(height), size = checked(stride * rows);
            int offset = checked(header + BitConverter.ToInt32(bytes, 32) * 4);
            if (offset < header || (long)offset + size > bytes.Length) throw new IOException("Truncated DIB pixels");
            var pixels = new byte[size];
            for (int y = 0; y < rows; y++) Buffer.BlockCopy(bytes, offset + (height > 0 ? rows - 1 - y : y) * stride, pixels, y * stride, stride);
            var bitmap = BitmapSource.Create(width, rows, 96, 96, bits == 32 ? PixelFormats.Bgr32 : PixelFormats.Bgr24, null, pixels, stride);
            bitmap.Freeze(); return bitmap;
        }
        throw new NotSupportedException("DIB format falls back to CF_BITMAP");
    }
    public static string OwnerProcess()
    {
        var owner = GetClipboardOwner();
        if (owner == IntPtr.Zero) return "";
        GetWindowThreadProcessId(owner, out var pid);
        try { using var process = System.Diagnostics.Process.GetProcessById((int)pid); return process.ProcessName; }
        catch { return ""; }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetClipboardOwner();
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
}
