using System.Windows.Controls.Primitives;
using System.Windows.Shapes;

namespace Liangtu;

// One retained video surface; playback only switches existing views at time boundaries.
internal sealed class VideoAnnotationSurface : Grid
{
    readonly Canvas scene = new() { Background = Brushes.Transparent, ClipToBounds = true };
    readonly Canvas chrome = new();
    readonly MediaElement media;
    readonly Action pause;
    readonly List<VideoAnnotation> elements = new();
    readonly Dictionary<Guid, (UIElement view, Rect bounds)> views = new();
    readonly EditorViewport viewport;
    Point? gestureStart;
    VideoAnnotation? before;
    string? handle;
    Shape? drawing;
    Guid? editing;
    TextBox? input;
    AnnotationTextEditor? editingView;
    bool committing;
    double position;
    public string Tool { get; private set; } = "画笔";
    public Color Ink { get; set; } = Color.FromRgb(226, 77, 64);
    public double StrokeWidth { get; set; } = 4;
    public string TextFont { get; set; } = "Microsoft YaHei UI";
    public double TextSize { get; set; } = 28;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public Guid? SelectedId { get; private set; }
    public VideoAnnotation? Selected => SelectedId is Guid id ? elements.FirstOrDefault(x => x.Id == id) : null;
    public IReadOnlyList<VideoAnnotation> Elements => elements;
    public bool IsEditingText => editing != null;
    public bool IsGesturing => gestureStart != null;
    public VideoSelection CreationRange { get; set; }
    public int PixelWidth => (int)scene.Width;
    public int PixelHeight => (int)scene.Height;
    public double Zoom => viewport.Zoom;
    public event Action? Edited, SelectionChanged;
    public event Action<string>? Hint;

