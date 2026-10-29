using System.Text.Json;

namespace Liangtu;

// Isolated native acceptance fixture. Records observations; never injects input.
internal static class AnnotationAcceptance
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root=Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app=new App { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.Startup+=async (_,_)=> {
            var settings=new Settings{Sounds=false,AutoHang=false,NewReveal=false};
            typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app,settings);
            typeof(App).GetProperty(nameof(App.Store))!.SetValue(app,new Store(settings));
            Theme.Select(args.Length>2?args[2]:"linen");
            var bitmap=BitmapSource.Create(960,540,96,96,PixelFormats.Bgra32,null,Enumerable.Repeat((byte)245,960*540*4).ToArray(),960*4);
            var picturePath=Path.Combine(Settings.Inbox,"acceptance-picture.png"); MediaTools.SavePng(bitmap,picturePath);
            var image=new EditorWindow(app,new Photo{Path=picturePath,Image=bitmap}){Title="图片标注对照 — 隔离验收",Width=1120,Height=820};
            var video=new TrimWindow(app,new Photo{Path=Path.GetFullPath(args[1]),Image=await MediaTools.Thumbnail(args[1])}){Title="视频标注对照 — 隔离验收",Width=1120,Height=820};
            void Observe(Window window,string name) {
                foreach(var (routed,label) in new[]{(TextCompositionManager.PreviewTextInputStartEvent,"start"),(TextCompositionManager.PreviewTextInputUpdateEvent,"update"),(TextCompositionManager.PreviewTextInputEvent,"complete")})
                    window.AddHandler(routed,new TextCompositionEventHandler((_,e)=>File.AppendAllText(Path.Combine(Settings.Root,"composition.jsonl"),JsonSerializer.Serialize(new{Window=name,Stage=label,Text=e.Text,Composition=e.TextComposition.CompositionText,Language=InputLanguageManager.Current.CurrentInputLanguage.Name,Time=DateTime.UtcNow})+Environment.NewLine)),true);
            }
            Observe(image,"image"); Observe(video,"video"); image.Show(); video.Show();
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)}; string previous="";
            timer.Tick+=(_,_)=> {
                var media=video.Playback.Media; var bounds=media.TransformToAncestor(video).TransformBounds(new Rect(media.RenderSize));
                var state=JsonSerializer.Serialize(new {Video=new{Ready=video.Playback.Ready,Position=video.Playback.Position,Editing=video.AnnotationSurface.IsEditingText,Zoom=video.AnnotationSurface.Zoom,MediaBounds=new{bounds.X,bounds.Y,bounds.Width,bounds.Height},Objects=video.AnnotationSurface.Elements.Select(e=>new{e.Id,e.Name,e.Time,Text=e.Text,Bounds=e.Bounds}).ToArray()},Image=new{Zoom=image.Viewport.Zoom,Texts=image.Texts},Language=InputLanguageManager.Current.CurrentInputLanguage.Name,Ime=InputMethod.Current.ImeState.ToString(),Focus=(Keyboard.FocusedElement as FrameworkElement)?.GetType().Name});
                if(state!=previous){File.WriteAllText(Path.Combine(Settings.Root,"native-state.json"),state);File.AppendAllText(Path.Combine(Settings.Root,"native-states.jsonl"),state+Environment.NewLine);previous=state;}
            }; timer.Start();
            app.Exit+=(_,_)=>timer.Stop();
        };
        return app.Run();
    }
}
