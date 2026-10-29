namespace Liangtu;

// Only editor chrome; export draws TextAnnotation labels independently.
internal sealed class TextSelectionFrame : Border
{
    public double Zoom { get; set; } = 1;
    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(Background, null, bounds);
        if (BorderBrush == null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var width = 1 / Math.Max(.01, Zoom);
        var pen = new Pen(BorderBrush, width) { DashStyle = new DashStyle(new[] { 4d, 3d }, 0) };
        bounds.Inflate(-width / 2, -width / 2);
        if (!bounds.IsEmpty) dc.DrawRectangle(null, pen, bounds);
    }
}
