namespace Liangtu;

// PicRope's clipboard entry heuristic. This is an animation anchor, not proof
// that the image was captured from this rectangle. All values are physical px.
internal readonly record struct ClipboardFlightAnchor(Point Pointer, System.Drawing.Rectangle Screen)
{
    internal static ClipboardFlightAnchor? Capture(Func<Point?>? pointer = null, Func<Point,System.Drawing.Rectangle>? screen = null)
    {
        Point? point;
        if(pointer!=null) point=pointer();
        else point=Native.GetCursorPos(out var value)?new Point(value.X,value.Y):null;
        if(point is not Point at || !double.IsFinite(at.X) || !double.IsFinite(at.Y)) return null;
        var bounds=screen!=null?screen(at):System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)at.X,(int)at.Y)).Bounds;
        return bounds.Width>0&&bounds.Height>0?new ClipboardFlightAnchor(at,bounds):null;
    }
    internal Rect? ForImage(int pixelWidth,int pixelHeight)
    {
        if(pixelWidth<=0||pixelHeight<=0||Screen.Width<=0||Screen.Height<=0) return null;
        if(pixelWidth==Screen.Width&&pixelHeight==Screen.Height) return new Rect(Screen.Left,Screen.Top,Screen.Width,Screen.Height);
        var scale=Math.Min(1,Math.Min(Screen.Width*.7/pixelWidth,Screen.Height*.7/pixelHeight));
        var width=pixelWidth*scale;var height=pixelHeight*scale;
        return new Rect(Math.Clamp(Pointer.X-width,Screen.Left,Screen.Right-width),Math.Clamp(Pointer.Y-height,Screen.Top,Screen.Bottom-height),width,height);
    }
}
