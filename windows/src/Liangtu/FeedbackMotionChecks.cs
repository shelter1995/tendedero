using System.Reflection;
using System.Windows.Media.Animation;
namespace Liangtu;

internal static class FeedbackMotionChecks
{
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    [STAThread] public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var log = new List<string>();
        void Check(string name, Action action) { try { action(); log.Add("PASS " + name); } catch(Exception e) { log.Add("FAIL " + name + ": " + e.Message); } }
        void Assert(bool value, string message) { if(!value) throw new Exception(message); }
        app.Startup += async (_, _) => {
            LineWindow? line = null; BasketWindow? basket = null; PinnedImageWindow? pin = null;
            try {
                var settings = new Settings { ClipboardImages=true, HandleScreenshots=true, AutoHang=true, HoverReveal=false, NewReveal=false, Sounds=false, Welcomed=true, CollectionRevision=1 };
                var store = new Store(settings);
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app,settings);
                typeof(App).GetProperty(nameof(App.Store))!.SetValue(app,store);
                typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app,true);
                Theme.Select("linen");
                line = new LineWindow(app,new FullscreenProtection(()=>Array.Empty<ScreenWindow>(),()=>0));
                typeof(App).GetProperty(nameof(App.Line))!.SetValue(app,line);
                var visual=new DrawingVisual();
                using(var dc=visual.RenderOpen()) {
                    dc.DrawRectangle(Theme.Brush("#F8F3E9"),null,new Rect(0,0,1800,1000));
                    for(var x=0;x<1800;x+=100) dc.DrawLine(new Pen(Theme.Brush("#DED6C8"),1),new Point(x,0),new Point(x,1000));
                    for(var y=0;y<1000;y+=100) dc.DrawLine(new Pen(Theme.Brush("#DED6C8"),1),new Point(0,y),new Point(1800,y));
                    dc.DrawRectangle(Theme.Brush("#9DAF98"),null,new Rect(110,120,560,600));
                    dc.DrawRectangle(Theme.Brush("#B96549"),null,new Rect(730,120,960,180));
                    dc.DrawText(new FormattedText("原图 1800 × 1000\n桌面贴图 · 默认按像素显示",System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(Theme.Font,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),56,Theme.Brush("#493A2D"),1),new Point(730,380));
                }
                var bitmap=new RenderTargetBitmap(1800,1000,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();
                var path=Path.Combine(Settings.Inbox,"readable-pin.png");MediaTools.SavePng(bitmap,path);await store.Add(path);
                Check("reveal first frame starts hidden without a compositor tick",()=>{
                    var slide=new TranslateTransform();LineMotion.Reveal(slide,-222,true);
                    Assert(!SystemParameters.ClientAreaAnimation || Math.Abs(slide.Y+222)<.001,"First frame jumps to destination: "+slide.Y);
                });
                Check("reversing a retract preserves current presentation immediately",()=>{
                    var slide=new TranslateTransform();var motion=LineMotion.Retract(slide,-222);
                    motion.Controller!.SeekAlignedToLastTick(TimeSpan.FromSeconds(.12),TimeSeekOrigin.BeginTime);var before=slide.Y;
                    LineMotion.Reveal(slide,-222,false);
                    Assert(!SystemParameters.ClientAreaAnimation || Math.Abs(slide.Y-before)<.001,$"Direction change jumps {before} -> {slide.Y}");
                });
                line.Reveal(true,5);await Task.Delay(180);
                typeof(LineWindow).GetField("entered",Private)!.SetValue(line,true);
                line.Reveal(false,2);
                Check("automatic duplicate reveal retains manual intent and pointer entry",()=>{
                    Assert((bool)typeof(LineWindow).GetField("pinned",Private)!.GetValue(line)! && (bool)typeof(LineWindow).GetField("entered",Private)!.GetValue(line)!,"Duplicate reveal resets manual state");
                });
                await Task.Delay(500);
                var photo=store.Items[0];var bounds=line.Screen.Bounds;
                line.ReceiveFromScreen(photo,new Rect(bounds.Left+220,bounds.Top+500,320,180));
                Assert(line.ActiveTransferCount>0,"Transfer fixture did not start");line.Retract();line.Reveal(false,2);await Task.Delay(1500);
                Check("automatic notification cannot erase a retract requested during transfer",()=>Assert(!line.Revealed&&!line.IsVisible,"Retract request was dropped"));
                line.Reveal(true,5);await Task.Delay(100);
                line.ReceiveFromScreen(photo,new Rect(bounds.Left+220,bounds.Top+500,320,180));
                line.Toggle();line.Toggle();await Task.Delay(1400);
                Check("second manual toggle cancels a deferred retract",()=>Assert(line.Revealed&&line.IsVisible,"Latest explicit show lost to queued hide"));
                for(var i=0;i<12;i++) { line.Reveal(true,5);await Task.Delay(35);line.Retract();await Task.Delay(35); }
                line.Reveal(true,5);await Task.Delay(650);
                Check("rapid direction changes settle visible with a full native surface",()=>{
                    var slide=(TranslateTransform)typeof(LineWindow).GetField("slide",Private)!.GetValue(line)!;
                    Native.GetWindowRect(line.Hwnd,out var rect);
                    Assert(line.IsVisible&&line.Revealed&&Math.Abs(slide.Y)<.01&&rect.Right-rect.Left==line.Screen.Bounds.Width,"Stale hide or incomplete reveal");
                });
                line.Retract();
                line.ApplyDisplayLayout(new DisplayGeometry(line.Screen.DeviceName,line.Screen.Bounds,1.5,line.Screen.Primary));
                await Task.Delay(450);
                Check("display change during retract still retires the visible native surface",()=>Assert(!line.IsVisible,"Display reset cancelled the only hide completion"));
                line.MoveTo(line.Screen,manual:true);
                line.SuspendForGameMode();
                var data=new DataObject();data.SetImage(bitmap);var called=false;
                await store.CaptureClipboardData(data,"Snipaste",31,()=>31,(_,_,w,h)=>{called=true;return new Rect(300,400,w,h);});
                Check("third party clipboard images reach the validated origin matcher",()=>Assert(called,"Windows-only source gate bypasses matcher"));
                basket=new BasketWindow(app);basket.Show();basket.UpdateLayout();await Task.Delay(150);
                basket.MediaList.SelectedIndex=0;
                await Task.Delay(250);basket.UpdateLayout();
                Check("linen basket places preview to the right of the gallery",()=>{
                    var preview=(Image)typeof(BasketWindow).GetField("preview",Private)!.GetValue(basket)!;
                    var left=basket.MediaList.TransformToAncestor(basket).TransformBounds(new Rect(basket.MediaList.RenderSize));
                    var right=preview.TransformToAncestor(basket).TransformBounds(new Rect(preview.RenderSize));
                    Assert(right.Left>=left.Right&&right.Width>300&&right.Height>150,$"Gallery {left}, preview {right}");
                });
                Snapshot(basket,Path.Combine(Settings.Root,"linen-basket.png"));basket.Close();basket=null;
                pin=new PinnedImageWindow(app,photo);pin.ShowOn(line.Screen);await Task.Delay(450);pin.UpdateLayout();
                Check("pin initially displays original pixels rather than a 520 DIP thumbnail",()=>{
                    var dpi=Native.Dpi(line.Screen);var area=line.Screen.WorkingArea;
                    var expected=Math.Min(1800/dpi,Math.Min(area.Width/dpi*.8,(area.Height/dpi*.8-90)*1.8));
                    Assert(pin.Width>=expected-30,$"Expected near {expected:F0} DIP, actual {pin.Width:F0}");
                });
                Snapshot(pin,Path.Combine(Settings.Root,"readable-pin.png"));pin.CloseWithoutReturn();pin=null;
            } catch(Exception e) { log.Add("FAIL harness: "+e); }
            finally {
                pin?.CloseWithoutReturn();basket?.Close();line?.Close();
                File.WriteAllLines(Path.Combine(Settings.Root,"results.txt"),log);Console.WriteLine(string.Join(Environment.NewLine,log));
                app.Shutdown(log.Any(x=>x.StartsWith("FAIL"))?1:0);
            }
        };
        return app.Run();
    }
    static void Snapshot(Window window,string path)
    {
        window.UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(window);MediaTools.SavePng(image,path);
    }
}
