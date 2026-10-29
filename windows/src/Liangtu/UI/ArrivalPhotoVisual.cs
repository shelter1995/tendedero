using System.Windows.Media.Effects;
namespace Liangtu;

// Shares the resting card's frame renderer and keeps photograph pixels separate
// from its mat. The retained drawing is reused throughout the Bezier flight.
internal sealed class ArrivalPhotoVisual : FrameworkElement
{
    readonly BitmapSource bitmap;
    readonly int variant;
    DrawingGroup? drawing;
    ThemeProfile? profile;
    Size imageSize;
    bool highContrast;
    FrameMorph? morph;
    double physicalScaleX=1,physicalScaleY=1;
    internal Rect ImageBounds { get; private set; }
    internal Rect CurrentFrameBounds { get; private set; }
    internal FrameAppearanceState? Appearance { get; private set; }
    internal int DrawingBuildCount { get; private set; }
    internal ArrivalPhotoVisual(BitmapSource bitmap, int variant)
    {
        this.bitmap=bitmap;this.variant=variant;IsHitTestVisible=false;
        RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);
    }
    internal void Configure(ThemeProfile theme, Size size)
    {
        if(profile==theme&&imageSize==size&&highContrast==SystemParameters.HighContrast&&drawing!=null)return;
        profile=theme;imageSize=size;highContrast=SystemParameters.HighContrast;
        var mat=theme.PhotoMat;ImageBounds=new Rect(mat.Left,mat.Top,size.Width,size.Height);
        Width=size.Width+mat.Left+mat.Right;Height=size.Height+mat.Top+mat.Bottom;
        CurrentFrameBounds=new Rect(0,0,Width,Height);
        var group=new DrawingGroup();
        using(var dc=group.Open()) {
            GlassBorder.DrawFrame(dc,theme,new Rect(0,0,Width,Height),Math.Min(theme.FrameRadius,Math.Min(Width,Height)/2),variant);
            dc.PushClip(new RectangleGeometry(ImageBounds,theme.PhotoImageRadius,theme.PhotoImageRadius));
            dc.DrawImage(bitmap,ImageBounds);dc.Pop();
            var edge=ImageBounds;edge.Inflate(-.25,-.25);
            dc.DrawRoundedRectangle(null,new Pen(Theme.Token("FrameEdge"),.5),edge,Math.Max(0,theme.PhotoImageRadius-.25),Math.Max(0,theme.PhotoImageRadius-.25));
        }
        drawing=group.CloneCurrentValue();if(drawing.CanFreeze)drawing.Freeze();DrawingBuildCount++;
        Effect=new DropShadowEffect { BlurRadius=8,ShadowDepth=3,Opacity=theme.ShadowOpacity,Direction=270 };
        InvalidateVisual();
    }
    internal void SetMorph(FrameMorph transition) => morph=transition;
    internal void UpdatePin(PinAppearance pin)=>morph?.UpdatePin(pin);
    internal void SetPose(ArrivalPose pose,double progress)
    {
        if(morph==null)return;
        physicalScaleX=Math.Max(.001,pose.XAxis.Length/imageSize.Width);physicalScaleY=Math.Max(.001,pose.YAxis.Length/imageSize.Height);
        var state=morph.At(profile!,progress);Appearance=state;
        CurrentFrameBounds=new Rect(ImageBounds.Left-state.Insets.Left/physicalScaleX,ImageBounds.Top-state.Insets.Top/physicalScaleY,
            ImageBounds.Width+(state.Insets.Left+state.Insets.Right)/physicalScaleX,ImageBounds.Height+(state.Insets.Top+state.Insets.Bottom)/physicalScaleY);
        if(Effect is DropShadowEffect shadow) { shadow.Opacity=state.ShadowOpacity;shadow.BlurRadius=state.ShadowBlur/physicalScaleX;shadow.ShadowDepth=state.ShadowDepth/physicalScaleY; }
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(morph==null||Appearance is not FrameAppearanceState state) { if(drawing!=null)dc.DrawDrawing(drawing);return; }
        var rect=CurrentFrameBounds;
        var clipping=state.PinWeight>0&&state.PinWeight<1;
        if(clipping) { var outer=rect;var inset=(1-state.PinWeight)*morph.LineScaleX/physicalScaleX;outer.Inflate(inset,inset);dc.PushClip(new RectangleGeometry(outer,state.Radius/physicalScaleX+inset,state.Radius/physicalScaleY+inset)); }
        if(state.PinWeight<1) {
            // Draw the material in its original screen density. Scaling the
            // photograph must not turn a 7 DIP mat into a 70 DIP mat.
            var x=morph.LineScaleX/physicalScaleX;var y=morph.LineScaleY/physicalScaleY;
            // Keep the old material as the base and composite the new material
            // over it. Fading both layers would punch an alpha hole mid-flight.
            dc.PushTransform(new ScaleTransform(x,y));
            GlassBorder.DrawFrame(dc,profile!,new Rect(rect.X/x,rect.Y/y,rect.Width/x,rect.Height/y),state.Radius/morph.LineScaleX,variant);
            dc.Pop();
        }
        var pin=morph.Pin;var map=new Matrix(rect.Width/pin.Size.Width,0,0,rect.Height/pin.Size.Height,rect.X,rect.Y);
        if(state.PinWeight>0) { dc.PushOpacity(state.PinWeight);dc.PushTransform(new MatrixTransform(map));dc.DrawDrawing(pin.Body);dc.Pop();dc.Pop(); }
        if(state.Controls>0) { dc.PushOpacity(state.Controls);dc.PushTransform(new MatrixTransform(map));dc.DrawDrawing(pin.Controls);dc.Pop();dc.Pop(); }
        if(clipping)dc.Pop();
        // The only photograph draw in this path. Chrome crossfades cannot
        // produce a second, differently sized copy of the user's image.
        var rx=state.ImageRadius/physicalScaleX;var ry=state.ImageRadius/physicalScaleY;
        dc.PushClip(new RectangleGeometry(ImageBounds,rx,ry));dc.DrawImage(bitmap,ImageBounds);dc.Pop();
        if(state.PinWeight<1) {
            var edge=ImageBounds;edge.Inflate(-.25*morph.LineScaleX/physicalScaleX,-.25*morph.LineScaleY/physicalScaleY);
            dc.PushOpacity(1-state.PinWeight);dc.DrawRoundedRectangle(null,new Pen(Theme.Token("FrameEdge"),.5*morph.LineScaleX/physicalScaleX),edge,Math.Max(0,rx-.25),Math.Max(0,ry-.25));dc.Pop();
        }
    }
}

