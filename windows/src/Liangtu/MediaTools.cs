using System.Diagnostics;
namespace Liangtu;
public static class MediaTools
{
    public static string? Ffmpeg
    {
        get
        {
            var local = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe");
            if (File.Exists(local))
                return local;
            return (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(p => Path.Combine(p, "ffmpeg.exe")).FirstOrDefault(File.Exists);
        }
    }
    public static bool IsVideo(string path) => new[] { ".mp4", ".mov", ".m4v", ".avi", ".wmv" }.Contains(Path.GetExtension(path).ToLowerInvariant());
    public static bool NeedsSeparateEdit(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);
        return decoder.Frames.Count > 1 || Path.GetExtension(path).Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }
    public static BitmapSource LoadImage(string path, int decode = 0)
    {
        using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var b = new BitmapImage();
        b.BeginInit();
        b.CacheOption = BitmapCacheOption.OnLoad;
        if (decode > 0)
            b.DecodePixelWidth = decode;
        b.StreamSource = s;
        b.EndInit();
        if ((long)b.PixelWidth * b.PixelHeight > 80_000_000)
            throw new IOException("图片尺寸超过限制");
        b.Freeze();
        return b;
    }
    public static void SavePng(BitmapSource image, string path)
    {
        BitmapEncoder encoder = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
            ".bmp" => new BmpBitmapEncoder(),
            ".tif" or ".tiff" => new TiffBitmapEncoder(),
            ".gif" => new GifBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
        encoder.Frames.Add(BitmapFrame.Create(image));
        var tmp = Path.Combine(Path.GetDirectoryName(path)!, "." + Guid.NewGuid() + ".tmp");
        try
        {
            using (var s = File.Create(tmp))
                encoder.Save(s);
            File.Move(tmp, path, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
    public static async Task<BitmapSource> Thumbnail(string path)
    {
        if (!IsVideo(path))
            return await Task.Run(() => LoadImage(path, 600));
        if (Ffmpeg == null)
            return VideoPlaceholder();
        var psi = new ProcessStartInfo(Ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "-v", "error", "-protocol_whitelist", "file,pipe", "-i", path, "-frames:v", "1", "-vf", "scale=600:-1", "-f", "image2pipe", "-vcodec", "png", "pipe:1" })
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var stderr = p.StandardError.ReadToEndAsync();
        using var mem = new MemoryStream();
        var output = p.StandardOutput.BaseStream.CopyToAsync(mem);
        using var limit = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await p.WaitForExitAsync(limit.Token);
            await output;
            await stderr;
            if (p.ExitCode != 0 || mem.Length == 0)
                return VideoPlaceholder();
            mem.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = mem;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { try { p.Kill(true); } catch { } return VideoPlaceholder(); }
    }
    public static BitmapSource VideoPlaceholder()
    {
        var dv = new DrawingVisual();
        using (var c = dv.RenderOpen())
        {
            c.DrawRectangle(new SolidColorBrush(Color.FromRgb(37, 42, 52)), null, new Rect(0, 0, 480, 270));
        }
        var b = new RenderTargetBitmap(480, 270, 96, 96, PixelFormats.Pbgra32);
        b.Render(dv);
        b.Freeze();
        return b;
    }
    public static async Task Trim(string path, double start, double end, System.Threading.CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        if (Ffmpeg == null)
            throw new InvalidOperationException("未找到 ffmpeg.exe。请将其放入程序 tools 文件夹。");
        var temp = Path.Combine(Path.GetDirectoryName(path)!, ".trimming-" + Guid.NewGuid() + Path.GetExtension(path));
        var psi = new ProcessStartInfo(Ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var a in new[] { "-v", "error", "-nostdin", "-y", "-protocol_whitelist", "file,pipe", "-ss", start.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), "-i", path, "-t", (end - start).ToString("F3", System.Globalization.CultureInfo.InvariantCulture), "-map", "0:v:0", "-map", "0:a?", "-c:v", "libx264", "-preset", "fast", "-crf", "18", "-c:a", "aac", "-movflags", "+faststart", temp })
            psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            var error = p.StandardError.ReadToEndAsync();
            try { await p.WaitForExitAsync(cancel); }
            catch { try { if (!p.HasExited) p.Kill(true); } catch { } await p.WaitForExitAsync(); await error; throw; }
            if (p.ExitCode != 0 || !File.Exists(temp) || new FileInfo(temp).Length == 0)
                throw new IOException(await error);
            cancel.ThrowIfCancellationRequested(); File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

