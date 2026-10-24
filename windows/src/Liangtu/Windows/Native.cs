using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace Liangtu;
internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int n);
    [DllImport("user32.dll")] public static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int n, IntPtr v);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint affinity);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetCapture();
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    public static void ReleaseOwnedCapture(Visual visual)
    {
        var source = PresentationSource.FromVisual(visual) as HwndSource;
        if (source != null && GetCapture() == source.Handle)
            ReleaseCapture();
    }
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] public static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT p, uint flags);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int kind, out uint x, out uint y);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
    public static double Dpi(System.Windows.Forms.Screen screen)
    {
        try
        {
            GetDpiForMonitor(MonitorFromPoint(new POINT { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 }, 2), 0, out var x, out _);
            return Math.Max(1, x / 96.0);
        }
        catch { return 1; }
    }
    public static bool Fullscreen(System.Windows.Forms.Screen s)
    {
        var h = GetForegroundWindow();
        if (h == IntPtr.Zero)
            return false;
        var cls = new System.Text.StringBuilder(256);
        GetClassName(h, cls, 256);
        if (cls.ToString() is "WorkerW" or "Progman" or "Shell_TrayWnd")
            return false;
        GetWindowRect(h, out var r);
        var b = s.Bounds;
        return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
    }
    public static void NonActivating(Window w)
    {
        var h = new WindowInteropHelper(w).Handle;
        SetWindowLongPtr(h, -20, new IntPtr(GetWindowLongPtr(h, -20).ToInt64() | 0x08000000 | 0x80));
    }
    public static BitmapSource Capture(System.Drawing.Rectangle b)
    {
        using var bmp = new System.Drawing.Bitmap(b.Width, b.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
            g.CopyFromScreen(b.Left, b.Top, 0, 0, b.Size);
        var h = bmp.GetHbitmap();
        try
        {
            var image = Imaging.CreateBitmapSourceFromHBitmap(h, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally { DeleteObject(h); }
    }
}
