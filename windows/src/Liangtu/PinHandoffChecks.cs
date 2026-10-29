using System.Reflection;
namespace Liangtu;

// Exercises the production pin/close path using owned images and WPF windows.
// Does not install hooks, touch the clipboard or inject system input.
internal static class PinHandoffChecks
{
    [STAThread] public static int Main(string[] args)
    {
        Settings.Root=Path.GetFullPath(args[0]);Directory.CreateDirectory(Settings.Inbox);
        var app=new App { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        var log=new List<string>();
        void Check(string name,Action body) { try { body();log.Add("PASS "+name); } catch(Exception e) { log.Add("FAIL "+name+": "+e.Message); } }
        void Assert(bool okay,string message) { if(!okay) throw new Exception(message); }
        app.Startup+=async(_,_)=>{
            LineWindow? line=null;
            try {
                var settings=new Settings { AutoHang=true,NewReveal=false,HoverReveal=false,Sounds=false,Welcomed=true,CollectionRevision=1 };
                var store=new Store(settings);typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app,settings);typeof(App).GetProperty(nameof(App.Store))!.SetValue(app,store);
                Theme.Select("linen");
                line=new LineWindow(app,new FullscreenProtection(()=>Array.Empty<ScreenWindow>(),()=>0));typeof(App).GetProperty(nameof(App.Line))!.SetValue(app,line);
                store.Hanging+=app.OnPhotoHanging;store.Added+=app.OnPhotoAdded;
                var image=BitmapSource.Create(1400,860,96,96,PixelFormats.Bgr32,null,Enumerable.Repeat((byte)189,1400*860*4).ToArray(),1400*4);image.Freeze();
                var path=Path.Combine(Settings.Inbox,"pin-handoff.png");MediaTools.SavePng(image,path);await store.Add(path);
                var photo=store.Items.Single();line.Reveal(true,10);await Task.Delay(1650);
                Native.GetWindowRect(line.Hwnd,out var shelfBefore);
                var maxHeight=shelfBefore.Bottom-shelfBefore.Top;var heights=new List<int>();var pinGaps=0;
                var departing=((Canvas)line.Content).Children.OfType<Canvas>().Single().Children.OfType<Card>().Single(c=>c.Photo==photo);
                var sourceFrame=(Border)((Grid)departing.Children[0]).Children[0];
                EventHandler surfaceSample=(_,_)=>{
                    Native.GetWindowRect(line.Hwnd,out var rect);heights.Add(rect.Bottom-rect.Top);
                    var source=departing.IsVisible&&sourceFrame.Opacity>.95;
                    var destination=app.Windows.OfType<PinnedImageWindow>().Any(w=>w.IsVisible&&w.Opacity>.95);
                    var flight=app.Windows.OfType<ArrivalFlight>().Any(w=>w.IsVisible&&w.HasPresented);
                    if(!source&&!destination&&!flight)pinGaps++;
                };
                CompositionTarget.Rendering+=surfaceSample;
                app.Pin(photo);
                Native.GetWindowRect(line.Hwnd,out var pinStart);
                Check("pin departure never expands the shelf HWND to the full screen",()=>Assert(pinStart.Bottom-pinStart.Top<=maxHeight,$"Shelf grew {maxHeight} -> {pinStart.Bottom-pinStart.Top}px"));
                var pinned=app.Windows.OfType<PinnedImageWindow>().Single();var closed=false;pinned.Closed+=(_,_)=>closed=true;
                await Task.Delay(1200);CompositionTarget.Rendering-=surfaceSample;
                Check("shelf native surface remains bounded throughout pin transfer",()=>Assert(heights.All(h=>h<=maxHeight),"Full-screen shelf frame observed: "+heights.Max()));
                Check("pin departure keeps a source or destination visible on every frame",()=>Assert(pinGaps==0,"Pin handoff contained empty frames: "+pinGaps));
                Check("pin landing restores the destination and retires transfer windows",()=>Assert(pinned.IsVisible&&pinned.Opacity==1&&pinned.IsHitTestVisible&&line.ActiveTransferCount==0,"Pin landing was incomplete"));
                line.Retract();await Task.Delay(450);
                var gaps=0;var frameCount=0;
                bool HasImage() {
                    if(!closed&&pinned.IsVisible&&pinned.Opacity>.95) return true;
                    if(app.Windows.OfType<ArrivalFlight>().Any(w=>w.IsVisible&&w.HasPresented)) return true;
                    var card=((Canvas)line.Content).Children.OfType<Canvas>().Single().Children.OfType<Card>().SingleOrDefault(c=>c.Photo==photo);
                    if(card is not {IsVisible:true})return false;
                    var frame=(Border)((Grid)card.Children[0]).Children[0];
                    return frame.IsVisible&&frame.Opacity>.95;
                }
                EventHandler sample=(_,_)=>{frameCount++;if(!HasImage()) gaps++;};
                CompositionTarget.Rendering+=sample;
                pinned.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(pinned),Environment.TickCount,Key.Escape) { RoutedEvent=Keyboard.KeyDownEvent });
                Check("Escape keeps the desktop picture until a return frame exists",()=>Assert(HasImage(),"Pin closed before the desktop arrival window had a first frame"));
                await Task.Delay(2000);CompositionTarget.Rendering-=sample;
                Check("return handoff contains no frame with both source and flight absent",()=>Assert(frameCount>10&&gaps==0,$"Observed {gaps} empty frames out of {frameCount}"));
                Check("Escape eventually closes the pin and mounts exactly one photo",()=>Assert(closed&&app.PinCount==0&&store.Items.Count(p=>p==photo)==1&&!app.Windows.OfType<ArrivalFlight>().Any(),"Return did not settle"));
                log.Add($"MEASURE shelf={maxHeight}px, pinFrames={heights.Count}, maxShelf={(heights.Count>0?heights.Max():0)}px, returnFrames={frameCount}, emptyFrames={gaps}");
                var target=new Rect(line.Screen.Bounds.Left+300,line.Screen.Bounds.Bottom-310,360,220);
                var completions=0;
                Assert(line.PreparePinTransfer(photo,target,()=>completions++),"Completion fixture did not prepare");store.TakeDown(photo,quiet:true);await Task.Delay(1200);
                Check("desktop destination completion is invoked exactly once",()=>Assert(completions==1,"Completion count="+completions));
                store.Mount(photo);line.Reveal(true,5);await Task.Delay(1650);
                var cancelled=0;Assert(line.PreparePinTransfer(photo,target,()=>cancelled++),"Cancel fixture did not prepare");store.TakeDown(photo,quiet:true);await Task.Delay(50);line.SuspendForGameMode();await Task.Delay(80);
                Check("cancelling a desktop transfer releases its window and destination exactly once",()=>Assert(cancelled==1&&line.ActiveTransferCount==0&&!app.Windows.OfType<ArrivalFlight>().Any(),"Cancellation retained a surface or callback"));
                var deferred=new PinnedImageWindow(app,photo);double firstOpacity=double.NaN;var firstInput=true;
                deferred.SourceInitialized+=(_,_)=>{firstOpacity=deferred.Opacity;firstInput=deferred.IsHitTestVisible;};
                deferred.ShowOn(line.Screen,true);deferred.UpdateLayout();
                Check("pin target is hidden before its first native show",()=>Assert(firstOpacity==0&&!firstInput,"Destination was shown before hiding it"));
                deferred.FinishTransfer();
                Check("landing clears opacity clocks before showing the pin",()=>Assert(deferred.Opacity==1&&!System.Windows.DependencyPropertyHelper.GetValueSource(deferred,UIElement.OpacityProperty).IsAnimated&&deferred.IsHitTestVisible,"An old opacity clock overrode landing"));
                deferred.CloseWithoutReturn();
                app.Pin(photo);var migrating=app.Windows.OfType<PinnedImageWindow>().Single();await Task.Delay(250);var migrationClosed=false;migrating.Closed+=(_,_)=>migrationClosed=true;
                line.ApplyDisplayLayout(new DisplayGeometry("previous-display",line.Screen.Bounds,Native.Dpi(line.Screen),line.Screen.Primary));
                app.ReturnToLine(photo);
                Check("monitor migration cannot release the pin before its return card exists",()=>Assert(!migrationClosed||app.Windows.OfType<ArrivalFlight>().Any(w=>w.IsVisible&&w.HasPresented),"Display migration cancelled the newly registered source handoff"));
                await Task.Delay(1900);
                Check("return after monitor migration settles without retaining the source",()=>Assert(migrationClosed&&app.PinCount==0&&store.Items.Count(p=>p==photo)==1,"Migrated return retained a source"));
                app.Pin(photo);await Task.Delay(1200);app.ReturnToLine(photo);settings.GameDoNotDisturb=true;line.SuspendForGameMode();line.SuspendForGameMode();await Task.Delay(80);
                Check("cancelling a pending return releases source pins and arrival surfaces",()=>Assert(app.PinCount==0&&line.ActiveTransferCount==0&&!app.Windows.OfType<ArrivalFlight>().Any(),"Cancelled return retained a source or rendering surface"));
                Check("cancelled return preserves the mounted photo and original file",()=>Assert(store.Items.Count(p=>p==photo)==1&&store.Collection.Contains(photo)&&File.Exists(path),"Cancellation lost media"));
                line.Close();
                settings=new Settings { AutoHang=true,NewReveal=false,HoverReveal=false,OverFullscreen=false,Sounds=false,Welcomed=true,CollectionRevision=1 };store=new Store(settings);
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app,settings);typeof(App).GetProperty(nameof(App.Store))!.SetValue(app,store);
                var protectedBounds=System.Windows.Forms.Screen.AllScreens.Select(s=>new ScreenWindow(s.Bounds)).ToArray();
                line=new LineWindow(app,new FullscreenProtection(()=>protectedBounds,()=>0));typeof(App).GetProperty(nameof(App.Line))!.SetValue(app,line);
                store.Hanging+=app.OnPhotoHanging;store.Added+=app.OnPhotoAdded;await store.Add(path);photo=store.Items.Single();app.Pin(photo);
                var protectedPin=app.Windows.OfType<PinnedImageWindow>().Single();var earlyProtectedClose=false;
                protectedPin.Closed+=(_,_)=>{
                    var hasFlight=app.Windows.OfType<ArrivalFlight>().Any(w=>w.IsVisible&&w.HasPresented);
                    var card=((Canvas)line.Content).Children.OfType<Canvas>().Single().Children.OfType<Card>().SingleOrDefault(c=>c.Photo==photo);
                    var hasCard=card is {IsVisible:true}&&((Border)((Grid)card.Children[0]).Children[0]).Opacity>.95;
                    if(!hasFlight&&!hasCard)earlyProtectedClose=true;
                };
                app.ReturnToLine(photo);
                Check("explicit fullscreen return starts with manual reveal",()=>Assert(line.Revealed&&line.IsVisible,"Return did not start manually"));
                typeof(LineWindow).GetMethod("RefreshDisplay",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(line,null);
                Check("DPI maintenance preserves an explicit fullscreen return",()=>Assert(line.Revealed&&line.IsVisible,"Display maintenance reclassified the return as automatic"));
                await Task.Delay(1900);
                Check("explicit return above fullscreen never closes the source before a target frame",()=>Assert(!earlyProtectedClose,"Automatic Hanging cancelled the explicit return before Card creation"));
                Check("explicit return preserves manual reveal and settles above fullscreen",()=>Assert(line.Revealed&&line.IsVisible&&app.PinCount==0&&store.Items.Contains(photo),"Explicit return lost its manual presentation intent"));
                store.TakeDown(photo,quiet:true);line.Retract();await Task.Delay(1400);settings.NewReveal=true;store.Mount(photo);
                Check("manual return intent does not leak into later automatic mounts",()=>Assert(!line.Revealed&&!line.IsVisible,"A later automatic mount bypassed fullscreen protection"));
            } catch(Exception e) { log.Add("FAIL harness: "+e); }
            finally {
                foreach(var window in app.Windows.OfType<PinnedImageWindow>().ToArray()) window.CloseWithoutReturn();line?.Close();
                File.WriteAllLines(Path.Combine(Settings.Root,"results.txt"),log);Console.WriteLine(string.Join(Environment.NewLine,log));app.Shutdown(log.Any(s=>s.StartsWith("FAIL"))?1:0);
            }
        };
        return app.Run();
    }
}
