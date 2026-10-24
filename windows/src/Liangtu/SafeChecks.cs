using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
namespace Liangtu;
public static class SafeChecks
{
    [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("ole32.dll")] static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern uint DragQueryFile(IntPtr drop,uint index,System.Text.StringBuilder name,uint capacity);
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(Settings.Root);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            var log = new List<string>();
            void Test(string name, Action body)
            {
                try
                {
                    body();
                    log.Add("PASS " + name);
                }
                catch (Exception e) { log.Add("FAIL " + name + ": " + e); }
            }
            void Assert(bool ok)
            {
                if (!ok)
                    throw new InvalidOperationException("Assertion failed");
            }
            ClipboardChecks.Run(Test);
            EditorChecks.Run(Test);
            log.AddRange(await ClipboardWriteChecks.Run());
            Test("native glass input region excludes desktop and gaps", () =>
            {
                var region = GlassBackdrop.BuildInputRegion(new[] { new[] { new Point(100, 20), new Point(200, 20), new Point(200, 120), new Point(100, 120) }, new[] { new Point(300, 30), new Point(400, 30), new Point(400, 130), new Point(300, 130) } });
                try
                {
                    Assert(GlassBackdrop.RegionContains(region, 150, 60));
                    Assert(GlassBackdrop.RegionContains(region, 350, 60));
                    Assert(!GlassBackdrop.RegionContains(region, 250, 60));
                    Assert(!GlassBackdrop.RegionContains(region, 950, 600));
                    Assert(!GlassBackdrop.RegionContains(region, 0, 0));
                }
                finally { Native.DeleteObject(region); }
            });
            Test("empty native glass input region accepts no input", () => { var r = GlassBackdrop.BuildInputRegion(Array.Empty<Point[]>()); try { Assert(!GlassBackdrop.RegionContains(r, 0, 0)); Assert(!GlassBackdrop.RegionContains(r, 500, 500)); } finally { Native.DeleteObject(r); } });
            var testPixels=Enumerable.Repeat((byte)255,32*24*4).ToArray();
            var testBitmap=BitmapSource.Create(32,24,96,96,PixelFormats.Bgra32,null,testPixels,128);
            var testPath=Path.Combine(Settings.Root,"payload.png");MediaTools.SavePng(testBitmap,testPath);
            var payload=new DataObject();payload.SetData(DataFormats.FileDrop,new[]{testPath});payload.SetImage(testBitmap);
            var data = new ShellDataObject(payload);
            Test("Shell wrapper preserves file-drop payload",()=>{
                var f=new FORMATETC{cfFormat=15,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL};data.GetData(ref f,out var medium);
                try{var path=new System.Text.StringBuilder(32768);DragQueryFile(medium.unionmember,0,path,(uint)path.Capacity);Assert(path.ToString()==testPath);}finally{ReleaseStgMedium(ref medium);}
            });
            Test("Shell wrapper preserves bitmap dimensions",()=>{
                var f=new FORMATETC{cfFormat=2,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_GDI};data.GetData(ref f,out var medium);
                try{var image=System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(medium.unionmember,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());Assert(image.PixelWidth==32&&image.PixelHeight==24);}finally{ReleaseStgMedium(ref medium);}
            });
            Test("Shell backing store retains private drag format with owned copy", () =>
            {
                var format = new FORMATETC { cfFormat = (short)DataFormats.GetDataFormat("Liangtu.SafeTest").Id, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
                var memory = GlobalAlloc(0x42, (UIntPtr)4);
                var pointer = GlobalLock(memory);
                Marshal.WriteInt32(pointer, 123456);
                GlobalUnlock(memory);
                var medium = new STGMEDIUM { tymed = TYMED.TYMED_HGLOBAL, unionmember = memory, pUnkForRelease = null };
                try
                {
                    data.SetData(ref format, ref medium, false);
                }
                finally { ReleaseStgMedium(ref medium); }
                data.GetData(ref format, out var copy);
                try
                {
                    var p = GlobalLock(copy.unionmember);
                    Assert(Marshal.ReadInt32(p) == 123456);
                    GlobalUnlock(copy.unionmember);
                }
                finally { ReleaseStgMedium(ref copy); }
            });
            Test("Shell drag image initialization requires no mouse capture", () =>
            {
                var pixels = Enumerable.Repeat((byte)255, 32 * 24 * 4).ToArray();
                var bitmap = BitmapSource.Create(32, 24, 96, 96, PixelFormats.Bgra32, null, pixels, 128);
                using var preview = new DragImage(bitmap, data, 32, 24, 1);
                Assert(preview.Initialized);
                Assert(Mouse.Captured == null);
            });
            Test("original eleven language resources load", () => { Assert(Localization.LanguageCount == 11); Assert(Localization.Text("Copied", "en") == "Copied"); Assert(Localization.Text("Copied", "zh-CN") == "已复制"); Assert(Localization.Text("Copied", "fr") != "Copied"); });
            Test("formal hidden window is capturable and compositor initializes", () =>
            {
                typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, new Settings());
                typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, new Store(app.Settings));
                var line = new LineWindow(app);
                typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, line);
                try
                {
                    Assert(!line.IsVisible);
                    Assert(Native.GetWindowDisplayAffinity(line.Hwnd, out var affinity) && affinity == 0);
                    Assert(line.LiveGlassAvailable);
                    Assert(Mouse.Captured == null);
                    LineMotionChecks.Run(app, line, testBitmap, Test);
                    MultiDisplayChecks.Run(app, line, testBitmap, Test);
                }
                finally { line.Close(); }
            });
            Test("second-instance activation reaches first instance", () =>
            {
                using var signaled = new System.Threading.ManualResetEventSlim();
                var root = Path.Combine(Settings.Root, Guid.NewGuid().ToString("N"));
                using var first = new ActivationGate(root, () => signaled.Set());
                using var second = new ActivationGate(root, () => { });
                Assert(first.IsFirst && !second.IsFirst && signaled.Wait(1000));
            });
            UiChecks.Run(app, Test);
            ToolbarChecks.Run(app, Test);
            var captureSettings = new Settings { Keep = 100 };
            var captureStore = new Store(captureSettings);
            var firstCapture = Path.Combine(Settings.Root, "capture-first.png");
            var secondCapture = Path.Combine(Settings.Root, "capture-second.png");
            MediaTools.SavePng(testBitmap, firstCapture); MediaTools.SavePng(testBitmap, secondCapture);
            await captureStore.Add(firstCapture, screenshot: true);
            await captureStore.Add(secondCapture, screenshot: true);
            Test("clipboard and disk capture deduplication preserves external files", () => Assert(captureStore.Items.Count == 1 && File.Exists(secondCapture)));
            await captureStore.Add(secondCapture);
            Test("ordinary file imports are not deduplicated by screenshot rules", () => Assert(captureStore.Items.Count == 2));
            await Task.Delay(20);
            log.Add("No shown windows, mouse/keyboard injection, clipboard content access, or DoDragDrop calls were used.");
            File.WriteAllLines(Path.Combine(Settings.Root, "results.txt"), log);
            Console.WriteLine(string.Join(Environment.NewLine, log));
            app.Shutdown(log.Any(s => s.StartsWith("FAIL")) ? 1 : 0);
        };
        return app.Run();
    }
}
