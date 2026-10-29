using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace Liangtu;

internal static class MotionPolishChecks
{
    [DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr window, IntPtr region);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var log = new List<string>();
        void Test(string name, Action body) { try { body(); log.Add("PASS " + name); } catch(Exception error) { log.Add("FAIL " + name + ": " + error); } }
        void Assert(bool okay,string reason) { if(!okay) throw new Exception(reason); }
        app.Startup += async (_,_) => {
            Window? fixture = null; LineWindow? line = null;
            try {
                var settings = new Settings { Sounds=false, NewReveal=false, HoverReveal=false, Keep=100 };
                var store = new Store(settings); typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app,settings); typeof(App).GetProperty(nameof(App.Store))!.SetValue(app,store); typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app,true);
                var bounds=System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
                var fixtureBrush=new SolidColorBrush(Color.FromRgb(31,91,141));
                fixture = new Window { Title="晾图透明度基准测试", Width=bounds.Width/Native.Dpi(System.Windows.Forms.Screen.PrimaryScreen!),Height=bounds.Height/Native.Dpi(System.Windows.Forms.Screen.PrimaryScreen!), Content=new Border { Background=fixtureBrush }, WindowStyle=WindowStyle.None, ResizeMode=ResizeMode.NoResize, Background=fixtureBrush, Topmost=true, ShowActivated=false, ShowInTaskbar=true };
                fixture.Show(); Native.SetWindowPos(new WindowInteropHelper(fixture).Handle,new IntPtr(-1),bounds.Left,bounds.Top,bounds.Width,bounds.Height,0x10);
                await Task.Delay(120);
                var point=new System.Drawing.Point(bounds.Left+bounds.Width/2,bounds.Top+Math.Min(600,bounds.Height-30));
                fixture.UpdateLayout();var fixtureBitmap=new RenderTargetBitmap((int)fixture.ActualWidth,(int)fixture.ActualHeight,96,96,PixelFormats.Pbgra32);fixtureBitmap.Render(fixture);MediaTools.SavePng(fixtureBitmap,Path.Combine(Settings.Root,"fixture.png"));
                log.Add($"MEASURE fixture WPF BGRA={string.Join(',',Pixel(fixtureBitmap,10,10))}, physicalHwnd={new WindowInteropHelper(fixture).Handle}");
                log.Add("LIMIT native desktop pixels were not verified: WGC app approval timed out; prior GDI fixture pixels did not match the owned reference. No alternate desktop capture used.");
                Theme.Select("sky");
                line=new LineWindow(app,new FullscreenProtection(()=>Array.Empty<ScreenWindow>(),()=>0)); typeof(App).GetProperty(nameof(App.Line))!.SetValue(app,line);
                var bitmap=BitmapSource.Create(160,100,96,96,PixelFormats.Bgra32,null,Enumerable.Repeat((byte)255,160*100*4).ToArray(),640);bitmap.Freeze();
                var photo=new Photo { Path=Path.Combine(Settings.Root,"owned-photo.png"),Image=bitmap }; MediaTools.SavePng(bitmap,photo.Path);store.Collection.Add(photo);store.Items.Add(photo);line.Refresh();
                async Task Observe(string name) {
                    line.Reveal(true,5);line.UpdateLayout(); await Task.Delay(650);
                    var bmp=new RenderTargetBitmap((int)Math.Ceiling(line.Width),(int)Math.Ceiling(line.Height),96,96,PixelFormats.Pbgra32);bmp.Render(line);
                    var local=line.PointFromScreen(new Point(point.X,point.Y));var alpha=Pixel(bmp,(int)local.X,(int)local.Y)[3];
                    var glass=(GlassBackdrop)typeof(LineWindow).GetField("glass",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(line)!;
                    var hwnd=(IntPtr)typeof(GlassBackdrop).GetField("handle",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(glass)!;
                    var region=CreateRectRgn(0,0,0,0);var regionKind=GetWindowRgn(hwnd,region);Native.GetWindowRect(hwnd,out var nativeBounds);
                    var glassCovers=GlassBackdrop.RegionContains(region,point.X-nativeBounds.Left,point.Y-nativeBounds.Top);Native.DeleteObject(region);
                    var background=((SolidColorBrush)line.Background).Color;
                    var target=HwndSource.FromHwnd(line.Hwnd).CompositionTarget.BackgroundColor;
                    var cornerStatus=DwmGetWindowAttribute(line.Hwnd,33,out var corners,4);var backdropStatus=DwmGetWindowAttribute(line.Hwnd,38,out var backdrop,4);var nonclientStatus=DwmGetWindowAttribute(line.Hwnd,1,out var nonclient,4);
                    log.Add($"MEASURE {name}: WPFalpha={alpha}, WindowBackgroundA={background.A}, CompositionA={target.A}, glassVisible={IsWindowVisible(hwnd)}, glassRegion={regionKind}, glassCoversBlank={glassCovers}, DWMcorner={corners}/{cornerStatus}, backdrop={backdrop}/{backdropStatus}, nonclient={nonclient}/{nonclientStatus}");
                    Test(name+" has zero-alpha empty pixels and a bounded native glass region",()=>Assert(alpha==0&&background.A==0&&target.A==0&&!glassCovers,"Transparent area painted or covered"));
                    Test(name+" native overlay has no DWM corner/backdrop/nonclient decoration",()=>Assert(cornerStatus==0&&backdropStatus==0&&nonclientStatus==0&&corners==1&&backdrop==1&&nonclient==0,"Native overlay decoration survives"));
                    MediaTools.SavePng(bmp,Path.Combine(Settings.Root,name+"-alpha.png"));
                }
                await Observe("initial-sky");
                if(args.Contains("--hold")) await Task.Delay(45000);
                foreach(var theme in new[]{"linen","color-play","darkroom","porcelain","sky"}) { Theme.Select(theme);line.Refresh();await Observe("shown-"+theme);line.SuspendForGameMode();Theme.Select(theme);line.Refresh();await Observe("hidden-"+theme); }
                foreach(var theme in new[]{"sky","paper","night","porcelain","darkroom","linen","color-play"}) {
                    Theme.Select(theme);line.Refresh();line.UpdateLayout();
                    var card=((Canvas)line.Content).Children.OfType<Canvas>().Single().Children.OfType<Card>().Single();card.FinishArrival();
                    var cardFrame=(GlassBorder)((Grid)card.Children[0]).Children[0];
                    var reflow=LineMotion.Reflow(card);reflow.BeginAnimation(TranslateTransform.XProperty,null);reflow.BeginAnimation(TranslateTransform.YProperty,null);reflow.X=reflow.Y=0;
                    foreach(var transform in ((TransformGroup)cardFrame.RenderTransform).Children.OfType<ScaleTransform>()) { transform.BeginAnimation(ScaleTransform.ScaleXProperty,null);transform.BeginAnimation(ScaleTransform.ScaleYProperty,null);transform.ScaleX=transform.ScaleY=1; }
                    cardFrame.UpdateLayout();
                    var image=(Image)((Grid)cardFrame.Child).Children[0];var size=image.RenderSize;var targetPose=card.ImageScreenPose();
                    var source=new Rect(bounds.Left+180,bounds.Top+700,320,200);
                    var flight=new ArrivalFlight(bitmap,line,source,targetPose,size,cardFrame.ArtVariant);flight.Prepare();
                    try {
                        flight.Present(ArrivalPose.FromRect(source),size);flight.UpdateLayout();
                        Test(theme+" first carries its frame around the exact capture pixels",()=>Assert(Near(flight.RenderedScreenBounds,source)&&flight.RenderedFrameBounds.Contains(flight.RenderedScreenBounds)&&flight.RenderedFrameBounds.Width>source.Width&&flight.RenderedFrameBounds.Height>source.Height,"Frame covers/shrinks source content"));
                        var builds=flight.ArtworkBuildCount;
                        for(int frameIndex=1;frameIndex<60;frameIndex++)flight.Present(ArrivalPose.Travel(source,targetPose,frameIndex/60d),size);
                        flight.Present(targetPose,size);flight.UpdateLayout();
                        var cardRect=cardFrame.TransformToAncestor(line).TransformBounds(new Rect(cardFrame.RenderSize));
                        var targetFrame=new MatrixTransform(ArrivalPose.WindowToScreen(line)).TransformBounds(cardRect);
                        log.Add($"MEASURE {theme} imageExpected={targetPose.Bounds} imageActual={flight.RenderedScreenBounds} frameExpected={targetFrame} frameActual={flight.RenderedFrameBounds} size={size} mat={Theme.Current.PhotoMat}");
                        Test(theme+" flight hands off both the tilted image and its mat without a jump",()=>Assert(Near(flight.RenderedScreenBounds,targetPose.Bounds)&&Near(flight.RenderedFrameBounds,targetFrame),"Content/frame endpoint does not match Card"));
                        Test(theme+" frame drawing is retained across sixty flight frames",()=>Assert(flight.ArtworkBuildCount==builds&&flight.SurfaceExpansionCount==0,"Flight rebuilds art or resizes native surface per frame"));
                        DwmGetWindowAttribute(new WindowInteropHelper(flight).Handle,33,out var flightCorners,4);DwmGetWindowAttribute(new WindowInteropHelper(flight).Handle,38,out var flightBackdrop,4);
                        Test(theme+" flight is also a decoration-free native overlay",()=>Assert(flightCorners==1&&flightBackdrop==1,"Flight reintroduced DWM background"));
                        var rendered=new ArrivalPhotoVisual(bitmap,cardFrame.ArtVariant);rendered.Configure(Theme.Current,size);rendered.Effect=null;
                        rendered.Measure(new Size(rendered.Width,rendered.Height));rendered.Arrange(new Rect(0,0,rendered.Width,rendered.Height));rendered.UpdateLayout();
                        var picture=new RenderTargetBitmap((int)Math.Ceiling(rendered.Width),(int)Math.Ceiling(rendered.Height),96,96,PixelFormats.Pbgra32);picture.Render(rendered);MediaTools.SavePng(picture,Path.Combine(Settings.Root,theme+"-framed-flight.png"));
                        Test(theme+" framed preview preserves photograph pixels",()=>Assert(Pixel(picture,(int)rendered.ImageBounds.X+(int)rendered.ImageBounds.Width/2,(int)rendered.ImageBounds.Y+(int)rendered.ImageBounds.Height/2).All(value=>value==255),"Frame renderer modifies image content"));
                    } finally { flight.Close(); }
                }
                Test("flight surface clipping preserves negative virtual-desktop origins",()=>{
                    var virtualBounds=new System.Drawing.Rectangle(-3840,-1440,6400,3600);
                    Assert(ArrivalFlight.ClipSurface(new Rect(-4500,-2000,8000,5000),virtualBounds)==virtualBounds,"Large envelope escaped negative-origin desktop");
                    Assert(ArrivalFlight.ClipSurface(new Rect(-4000,-1500,300,300),virtualBounds)==new System.Drawing.Rectangle(-3840,-1440,140,240),"Partially visible negative-origin envelope was shifted");
                    var outside=ArrivalFlight.ClipSurface(new Rect(-9000,-8000,500,400),virtualBounds);
                    Assert(virtualBounds.Contains(outside)&&outside.Width==1&&outside.Height==1,"Invisible pose allocated an off-screen surface");
                });
                var virtualScreen=System.Windows.Forms.SystemInformation.VirtualScreen;
                var finalCard=((Canvas)line.Content).Children.OfType<Canvas>().Single().Children.OfType<Card>().Single();
                var finalFrame=(GlassBorder)((Grid)finalCard.Children[0]).Children[0];var finalImage=(Image)((Grid)finalFrame.Child).Children[0];
                foreach(var capture in new[]{new Rect(virtualScreen.Left,virtualScreen.Top,virtualScreen.Width,virtualScreen.Height),new Rect(virtualScreen.Left-160,virtualScreen.Top-110,500,320)}) {
                    var targetPose=finalCard.ImageScreenPose();var flight=new ArrivalFlight(bitmap,line,capture,targetPose,finalImage.RenderSize,finalFrame.ArtVariant);flight.Prepare();
                    try {
                        flight.Present(ArrivalPose.FromRect(capture),finalImage.RenderSize);flight.UpdateLayout();
                        Native.GetWindowRect(new WindowInteropHelper(flight).Handle,out var surface);
                        var actual=System.Drawing.Rectangle.FromLTRB(surface.Left,surface.Top,surface.Right,surface.Bottom);
                        Test($"large/partial capture {capture.Width}x{capture.Height} uses only visible desktop raster",()=>Assert(virtualScreen.Contains(actual)&&actual==flight.SurfaceBounds&&Near(flight.RenderedScreenBounds,capture),"HWND oversized or global image pose changed"));
                        log.Add($"MEASURE bounded framed flight: source={capture}, HWND={actual}, virtualDesktop={virtualScreen}, dpi={Native.GetDpiForWindow(new WindowInteropHelper(flight).Handle)}");
                        for(int tick=0;tick<=120;tick++)flight.Present(ArrivalPose.Travel(capture,targetPose,tick/120d),finalImage.RenderSize);
                        Test($"large/partial capture {capture.Width}x{capture.Height} does not resize its HWND every frame",()=>Assert(flight.SurfaceExpansionCount==0&&virtualScreen.Contains(flight.SurfaceBounds),"Clipped frame repeatedly expanded native surface"));
                        var before=flight.SurfaceBounds;var expansions=flight.SurfaceExpansionCount;
                        var offscreen=ArrivalPose.FromRect(new Rect(virtualScreen.Left-1200,virtualScreen.Top-1200,100,80));
                        for(int tick=0;tick<30;tick++)flight.Present(offscreen,finalImage.RenderSize);
                        Test($"off-screen drawing after {capture.Width}x{capture.Height} leaves native surface stationary",()=>Assert(flight.SurfaceBounds==before&&flight.SurfaceExpansionCount==expansions,"Invisible frame moved the HWND"));
                        if(capture.Width<virtualScreen.Width) {
                            var edgePose=ArrivalPose.FromRect(new Rect(virtualScreen.Right-80,virtualScreen.Bottom-60,300,200));
                            for(int tick=0;tick<30;tick++)flight.Present(edgePose,finalImage.RenderSize);
                            Native.GetWindowRect(new WindowInteropHelper(flight).Handle,out var expandedNative);
                            var expandedActual=System.Drawing.Rectangle.FromLTRB(expandedNative.Left,expandedNative.Top,expandedNative.Right,expandedNative.Bottom);
                            Test("flight expansion clips at virtual desktop edges and occurs only once",()=>Assert(flight.SurfaceExpansionCount==expansions+1&&virtualScreen.Contains(expandedActual)&&expandedActual==flight.SurfaceBounds&&Near(flight.RenderedScreenBounds,edgePose.Bounds),"Expansion grew off-screen or repeated every frame"));
                        }
                    } finally { flight.Close(); }
                }
                var originPhoto=new Photo { Path=Path.Combine(Settings.Root,"arrival-cancel.png"),Image=bitmap,Origin=new Rect(bounds.Left+150,bounds.Top+700,320,200),ArrivalPending=true,ArrivalStarted=false };
                store.Items.Clear();store.Items.Add(originPhoto);line.Refresh();line.Reveal(true,4);await Task.Delay(100);
                var arriving=((Canvas)line.Content).Children.OfType<Canvas>().Single().Children.OfType<Card>().Single(card=>card.Photo==originPhoto);
                Test("known screenshot is framed at its source before Bezier travel begins",()=>Assert(!SystemParameters.ClientAreaAnimation||arriving.ArrivalFrameBounds!=null&&Near(arriving.ArrivalRenderedBounds!.Value,new Rect(bounds.Left+150,bounds.Top+700,320,200)),"No framed source lead-in"));
                line.SuspendForGameMode();
                Test("arrival cancellation retires its native framed surface",()=>Assert(arriving.ArrivalFlightBounds==null&&!app.Windows.OfType<ArrivalFlight>().Any()&&!line.IsVisible,"Cancelled flight remains visible"));
            } catch(Exception error) { log.Add("FAIL harness: "+error); }
            finally { line?.Close();fixture?.Close();File.WriteAllLines(Path.Combine(Settings.Root,"results.txt"),log);Console.WriteLine(string.Join(Environment.NewLine,log));app.Shutdown(log.Any(item=>item.StartsWith("FAIL"))?1:0); }
        };
        return app.Run();
    }
    static byte[] Pixel(BitmapSource source,int x,int y)
    {
        var converted=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var pixel=new byte[4];converted.CopyPixels(new Int32Rect(x,y,1,1),pixel,4,0);return pixel;
    }
    static bool Near(Rect first,Rect second)=>Math.Abs(first.X-second.X)<1&&Math.Abs(first.Y-second.Y)<1&&Math.Abs(first.Width-second.Width)<1&&Math.Abs(first.Height-second.Height)<1;
}
