namespace Liangtu;

internal sealed class EditorViewport : Grid
{
    readonly Canvas document = new(), stage = new();
    readonly MatrixTransform transform = new();
    readonly ImageViewState view = new();
    Point? panStart;
    bool panMode, spaceHeld;
    public bool PanMode { get => panMode; set { panMode = value; UpdateCursor(); } }
    public bool SpaceHeld { get => spaceHeld; set { spaceHeld = value; UpdateCursor(); } }
    public double Zoom => view.Zoom;
    public event Action? Changed;
    public EditorViewport(Canvas canvas)
    {
        ClipToBounds = true; Focusable = true;
        Background = Theme.Dark ? Theme.Brush("#17181B") : Theme.Brush("#E3E5E9");
        document.Children.Add(canvas); document.RenderTransform = transform;
        stage.Children.Add(document); Children.Add(stage);
        SizeChanged += (_, e) => {
            view.ViewSize = e.NewSize;
            if (view.Fitted) view.Fit();
            else view.Pan(new Vector((e.NewSize.Width - e.PreviousSize.Width) / 2, (e.NewSize.Height - e.PreviousSize.Height) / 2));
            Apply();
        };
        PreviewMouseWheel += (_, e) => {
            if (Mouse.Captured != null && panStart == null) return;
            view.ZoomAt(view.Zoom * Math.Pow(1.15, e.Delta / 120.0), e.GetPosition(this)); Apply(); e.Handled = true;
        };
        PreviewMouseDown += (_, e) => {
            if (Mouse.Captured != null || !(e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left && (PanMode || SpaceHeld))) return;
            Focus(); panStart = e.GetPosition(this); CaptureMouse(); UpdateCursor(); e.Handled = true;
        };
        PreviewMouseMove += (_, e) => {
            if (panStart is not Point last) return;
            var point = e.GetPosition(this); view.Pan(point - last); panStart = point; Apply(); e.Handled = true;
        };
        PreviewMouseUp += (_, e) => { if (panStart != null && e.ChangedButton is MouseButton.Left or MouseButton.Middle) { EndPan(); e.Handled = true; } };
        LostMouseCapture += (_, _) => { panStart = null; UpdateCursor(); };
    }
    public void SetImageSize(int width, int height)
    {
        bool resized = view.ImageSize != new Size(width, height);
        document.Width = width; document.Height = height; view.ImageSize = new Size(width, height);
        if (resized || view.Fitted) view.Fit(); Apply();
    }
    public void Fit() { view.Fit(); Apply(); }
    public void SetZoom(double zoom) { view.ZoomAt(zoom, new Point(ActualWidth / 2, ActualHeight / 2)); Apply(); }
    public void ZoomBy(double factor) => SetZoom(view.Zoom * factor);
    public void Pan(double x, double y) { view.Pan(new Vector(x, y)); Apply(); }
    public void EndPan() { panStart = null; if (Mouse.Captured == this) ReleaseMouseCapture(); UpdateCursor(); }
    void UpdateCursor() => Cursor = panStart != null ? Cursors.SizeAll : PanMode || SpaceHeld ? Cursors.Hand : Cursors.Arrow;
    void Apply()
    {
        transform.Matrix = new Matrix(view.Zoom, 0, 0, view.Zoom, view.Offset.X, view.Offset.Y);
        Changed?.Invoke();
    }
}
