using System.Diagnostics;
using System.Reflection;
namespace Liangtu;

internal static class PinMorphChecks
{
    [STAThread] public static int Main(string[] args)
    {
        Settings.Root=Path.GetFullPath(args[0]);Directory.CreateDirectory(Settings.Inbox);
        var app=new App {ShutdownMode=ShutdownMode.OnExplicitShutdown};var log=new List<string>();
        void Check(string name,Action action){try{action();log.Add("PASS "+name);}catch(Exception e){log.Add("FAIL "+name+": "+e.Message);}}
        void Assert(bool yes,string message){if(!yes)throw new Exception(message);}
        void Near(Rect a,Rect b){Assert((a.TopLeft-b.TopLeft).Length<1.5&&Math.Abs(a.Width-b.Width)<1.5&&Math.Abs(a.Height-b.Height)<1.5,$"Frame mismatch: {a} vs {b}");}
        app.Startup+=async(_,_)=>{
            LineWindow? line=null;
            try{
                var settings=new Settings {AutoHang=true,NewReveal=false,HoverReveal=false,Sounds=false,Welcomed=true,CollectionRevision=1};var store=new Store(settings);
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app,settings);typeof(App).GetProperty(nameof(App.Store))!.SetValue(app,store);
                Theme.Select("linen");line=new LineWindow(app,new FullscreenProtection(()=>Array.Empty<ScreenWindow>(),()=>0));typeof(App).GetProperty(nameof(App.Line))!.SetValue(app,line);
                store.Hanging+=app.OnPhotoHanging;store.Added+=app.OnPhotoAdded;
                var image=BitmapSource.Create(1400,860,96,96,PixelFormats.Bgr32,null,Enumerable.Repeat((byte)189,1400*860*4).ToArray(),1400*4);image.Freeze();var path=Path.Combine(Settings.Inbox,"morph.png");MediaTools.SavePng(image,path);await store.Add(path);
                var photo=store.Items.Single();line.Reveal(true,8);await Task.Delay(1650);
                var card=((Canvas)line.Content).Children.OfType<Canvas>().Single().Children.OfType<Card>().Single();
                var source=card.ImageScreenPose().Bounds;var watch=Stopwatch.StartNew();double? pinMoved=null;Rect? pinLastFrame=null;
                EventHandler pinSample=(_,_)=>{var flight=app.Windows.OfType<ArrivalFlight>().FirstOrDefault(w=>w.IsVisible&&w.HasPresented);if(flight==null)return;pinLastFrame=flight.RenderedFrameBounds;if(pinMoved==null&&((flight.PhysicalBounds.TopLeft-source.TopLeft).Length>1||Math.Abs(flight.PhysicalBounds.Width-source.Width)>1))pinMoved=watch.Elapsed.TotalMilliseconds;};
                CompositionTarget.Rendering+=pinSample;
                var qualification=typeof(Card).GetMethod("CommitPegHold",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(card,null);
                Check("qualified peg hold starts pinning synchronously without a second hold",()=>Assert(app.PinCount==1,"Pin request was deferred after the hold qualified"));
                if(qualification is Task pending)await pending;
                await Task.Delay(1300);CompositionTarget.Rendering-=pinSample;
                var pin=app.Windows.OfType<PinnedImageWindow>().Single();pin.UpdateLayout();
                var pinFrame=new Rect(pin.PointToScreen(new Point()),pin.PointToScreen(new Point(pin.ActualWidth,pin.ActualHeight)));
                Check("pinning first movement does not include the old 180 plus 140ms waits",()=>Assert(pinMoved is double ms&&ms<200,"First movement ms="+pinMoved));
                Check("last outbound frame matches the actual pinned panel",()=>{Assert(pinLastFrame!=null,"No pin frame");Near(pinLastFrame!.Value,pinFrame);});
                line.Retract();await Task.Delay(450);var pinPicture=pin.PictureBounds()!.Value;
                Rect? returnFirstFrame=null;double? returnedMoved=null;watch.Restart();
                EventHandler returnSample=(_,_)=>{var flight=app.Windows.OfType<ArrivalFlight>().FirstOrDefault(w=>w.IsVisible&&w.HasPresented);if(flight==null)return;returnFirstFrame??=flight.RenderedFrameBounds;if(returnedMoved==null&&((flight.PhysicalBounds.TopLeft-pinPicture.TopLeft).Length>1||Math.Abs(flight.PhysicalBounds.Width-pinPicture.Width)>1))returnedMoved=watch.Elapsed.TotalMilliseconds;};
                CompositionTarget.Rendering+=returnSample;
                pin.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(pin),Environment.TickCount,Key.Escape){RoutedEvent=Keyboard.KeyDownEvent});
                await Task.Delay(1900);CompositionTarget.Rendering-=returnSample;
                Check("return starts with the pinned panel rather than a scaled theme mat",()=>{Assert(returnFirstFrame!=null,"No return frame");Near(returnFirstFrame!.Value,pinFrame);});
                Check("Escape moves the picture while the shelf is still revealing",()=>Assert(returnedMoved is double ms&&ms<200,"First return movement ms="+returnedMoved));
                log.Add($"MEASURE qualified-pin-motion={pinMoved:F2}ms; Escape-motion={returnedMoved:F2}ms; outbound-last={pinLastFrame}; return-first={returnFirstFrame}; pin-panel={pinFrame}");
                Check("manual pin return moves immediately while ordinary capture still stages its peg",()=>{
                    Assert(ArrivalPhase.PinReturn(.03).Picture>0&&ArrivalPhase.PinReturn(.03).PictureVisible,"Quick return retained a picture lead-in");
                    Assert(!ArrivalPhase.At(.18).PictureVisible&&ArrivalPhase.At(.18).Picture==0,"Ordinary screenshot choreography changed");
                });
                var preview=new PinnedImageWindow(app,photo);preview.ShowOn(line.Screen,true);preview.FinishTransfer();await Task.Delay(180);
                try {
                    foreach(var id in new[]{"linen","color-play","porcelain","darkroom","sky","paper","night"}) {
                        Theme.Select(id);preview.UpdateLayout();var appearance=preview.CaptureAppearance()!;var profile=Theme.Current;
                        Check(id+" pinned footer survives chrome capture",()=>{
                            Assert(appearance.Controls.Children.Count==2&&!appearance.Controls.Children[^1].Bounds.IsEmpty,"Footer capture is empty");
                        });
                        var morph=new FrameMorph(appearance,false,2,2);var inverse=new FrameMorph(appearance,true,2,2);
                        var states=Enumerable.Range(0,101).Select(i=>morph.At(profile,i/100d)).ToArray();
                        Check(id+" frame weights and physical insets stay continuous and bounded",()=>{
                            Assert(states[0].PinWeight==0&&states[^1].PinWeight==1,"Frame endpoints lost");
                            for(var i=1;i<states.Length;i++)Assert(states[i].PinWeight>=states[i-1].PinWeight&&Math.Abs(states[i].Insets.Top-states[i-1].Insets.Top)<20,"Frame jumps");
                            var low=Math.Min(states[0].Insets.Left,states[^1].Insets.Left);var high=Math.Max(states[0].Insets.Left,states[^1].Insets.Left);
                            Assert(states.All(s=>s.Insets.Left>=low-.001&&s.Insets.Left<=high+.001),"Mat grows with image scale");
                            Assert(inverse.At(profile,0).PinWeight==1&&inverse.At(profile,1).PinWeight==0,"Return appearance does not reverse");
                        });
                        var dir=Path.Combine(Settings.Root,"frames",id);Directory.CreateDirectory(dir);
                        int? decoded=null;
                        foreach(var p in new[]{0d,.25,.5,.75,1}) {
                            var bitmap=RenderAppearance(image,appearance,profile,p,out var center,out var matPoint);
                            MediaTools.SavePng(bitmap,Path.Combine(dir,(p*100).ToString("000")+".png"));
                            if(p==0)decoded=RasterThemeArt.DecodeOperations;
                            var reverse=RenderAppearance(image,appearance,profile,p,out _,out _,fromPin:true);
                            MediaTools.SavePng(reverse,Path.Combine(dir,"return-"+(p*100).ToString("000")+".png"));
                            Check(id+" image pixels stay opaque and unchanged at "+p,()=>{
                                var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(center.X,center.Y,1,1),pixel,4,0);
                                Assert(pixel[0]==189&&pixel[1]==189&&pixel[2]==189&&pixel[3]==255,"Photo was crossfaded or covered by chrome");
                            });
                            if(p==.75&&profile.Color("FrameFill").A==255) Check(id+" material blend never opens a transparent hole in an opaque mat",()=>{
                                var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(matPoint.X,matPoint.Y,1,1),pixel,4,0);Assert(pixel[3]>=254,"Mat alpha dipped to "+pixel[3]);
                            });
                        }
                        Check(id+" frame morph reuses decoded theme assets",()=>Assert(decoded==RasterThemeArt.DecodeOperations,"Morph decoded artwork after its first frame"));
                    }
                } finally { preview.CloseWithoutReturn(); }
                Theme.Select("linen");line.Refresh();line.UpdateLayout();Rect? switchedImage=null;double? switchedRadius=null;
                EventHandler switchSample=(_,_)=>{var flight=app.Windows.OfType<ArrivalFlight>().FirstOrDefault(w=>w.IsVisible&&w.HasPresented);if(flight!=null){switchedImage=flight.RenderedScreenBounds;switchedRadius=flight.Appearance?.Radius;}};
                CompositionTarget.Rendering+=switchSample;app.Pin(photo);await Task.Delay(280);
                var changingPin=app.Windows.OfType<PinnedImageWindow>().Single();Theme.Select("color-play");line.Refresh();changingPin.UpdateLayout();await Task.Delay(1050);CompositionTarget.Rendering-=switchSample;
                Check("theme switch during pinning retargets the actual image rectangle",()=>{Assert(switchedImage!=null,"No switched flight");Near(switchedImage!.Value,changingPin.PictureBounds()!.Value);});
                var currentRadius=((MaterialPanel)changingPin.Content).CornerRadius.TopLeft*VisualTreeHelper.GetDpi(changingPin).DpiScaleX;
                Check("theme switch during pinning refreshes the destination border",()=>Assert(switchedRadius is double radius&&Math.Abs(radius-currentRadius)<.1,"Old pinned radius survived theme switch: "+switchedRadius));
                changingPin.CloseWithoutReturn();
            }catch(Exception e){log.Add("FAIL harness: "+e);}
            finally{foreach(var pin in app.Windows.OfType<PinnedImageWindow>().ToArray())pin.CloseWithoutReturn();line?.Close();File.WriteAllLines(Path.Combine(Settings.Root,"results.txt"),log);Console.WriteLine(string.Join(Environment.NewLine,log));app.Shutdown(log.Any(s=>s.StartsWith("FAIL"))?1:0);}
        };return app.Run();
    }
    static BitmapSource RenderAppearance(BitmapSource image,PinAppearance pin,ThemeProfile profile,double progress,out System.Drawing.Point center,out System.Drawing.Point matPoint,bool fromPin=false)
    {
        var size=new Size(200,200d*860/1400);var visual=new ArrivalPhotoVisual(image,0);visual.Configure(profile,size);
        visual.SetMorph(new FrameMorph(pin,fromPin,2,2));
        var t=MotionCurve.Ease(progress);var width=fromPin?1400-(1400-400)*t:400+(1400-400)*t;var height=width*860/1400;
        visual.SetPose(ArrivalPose.FromRect(new Rect(0,0,width,height)),progress);
        var frame=visual.CurrentFrameBounds;var sx=width/size.Width;var sy=height/size.Height;
        var output=new Canvas {Width=Math.Ceiling(frame.Width*sx)+48,Height=Math.Ceiling(frame.Height*sy)+48,Background=Brushes.Transparent};
        visual.RenderTransform=new MatrixTransform(new Matrix(sx,0,0,sy,24-frame.X*sx,24-frame.Y*sy));output.Children.Add(visual);
        output.Measure(new Size(output.Width,output.Height));output.Arrange(new Rect(0,0,output.Width,output.Height));output.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)output.Width,(int)output.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(output);bitmap.Freeze();
        center=new System.Drawing.Point((int)(24+(visual.ImageBounds.X+visual.ImageBounds.Width/2-frame.X)*sx),(int)(24+(visual.ImageBounds.Y+visual.ImageBounds.Height/2-frame.Y)*sy));
        matPoint=new System.Drawing.Point((int)(24+(visual.ImageBounds.X-frame.X)*sx/2),center.Y);return bitmap;
    }
}
