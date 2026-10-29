using System.Runtime.InteropServices;
namespace Liangtu;

/// Windows Composition clips the live host backdrop inside each glass geometry.
/// This layer remains visible to remote sessions and never captures desktop pixels.
public sealed class GlassBackdrop : IDisposable
{
    readonly Window owner; readonly IntPtr ownerHandle; readonly WndProc procedure;
    IntPtr handle, context; bool visible;
    readonly List<Shape> shapeBuffer = new();
    Shape[] previousShapes = Array.Empty<Shape>();
    Rect previousBounds;
    internal int NativeUpdateCount { get; private set; }
    internal bool RenderingAttached { get; private set; }
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
        RefreshMaterial();
        Theme.Changed += RefreshMaterial;
        owner.IsVisibleChanged += OwnerVisibilityChanged;
        UpdateRendering();
    }
    void RefreshMaterial()
    {
        if (handle == IntPtr.Zero) return;
        if (Theme.Current.Material != "canvas" || SystemParameters.HighContrast) {
            Hide();
            var retired = context; context = IntPtr.Zero;
            previousShapes = Array.Empty<Shape>(); previousBounds = Rect.Empty;
            if (retired != IntPtr.Zero) GlassDestroy(retired);
            UpdateRendering();
            return;
        }
        if (Available) { UpdateRendering(); return; }
        try
        {
            ErrorCode = GlassCreate(handle, out context);
        }
        catch (Exception e) { Settings.Log("Glass adapter " + e.Message); ErrorCode = e.HResult; }
        Settings.Log($"Composition backdrop available={Available}, HRESULT=0x{ErrorCode:X8}");
        UpdateRendering();
    }
    void OwnerVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateRendering();
    void UpdateRendering()
    {
        var active = handle != IntPtr.Zero && Available && owner.IsVisible;
        if (RenderingAttached == active) return;
        RenderingAttached = active;
        if (active) CompositionTarget.Rendering += Render;
        else { CompositionTarget.Rendering -= Render; Hide(); }
    }
    void Render(object? sender, EventArgs e)
    {
        if (!Available || !owner.IsVisible || (owner is LineWindow line && line.Busy))
        {
            Hide();
            return;
        }
        Native.GetWindowRect(ownerHandle, out var bounds);
        var shapes = shapeBuffer; shapes.Clear();
        foreach (var g in GlassBorder.In(owner))
        {
            try
            {
                var p = g.PointToScreen(new Point());
                var px = g.PointToScreen(new Point(1, 0));
                var py = g.PointToScreen(new Point(0, 1));
                shapes.Add(new Shape { Width = (float)g.ActualWidth, Height = (float)g.ActualHeight, Radius = (float)Math.Min(g.CornerRadius.TopLeft, Math.Min(g.ActualWidth, g.ActualHeight) / 2), Opacity = 1, M11 = (float)(px.X - p.X), M12 = (float)(px.Y - p.Y), M21 = (float)(py.X - p.X), M22 = (float)(py.Y - p.Y), X = (float)(p.X - bounds.Left), Y = (float)(p.Y - bounds.Top) });
            }
            catch (InvalidOperationException) { }
        }
        if (shapes.Count == 0)
        {
            Hide();
            return;
        }
        var rectangle = new Rect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        if (visible && rectangle == previousBounds && shapes.Count == previousShapes.Length && shapes.SequenceEqual(previousShapes)) return;
        var updatedShapes = shapes.ToArray();
        var status = GlassUpdate(context, updatedShapes, updatedShapes.Length);
        if (status < 0)
        {
            Settings.Log($"Glass update failed 0x{status:X8}");
            Hide();
            return;
        }
        // Only changed geometry needs the 40-point rounded outlines and GDI region.
        var region = BuildInputRegion(shapes.Select(Outline));
        if (SetWindowRgn(handle, region, false) == 0)
        {
            Native.DeleteObject(region);
            Hide();
            return;
        }
        Native.SetWindowPos(handle, ownerHandle, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x10 | 0x40);
        previousShapes = updatedShapes; previousBounds = rectangle; NativeUpdateCount++;
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
    static readonly Point[] cornerArc = Enumerable.Range(0, 10).Select(i => new Point(Math.Cos(i * Math.PI / 18), Math.Sin(i * Math.PI / 18))).ToArray();
    static Point[] Outline(Shape s)
    {
        var points = new Point[40];
        // Use the affine transform already sampled for Windows Composition.
        // Forty PointToScreen calls per border per frame used to repeat this work.
        for (int corner = 0; corner < 4; corner++) for (int i = 0; i < 10; i++) {
            var arc = cornerArc[i];
            var (x, y) = corner switch {
                0 => (s.Width - s.Radius + arc.Y * s.Radius, s.Radius - arc.X * s.Radius),
                1 => (s.Width - s.Radius + arc.X * s.Radius, s.Height - s.Radius + arc.Y * s.Radius),
                2 => (s.Radius - arc.Y * s.Radius, s.Height - s.Radius + arc.X * s.Radius),
                _ => (s.Radius - arc.X * s.Radius, s.Radius - arc.Y * s.Radius)
            };
            points[corner * 10 + i] = new Point(s.X + x * s.M11 + y * s.M21, s.Y + x * s.M12 + y * s.M22);
        }
        return points;
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
        Theme.Changed -= RefreshMaterial;
        owner.IsVisibleChanged -= OwnerVisibilityChanged;
        CompositionTarget.Rendering -= Render;
        RenderingAttached = false;
        // Native queue shutdown pumps messages. Publish detached state before
        // entering native cleanup so reentrant callbacks cannot reuse ownership.
        var retiredContext = context; context = IntPtr.Zero;
        var retiredHandle = handle; handle = IntPtr.Zero; visible = false;
        if (retiredContext != IntPtr.Zero)
        {
            GlassDestroy(retiredContext);
        }
        if (retiredHandle != IntPtr.Zero)
        {
            DestroyWindow(retiredHandle);
        }
    }
}
