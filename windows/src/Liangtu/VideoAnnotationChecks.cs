namespace Liangtu;

internal static class VideoAnnotationChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => {
            var log = new List<string>();
            try {
                var settings = new Settings { Sounds = false, AutoHang = false, NewReveal = false };
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings);
                typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, new Store(settings));
                var photo = new Photo { Path = Path.GetFullPath(args[1]), Image = await MediaTools.Thumbnail(args[1]) };
                if (args.Contains("--interactive")) {
                    Theme.Select("linen"); var preview = new TrimWindow(app,photo) { Title = "视频标注体验 — 隔离测试", Width = 1120, Height = 820 };
                    preview.Closed += (_,_) => app.Shutdown(); preview.Show(); return;
                }
                var window = new TrimWindow(app, photo); window.Show();
                try {
                    if (typeof(TrimWindow).GetProperty("AnnotationSurface", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) == null)
                        throw new Exception("Video editor has no annotation workspace");
                    log.Add("PASS video editor exposes annotation workspace");
                    async Task Test(string name,Func<Task> body) { try { await body(); log.Add("PASS " + name); } catch (Exception ex) { log.Add("FAIL " + name + ": " + ex); } File.WriteAllLines(Path.Combine(Settings.Root,"results.txt"),log); }
                    void Assert(bool value,string message) { if (!value) throw new Exception(message); }
                    for (int i=0;i<100 && !window.Playback.Ready;i++) await Task.Delay(100);
                    Assert(window.Playback.Ready,"Video failed to open");
                    var surface = window.AnnotationSurface;
                    var undo = Children<Button>(window).Single(b => (b.ToolTip as string)?.StartsWith("撤销") == true);
                    var redo = Children<Button>(window).Single(b => (b.ToolTip as string)?.StartsWith("重做") == true);
                    await Test("text creates a timed element and undo restores video document",() => {
                        var text = surface.AddText(new Point(80,60),"中文标注\n第二行");
                        Assert(surface.Elements.Count == 1 && text.Time.Start == 0 && text.Time.End == 3,"Wrong default time");
                        Assert(window.ElementTrack.Visibility == Visibility.Visible,"Missing element track");
                        undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(surface.Elements.Count == 0,"Undo lost element creation");
                        redo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(surface.Elements.Single().Text!.Text == "中文标注\n第二行","Redo lost Unicode or multiline text"); return Task.CompletedTask;
                    });
                    await Test("text commits editable input and retains style",() => {
                        var id = surface.Elements.Single().Id; surface.BeginText(id);
                        var input = Children<TextBox>(surface).Single(x => !x.IsReadOnly); input.Text = "你好，朋友！\n标注🙂"; surface.CommitText();
                        Assert(surface.Elements.Single().Text!.Text == "你好，朋友！\n标注🙂","Input lost");
                        surface.TextSize = 48; surface.Bold = true; surface.ApplyStyle(); Assert(surface.Selected?.Text is { FontSize:48, Bold:true },"Text style not applied"); return Task.CompletedTask;
                    });
                    await Test("timed visibility is half-open and does not rebuild object views",() => {
                        var id=surface.Elements.Single().Id; surface.SetTime(id,new(1,3)); var label=Children<TextBox>(surface).Single();
                        surface.SetPosition(.5); Assert(!label.IsVisible,"Element visible early"); surface.SetPosition(1); Assert(label.IsVisible,"Element missing at start");
                        for(int i=0;i<100;i++) surface.SetPosition(1+i*.01);
                        Assert(ReferenceEquals(label,Children<TextBox>(surface).Single()),"Playback rebuilt text views"); surface.SetPosition(3); Assert(!label.IsVisible,"Element visible at end"); return Task.CompletedTask;
                    });
                    await Test("editing duration never seeks the native video",() => {
                        var before=window.Playback.Media.Position; var id=surface.Elements.Single().Id;
                        for(int i=0;i<100;i++) surface.SetTime(id,new(1,3+i*.01),false); surface.CommitEdit();
                        Assert(window.Playback.Media.Position==before,"Element duration triggered decode"); return Task.CompletedTask;
                    });
                    await Test("copy move and delete remain in unified history",() => {
                        surface.Select(surface.Elements.Single().Id); surface.DuplicateSelected(); Assert(surface.Elements.Count==2,"Duplicate missing");
                        var before=surface.Selected!.Text!.X; surface.Nudge(new Vector(10,0)); Assert(surface.Selected!.Text!.X==before+10,"Nudge missing");
                        surface.DeleteSelected(); Assert(surface.Elements.Count==1,"Delete missing"); undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert(surface.Elements.Count==2,"Delete not undoable"); return Task.CompletedTask;
                    });
                    await Test("seven themes and narrow layouts retain elements and font styling",async () => {
                        window.Playback.Seek(1.5); surface.Select(surface.Elements[0].Id);
                        foreach(var theme in new[]{"sky","paper","night","porcelain","darkroom","linen","color-play"}) {
                            Theme.Select(theme); window.Width=theme=="color-play"?760:1060; window.Height=760; await Task.Delay(100); window.UpdateLayout();
                            Assert(surface.ActualWidth>250 && surface.ActualHeight>100,"Video canvas crowded out for "+theme);
                            Assert(surface.Elements.Count==2,"Theme changed document");
                            var root=(FrameworkElement)window.Content; var bmp=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32); bmp.Render(root); MediaTools.SavePng(bmp,Path.Combine(Settings.Root,"theme-"+theme+".png"));
                        }
                    });
                    await Test("scrolling many overlapping elements keeps the shared playhead aligned",async () => {
                        for(int i=0;i<8;i++) { var id=Guid.NewGuid(); surface.Add(new(id,new(1,10),"矩形",Shape:new(id,new RectangleGeometry(new Rect(10+i*5,10,30,30)),Colors.Red,4,1,false))); }
                        window.Playback.Seek(8); await Task.Delay(100); window.UpdateLayout();
                        var canvas=Children<Canvas>(window.ElementTrack).First(); var cursor=canvas.Children.OfType<Border>().Single();
                        var actual=canvas.TranslatePoint(new Point(((TranslateTransform)cursor.RenderTransform).X+1,0),window).X;
                        var expected=window.Timeline.TranslatePoint(new Point(18+(window.Timeline.ActualWidth-36)*8/window.Playback.Duration,0),window).X;
                        Assert(Math.Abs(actual-expected)<.5,$"Track playhead differs by {actual-expected:F2} DIP");
                    });
                    await Test("editing annotation properties pauses playback",() => {
                        window.Playback.Seek(2); window.Playback.Toggle(); Assert(window.Playback.Playing,"Playback did not start"); surface.ApplyStyle(); Assert(!window.Playback.Playing,"Style edit left video playing"); return Task.CompletedTask;
                    });
                    await Test("selected transform controls return after changing the visible time",() => {
                        window.Playback.Seek(2); surface.Select(surface.Elements.Last().Id); surface.SetTime(surface.SelectedId!.Value,new(3,10)); window.Playback.Seek(4);
                        Assert(Children<TextSelectionFrame>(surface).Any(),"Selected element lost transform controls after seeking into its new time"); return Task.CompletedTask;
                    });
                    await Test("property combo arrow keys do not seek the video",() => {
                        surface.SelectTool("文字"); var choice=Children<ComboBox>(window).Single(x=>Equals(x.ToolTip,"文字字体")); choice.Focus(); var native=window.Playback.Media.Position;
                        choice.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,Key.Right){RoutedEvent=Keyboard.PreviewKeyDownEvent});
                        Assert(window.Playback.Media.Position==native,"Property arrow was consumed as video seek"); return Task.CompletedTask;
                    });
                    await Test("element time input pauses playback before changing the interval",() => {
                        surface.Select(surface.Elements.Last().Id); window.Playback.Seek(4); window.Playback.Toggle();
                        var box=Children<TextBox>(window).Single(x=>Equals(x.ToolTip,"元素出现时间")); box.Text="3.5";
                        box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});
                        Assert(!window.Playback.Playing,"Duration input left video playing"); return Task.CompletedTask;
                    });
                    await Test("duration-track arrow keys move an element without seeking video",() => {
                        window.Playback.Seek(2); var canvas=Children<Canvas>(window.ElementTrack).First(); var row=canvas.Children.OfType<Grid>().First(); row.Focus();
                        var element=surface.Elements.OrderBy(x=>x.Time.Start).First(); var native=window.Playback.Media.Position;
                        row.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,Key.Right){ RoutedEvent=Keyboard.PreviewKeyDownEvent });
                        Assert(Math.Abs(surface.Elements.Single(x=>x.Id==element.Id).Time.Start-element.Time.Start-.1)<.001,"Arrow did not move element");
                        Assert(window.Playback.Media.Position==native,"Track arrow sought video"); return Task.CompletedTask;
                    });
                    await Test("starting an off-cursor duration drag does not request decode",() => {
                        window.Playback.Seek(11); var native=window.Playback.Media.Position;
                        var thumb=Children<System.Windows.Controls.Primitives.Thumb>(window.ElementTrack).First();
                        thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0,0));
                        try { Assert(window.Playback.Media.Position==native,"Duration drag selected by seeking"); }
                        finally { thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(0,0,true)); }
                        return Task.CompletedTask;
                    });
                    await Test("selected rectangle interior directly moves instead of drawing another",() => {
                        surface.SetElements(Array.Empty<VideoAnnotation>()); surface.CommitEdit(); window.Playback.Seek(0); surface.SelectTool("矩形");
                        surface.BeginStroke(new(100,100)); surface.EndStroke(new(250,180)); var original=surface.Elements.Single();
                        surface.BeginStroke(new(170,140)); surface.EndStroke(new(190,160));
                        Assert(surface.Elements.Count==1,"Interior drag drew another rectangle"); Assert(Math.Abs(surface.Elements.Single().Bounds.X-original.Bounds.X-20)<.001,"Interior drag did not move object"); return Task.CompletedTask;
                    });
                    async Task Export(string path,IReadOnlyList<VideoAnnotation> elements,System.Threading.CancellationToken cancel=default,IProgress<double>? progress=null) {
                        var type=typeof(TrimWindow).Assembly.GetType("Liangtu.VideoAnnotationExporter") ?? throw new Exception("Timed annotations are not exported");
                        await (Task)type.GetMethod("Export")!.Invoke(null,new object?[]{path,new VideoSelection(1,3),elements,640,360,cancel,progress})!;
                    }
                    var annotationId=Guid.NewGuid();
                    var shape=new VideoAnnotation(annotationId,new VideoElementTime(1.5,2.5),"矩形",Shape:new ShapeAnnotation(annotationId,new RectangleGeometry(new Rect(50,50,140,100)),Colors.Red,8,1,false));
                    await Test("export clips source time and burns shape only inside its interval",async () => {
                        var path=Path.Combine(Settings.Root,"export-test.mp4");
                        await Ffmpeg("-f","lavfi","-i","color=c=blue:s=640x360:r=30:d=4","-f","lavfi","-i","sine=frequency=440:duration=4","-c:v","libx264","-preset","ultrafast","-c:a","aac","-shortest",path);
                        var before=await File.ReadAllBytesAsync(path);
                        await Export(path,new[]{shape});
                        foreach(var (time,red) in new[]{("0.2",false),("0.7",true),("1.7",false)}) {
                            var pixels=await Ffmpeg("-ss",time,"-i",path,"-frames:v","1","-f","rawvideo","-pix_fmt","rgb24","pipe:1"); var offset=(90*640+50)*3;
                            Assert(pixels.Length==640*360*3,"Wrong exported dimensions"); Assert(red ? pixels[offset]>180 && pixels[offset+2]<80 : pixels[offset]<80 && pixels[offset+2]>180,"Timed overlay wrong at "+time);
                        }
                        var info=System.Text.Encoding.UTF8.GetString(await Ffmpeg("-i",path,"-map","0:a:0","-f","null","pipe:1"));
                        Assert(!before.SequenceEqual(await File.ReadAllBytesAsync(path)),"Export did not replace successful result");
                    });
                    await Test("failed and cancelled exports preserve source bytes and remove temporary files",async () => {
                        var path=Path.Combine(Settings.Root,"bad-export.mp4"); var bytes=new byte[]{1,2,3,4}; await File.WriteAllBytesAsync(path,bytes);
                        try { await Export(path,new[]{shape}); throw new Exception("Damaged media accepted"); } catch(System.IO.IOException) { }
                        Assert(bytes.SequenceEqual(await File.ReadAllBytesAsync(path)),"Failed export overwrote source");
                        using var cancel=new System.Threading.CancellationTokenSource(); cancel.Cancel();
                        try { await Export(path,new[]{shape},cancel.Token); throw new Exception("Cancelled export accepted"); } catch(OperationCanceledException) { }
                        Assert(bytes.SequenceEqual(await File.ReadAllBytesAsync(path)),"Cancelled export overwrote source");
                        Assert(!Directory.EnumerateFiles(Settings.Root,".trimming-*").Any(),"Temporary video leaked");
                    });
                    await Test("rotated multiline Chinese text matches opaque preview pixels after export",async () => {
                        var id=Guid.NewGuid(); var text=new VideoAnnotation(id,new(1,3),"文字",Text:new(id,"中文标注！\n第二行",180,100,"Microsoft YaHei UI",40,Colors.White,true,true,15));
                        var preview=new Canvas{Width=640,Height=360}; preview.Children.Add(text.CreateView()); preview.Measure(new Size(640,360)); preview.Arrange(new Rect(0,0,640,360)); preview.UpdateLayout();
                        var raster=new RenderTargetBitmap(640,360,96,96,PixelFormats.Pbgra32); raster.Render(preview); var expected=new byte[640*360*4]; raster.CopyPixels(expected,640*4,0);
                        var path=Path.Combine(Settings.Root,"chinese-export.mp4"); await Ffmpeg("-f","lavfi","-i","color=c=blue:s=640x360:r=30:d=4","-c:v","libx264","-preset","ultrafast",path); await Export(path,new[]{text});
                        var actual=await Ffmpeg("-ss","0.7","-i",path,"-frames:v","1","-f","rawvideo","-pix_fmt","rgb24","pipe:1"); int opaque=0,matched=0;
                        MediaTools.SavePng(raster,Path.Combine(Settings.Root,"chinese-preview.png")); MediaTools.SavePng(BitmapSource.Create(640,360,96,96,PixelFormats.Rgb24,null,actual,640*3),Path.Combine(Settings.Root,"chinese-decoded.png"));
                        for(int i=0;i<640*360;i++) if(expected[i*4+3]>240) { opaque++; if(actual[i*3]>170 && actual[i*3+1]>170 && actual[i*3+2]>170) matched++; }
                        Assert(opaque>100 && matched/(double)opaque>.95,$"Chinese text differs from preview: {matched}/{opaque} opaque pixels");
                    });
                    await Test("cancelling an encoder from its progress preserves original bytes",async () => {
                        var path=Path.Combine(Settings.Root,"cancel-running.mp4"); File.Copy(args[1],path,true); var bytes=await File.ReadAllBytesAsync(path);
                        using var cancel=new System.Threading.CancellationTokenSource(); var received=false;
                        try { await Export(path,new[]{shape},cancel.Token,new ImmediateProgress(_=>{received=true;cancel.Cancel();})); throw new Exception("Running export ignored cancellation"); } catch(OperationCanceledException) { }
                        Assert(received,"Encoder did not publish progress"); Assert(bytes.SequenceEqual(await File.ReadAllBytesAsync(path)),"Running cancellation changed original");
                    });
                    async Task WithSaveWindow(string name,Func<TrimWindow,Task> body) {
                        var path=Path.Combine(Settings.Root,name+".mp4"); File.Copy(args[1],path,true); var edit=new TrimWindow(app,new Photo{Path=path,Image=photo.Image}); edit.Show();
                        try { for(int i=0;i<100 && !edit.Playback.Ready;i++) await Task.Delay(50); Assert(edit.Playback.Ready,"Save window did not load"); edit.AnnotationSurface.AddText(new(20,20),"待保存文字"); await body(edit); }
                        finally { for(int i=0;i<100 && edit.IsVisible && !edit.Playback.Ready;i++) await Task.Delay(50); edit.Close(); }
                    }
                    await Test("invalid element time blocks saving before media closes",async () => {
                        await WithSaveWindow("invalid-time",async edit=> {
                            var box=Children<TextBox>(edit).Single(x=>Equals(x.ToolTip,"元素出现时间")); box.Text="invalid";
                            Children<Button>(edit).Single(x=>Equals(x.Content,"保存片段")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Assert(edit.Playback.Ready,"Invalid element time still started save"); await Task.CompletedTask;
                        });
                    });
                    await Test("save disables properties and cancellation restores the editable document",async () => {
                        await WithSaveWindow("cancel-window",async edit=> {
                            var path=Path.Combine(Settings.Root,"cancel-window.mp4"); var before=await File.ReadAllBytesAsync(path); var selected=edit.AnnotationSurface.Elements.Single();
                            Children<Button>(edit).Single(x=>Equals(x.Content,"保存片段")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            var combo=Children<ComboBox>(edit).Single(x=>Equals(x.ToolTip,"文字字体"));
                            var disabled=!combo.IsEnabled;
                            Children<Button>(edit).Single(x=>Equals(x.Content,"取消保存")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            for(int i=0;i<100 && !edit.Playback.Ready;i++) await Task.Delay(50);
                            Assert(disabled,"Property bar remained editable during save"); Assert(edit.Playback.Ready && edit.Timeline.IsEnabled && combo.IsEnabled,"Editor did not recover after cancellation");
                            Assert(edit.AnnotationSurface.Elements.Single()==selected,"Cancelled save lost annotations"); Assert(before.SequenceEqual(await File.ReadAllBytesAsync(path)),"Cancelled window save changed source");
                        });
                    });
                    await Test("untrimmed annotation edits participate in update protection",() => { window.Timeline.ResetSelection(); Assert(window.HasPendingSelection,"Updater cannot detect pending annotations"); return Task.CompletedTask; });
                    await Test("retained timing updates keep the same fifty annotation views",() => {
                        var elements=Enumerable.Range(0,50).Select(i=>{var id=Guid.NewGuid();return new VideoAnnotation(id,new(1,3),"矩形",Shape:new(id,new RectangleGeometry(new Rect(i*8,20,40,40)),Colors.Red,3,1,false));}).ToArray();
                        surface.SetElements(elements); surface.SetPosition(1.5); var views=Children<System.Windows.Shapes.Path>(surface).ToArray();
                        var bytes=GC.GetAllocatedBytesForCurrentThread(); var clock=System.Diagnostics.Stopwatch.StartNew(); for(int i=0;i<1200;i++) surface.SetPosition((i%400)*.01); clock.Stop(); var allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
                        Assert(views.SequenceEqual(Children<System.Windows.Shapes.Path>(surface)),"Timing updates rebuilt fifty shapes"); Assert(allocated<16_000_000,"Timing updates allocate video-sized buffers"); log.Add($"MEASURE 50 retained objects / 1200 time updates: {clock.Elapsed.TotalMilliseconds:F2}ms, UI-thread allocated={allocated} bytes; not a desktop FPS test"); return Task.CompletedTask;
                    });
                    await Test("compact annotated editor keeps a usable video viewport",async () => {
                        Theme.Select("linen"); window.Width=700; window.Height=480; await Task.Delay(100); window.UpdateLayout();
                        Assert(surface.ActualHeight>=100,$"Annotated viewport collapsed to {surface.ActualHeight:F1} DIP");
                        Assert(surface.PixelHeight*surface.Zoom>=100,$"Fitted video became too small: {surface.PixelHeight*surface.Zoom:F1} DIP");
                        var root=(FrameworkElement)window.Content; var bmp=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32); bmp.Render(root); MediaTools.SavePng(bmp,Path.Combine(Settings.Root,"annotated-compact.png"));
                    });
                } finally { window.Close(); }
            } catch (Exception ex) { log.Add("FAIL " + ex); }
            File.WriteAllLines(Path.Combine(Settings.Root, "results.txt"), log); app.Shutdown(log.Any(x => x.StartsWith("FAIL")) ? 1 : 0);
        };
        return app.Run();
    }
    static IEnumerable<T> Children<T>(DependencyObject node) where T:DependencyObject { if(node is T item) yield return item; for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) foreach(var child in Children<T>(VisualTreeHelper.GetChild(node,i))) yield return child; }
    static async Task<byte[]> Ffmpeg(params string[] args) {
        var psi=new System.Diagnostics.ProcessStartInfo(MediaTools.Ffmpeg ?? throw new Exception("FFmpeg unavailable")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
        foreach(var arg in new[]{"-hide_banner","-v","error","-nostdin","-y"}.Concat(args)) psi.ArgumentList.Add(arg);
        using var process=System.Diagnostics.Process.Start(psi)!; var error=process.StandardError.ReadToEndAsync(); using var memory=new MemoryStream(); var output=process.StandardOutput.BaseStream.CopyToAsync(memory); await process.WaitForExitAsync(); await output; var text=await error; if(process.ExitCode!=0) throw new IOException(text); return memory.ToArray();
    }
    sealed class ImmediateProgress(Action<double> report):IProgress<double> { public void Report(double value)=>report(value); }
}
