namespace Liangtu;

// Owned clipboard objects; no hooks, clipboard access or input injection.
internal static class ClipboardFlightChecks
{
    [STAThread] public static int Main(string[] args)
    {
        Settings.Root=Path.GetFullPath(args[0]);Directory.CreateDirectory(Settings.Inbox);
        var app=new App { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        var log=new List<string>();
        void Test(string name,Action action) { try { action();log.Add("PASS "+name); } catch(Exception e) { log.Add("FAIL "+name+": "+e.Message); } }
        void Assert(bool okay,string message) { if(!okay) throw new Exception(message); }
        void Near(Rect? actual,Rect expected) { Assert(actual is Rect a&&Math.Abs(a.X-expected.X)<.001&&Math.Abs(a.Y-expected.Y)<.001&&Math.Abs(a.Width-expected.Width)<.001&&Math.Abs(a.Height-expected.Height)<.001,$"Expected {expected}; actual {actual}"); }
        app.Startup+=async(_,_)=>{
            try {
                var store=new Store(new Settings { ClipboardImages=true,HandleScreenshots=true,AutoHang=true,Sounds=false,CollectionRevision=1 });
                var image=BitmapSource.Create(1896,1173,96,96,PixelFormats.Bgr32,null,Enumerable.Repeat((byte)209,1896*1173*4).ToArray(),1896*4);image.Freeze();
                var data=new DataObject();data.SetImage(image);
                await store.CaptureClipboardEvent(()=>"snipaste",()=>128,()=>data,_=>Task.CompletedTask);
                var photo=store.Collection.Single();
                Test("Snipaste automatic selection and copy click receive a desktop flight origin without a drag",()=>Assert(photo.Origin!=null,"No measured drag: clipboard image still uses the shelf pop-in"));
                Test("pointer-derived entry is never labelled a measured Windows selection",()=>Assert(!photo.ValidatedWindowsCapture,"Reference pointer origin was mislabelled as a verified selection"));
                var screen=new System.Drawing.Rectangle(0,0,3840,2160);
                Test("reference physical 4K rectangle uses original dimensions at the Snipaste copy point",()=>Near(new ClipboardFlightAnchor(new Point(3147,1001),screen).ForImage(1896,1173),new Rect(1251,0,1896,1173)));
                Test("reference large image is capped to seventy percent of the screen",()=>Near(new ClipboardFlightAnchor(new Point(3500,2000),screen).ForImage(4000,3000),new Rect(1484,488,2016,1512)));
                Test("reference whole-monitor capture bypasses the seventy percent cap",()=>Near(new ClipboardFlightAnchor(new Point(100,200),screen).ForImage(3840,2160),new Rect(0,0,3840,2160)));
                Test("reference negative-origin monitor keeps physical coordinates",()=>Near(new ClipboardFlightAnchor(new Point(-101,-90),new System.Drawing.Rectangle(-2560,-300,2560,1440)).ForImage(1000,500),new Rect(-1101,-300,1000,500)));
                Test("reference offset monitor clamps to that monitor rather than primary origin",()=>Near(new ClipboardFlightAnchor(new Point(4000,300),new System.Drawing.Rectangle(3840,120,2560,1440)).ForImage(1000,500),new Rect(3840,120,1000,500)));
                Test("reference invalid image dimensions never construct a rectangle",()=>Assert(new ClipboardFlightAnchor(new Point(100,200),screen).ForImage(0,500)==null,"Invalid dimensions accepted"));
                var point=new Point(3147,1001);var reads=0;
                var delayedStore=new Store(new Settings { ClipboardImages=true,HandleScreenshots=true,AutoHang=true,Sounds=false,CollectionRevision=1 });
                await delayedStore.CaptureClipboardEvent(()=>"snipaste",()=>201,()=>data,_=>{point=new Point(3800,2000);return Task.CompletedTask;},
                    eventPointer:()=>{reads++;return point;},eventScreen:_=>screen);
                Test("clipboard event freezes the pointer before delayed decoding",()=>{Near(delayedStore.Collection.Single().Origin,new Rect(1251,0,1896,1173));Assert(reads==1,"Pointer was resampled after asynchronous work");});
                Test("thumbnail decoding never replaces original flight dimensions",()=>Assert(delayedStore.Collection[0].ThumbnailWidth==600&&delayedStore.Collection[0].Origin!.Value.Width==1896,"Source rectangle used thumbnail size"));
                uint sequence=220;
                var changedStore=new Store(new Settings { ClipboardImages=true,HandleScreenshots=true,AutoHang=true,Sounds=false,CollectionRevision=1 });
                await changedStore.CaptureClipboardEvent(()=>"snipaste",()=>sequence,()=>data,_=>{sequence=221;return Task.CompletedTask;},eventPointer:()=>new Point(3147,1001),eventScreen:_=>screen);
                Test("a newer clipboard sequence cannot borrow an older event point",()=>Assert(changedStore.Collection.Single().Origin==null,"Old pointer snapshot leaked to a new sequence"));
                var anchor=new ClipboardFlightAnchor(new Point(3147,1001),screen);
                var measuredStore=new Store(new Settings { ClipboardImages=true,HandleScreenshots=true,AutoHang=true,Sounds=false,CollectionRevision=1 });
                var measured=new Rect(600,400,1896,1173);
                await measuredStore.CaptureClipboardData(data,"snipaste",240,()=>240,(_,_,_,_)=>measured,anchor);
                Test("a measured rectangle takes priority over the reference pointer",()=>{Near(measuredStore.Collection[0].Origin,measured);Assert(measuredStore.Collection[0].ValidatedWindowsCapture,"Measured origin lost its proof");});
                foreach(var excluded in new[]{"Text","ExcludeClipboardContentFromMonitorProcessing","Liangtu.Internal"}) {
                    var rejected=new DataObject();rejected.SetImage(image);rejected.SetData(excluded,"fixture");
                    var rejectedStore=new Store(new Settings { ClipboardImages=true,HandleScreenshots=true,AutoHang=true,Sounds=false,CollectionRevision=1 });
                    await rejectedStore.CaptureClipboardData(rejected,"snipaste",260,()=>260,eventAnchor:anchor);
                    Test("reference origin respects clipboard filter "+excluded,()=>Assert(rejectedStore.Collection.Count==0,"Excluded image was imported"));
                }
                var quietStore=new Store(new Settings { ClipboardImages=true,HandleScreenshots=true,AutoHang=true,GameDoNotDisturb=true,Sounds=false,CollectionRevision=1 });
                await quietStore.CaptureClipboardData(data,"snipaste",280,()=>280,eventAnchor:anchor);
                Test("game mode accepts quietly without a pointer-derived flight",()=>Assert(quietStore.Collection.Single().Origin==null,"Game mode acquired a presentation anchor"));
                var disabledStore=new Store(new Settings { ClipboardImages=false,HandleScreenshots=true,AutoHang=true,Sounds=false,CollectionRevision=1 });
                await disabledStore.CaptureClipboardData(data,"snipaste",290,()=>290,eventAnchor:anchor);
                Test("reference origin never enables disabled general clipboard intake",()=>Assert(disabledStore.Collection.Count==0,"General image collection was enabled implicitly"));
                var basketStore=new Store(new Settings { ClipboardImages=true,HandleScreenshots=true,AutoHang=false,Sounds=false,CollectionRevision=1 });
                var forced=0;basketStore.WindowsCaptureAccepted+=_=>forced++;
                await basketStore.CaptureClipboardData(data,"snipaste",300,()=>300,eventAnchor:anchor);
                Test("AutoHang off retains the image in the basket without forcing reveal",()=>Assert(basketStore.Collection.Count==1&&basketStore.Items.Count==0&&forced==0,"Pointer-derived import forced a reveal"));
                var appSettings=new Settings { ClipboardImages=true,HandleScreenshots=true,AutoHang=true,NewReveal=true,HoverReveal=false,Sounds=false,CollectionRevision=1 };
                var appStore=new Store(appSettings);
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app,appSettings);typeof(App).GetProperty(nameof(App.Store))!.SetValue(app,appStore);typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app,true);
                Theme.Select("linen");
                var line=new LineWindow(app,new FullscreenProtection(()=>Array.Empty<ScreenWindow>(),()=>0));typeof(App).GetProperty(nameof(App.Line))!.SetValue(app,line);
                try {
                    appStore.Hanging+=app.OnPhotoHanging;appStore.Added+=app.OnPhotoAdded;app.ConfigureWindowsSnipIntake(null);
                    var bounds=line.Screen.Bounds;var at=new Point(bounds.Left+bounds.Width*.82,bounds.Top+bounds.Height*.46);
                    var expected=new ClipboardFlightAnchor(at,bounds).ForImage(image.PixelWidth,image.PixelHeight)!.Value;
                    var samples=new List<(Rect physical,Rect rendered)>();
                    EventHandler sample=(_,_)=>{
                        var card=((Canvas)line.Content).Children.OfType<Canvas>().Single().Children.OfType<Card>().SingleOrDefault();
                        if(card?.ArrivalFlightBounds is Rect physical&&card.ArrivalRenderedBounds is Rect rendered) samples.Add((physical,rendered));
                    };
                    CompositionTarget.Rendering+=sample;
                    try {
                        await appStore.CaptureClipboardEvent(()=>"snipaste",()=>340,()=>data,_=>Task.CompletedTask,eventPointer:()=>at,eventScreen:_=>bounds);
                        await Task.Delay(1900);
                    } finally { CompositionTarget.Rendering-=sample; }
                    Test("unmeasured third-party clipboard uses an actual desktop flight window",()=>Assert(line.Revealed&&samples.Count>3,"No rendered desktop flight: "+samples.Count));
                    Test("first rendered flight rectangle matches the frozen reference origin",()=>{Assert(samples.Count>0,"No source frame");Near(samples[0].physical,expected);Near(samples[0].rendered,expected);});
                    Test("physical source stays unchanged by the monitor DPI conversion",()=>Assert(samples.All(s=>Math.Abs(s.physical.X-s.rendered.X)<1&&Math.Abs(s.physical.Y-s.rendered.Y)<1&&Math.Abs(s.physical.Width-s.rendered.Width)<1&&Math.Abs(s.physical.Height-s.rendered.Height)<1),"DPI conversion moved/scaled the source twice"));
                    Test("reference entry finishes and retires its native flight window",()=>Assert(!app.Windows.OfType<ArrivalFlight>().Any(),"Reference flight did not complete"));
                    File.WriteAllText(Path.Combine(Settings.Root,"rendered-origin.txt"),$"dpi={Native.Dpi(line.Screen)}; source={expected}; frames={samples.Count}; first={(samples.Count>0?samples[0].rendered:null)}");
                } finally { line.Close(); }
                var waitingSettings=new Settings { ClipboardImages=true,HandleScreenshots=true,AutoHang=true,NewReveal=false,HoverReveal=false,Sounds=false,CollectionRevision=1 };
                var waitingStore=new Store(waitingSettings);
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app,waitingSettings);typeof(App).GetProperty(nameof(App.Store))!.SetValue(app,waitingStore);
                var waitingLine=new LineWindow(app,new FullscreenProtection(()=>Array.Empty<ScreenWindow>(),()=>0));typeof(App).GetProperty(nameof(App.Line))!.SetValue(app,waitingLine);
                try {
                    waitingStore.Hanging+=app.OnPhotoHanging;waitingStore.Added+=app.OnPhotoAdded;app.ConfigureWindowsSnipIntake(null);
                    await waitingStore.CaptureClipboardEvent(()=>"snipaste",()=>360,()=>data,_=>Task.CompletedTask,eventPointer:()=>new Point(3147,1001),eventScreen:_=>screen);
                    var waitingCard=((Canvas)waitingLine.Content).Children.OfType<Canvas>().Single().Children.OfType<Card>().Single();
                    Assert(!waitingLine.IsVisible&&!waitingStore.Collection[0].ArrivalStarted,"Pending-origin fixture was already shown");
                    var actualSelection=new Rect(400,300,1896,1173);
                    await waitingStore.CaptureClipboardData(data,"SnippingTool",361,()=>361,(_,_,_,_)=>actualSelection);
                    Test("a later measured selection replaces an unshown pointer-derived card origin",()=>{Assert(waitingStore.Collection.Count==1,"Duplicate capture created another photo");Near(waitingCard.PendingArrivalOrigin,actualSelection);});
                } finally { waitingLine.Close(); }
                var repeatStore=new Store(new Settings { ClipboardImages=true,HandleScreenshots=true,AutoHang=true,Sounds=false,CollectionRevision=1 });
                await repeatStore.CaptureClipboardEvent(()=>"snipaste",()=>380,()=>data,_=>Task.CompletedTask,eventPointer:()=>new Point(3147,1001),eventScreen:_=>screen);
                ((System.Collections.IList)typeof(Store).GetField("recentCaptures",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(repeatStore)!).Clear();
                await repeatStore.CaptureClipboardEvent(()=>"snipaste",()=>381,()=>data,_=>Task.CompletedTask,eventPointer:()=>new Point(2800,1400),eventScreen:_=>screen);
                Test("ordinary copied images retain hash deduplication after the short capture window expires",()=>Assert(repeatStore.Collection.Count==1,"Pointer-derived origin bypassed ordinary clipboard deduplication"));
            } catch(Exception e) { log.Add("FAIL harness: "+e); }
            finally {
                File.WriteAllLines(Path.Combine(Settings.Root,"results.txt"),log);Console.WriteLine(string.Join(Environment.NewLine,log));app.Shutdown(log.Any(x=>x.StartsWith("FAIL"))?1:0);
            }
        };
        return app.Run();
    }
}
