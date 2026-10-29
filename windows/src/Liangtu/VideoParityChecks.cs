namespace Liangtu;

internal static class VideoParityChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root=Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app=new App{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Startup+=async (_,_)=> {
            var log=new List<string>(); var settings=new Settings{Sounds=false,AutoHang=false,NewReveal=false};
            typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app,settings); typeof(App).GetProperty(nameof(App.Store))!.SetValue(app,new Store(settings));
            var window=new TrimWindow(app,new Photo{Path=Path.GetFullPath(args[1]),Image=await MediaTools.Thumbnail(args[1])});window.Show();
            try {
                for(int i=0;i<100&&!window.Playback.Ready;i++)await Task.Delay(50);
                void Assert(bool value,string message){if(!value)throw new Exception(message);}
                async Task Test(string name,Func<Task> body){try{await body();log.Add("PASS "+name);}catch(Exception ex){log.Add("FAIL "+name+": "+ex.Message);}File.WriteAllLines(Path.Combine(Settings.Root,"results.txt"),log);}
                var surface=window.AnnotationSurface;
                await Test("common toolbar controls match image editor across all themes",async()=>{
                    var path=Path.Combine(Settings.Inbox,"reference.png");MediaTools.SavePng(MediaTools.VideoPlaceholder(),path);var reference=new EditorWindow(app,new Photo{Path=path}){Width=1120,Height=820};reference.Show();
                    try{foreach(var theme in new[]{"sky","paper","night","porcelain","darkroom","linen","color-play"}){
                        Theme.Select(theme);await Task.Delay(50);window.UpdateLayout();reference.UpdateLayout();
                        foreach(var name in new[]{"矩形","圆形","直线","箭头","画笔","文字","平移"}){
                            var imageButton=Children<RadioButton>(reference).Single(x=>(x.ToolTip as string)?.StartsWith(name+" ·")==true);var videoButton=Children<RadioButton>(window).Single(x=>(x.ToolTip as string)?.StartsWith(name+" ·")==true);
                            Assert(imageButton.Width==videoButton.Width&&imageButton.Height==videoButton.Height&&imageButton.Padding==videoButton.Padding&&imageButton.Margin==videoButton.Margin,$"{theme}/{name}: toolbar metrics differ");
                        }
                        var root=(FrameworkElement)window.Content;var image=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(root);MediaTools.SavePng(image,Path.Combine(Settings.Root,"video-"+theme+".png"));
                        root=(FrameworkElement)reference.Content;image=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(root);MediaTools.SavePng(image,Path.Combine(Settings.Root,"image-"+theme+".png"));
                    }}finally{reference.Close();}
                });
                await Test("video attributes offer image palette and continuous pen widths",()=> {
                    var colors=Children<RadioButton>(window).Where(x=>(x.ToolTip as string)?.StartsWith("颜色 #")==true).Select(x=>(string)x.ToolTip).ToArray();
                    Assert(colors.Length==8,"Image palette has eight selectable swatches; video differs");
                    var slider=Children<Slider>(window).SingleOrDefault(x=>(x.ToolTip as string)?.StartsWith("笔宽")==true);Assert(slider!=null&&slider.Minimum==1&&slider.Maximum==24,"Video pen width does not match image's 1–24px slider");return Task.CompletedTask;
                });
                await Test("editing video text uses the image dashed frame and drag handle",async()=> {
                    surface.SetElements(Array.Empty<VideoAnnotation>());window.Playback.Seek(0);var text=surface.AddText(new(100,100),"文字拖动");surface.BeginText(text.Id);await Task.Delay(50);window.UpdateLayout();
                    Assert(Children<TextSelectionFrame>(surface).Any(x=>x.IsVisible),"Editable text has no image-style dashed frame");
                    Assert(Children<Border>(surface).Any(x=>Equals(x.ToolTip,"拖动调整文字位置")&&x.IsVisible),"Editable text has no image-style drag handle");surface.CommitText();
                });
                await Test("canvas shortcuts disable composition while native text input enables it",async()=>{
                    var viewport=Children<EditorViewport>(surface).Single();Assert(!InputMethod.GetIsInputMethodEnabled(viewport),"Canvas still accepts hidden IME composition instead of tool shortcuts");
                    surface.SetElements(Array.Empty<VideoAnnotation>());window.Playback.Seek(0);var text=surface.AddText(new(100,100),"输入法");surface.BeginText(text.Id);await Task.Delay(30);var input=Children<TextBox>(surface).Single(x=>!x.IsReadOnly);Assert(InputMethod.GetIsInputMethodEnabled(input),"Native annotation input inherited disabled IME");surface.CommitText();
                });
                await Test("composition Escape stays inside the text input instead of finishing editing",async()=> {
                    surface.SetElements(Array.Empty<VideoAnnotation>());window.Playback.Seek(0);var text=surface.AddText(new(100,100),"原文字");surface.BeginText(text.Id);await Task.Delay(50);
                    var input=Children<TextBox>(surface).Single(x=>!x.IsReadOnly);var composition=new TextComposition(InputManager.Current,input,"ni",TextCompositionAutoComplete.Off);TextCompositionManager.StartComposition(composition);
                    var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent};input.RaiseEvent(key);
                    Assert(surface.IsEditingText&&window.IsVisible&&!key.Handled,"Escape during composition finished video text editing");TextCompositionManager.CompleteComposition(composition);surface.CommitText();
                });
                await Test("text styling retains caret and dragging commits a single undo step",async()=>{
                    surface.SetElements(Array.Empty<VideoAnnotation>());window.Playback.Seek(0);var text=surface.AddText(new(100,100),"中文文字");surface.BeginText(text.Id);await Task.Delay(50);
                    var input=Children<TextBox>(surface).Single(x=>!x.IsReadOnly);input.Text="中文文字第二行";input.CaretIndex=2;surface.TextSize=40;surface.ApplyStyle();
                    Assert(surface.IsEditingText&&ReferenceEquals(input,Children<TextBox>(surface).Single(x=>!x.IsReadOnly))&&input.CaretIndex==2,"Style edit recreated active text input or lost caret");
                    surface.BeginTextMove(new(75,100));surface.MoveStroke(new(115,125));surface.EndStroke(new(115,125));
                    Assert(surface.Selected?.Text is {X:140,Y:125,Text:"中文文字第二行"},"Drag handle did not move authored text");
                    Children<Button>(window).Single(x=>(x.ToolTip as string)?.StartsWith("撤销")==true).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(surface.Elements.Single().Text is {X:100,Y:100,Text:"中文文字第二行"},"Undo did not restore one complete drag");
                });
                await Test("highlighter retains image opacity and pen width semantics",()=>{
                    surface.SetElements(Array.Empty<VideoAnnotation>());surface.SelectTool("荧光笔");surface.StrokeWidth=4;window.Playback.Seek(0);surface.BeginStroke(new(30,40));surface.MoveStroke(new(80,70));surface.EndStroke(new(120,80));
                    Assert(surface.Elements.Single().Shape is {Opacity:.35,Width:24},"Highlighter does not match image editor");surface.Select(surface.Elements[0].Id);Assert(surface.StrokeWidth==4,"Selecting highlighter multiplied its width");return Task.CompletedTask;
                });
                await Test("pen slider keyboard stays in attributes",()=>{
                    surface.SetElements(Array.Empty<VideoAnnotation>());surface.SelectTool("矩形");window.Playback.Seek(0);var id=Guid.NewGuid();surface.Add(new(id,new(0,3),"矩形",Shape:new(id,new RectangleGeometry(new Rect(20,20,60,50)),Colors.Red,4,1,false)));
                    var slider=Children<Slider>(window).Single();slider.Focus();var bounds=surface.Selected!.Bounds;
                    slider.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,Key.Right){RoutedEvent=Keyboard.PreviewKeyDownEvent});
                    Assert(surface.Selected!.Bounds==bounds,"Slider arrow moved annotation instead of the attribute");
                    return Task.CompletedTask;
                });
                await Test("one complete pen slider drag undoes in one step",()=>{
                    var slider=Children<Slider>(window).Single();
                    slider.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0,0));for(int width=5;width<=24;width++)slider.Value=width;slider.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(0,0,false));
                    Children<Button>(window).Single(x=>(x.ToolTip as string)?.StartsWith("撤销")==true).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert(surface.Elements.Single().Shape!.Width==4,"One undo did not restore whole width drag");Assert(slider.Value==4,"Undo restored the object but left a stale width attribute");return Task.CompletedTask;
                });
                await Test("blank styled text never enters committed video history",()=>{
                    var history=new VideoEditHistory(new(new(0,12),Array.Empty<VideoAnnotation>()));var id=Guid.NewGuid();var draft=new VideoAnnotation(id,new(0,3),"文字",Text:new(id,"",20,20,"Microsoft YaHei UI",40,Colors.Red,true));history.Commit(new(new(0,12),new[]{draft}));
                    Assert(!history.CanUndo&&history.Current.Elements.Count==0,"Blank draft became an undoable annotation");return Task.CompletedTask;
                });
                await Test("clicking play finishes text editing and hides input chrome",async()=>{
                    surface.SetElements(Array.Empty<VideoAnnotation>());window.Playback.Seek(0);var text=surface.AddText(new(100,100),"播放前输入");surface.BeginText(text.Id);await Task.Delay(30);
                    try{Children<Button>(window).Single(x=>(x.ToolTip as string)?.StartsWith("播放选区")==true).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(window.Playback.Playing&&!surface.IsEditingText&&!Children<TextSelectionFrame>(surface).Any(x=>x.IsVisible),"Playback retained active text editor");}finally{window.Playback.Pause();surface.CommitText();}
                });
                await Test("Q selection mode can switch back to the already used brush",()=>{
                    var brush=Children<RadioButton>(window).Single(x=>(x.ToolTip as string)?.StartsWith("画笔 ·")==true);brush.IsChecked=true;
                    foreach(var key in new[]{Key.Q,Key.B})((UIElement)window.Content).RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window)!,Environment.TickCount,key){RoutedEvent=Keyboard.PreviewKeyDownEvent});
                    Assert(surface.Tool=="画笔"&&brush.IsChecked==true,"Brush stayed in hidden selection mode");return Task.CompletedTask;
                });
                await Test("completed text scaling synchronizes font properties",()=>{
                    surface.SetElements(Array.Empty<VideoAnnotation>());window.Playback.Seek(0);surface.TextSize=28;surface.AddText(new(100,100),"缩放文字");var corner=surface.Selected!.Bounds.BottomRight;
                    typeof(VideoAnnotationSurface).GetMethod("StartGesture",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(surface,new object?[]{corner,"br"});surface.MoveStroke(corner+new Vector(40,30));surface.EndStroke(corner+new Vector(40,30));
                    Assert(surface.Selected!.Text!.FontSize>28&&surface.TextSize==surface.Selected.Text.FontSize,"Scaled text keeps old font size in attribute controls");return Task.CompletedTask;
                });
                await Test("adding deleting and selecting annotations keeps video bounds stable",async()=> {
                    foreach(var theme in new[]{"sky","linen","color-play","darkroom"}) {
                        Theme.Select(theme);window.Width=1120;window.Height=820;surface.SetElements(Array.Empty<VideoAnnotation>());surface.SelectTool("矩形");await Task.Delay(100);window.UpdateLayout();
                        Rect Bounds()=>window.Playback.Media.TransformToAncestor(window).TransformBounds(new Rect(window.Playback.Media.RenderSize));var baseline=Bounds();
                        void Same(string step){var current=Bounds();Assert((baseline.TopLeft-current.TopLeft).Length<.5&&Math.Abs(baseline.Width-current.Width)<.5&&Math.Abs(baseline.Height-current.Height)<.5,$"{theme}/{step}: preview moved {baseline} -> {current}");}
                        var text=surface.AddText(new(100,100),"文字标注");await Task.Delay(50);window.UpdateLayout();Same("first object");
                        surface.SelectTool("文字");surface.Select(text.Id);await Task.Delay(50);window.UpdateLayout();Same("text properties");
                        for(int i=0;i<4;i++){var id=Guid.NewGuid();surface.Add(new(id,new(0,3),"矩形",Shape:new(id,new RectangleGeometry(new Rect(20+i*30,20,30,30)),Colors.Red,4,1,false)));}await Task.Delay(50);window.UpdateLayout();Same("overlapping rows");
                        foreach(var item in surface.Elements.ToArray()){surface.Select(item.Id);surface.DeleteSelected();}await Task.Delay(50);window.UpdateLayout();Same("last object deleted");
                    }
                });
            } finally {window.Close();File.WriteAllLines(Path.Combine(Settings.Root,"results.txt"),log);app.Shutdown(log.Any(x=>x.StartsWith("FAIL"))?1:0);}
        };return app.Run();
    }
    static IEnumerable<T> Children<T>(DependencyObject node)where T:DependencyObject{if(node is T item)yield return item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Children<T>(VisualTreeHelper.GetChild(node,i)))yield return child;}
}
