using System.Diagnostics;
using System.Threading;
namespace Liangtu;

internal static class VideoFrames
{
    public static async Task Load(string path, double duration, Action<IReadOnlyList<BitmapSource>> update, CancellationToken cancel)
    {
        var executable = MediaTools.Ffmpeg;
        if (executable == null || duration <= 0) return;
        var frames = new List<BitmapSource>();
        for (var i = 0; i < 8; i++)
        {
            cancel.ThrowIfCancellationRequested();
            var psi = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-v", "error", "-nostdin", "-protocol_whitelist", "file,pipe", "-ss", (duration * i / 8).ToString("F3", System.Globalization.CultureInfo.InvariantCulture), "-i", path, "-frames:v", "1", "-vf", "scale=192:-1", "-f", "image2pipe", "-vcodec", "png", "pipe:1" }) psi.ArgumentList.Add(arg);
            using var process = Process.Start(psi)!;
            using var stream = new MemoryStream();
            var stderr = process.StandardError.ReadToEndAsync(); var stdout = process.StandardOutput.BaseStream.CopyToAsync(stream);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel); timeout.CancelAfter(TimeSpan.FromSeconds(12));
            try
            {
                await process.WaitForExitAsync(timeout.Token); await stdout; await stderr;
                if (process.ExitCode != 0 || stream.Length == 0) return;
                stream.Position = 0; var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); frames.Add(bitmap);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }
                try { await Task.WhenAll(stdout, stderr); } catch { }
                cancel.ThrowIfCancellationRequested(); return;
            }
        }
        cancel.ThrowIfCancellationRequested(); update(frames);
    }
}
