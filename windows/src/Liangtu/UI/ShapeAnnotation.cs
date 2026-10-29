using System.Windows.Shapes;
namespace Liangtu;

// Frozen geometry and clips make history snapshots independent of live WPF controls.
internal sealed record ShapeAnnotation(Guid Id, Geometry Geometry, Color Color, double Width, double Opacity,
    bool Round, Geometry? Clip = null)
{
    Pen Pen(double extra = 0) => new(new SolidColorBrush(Color), Width + extra) {
        StartLineCap = Round ? PenLineCap.Round : PenLineCap.Flat,
        EndLineCap = Round ? PenLineCap.Round : PenLineCap.Flat,
        LineJoin = Round ? PenLineJoin.Round : PenLineJoin.Miter
    };
    public Rect Bounds => VisibleInk.Bounds;
    Geometry VisibleInk => Clip == null ? Geometry.GetWidenedPathGeometry(Pen()) : System.Windows.Media.Geometry.Combine(Geometry.GetWidenedPathGeometry(Pen()), Clip, GeometryCombineMode.Intersect, null);
    public bool Hit(Point point, double tolerance) => (Clip == null || Clip.FillContains(point)) && Geometry.StrokeContains(Pen(tolerance * 2), point);
    public System.Windows.Shapes.Path CreateView() => new() {
        Data = Geometry, Stroke = new SolidColorBrush(Color), StrokeThickness = Width, Opacity = Opacity,
        StrokeStartLineCap = Round ? PenLineCap.Round : PenLineCap.Flat,
        StrokeEndLineCap = Round ? PenLineCap.Round : PenLineCap.Flat,
        StrokeLineJoin = Round ? PenLineJoin.Round : PenLineJoin.Miter,
        Clip = Clip, IsHitTestVisible = false
    };
    public static ShapeAnnotation FromView(Shape view)
    {
        var x = Canvas.GetLeft(view); var y = Canvas.GetTop(view);
        // RenderedGeometry can be empty while a window is hidden or layout is
        // invalidated. Capture the authored geometry, never a layout cache.
        Geometry authored;
        if (view is System.Windows.Shapes.Path path) authored = path.Data ?? Geometry.Empty;
        else if (view is Polyline line) {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open()) if (line.Points.Count > 0) {
                context.BeginFigure(line.Points[0], false, false);
                context.PolyLineTo(line.Points.Skip(1).ToArray(), true, false);
            }
            authored = geometry;
        }
        else {
            var inset = view.StrokeThickness / 2;
            var bounds = new Rect(inset, inset, Math.Max(0, view.Width - view.StrokeThickness), Math.Max(0, view.Height - view.StrokeThickness));
            authored = view is Ellipse ? new EllipseGeometry(bounds) : new RectangleGeometry(bounds);
        }
        var data = Transform(authored, new TranslateTransform(double.IsNaN(x) ? 0 : x, double.IsNaN(y) ? 0 : y));
        return new(Guid.NewGuid(), data, ((SolidColorBrush)view.Stroke).Color, view.StrokeThickness, view.Opacity, view is Polyline);
    }
    public ShapeAnnotation Move(Vector delta) => Apply(new TranslateTransform(delta.X, delta.Y));
    public ShapeAnnotation Rotate(double oldHeight) => Apply(new MatrixTransform(0, 1, -1, 0, oldHeight, 0));
    public ShapeAnnotation Crop(Rect area)
    {
        Geometry clip = Clip == null ? new RectangleGeometry(area) : System.Windows.Media.Geometry.Combine(Clip, new RectangleGeometry(area), GeometryCombineMode.Intersect, null);
        clip.Freeze(); return (this with { Clip = clip }).Move(new Vector(-area.X, -area.Y));
    }
    public ShapeAnnotation Erase(Point from, Point to, double radius)
    {
        var line = new LineGeometry(from, to);
        var pen = new Pen(Brushes.Black, radius * 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        Geometry cut = (to - from).Length < .001 ? new EllipseGeometry(from, radius, radius) : line.GetWidenedPathGeometry(pen);
        if (VisibleInk.FillContainsWithDetail(cut) == IntersectionDetail.Empty) return this;
        var keep = System.Windows.Media.Geometry.Combine(Clip ?? new RectangleGeometry(Geometry.GetRenderBounds(Pen())), cut, GeometryCombineMode.Exclude, null);
        keep.Freeze(); return this with { Clip = keep };
    }
    internal ShapeAnnotation Apply(Transform transform) => this with { Geometry = Transform(Geometry, transform), Clip = Clip == null ? null : Transform(Clip, transform) };
    static Geometry Transform(Geometry geometry, Transform transform)
    {
        var copy = geometry.CloneCurrentValue(); var matrix = copy.Transform?.Value ?? Matrix.Identity;
        matrix.Append(transform.Value); copy.Transform = new MatrixTransform(matrix); copy.Freeze(); return copy;
    }
}

