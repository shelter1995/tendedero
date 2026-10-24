namespace Liangtu;

internal static class ClipboardChecks
{
    public static void Run(Action<string, Action> test)
    {
        void Assert(bool value) { if (!value) throw new InvalidOperationException("Clipboard regression"); }
        var opaque = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgr32, null, new byte[] { 30, 80, 210, 0, 220, 40, 10, 0 }, 8);
        var legacy = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 30, 80, 210, 0, 220, 40, 10, 0 }, 8);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(opaque));
        using var stream = new MemoryStream(); png.Save(stream);
        test("clipboard prefers PNG over ambiguous bitmap alpha", () => {
            var data = new DataObject(); data.SetData("PNG", new MemoryStream(stream.ToArray())); data.SetImage(legacy);
            var result = ClipboardImage.Read(data)!;
            var pixels = new byte[8]; new FormatConvertedBitmap(result, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, 8, 0);
            Assert(pixels[2] == 210 && pixels[3] == 255 && pixels[4] == 220);
        });
        test("clipboard reads PNG-only image", () => {
            var data = new DataObject(); data.SetData("PNG", new MemoryStream(stream.ToArray()));
            Assert(ClipboardImage.Read(data)?.PixelWidth == 2);
        });
        test("system capture accepted independently of general clipboard option", () => {
            Assert(ClipboardImage.ShouldCapture(new Settings { HandleScreenshots = true }, "SnippingTool"));
            Assert(ClipboardImage.ShouldCapture(new Settings { HandleScreenshots = true }, "ScreenClippingHost"));
            Assert(!ClipboardImage.ShouldCapture(new Settings { HandleScreenshots = true }, "other-app"));
            Assert(!ClipboardImage.ShouldCapture(new Settings(), "SnippingTool"));
        });
        test("legacy bitmap reserved alpha does not make screenshot invisible", () => {
            var data = new DataObject(); data.SetImage(legacy);
            var pixels = new byte[8]; new FormatConvertedBitmap(ClipboardImage.Read(data), PixelFormats.Bgra32, null, 0).CopyPixels(pixels, 8, 0);
            Assert(pixels[2] == 210 && pixels[3] == 255);
        });
        test("PNG preserves intentional transparency", () => {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(legacy));
            using var bytes = new MemoryStream(); encoder.Save(bytes);
            var data = new DataObject(); data.SetData("PNG", bytes);
            var pixels = new byte[8]; new FormatConvertedBitmap(ClipboardImage.Read(data), PixelFormats.Bgra32, null, 0).CopyPixels(pixels, 8, 0);
            Assert(pixels[3] == 0);
        });
        test("DIB handles bottom-up and top-down RGB with zero spare bytes", () => {
            foreach (int height in new[] { 2, -2 }) {
                var dib = new byte[48]; BitConverter.GetBytes(40).CopyTo(dib, 0); BitConverter.GetBytes(1).CopyTo(dib, 4); BitConverter.GetBytes(height).CopyTo(dib, 8);
                dib[12] = 1; dib[14] = 32;
                dib[40] = 200; dib[46] = 240;
                var result = ClipboardImage.DecodeDib(dib); var pixels = new byte[8];
                new FormatConvertedBitmap(result, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, 4, 0);
                Assert(pixels[3] == 255 && pixels[7] == 255 && (height > 0 ? pixels[2] == 240 : pixels[0] == 200));
            }
        });
        test("DIB rejects truncated and oversized payloads", () => {
            foreach (var width in new[] { 1, int.MaxValue }) {
                var dib = new byte[40]; BitConverter.GetBytes(40).CopyTo(dib, 0); BitConverter.GetBytes(width).CopyTo(dib, 4); BitConverter.GetBytes(2).CopyTo(dib, 8); dib[14] = 32;
                bool failed = false; try { ClipboardImage.DecodeDib(dib); } catch (IOException) { failed = true; } Assert(failed);
            }
        });
        test("privacy markers excluded but history flag is not a monitor exclusion", () => {
            var data = new DataObject(); data.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4]));
            Assert(!ClipboardImage.Excluded(data));
            data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[4])); Assert(ClipboardImage.Excluded(data));
            var own = new DataObject(); own.SetData("Liangtu.Internal", true); Assert(ClipboardImage.Excluded(own));
        });
        test("both shortcuts persist independently including disabled capture", () => {
            var settings = new Settings { HotkeyKey = 0x70, HotkeyModifiers = 2, CaptureHotkeyKey = 0, CaptureHotkeyModifiers = 0 };
            settings.Save(); var loaded = Settings.Load();
            Assert(loaded.HotkeyKey == 0x70 && loaded.HotkeyModifiers == 2 && loaded.CaptureHotkeyKey == 0);
        });
    }
}