    public VideoAnnotationSurface(MediaElement media, Action pause)
    {
        this.media = media; this.pause = pause;
        media.Margin = new Thickness(0); media.Stretch = Stretch.Fill; media.IsHitTestVisible = false;
        scene.Children.Add(media); viewport = new EditorViewport(scene); viewport.AttachOverlay(chrome); Children.Add(viewport);
        viewport.Changed += DrawChrome;
        scene.MouseLeftButtonDown += Down; scene.MouseMove += Move; scene.MouseLeftButtonUp += Up;
        scene.LostMouseCapture += (_, _) => { if (gestureStart != null) CancelGesture(); };
        SetSize(640, 360);
        System.Windows.Automation.AutomationProperties.SetName(this, "视频标注画布");
    }
    public void SetSize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        scene.Width = media.Width = width; scene.Height = media.Height = height;
        viewport.SetImageSize(width, height); viewport.AttachOverlay(chrome);
    }
    public void Fit() => viewport.Fit();
    public void SelectTool(string tool)
    {
        CommitText(); CancelGesture(); Tool = tool; viewport.PanMode = tool == "平移"; ClearSelection();
        Hint?.Invoke(tool == "文字" ? "点击画面输入文字 · Esc 完成输入" : tool == "选择" ? "点击标注调整 · 拖动时长条控制显示时间" : tool == "平移" ? "拖动画布 · 滚轮缩放" : "拖动绘制 · Shift 约束形状 · Alt 穿过已有标注");
    }
    public void SetElements(IReadOnlyList<VideoAnnotation> value)
    {
        CancelGesture(); CommitText(false); elements.Clear(); elements.AddRange(value);
        if (!elements.Any(x => x.Id == SelectedId)) SelectedId = null;
        Rebuild(); Select(SelectedId);
    }
    public void SetPosition(double value, bool playing = false)
    {
        position = value;
        if (elements.Count == 0) { chrome.Visibility = Visibility.Hidden; return; }
        foreach (var element in elements) if (views.TryGetValue(element.Id, out var item)) {
            var visibility = element.Time.Contains(value) ? Visibility.Visible : Visibility.Hidden;
            if (item.view.Visibility != visibility) item.view.Visibility = visibility;
        }
        chrome.Visibility = playing ? Visibility.Hidden : Visibility.Visible;
        if (!playing && Selected is { } selected && !selected.Time.Contains(value)) chrome.Visibility = Visibility.Hidden;
        if (!playing && editing == null && chrome.Visibility == Visibility.Visible && Selected != null && chrome.Children.Count == 0) DrawChrome();
    }
    public void Select(Guid? id)
    {
        if (SelectedId != id) CommitText();
        SelectedId = elements.Any(x => x.Id == id) ? id : null;
        if (Selected is { } selected) {
            Ink = selected.Text?.Color ?? selected.Shape!.Color;
            if (selected.Text is { } text) { TextFont = text.FontFamily; TextSize = text.FontSize; Bold = text.Bold; Italic = text.Italic; }
            if (selected.Shape is { } shape) StrokeWidth = shape.Width/(selected.Name=="荧光笔"?6:1);
        }
        DrawChrome(); SelectionChanged?.Invoke();
    }
    public void ClearSelection() { CommitText(); SelectedId = null; DrawChrome(); SelectionChanged?.Invoke(); }
    VideoElementTime NewTime()
    {
        var length = CreationRange.End - CreationRange.Start;
        if (length <= 0) throw new InvalidOperationException("视频尚未载入");
        var start = Math.Clamp(position, CreationRange.Start, CreationRange.End - Math.Min(.1, length));
        return new(start, Math.Min(start + 3, CreationRange.End));
    }
    public VideoAnnotation AddText(Point point, string text = "")
    {
        pause(); CommitText(); var id = Guid.NewGuid();
        var annotation = new VideoAnnotation(id, NewTime(), "文字", new(id, text, Math.Clamp(point.X, 0, Math.Max(0, scene.Width - 72)), Math.Clamp(point.Y, 0, Math.Max(0, scene.Height - TextSize - 8)), TextFont, TextSize, Ink, Bold, Italic));
        elements.Add(annotation); SelectedId = id; Rebuild();
        if (text.Length > 0) { Edited?.Invoke(); SelectionChanged?.Invoke(); } else BeginText(id);
        return annotation;
    }
    public void Add(VideoAnnotation annotation) { elements.Add(annotation); SelectedId = annotation.Id; Rebuild(); Edited?.Invoke(); SelectionChanged?.Invoke(); }
    public void SetTime(Guid id, VideoElementTime time, bool commit = true)
    {
        var index = elements.FindIndex(x => x.Id == id); if (index < 0) return;
        elements[index] = elements[index] with { Time = time }; SetPosition(position); DrawChrome(); if (commit) Edited?.Invoke();
    }
    public void CommitEdit() { CommitText(); Edited?.Invoke(); }
    public void DeleteSelected()
    {
        pause(); CommitText(); if (SelectedId == null) return;
        elements.RemoveAll(x => x.Id == SelectedId); SelectedId = null; Rebuild(); Edited?.Invoke(); SelectionChanged?.Invoke();
    }
    public void DuplicateSelected()
    {
        pause(); CommitText(); if (Selected is not { } element) return;
        Add(element.Duplicate());
    }
    VideoAnnotation? copied;
    public void CopySelected() { CommitText(); copied = Selected; }
    public void Paste() { pause(); CommitText(); if (copied != null) Add(copied.Duplicate()); }
    VideoAnnotation ClampMovedText(VideoAnnotation element)=>element.Text is {} text?element with{Text=AnnotationTextEditor.ClampPosition(text,new Point(text.X,text.Y),new Size(scene.Width,scene.Height))}:element;
    public void Nudge(Vector delta) { pause(); CommitText(); if (Selected is { } element) { Replace(ClampMovedText(element.Move(delta))); Edited?.Invoke(); } }
    public void ApplyStyle()
    {
        pause(); if (Selected is not { } element) return;
        var changed = element with { Text = element.Text is { } text ? text with { FontFamily = TextFont, FontSize = TextSize, Color = Ink, Bold = Bold, Italic = Italic } : null,
            Shape = element.Shape is { } shape ? shape with { Color = Ink, Width = StrokeWidth*(element.Name=="荧光笔"?6:1) } : null };
        if(editing==element.Id && editingView!=null) {
            elements[elements.FindIndex(x=>x.Id==element.Id)]=changed;views[element.Id]=(editingView.Frame,changed.Bounds);DrawChrome();
        } else { CommitText(); Replace(changed); }
        Edited?.Invoke();
    }
    void Replace(VideoAnnotation element)
    {
        var index = elements.FindIndex(x => x.Id == element.Id); if (index < 0) return;
        elements[index] = element;
        if (views.Remove(element.Id, out var old)) scene.Children.Remove(old.view);
        var view = element.CreateView(); scene.Children.Insert(Math.Min(index + 1, scene.Children.Count), view); views[element.Id] = (view, element.Bounds);
        SetPosition(position); DrawChrome();
    }
    void Rebuild()
    {
        foreach (var item in views.Values) scene.Children.Remove(item.view); views.Clear();
        foreach (var element in elements) { var view = element.CreateView(); scene.Children.Add(view); views[element.Id] = (view, element.Bounds); }
        SetPosition(position); DrawChrome();
    }
    public void BeginText(Guid id)
    {
        pause(); CommitText(); Select(id); if (Selected?.Text is not { } text) return;
        editing = id; var old = views[id]; scene.Children.Remove(old.view);
        editingView=new AnnotationTextEditor(text,"视频内文字");var editor=editingView;var border=editor.Frame;input=editor.Input;
        AnnotationTextEditor.Apply(editor.Frame,editor.Input,editor.Handle,text,Zoom,new Size(scene.Width,scene.Height),true);
        scene.Children.Add(border); views[id] = (border, old.bounds);
        input.TextChanged += (_, _) => {
            var index = elements.FindIndex(x => x.Id == id); if (index >= 0 && input != null) {elements[index] = elements[index] with { Text = elements[index].Text! with { Text = input.Text } };views[id]=(border,elements[index].Bounds);DrawChrome();}
        };
        input.LostKeyboardFocus += (_, _) => { if (!committing && editing==id && Selected?.Text is { } value && !string.IsNullOrWhiteSpace(value.Text)) Edited?.Invoke(); };
        border.MouseLeftButtonDown+=(_,e)=>e.Handled=true;
        editor.Handle.MouseLeftButtonDown+=(_,e)=>{
            if(viewport.PanMode || viewport.SpaceHeld)return;
            BeginTextMove(e.GetPosition(scene));e.Handled=true;
        };
        var focusInput = input;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { if (editing == id && focusInput.IsVisible) { focusInput.Focus(); focusInput.CaretIndex = focusInput.Text.Length; } }));
        DrawChrome(); SelectionChanged?.Invoke();
    }
    public void CommitText(bool notify = true)
    {
        if (editing == null || committing) return;
        committing = true;
        try { editing = null; input = null; editingView=null; elements.RemoveAll(x => x.Text != null && string.IsNullOrWhiteSpace(x.Text.Text)); if (!elements.Any(x => x.Id == SelectedId)) SelectedId = null; Rebuild(); }
        finally { committing = false; }
        if (notify) Edited?.Invoke();
    }
    internal void BeginTextMove(Point point)
    {
        CommitText();if(Selected!=null){viewport.Focus();StartGesture(point,null);}
    }
    public void CancelGesture()
    {
        if (drawing != null) scene.Children.Remove(drawing);
        drawing = null; var restore = before; before = null; gestureStart = null; handle = null;
        if (Mouse.Captured == scene) scene.ReleaseMouseCapture();
        if (restore != null) Replace(restore);
    }
    Point Bound(Point point) => new(Math.Clamp(point.X, 0, scene.Width), Math.Clamp(point.Y, 0, scene.Height));
    void Down(object sender, MouseButtonEventArgs e)
    {
        if (viewport.PanMode || viewport.SpaceHeld || CreationRange.End <= 0 || editing != null && input?.IsKeyboardFocusWithin == true && input.IsMouseOver) return;
        BeginStroke(e.GetPosition(scene),Keyboard.Modifiers.HasFlag(ModifierKeys.Alt),e.ClickCount); e.Handled = true;
    }
    internal void BeginStroke(Point point,bool drawThrough = false,int clickCount = 1)
    {
        pause(); CommitText(); viewport.Focus(); point = Bound(point);
        var hit = Selected is { } chosen && chosen.Time.Contains(position) && views.TryGetValue(chosen.Id,out var selectedView) && selectedView.bounds.Contains(point) ? chosen
            : elements.AsEnumerable().Reverse().FirstOrDefault(x => x.Time.Contains(position) && views.TryGetValue(x.Id, out var v) && (x.Shape != null ? x.Shape.Hit(point, 6 / Zoom) : v.bounds.Contains(point)));
        if (hit != null && !drawThrough) {
            Select(hit.Id); if (clickCount >= 2 && hit.Text != null) BeginText(hit.Id); else StartGesture(point, null);
        } else if (Tool == "文字") AddText(point);
        else if (Tool == "选择") ClearSelection();
        else {
            ClearSelection(); gestureStart = point;
            drawing = Tool is "画笔" or "荧光笔" ? new Polyline { Points = new PointCollection { point, point }, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round } : new System.Windows.Shapes.Path();
            drawing.Stroke = new SolidColorBrush(Ink); drawing.StrokeThickness = StrokeWidth*(Tool=="荧光笔"?6:1);drawing.Opacity=Tool=="荧光笔"?.35:1; drawing.IsHitTestVisible = false; scene.Children.Add(drawing); scene.CaptureMouse();
        }
    }
    void StartGesture(Point point, string? kind)
    {
        before = Selected; if (before == null) return;
        gestureStart = point; handle = kind; scene.CaptureMouse();
    }
    void Move(object sender, MouseEventArgs e)
        => MoveStroke(e.GetPosition(scene),Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
    internal void MoveStroke(Point point,bool shift = false)
    {
        if (gestureStart is not Point start) return;
        if (before != null) {
            var matrix = Matrix.Identity; var bounds = before.Bounds;
            if (handle == "rotate") {
                var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                var angle = (Math.Atan2(point.Y - center.Y, point.X - center.X) - Math.Atan2(start.Y - center.Y, start.X - center.X)) * 180 / Math.PI;
                if (shift) angle = Math.Round(angle / 15) * 15; matrix.RotateAt(angle, center.X, center.Y);
            } else if (handle != null) {
                var anchor = handle switch { "tl" => bounds.BottomRight, "tr" => bounds.BottomLeft, "bl" => bounds.TopRight, _ => bounds.TopLeft };
                double sx = Math.Abs(start.X - anchor.X) < .01 ? 1 : Math.Clamp((point.X - anchor.X) / (start.X - anchor.X), .05, 20), sy = Math.Abs(start.Y - anchor.Y) < .01 ? 1 : Math.Clamp((point.Y - anchor.Y) / (start.Y - anchor.Y), .05, 20);
                if (shift || before.Text != null) sx = sy = Math.Max(.05, Math.Max(sx, sy)); matrix.ScaleAt(sx, sy, anchor.X, anchor.Y);
            } else matrix.Translate(point.X - start.X, point.Y - start.Y);
            var changed=before.Transform(matrix);Replace(handle==null?ClampMovedText(changed):changed); return;
        }
        point = Bound(point);
        if (drawing is Polyline line) { if ((point - line.Points[^1]).Length > .8 / Zoom) line.Points.Add(point); return; }
        if (drawing is not System.Windows.Shapes.Path path) return;
        if (Tool is "箭头" or "直线") {
            point = ShapeBounds.LineEnd(start, point, new Size(scene.Width, scene.Height), shift);
            var geometry = new StreamGeometry(); using (var c = geometry.Open()) {
                c.BeginFigure(start, false, false); c.LineTo(point, true, false);
                if (Tool == "箭头") { var angle = Math.Atan2(point.Y - start.Y, point.X - start.X); var length = StrokeWidth * 6; c.BeginFigure(new Point(point.X - length * Math.Cos(angle - .5), point.Y - length * Math.Sin(angle - .5)), false, false); c.LineTo(point, true, false); c.LineTo(new Point(point.X - length * Math.Cos(angle + .5), point.Y - length * Math.Sin(angle + .5)), true, false); }
            } path.Data = geometry;
        } else { var rect = ShapeBounds.Calculate(start, point, new Size(scene.Width, scene.Height), shift); path.Data = Tool == "圆形" ? new EllipseGeometry(rect) : new RectangleGeometry(rect); }
    }
    void Up(object sender, MouseButtonEventArgs e)
    {
        if (gestureStart == null) return; EndStroke(e.GetPosition(scene),Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)); e.Handled = true;
    }
    internal void EndStroke(Point point,bool shift = false)
    {
        if (gestureStart == null) return;
        MoveStroke(point,shift); var active = drawing; var moved = before != null; drawing = null; before = null; gestureStart = null; handle = null;
        if (Mouse.Captured == scene) scene.ReleaseMouseCapture();
        if (active != null) {
            scene.Children.Remove(active); var shape = ShapeAnnotation.FromView(active);
            if (!shape.Bounds.IsEmpty && (shape.Bounds.Width > StrokeWidth + 1 || shape.Bounds.Height > StrokeWidth + 1)) Add(new(shape.Id, NewTime(), Tool, Shape: shape));
        } else if (moved) {Select(SelectedId);Edited?.Invoke();}
        DrawChrome();
    }
    void DrawChrome()
    {
        chrome.Children.Clear();
        if (Selected is not { } selected || !selected.Time.Contains(position) || !views.TryGetValue(selected.Id, out var view)) return;
        if(editing!=null && editingView is { } editor && selected.Text is { } editingTextModel) {
            AnnotationTextEditor.Apply(editor.Frame,editor.Input,editor.Handle,editingTextModel,Zoom,new Size(scene.Width,scene.Height),true);chrome.Children.Add(editor.Handle);return;
        }
        var bounds = view.bounds; if (bounds.IsEmpty) return; var zoom = Math.Max(.01, Zoom); bounds.Inflate(4 / zoom, 4 / zoom);
        bounds.Inflate(Math.Max(0,(40/zoom-bounds.Width)/2),Math.Max(0,(40/zoom-bounds.Height)/2));
        var frame = new TextSelectionFrame { Width = bounds.Width, Height = bounds.Height, Zoom = zoom, BorderBrush = Theme.Accent, IsHitTestVisible = false };
        Canvas.SetLeft(frame, bounds.X); Canvas.SetTop(frame, bounds.Y); chrome.Children.Add(frame);
        var visible = new Rect(viewport.TranslatePoint(new Point(8, 8), scene), viewport.TranslatePoint(new Point(Math.Max(9, viewport.ActualWidth - 8), Math.Max(9, viewport.ActualHeight - 8)), scene));
        Point Clamp(Point p) => new(Math.Clamp(p.X, visible.Left, visible.Right), Math.Clamp(p.Y, visible.Top, visible.Bottom));
        foreach (var (kind, location) in new[] { ("tl", bounds.TopLeft), ("tr", bounds.TopRight), ("br", bounds.BottomRight), ("bl", bounds.BottomLeft), ("rotate", new Point(bounds.X + bounds.Width / 2, bounds.Top - 26 / zoom)) }) {
            var point = Clamp(location); var target = new Border { Width = 18 / zoom, Height = 18 / zoom, Background = Brushes.Transparent, Cursor = kind == "rotate" ? Cursors.Hand : kind is "tl" or "br" ? Cursors.SizeNWSE : Cursors.SizeNESW };
            Shape dot = kind == "rotate" ? new Ellipse() : new Rectangle(); dot.Width = dot.Height = 10 / zoom; dot.Stroke = Theme.Accent; dot.Fill = Theme.ControlSurface; dot.StrokeThickness = 1.5 / zoom; target.Child = dot;
            target.MouseLeftButtonDown += (_, e) => { pause(); viewport.Focus(); StartGesture(e.GetPosition(scene), kind); e.Handled = true; };
            Canvas.SetLeft(target, point.X - 9 / zoom); Canvas.SetTop(target, point.Y - 9 / zoom); chrome.Children.Add(target);
        }
        if (gestureStart != null) return;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (text, action) in new (string, Action)[] { ("复制", DuplicateSelected), ("删除", DeleteSelected), ("完成", ClearSelection) }) {
            var button = Theme.Button(text, action); button.Width = 48; button.Height = 30; button.MinHeight = 0; button.Padding = new Thickness(0); button.Margin = new Thickness(row.Children.Count == 0 ? 0 : 8, 0, 0, 0); button.Focusable = false; row.Children.Add(button);
        }
        var actions = new Border { Child = row, Padding = new Thickness(6), Background = Theme.ControlSurface, CornerRadius = new CornerRadius(6), RenderTransform = new ScaleTransform(1 / zoom, 1 / zoom) };
        Canvas.SetLeft(actions, Math.Clamp(bounds.Left, visible.Left, Math.Max(visible.Left, visible.Right - 172 / zoom)));
        Canvas.SetTop(actions, Math.Clamp(bounds.Bottom + 12 / zoom, visible.Top, Math.Max(visible.Top, visible.Bottom - 42 / zoom))); chrome.Children.Add(actions);
    }
}