internal readonly record struct ArrivalPose(Point Origin, Vector XAxis, Vector YAxis)
{
    internal static Matrix WindowToScreen(Window window)
    {
        var matrix=PresentationSource.FromVisual(window)?.CompositionTarget?.TransformToDevice??Matrix.Identity;
        var origin=window.PointToScreen(new Point());matrix.OffsetX+=origin.X;matrix.OffsetY+=origin.Y;return matrix;
    }
    internal static ArrivalPose FromRect(Rect rect)=>new(rect.TopLeft,new Vector(rect.Width,0),new Vector(0,rect.Height));
    internal Point Center=>Origin+(XAxis+YAxis)/2;
    internal Rect Bounds { get { var rect=new Rect(Origin,Origin+XAxis);rect.Union(Origin+YAxis);rect.Union(Origin+XAxis+YAxis);return rect; } }
    internal Matrix Map(Rect image)
    {
        var x=XAxis/image.Width;var y=YAxis/image.Height;
        return new Matrix(x.X,x.Y,y.X,y.Y,Origin.X-x.X*image.X-y.X*image.Y,Origin.Y-x.Y*image.X-y.Y*image.Y);
    }
    internal static ArrivalPose Travel(Rect from,ArrivalPose to,double progress)
    {
        if(progress<=0)return FromRect(from);if(progress>=1)return to;
        var t=MotionCurve.Ease(progress);var width=to.XAxis.Length;var height=to.YAxis.Length;
        var rect=MediaTransferVisual.Travel(from,new Rect(to.Center.X-width/2,to.Center.Y-height/2,width,height),progress);
        var angle=Math.Atan2(to.XAxis.Y,to.XAxis.X)*t;
        var x=new Vector(Math.Cos(angle)*rect.Width,Math.Sin(angle)*rect.Width);
        var y=new Vector(-Math.Sin(angle)*rect.Height,Math.Cos(angle)*rect.Height);
        var center=new Point(rect.X+rect.Width/2,rect.Y+rect.Height/2);
        return new ArrivalPose(center-(x+y)/2,x,y);
    }
    internal static ArrivalPose Travel(ArrivalPose from,ArrivalPose to,double progress)
    {
        if(progress<=0)return from;if(progress>=1)return to;
        var start=new Rect(from.Center.X-from.XAxis.Length/2,from.Center.Y-from.YAxis.Length/2,from.XAxis.Length,from.YAxis.Length);
        var end=new Rect(to.Center.X-to.XAxis.Length/2,to.Center.Y-to.YAxis.Length/2,to.XAxis.Length,to.YAxis.Length);
        var rect=MediaTransferVisual.Travel(start,end,progress);var t=MotionCurve.Ease(progress);
        var angle=Math.Atan2(from.XAxis.Y,from.XAxis.X)+(Math.Atan2(to.XAxis.Y,to.XAxis.X)-Math.Atan2(from.XAxis.Y,from.XAxis.X))*t;
        var x=new Vector(Math.Cos(angle)*rect.Width,Math.Sin(angle)*rect.Width);var y=new Vector(-Math.Sin(angle)*rect.Height,Math.Cos(angle)*rect.Height);
        return new ArrivalPose(new Point(rect.X+rect.Width/2,rect.Y+rect.Height/2)-(x+y)/2,x,y);
    }
}
