using System.Runtime.InteropServices;
namespace Liangtu;

/// Windows Composition clips the live host backdrop inside each glass geometry.
/// This layer remains visible to remote sessions and never captures desktop pixels.
public sealed class GlassBackdrop : IDisposable
{
    readonly Window owner; readonly IntPtr ownerHandle; readonly WndProc procedure;
    IntPtr handle, context; bool visible;
    public bool Available => context != IntPtr.Zero;
    public int ErrorCode
    {
        get; private set;
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate IntPtr WndProc(IntPtr h, int m, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential)]
    struct Shape
    {
        public float Width, Height, Radius, Opacity, M11, M12, M21, M22, X, Y;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(int ex, string cls, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool ValidateRect(IntPtr hwnd, IntPtr rect);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
    [DllImport("gdi32.dll")] static extern IntPtr CreatePolygonRgn(Native.POINT[] points, int count, int mode);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] static extern int CombineRgn(IntPtr dest, IntPtr first, IntPtr second, int mode);
    [DllImport("gdi32.dll")] static extern bool PtInRegion(IntPtr region, int x, int y);
    [DllImport("GlassNative.dll", CallingConvention = CallingConvention.Cdecl)] static extern int GlassCreate(IntPtr hwnd, out IntPtr context);
    [DllImport("GlassNative.dll", CallingConvention = CallingConvention.Cdecl)] static extern int GlassUpdate(IntPtr context, [In] Shape[] shapes, int count);
    [DllImport("GlassNative.dll", CallingConvention = CallingConvention.Cdecl)] static extern void GlassDestroy(IntPtr context);
    public GlassBackdrop(Window owner, IntPtr ownerHandle)
    {
        this.owner = owner;
        this.ownerHandle = ownerHandle;
        procedure = (h, m, w, l) => { if (m == 0x84) return new IntPtr(-1); if (m == 0x21) return new IntPtr(3); if (m == 0xf) { ValidateRect(h, IntPtr.Zero); return IntPtr.Zero; } if (m == 0x14) return new IntPtr(1); return DefWindowProc(h, m, w, l); };
        handle = CreateWindowEx(0x08000000 | 0x80 | 0x20 | 0x8 | 0x00200000, "STATIC", "Liangtu glass material", 0x80000000, 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (handle == IntPtr.Zero)
            return;
        Native.SetWindowLongPtr(handle, -4, Marshal.GetFunctionPointerForDelegate(procedure));
        try
        {
            ErrorCode = GlassCreate(handle, out context);
        }
        catch (Exception e) { Settings.Log("Glass adapter " + e.Message); ErrorCode = e.HResult; }
        Settings.Log($"Composition backdrop available={Available}, HRESULT=0x{ErrorCode:X8}");
        CompositionTarget.Rendering += Render;
    }
    void Render(object? sender, EventArgs e)
    {
        if (!Available || !owner.IsVisible || (owner is LineWindow line && line.Busy))
        {
            Hide();
            return;
        }
        Native.GetWindowRect(ownerHandle, out var bounds);
        var shapes = new List<Shape>();
        var outlines = new List<Point[]>();
        foreach (var g in GlassBorder.In(owner))
        {
            try
            {
                var p = g.PointToScreen(new Point());
                var px = g.PointToScreen(new Point(1, 0));
                var py = g.PointToScreen(new Point(0, 1));
                shapes.Add(new Shape { Width = (float)g.ActualWidth, Height = (float)g.ActualHeight, Radius = (float)Math.Min(g.CornerRadius.TopLeft, Math.Min(g.ActualWidth, g.ActualHeight) / 2), Opacity = 1, M11 = (float)(px.X - p.X), M12 = (float)(px.Y - p.Y), M21 = (float)(py.X - p.X), M22 = (float)(py.Y - p.Y), X = (float)(p.X - bounds.Left), Y = (float)(p.Y - bounds.Top) });
                outlines.Add(g.ScreenOutline().Select(point => new Point(point.X - bounds.Left, point.Y - bounds.Top)).ToArray());
            }
            catch (InvalidOperationException) { }
        }
        if (shapes.Count == 0)
        {
            Hide();
            return;
        }
        var status = GlassUpdate(context, shapes.ToArray(), shapes.Count);
        if (status < 0)
        {
            Settings.Log($"Glass update failed 0x{status:X8}");
            Hide();
            return;
        }
        var region = BuildInputRegion(outlines);
        if (SetWindowRgn(handle, region, false) == 0)
        {
            Native.DeleteObject(region);
            Hide();
            return;
        }
        Native.SetWindowPos(handle, ownerHandle, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x10 | 0x40);
        visible = true;
    }
    public void Hide()
    {
        if (visible)
        {
            ShowWindow(handle, 0);
            visible = false;
        }
    }
    internal static IntPtr BuildInputRegion(IEnumerable<Point[]> outlines)
    {
        var result = CreateRectRgn(0, 0, 0, 0);
        foreach (var outline in outlines)
        {
            var points = outline.Select(p => new Native.POINT { X = (int)Math.Round(p.X), Y = (int)Math.Round(p.Y) }).ToArray();
            var polygon = CreatePolygonRgn(points, points.Length, 1);
            if (polygon != IntPtr.Zero)
            {
                CombineRgn(result, result, polygon, 2);
                Native.DeleteObject(polygon);
            }
        }
        return result;
    }
    internal static bool RegionContains(IntPtr region, int x, int y) => PtInRegion(region, x, y);
    public void Dispose()
    {
        CompositionTarget.Rendering -= Render;
        if (context != IntPtr.Zero)
        {
            GlassDestroy(context);
            context = IntPtr.Zero;
        }
        if (handle != IntPtr.Zero)
        {
            DestroyWindow(handle);
            handle = IntPtr.Zero;
        }
    }
}
