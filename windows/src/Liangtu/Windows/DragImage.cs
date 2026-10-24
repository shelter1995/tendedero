using System.Runtime.InteropServices;
using ComData = System.Runtime.InteropServices.ComTypes.IDataObject;
namespace Liangtu;
public sealed class DragImage : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    struct SIZE
    {
        public int Width, Height;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct DRAGIMAGE
    {
        public SIZE Size; public Native.POINT Offset; public IntPtr Bitmap; public uint ColorKey;
    }
    [ComImport, Guid("DE5BF786-477A-11D2-839D-00C04FD918D0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDragSourceHelper
    {
        void InitializeFromBitmap(ref DRAGIMAGE image, [MarshalAs(UnmanagedType.Interface)] ComData data);
        void InitializeFromWindow(IntPtr hwnd, ref Native.POINT point, [MarshalAs(UnmanagedType.Interface)] ComData data);
    }
    IntPtr bitmap; object? helper; public bool Initialized
    {
        get; private set;
    }
    public DragImage(BitmapSource source, ComData data, double width, double height, double dpi)
    {
        try
        {
            var w = Math.Max(1, (int)(width * dpi));
            var h = Math.Max(1, (int)(height * dpi));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
                dc.DrawImage(source, new Rect(0, 0, w, h));
            var rendered = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rendered.Render(visual);
            var pixels = new byte[w * h * 4];
            new FormatConvertedBitmap(rendered, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, w * 4, 0);
            using (var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var locked = bmp.LockBits(new System.Drawing.Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    Marshal.Copy(pixels, 0, locked.Scan0, pixels.Length);
                }
                finally { bmp.UnlockBits(locked); }
                bitmap = bmp.GetHbitmap(System.Drawing.Color.FromArgb(0));
            }
            helper = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("4657278A-411B-11D2-839A-00C04FD918D0"))!);
            var image = new DRAGIMAGE { Size = new SIZE { Width = w, Height = h }, Offset = new Native.POINT { X = w / 2, Y = h / 2 }, Bitmap = bitmap, ColorKey = 0xffffffff };
            ((IDragSourceHelper)helper!).InitializeFromBitmap(ref image, data);
            bitmap = IntPtr.Zero;
            Initialized = true;
            Settings.Log("Shell drag preview initialized");
        }
        catch (Exception e) { Settings.Log($"Shell drag preview HRESULT=0x{e.HResult:X8}: {e}"); }
    }
    public void Dispose()
    {
        if (bitmap != IntPtr.Zero)
        {
            Native.DeleteObject(bitmap);
            bitmap = IntPtr.Zero;
        }
        if (helper != null && Marshal.IsComObject(helper))
            Marshal.ReleaseComObject(helper);
        helper = null;
    }
}
public sealed class ReturnFlight : Window
{
    readonly System.Diagnostics.Stopwatch clock = new(); readonly Rect start, target;
    public ReturnFlight(BitmapSource image, Rect from, Rect to)
    {
        start = from;
        target = to;
        var dpi = Native.Dpi(System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)from.X, (int)from.Y)));
        Width = from.Width / dpi;
        Height = from.Height / dpi;
        AllowsTransparency = true;
        WindowStyle = WindowStyle.None;
        Background = Brushes.Transparent;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        IsHitTestVisible = false;
        Content = new Image { Source = image, Stretch = Stretch.Fill };
        SourceInitialized += (_, _) => { Native.NonActivating(this); var h = new System.Windows.Interop.WindowInteropHelper(this).Handle; Native.SetWindowLongPtr(h, -20, new IntPtr(Native.GetWindowLongPtr(h, -20).ToInt64() | 0x20)); Place(start); };
        Loaded += (_, _) => { clock.Restart(); CompositionTarget.Rendering += Tick; };
        Closed += (_, _) => CompositionTarget.Rendering -= Tick;
    }
    void Tick(object? sender, EventArgs e)
    {
        var t = Math.Clamp(clock.Elapsed.TotalSeconds / .28, 0, 1);
        var eased = 1 - Math.Pow(1 - t, 3);
        Place(Interpolate(start, target, eased));
        if (t >= 1)
            Close();
    }
    internal static Rect Interpolate(Rect from, Rect to, double amount) => new(from.X + (to.X - from.X) * amount, from.Y + (to.Y - from.Y) * amount, from.Width + (to.Width - from.Width) * amount, from.Height + (to.Height - from.Height) * amount);
    void Place(Rect rect) => Native.SetWindowPos(new System.Windows.Interop.WindowInteropHelper(this).Handle, new IntPtr(-1), (int)Math.Round(rect.X), (int)Math.Round(rect.Y), Math.Max(1, (int)Math.Round(rect.Width)), Math.Max(1, (int)Math.Round(rect.Height)), 0x10);
}
