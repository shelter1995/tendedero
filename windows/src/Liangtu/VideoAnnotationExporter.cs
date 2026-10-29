using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Liangtu;

internal static class VideoAnnotationExporter
{
    public static async Task Export(string path, VideoSelection trim, IReadOnlyList<VideoAnnotation> elements, int width, int height, CancellationToken cancel, IProgress<double>? progress = null)
    {
        cancel.ThrowIfCancellationRequested();
        if (!double.IsFinite(trim.Start) || !double.IsFinite(trim.End) || trim.Start < 0 || trim.End <= trim.Start || width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(trim));
        var executable = MediaTools.Ffmpeg ?? throw new InvalidOperationException("未找到 ffmpeg.exe。请安装视频组件。");
        var directory = Path.Combine(Path.GetTempPath(), "Liangtu-annotations-" + Guid.NewGuid().ToString("N"));
        var temp = Path.Combine(Path.GetDirectoryName(path)!, ".trimming-" + Guid.NewGuid().ToString("N") + Path.GetExtension(path));
        var files = new List<string>(); Directory.CreateDirectory(directory);
        try {
            var assets = new List<(string path, int x, int y, VideoSelection time)>();
            foreach (var element in elements) {
                cancel.ThrowIfCancellationRequested();
                if (element.Time.Clip(trim) is not VideoSelection time) continue;
                var bounds = element.Bounds; if (bounds.IsEmpty) continue; bounds.Inflate(2, 2); bounds.Intersect(new Rect(0, 0, width, height)); if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) continue;
                // The YUV420 overlay filter rounds placement to chroma boundaries.
                // Pad the asset to even source coordinates so text never shifts 1px.
                int x = (int)Math.Floor(bounds.X) & ~1, y = (int)Math.Floor(bounds.Y) & ~1, w = (int)Math.Ceiling(bounds.Right) - x, h = (int)Math.Ceiling(bounds.Bottom) - y;
                var local = new Canvas { Width = w, Height = h, ClipToBounds = true };
                var authored = new Canvas { Width = width, Height = height, RenderTransform = new TranslateTransform(-x, -y) }; authored.Children.Add(element.CreateView()); local.Children.Add(authored);
                local.Measure(new Size(w,h)); local.Arrange(new Rect(0,0,w,h)); local.UpdateLayout();
                var bitmap = new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32); bitmap.Render(local); bitmap.Freeze();
                var assetPath = Path.Combine(directory,"a" + assets.Count.ToString("D4",CultureInfo.InvariantCulture) + ".png"); files.Add(assetPath);
                // Assets are private to this export; no atomic replacement is needed.
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var outputFile = File.Create(assetPath)) encoder.Save(outputFile);
                assets.Add((assetPath,x,y,time));
            }
            if (assets.Count == 0) { await MediaTools.Trim(path,trim.Start,trim.End,cancel); return; }
            string Number(double value) => value.ToString("R",CultureInfo.InvariantCulture);
            var graph = new List<string> { "[0:v]setpts=PTS-STARTPTS[base]" };
            for (int i=0;i<assets.Count;i++) {
                var asset=assets[i]; var input=i==0?"base":"v"+(i-1); var outputLabel=i==assets.Count-1?"out":"v"+i;
                graph.Add($"[{input}][{i+1}:v]overlay=x={asset.x}:y={asset.y}:enable='gte(t,{Number(asset.time.Start)})*lt(t,{Number(asset.time.End)})':eof_action=repeat:repeatlast=1[{outputLabel}]");
            }
            var graphPath=Path.Combine(directory,"filters.txt"); files.Add(graphPath); await File.WriteAllTextAsync(graphPath,string.Join(";\n",graph),cancel);
            var psi=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
            var args=new List<string>{"-v","error","-nostdin","-y","-protocol_whitelist","file,pipe","-ss",Number(trim.Start),"-i",path};
            foreach(var asset in assets) args.AddRange(new[]{"-protocol_whitelist","file,pipe","-i",asset.path});
            args.AddRange(new[]{"-filter_complex_script",graphPath,"-t",Number(trim.End-trim.Start),"-map","[out]","-map","0:a?","-c:v","libx264","-preset","fast","-crf","18","-c:a","aac","-movflags","+faststart","-progress","pipe:1","-stats_period","0.1",temp});
            foreach(var arg in args) psi.ArgumentList.Add(arg);
            using var process=Process.Start(psi) ?? throw new IOException("无法启动视频编码器");
            var error=process.StandardError.ReadToEndAsync();
            async Task ReadProgress() {
                while(await process.StandardOutput.ReadLineAsync() is string line) if(line.StartsWith("out_time_us=",StringComparison.Ordinal) && double.TryParse(line.AsSpan(12),NumberStyles.Float,CultureInfo.InvariantCulture,out var microseconds)) progress?.Report(Math.Clamp(microseconds/1_000_000/(trim.End-trim.Start),0,1));
            }
            var output=ReadProgress();
            try { await process.WaitForExitAsync(cancel); await output; cancel.ThrowIfCancellationRequested(); }
            catch { try { if (!process.HasExited) process.Kill(true); } catch { } await process.WaitForExitAsync(); try { await output; } catch { } await error; throw; }
            var message=await error;
            if(process.ExitCode!=0 || !File.Exists(temp) || new FileInfo(temp).Length==0) throw new IOException(message.Length==0?"视频编码未生成有效文件":message);
            cancel.ThrowIfCancellationRequested(); File.Move(temp,path,true); progress?.Report(1);
        }
        finally {
            if(File.Exists(temp)) File.Delete(temp);
            foreach(var file in files) if(File.Exists(file)) File.Delete(file);
            if(Directory.Exists(directory)) Directory.Delete(directory);
        }
    }
}
