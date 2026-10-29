using System.Windows.Interop;
namespace Liangtu;

// A stationary, click-through desktop canvas keeps capture pixels independent
// of the rope's reveal/tilt transforms and of monitor DIP origins.
internal sealed class ArrivalFlight : Window
{
    readonly ArrivalPhotoVisual picture;
    readonly Canvas canvas = new();
    readonly MatrixTransform placement = new();
    readonly System.Drawing.Rectangle virtualDesktop = System.Windows.Forms.SystemInformation.VirtualScreen;
    System.Drawing.Rectangle desktop;
    IntPtr handle;
    bool placing;
    internal Rect PhysicalBounds { get; private set; }
    internal bool HasPresented { get; private set; }
    internal Rect RenderedScreenBounds {
        get { var rect = picture.TransformToAncestor(this).TransformBounds(picture.ImageBounds); return new MatrixTransform(ArrivalPose.WindowToScreen(this)).TransformBounds(rect); }
    }
    internal Rect RenderedFrameBounds {
        get { var rect=picture.TransformToAncestor(this).TransformBounds(picture.CurrentFrameBounds);return new MatrixTransform(ArrivalPose.WindowToScreen(this)).TransformBounds(rect); }
    }
    internal FrameAppearanceState? Appearance => picture.Appearance;
    internal void UpdatePinAppearance(PinAppearance pin)=>picture.UpdatePin(pin);
    internal int ArtworkBuildCount=>picture.DrawingBuildCount;
    internal int SurfaceExpansionCount { get; private set; }
    internal System.Drawing.Rectangle SurfaceBounds => desktop;
    internal static System.Drawing.Rectangle ClipSurface(Rect envelope, System.Drawing.Rectangle visibleDesktop)
    {
        var clipped=Rect.Intersect(envelope,new Rect(visibleDesktop.Left,visibleDesktop.Top,visibleDesktop.Width,visibleDesktop.Height));
        if(clipped.IsEmpty||clipped.Width<=0||clipped.Height<=0) {
            // A fully off-screen pose can still travel back into view. Retain a
            // harmless minimum surface inside the desktop; never move the pose.
            return new System.Drawing.Rectangle((int)Math.Clamp(Math.Floor(envelope.X),visibleDesktop.Left,visibleDesktop.Right-1),(int)Math.Clamp(Math.Floor(envelope.Y),visibleDesktop.Top,visibleDesktop.Bottom-1),1,1);
        }
        return System.Drawing.Rectangle.FromLTRB(Math.Max(visibleDesktop.Left,(int)Math.Floor(clipped.Left)),Math.Max(visibleDesktop.Top,(int)Math.Floor(clipped.Top)),Math.Min(visibleDesktop.Right,(int)Math.Ceiling(clipped.Right)),Math.Min(visibleDesktop.Bottom,(int)Math.Ceiling(clipped.Bottom)));
    }
    internal void DrawSnapshot(DrawingContext dc, Window target)
    {
        if(!HasPresented||!IsVisible)return;
        var inverse=ArrivalPose.WindowToScreen(target);inverse.Invert();
        var rect=new MatrixTransform(inverse).TransformBounds(new Rect(desktop.Left,desktop.Top,desktop.Width,desktop.Height));
        var brush=new VisualBrush(canvas) { ViewboxUnits=BrushMappingMode.Absolute, Viewbox=new Rect(0,0,canvas.ActualWidth,canvas.ActualHeight), Stretch=Stretch.Fill };
        dc.DrawRectangle(brush,null,rect);
    }
    public ArrivalFlight(BitmapSource bitmap, LineWindow owner, Rect from, ArrivalPose to, Size imageSize, int variant)
        : this(bitmap,owner,ArrivalPose.FromRect(from),to,imageSize,variant) { }
    internal ArrivalFlight(BitmapSource bitmap, LineWindow owner, ArrivalPose from, ArrivalPose to, Size imageSize, int variant,PinAppearance? pin=null,bool fromPin=false)
    {
        picture=new ArrivalPhotoVisual(bitmap,variant) { RenderTransform=placement };picture.Configure(Theme.Current,imageSize);
        if(pin!=null) { var line=fromPin?to:from;picture.SetMorph(new FrameMorph(pin,fromPin,line.XAxis.Length/imageSize.Width,line.YAxis.Length/imageSize.Height)); }
        picture.SetPose(from,0);var firstFrame=picture.CurrentFrameBounds;
        picture.SetPose(to,1);var lastFrame=picture.CurrentFrameBounds;picture.SetPose(from,0);
        var envelope = Rect.Union(new MatrixTransform(from.Map(picture.ImageBounds)).TransformBounds(firstFrame), new MatrixTransform(to.Map(picture.ImageBounds)).TransformBounds(lastFrame));
        var margin=128+12*Math.Max(from.XAxis.Length/imageSize.Width,from.YAxis.Length/imageSize.Height);envelope.Inflate(margin,margin);
        desktop = ClipSurface(envelope,virtualDesktop);
        var dpi = Native.Dpi(System.Windows.Forms.Screen.FromRectangle(desktop));
        Width = desktop.Width / dpi; Height = desktop.Height / dpi;
        Owner = owner; WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false;
        Topmost = true; IsHitTestVisible = false; ResizeMode = ResizeMode.NoResize;
        canvas.Children.Add(picture); Content = canvas;
        SourceInitialized += (_, _) => {
            handle = new WindowInteropHelper(this).Handle; Native.NonActivating(this);
            Theme.ApplyNativeChrome(this);
            Native.SetWindowLongPtr(handle, -20, new IntPtr(Native.GetWindowLongPtr(handle, -20).ToInt64() | 0x20));
            Native.SetWindowPos(handle, new IntPtr(-1), desktop.Left, desktop.Top, desktop.Width, desktop.Height, 0x10);
        };
    }
    internal void Prepare()
    {
        // Prewarm during the peg opening. Publish the instance before calling:
        // native window sizing can synchronously reenter WPF Rendering.
        if (handle != IntPtr.Zero || placing) return;
        placing = true;
        try { new WindowInteropHelper(this).EnsureHandle(); }
        finally { placing = false; }
    }
    internal void Present(ArrivalPose physical, Size imageSize,double progress=0)
    {
        if (placing) return;
        Prepare();
        placing = true;
        try {
            HasPresented=false;
            picture.Configure(Theme.Current,imageSize);
            picture.SetPose(physical,progress);
            var frame=new MatrixTransform(physical.Map(picture.ImageBounds)).TransformBounds(picture.CurrentFrameBounds);
            var shadow=12*Math.Max(physical.XAxis.Length/imageSize.Width,physical.YAxis.Length/imageSize.Height);frame.Inflate(shadow,shadow);
            var surface=new Rect(desktop.Left,desktop.Top,desktop.Width,desktop.Height);
            var visibleFrame=Rect.Intersect(frame,new Rect(virtualDesktop.Left,virtualDesktop.Top,virtualDesktop.Width,virtualDesktop.Height));
            if(!visibleFrame.IsEmpty&&!surface.Contains(visibleFrame)) {
                surface.Union(visibleFrame);surface.Inflate(16,16);
                var expanded=ClipSurface(surface,virtualDesktop);
                if(expanded!=desktop) {
                desktop=expanded;
                var dpi=Native.GetDpiForWindow(handle)/96d;if(dpi<=0)dpi=1;
                Width=desktop.Width/dpi;Height=desktop.Height/dpi;
                Native.SetWindowPos(handle,new IntPtr(-1),desktop.Left,desktop.Top,desktop.Width,desktop.Height,0x10);SurfaceExpansionCount++;
                }
            }
            void SetRectangle() {
                // EnsureHandle prewarms HWND resources, but WPF attaches the visual
                // PresentationSource only on Show. Seed the first frame with that
                // HWND's measured DPI, then use the attached visual conversion.
                var attached = PresentationSource.FromVisual(this) != null;
                var dpi = attached ? 1 : Native.GetDpiForWindow(handle) / 96.0;
                if (dpi <= 0) dpi = 1;
                var inverse=attached?ArrivalPose.WindowToScreen(this):Matrix.Identity;if(attached)inverse.Invert();
                Point Local(Point point)=>attached?inverse.Transform(point):new Point((point.X-desktop.Left)/dpi,(point.Y-desktop.Top)/dpi);
                var top=Local(physical.Origin);var x=Local(physical.Origin+physical.XAxis);var y=Local(physical.Origin+physical.YAxis);
                placement.Matrix=new ArrivalPose(top,x-top,y-top).Map(picture.ImageBounds);
            }
            SetRectangle();
            if (!IsVisible) {
                Show();
                Native.SetWindowPos(handle, new IntPtr(-1), desktop.Left, desktop.Top, desktop.Width, desktop.Height, 0x10);
                SetRectangle();
            }
            PhysicalBounds = physical.Bounds; HasPresented = true;
        } finally { placing = false; }
    }
}
