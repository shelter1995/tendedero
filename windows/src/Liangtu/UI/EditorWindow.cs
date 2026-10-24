using System.Windows.Shapes;
namespace Liangtu;

public sealed partial class EditorWindow : Window
{
    readonly App app;
    readonly Photo photo;
    readonly EditHistory history;
    readonly Canvas canvas = new() { Background = Brushes.Transparent, ClipToBounds = true };
    readonly Image image = new();
    readonly EditorViewport viewport;
    readonly TextBlock status = new(), dimensions = new(), zoomLabel = new();
    readonly Dictionary<Key, RadioButton> toolKeys = new();
    Button undoButton = null!, redoButton = null!;
    string tool = "画笔";
    Color color = Color.FromRgb(226, 77, 64);
    double brushWidth = 4;
    Point? start;
    Point eraseLast;
    Point lastPointer;
    Shape? active;
    Polyline? stroke;
    RasterEraser? eraser;
    bool saving;
    internal EditorViewport Viewport => viewport;
    bool Drawing => start != null || eraser != null;

    public EditorWindow(App app, Photo photo)
    {
        this.app = app; this.photo = photo;
        history = new EditHistory(MediaTools.LoadImage(photo.Path));
        color = AnnotationColors.Parse(app.Settings.AnnotationColor);
        InitializeTextDefaults();
        Title = System.IO.Path.GetFileName(photo.Path) + " — 标注";
        Width = Math.Min(1120, SystemParameters.WorkArea.Width - 40);
        Height = Math.Min(800, SystemParameters.WorkArea.Height - 40);
        MinWidth = 760; MinHeight = 460;
        Theme.WindowStyle(this);
        canvas.Children.Add(image); viewport = new EditorViewport(canvas);
        Content = BuildLayout();
        Restore();
        System.Windows.Automation.AutomationProperties.SetName(canvas, "图片标注画布");
        canvas.MouseLeftButtonDown += (_, e) => {
            if (viewport.PanMode || viewport.SpaceHeld) return;
            viewport.Focus();
            BeginStroke(e.GetPosition(canvas));
            if (Drawing) canvas.CaptureMouse();
            e.Handled = true;
        };
        canvas.MouseMove += (_, e) => { if (Drawing) { MoveStroke(e.GetPosition(canvas), Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)); e.Handled = true; } };
        canvas.MouseLeftButtonUp += (_, e) => { if (Drawing) { EndStroke(e.GetPosition(canvas), Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)); if (Mouse.Captured == canvas) canvas.ReleaseMouseCapture(); e.Handled = true; } };
        canvas.LostMouseCapture += (_, _) => { if (Drawing) CancelStroke(); };
        PreviewKeyDown += OnKey;
        PreviewKeyUp += (_, e) => {
            if (e.Key == Key.Space && e.OriginalSource is not TextBox) { viewport.SpaceHeld = false; e.Handled = true; }
            else if (e.Key is Key.LeftShift or Key.RightShift && UpdateShapeConstraint(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))) e.Handled = true;
        };
        Deactivated += (_, _) => { viewport.SpaceHeld = false; viewport.EndPan(); CancelStroke(); };
        Closing += (_, e) => {
            if (saving) { e.Cancel = true; return; }
            CancelStroke(); CommitText();
            if (history.IsDirty && MessageBox.Show(this, "放弃尚未保存的标注？", "晾图", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
        };
    }
    void Restore()
    {
        canvas.Children.Clear(); canvas.Children.Add(image);
        var bitmap = history.Current.Image; image.Source = bitmap;
        image.Width = canvas.Width = bitmap.PixelWidth; image.Height = canvas.Height = bitmap.PixelHeight;
        viewport.SetImageSize(bitmap.PixelWidth, bitmap.PixelHeight);
        texts.Clear(); texts.AddRange(history.Current.Texts); selectedText = null;
        RebuildTextViews();
        dimensions.Text = $"{bitmap.PixelWidth} × {bitmap.PixelHeight} px";
        undoButton.IsEnabled = history.CanUndo; redoButton.IsEnabled = history.CanRedo;
    }
    internal BitmapSource Flatten(bool includeText = true)
    {
        // View transforms live on the parent, never on this image-pixel canvas.
        canvas.Measure(new Size(canvas.Width, canvas.Height)); canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height)); canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)canvas.Width, (int)canvas.Height, 96, 96, PixelFormats.Pbgra32);
        textLayer.Visibility = Visibility.Hidden;
        try { bitmap.Render(canvas); }
        finally { textLayer.Visibility = Visibility.Visible; }
        if (includeText) {
            var export = new Canvas { Width = canvas.Width, Height = canvas.Height, ClipToBounds = true };
            foreach (var text in texts) {
                var label = text.CreateLabel(); label.RenderTransform = new RotateTransform(text.Angle);
                Canvas.SetLeft(label, text.X); Canvas.SetTop(label, text.Y); export.Children.Add(label);
            }
            export.Measure(new Size(canvas.Width, canvas.Height)); export.Arrange(new Rect(0, 0, canvas.Width, canvas.Height)); export.UpdateLayout();
            bitmap.Render(export);
        }
        bitmap.Freeze(); return bitmap;
    }
    void Commit(BitmapSource bitmap, BitmapSource? baseline = null)
    {
        history.Commit(bitmap, baseline ?? history.Current.Baseline, texts.Where(t => !string.IsNullOrWhiteSpace(t.Text)).ToArray()); Restore();
    }
    internal void Undo()
    {
        if (Drawing) { CancelStroke(); return; }
        CommitText();
        if (history.Undo()) { Restore(); status.Text = "已撤销"; }
    }
    internal void Redo()
    {
        if (Drawing) return;
        CommitText();
        if (history.Redo()) { Restore(); status.Text = "已重做"; }
    }
    internal void Rotate()
    {
        CancelStroke(); CommitText();
        var rotated = new TransformedBitmap(history.Current.Image, new RotateTransform(90)); rotated.Freeze();
        var baseline = new TransformedBitmap(history.Current.Baseline, new RotateTransform(90)); baseline.Freeze();
        for (int i = 0; i < texts.Count; i++) texts[i] = texts[i].Rotate(canvas.Height);
        Commit(rotated, baseline);
    }
    Point Bounded(Point point) => new(Math.Clamp(point.X, 0, canvas.Width), Math.Clamp(point.Y, 0, canvas.Height));
    internal void BeginStroke(Point point)
    {
        if (Drawing || tool == "平移") return;
        lastPointer = point;
        var p = Bounded(point);
        if (tool == "文字")
        {
            AddText(p); return;
        }
        if (tool == "橡皮擦")
        {
            eraser = new RasterEraser(history.Current.Image, history.Current.Baseline); eraseLast = p;
            eraser.Erase(p, p, brushWidth * 5); image.Source = eraser.Image; return;
        }
        start = p;
        if (tool is "画笔" or "荧光笔")
        {
            stroke = new Polyline { Stroke = new SolidColorBrush(color), StrokeThickness = brushWidth * (tool == "荧光笔" ? 6 : 1), Opacity = tool == "荧光笔" ? .35 : 1, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
            stroke.Points.Add(p); stroke.Points.Add(new Point(p.X + .01, p.Y)); active = stroke;
        }
        else
        {
            active = tool switch { "直线" or "箭头" => new System.Windows.Shapes.Path(), "圆形" => new Ellipse(), _ => new Rectangle() };
            active.Stroke = tool == "裁剪" ? Brushes.White : new SolidColorBrush(color); active.StrokeThickness = brushWidth; active.Fill = Brushes.Transparent;
            if (tool == "裁剪") active.StrokeDashArray = new DoubleCollection { 4, 3 };
        }
        canvas.Children.Add(active);
    }
    internal void MoveStroke(Point point, bool constrain = false)
    {
        lastPointer = point;
        var q = Bounded(point);
        if (eraser != null) { eraser.Erase(eraseLast, q, brushWidth * 5); eraseLast = q; return; }
        if (start is not Point p || active == null) return;
        if (stroke != null) { stroke.Points.Add(q); return; }
        if (active is System.Windows.Shapes.Path path)
        {
            q = ShapeBounds.LineEnd(p, point, new Size(canvas.Width, canvas.Height), constrain);
            if ((q - p).Length < .5) { path.Data = Geometry.Empty; return; }
            var geometry = new StreamGeometry();
            using (var c = geometry.Open()) {
                c.BeginFigure(p, false, false); c.LineTo(q, true, false);
                if (tool == "箭头") {
                    var angle = Math.Atan2(q.Y - p.Y, q.X - p.X); var length = brushWidth * 6;
                    c.BeginFigure(new Point(q.X - length * Math.Cos(angle - .5), q.Y - length * Math.Sin(angle - .5)), false, false);
                    c.LineTo(q, true, false); c.LineTo(new Point(q.X - length * Math.Cos(angle + .5), q.Y - length * Math.Sin(angle + .5)), true, false);
                }
            }
            path.Data = geometry;
        }
        else { var rect = ShapeBounds.Calculate(p, point, new Size(canvas.Width, canvas.Height), constrain && (tool is "矩形" or "圆形")); Canvas.SetLeft(active, rect.X); Canvas.SetTop(active, rect.Y); active.Width = rect.Width; active.Height = rect.Height; }
    }
    internal bool UpdateShapeConstraint(bool constrain)
    {
        if (start == null || active == null || tool is not ("矩形" or "圆形" or "直线" or "箭头")) return false;
        MoveStroke(lastPointer, constrain); return true;
    }
    internal void EndStroke(Point point, bool constrain = false)
    {
        if (!Drawing) return;
        MoveStroke(point, constrain);
        if (tool == "裁剪" && start is Point p)
        {
            if (active != null) canvas.Children.Remove(active);
            var rect = Rect.Intersect(new Rect(p, Bounded(point)), new Rect(0, 0, canvas.Width, canvas.Height));
            ClearGesture();
            if (!rect.IsEmpty && rect.Width >= 3 && rect.Height >= 3) {
                var area = new Int32Rect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                var cropped = new CroppedBitmap(history.Current.Image, area); cropped.Freeze();
                var baseline = new CroppedBitmap(history.Current.Baseline, area); baseline.Freeze();
                for (int i = 0; i < texts.Count; i++) texts[i] = texts[i].Crop(area.X, area.Y);
                Commit(cropped, baseline);
            } else Restore();
        }
        else if (active is Rectangle or Ellipse && (active.Width < .5 || active.Height < .5)) { ClearGesture(); Restore(); }
        else if (active is System.Windows.Shapes.Path path && (path.Data == null || path.Data.IsEmpty())) { ClearGesture(); Restore(); }
        else if (eraser == null && stroke == null && start is Point origin && (Bounded(point) - origin).Length < .5) { ClearGesture(); Restore(); }
        else { var bitmap = Flatten(false); ClearGesture(); Commit(bitmap); }
    }
    void ClearGesture() { start = null; active = null; stroke = null; eraser = null; }
    void CancelStroke()
    {
        if (!Drawing) return;
        ClearGesture(); if (Mouse.Captured == canvas) canvas.ReleaseMouseCapture(); Restore();
    }
    void OnKey(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is ComboBox || e.OriginalSource is System.Windows.Controls.Primitives.ToggleButton and not RadioButton) return;
        if (e.OriginalSource is TextBox) {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { Save(); e.Handled = true; }
            else if (e.Key == Key.Escape && selectedText != null) { CommitText(); selectedText = null; RebuildTextViews(); viewport.Focus(); e.Handled = true; }
            return;
        }
        if (e.Key is Key.LeftShift or Key.RightShift && UpdateShapeConstraint(true)) { e.Handled = true; return; }
        if (e.Key == Key.Space && e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase) return;
        var mods = Keyboard.Modifiers;
        if (e.Key == Key.Z && mods == ModifierKeys.Control) Undo();
        else if (e.Key == Key.Y && mods == ModifierKeys.Control || e.Key == Key.Z && mods == (ModifierKeys.Control | ModifierKeys.Shift)) Redo();
        else if (e.Key == Key.S && mods == ModifierKeys.Control) Save();
        else if (e.Key == Key.Escape) { if (Drawing) CancelStroke(); else if (selectedText != null) { CommitText(); selectedText = null; RebuildTextViews(); } else Close(); }
        else if (e.Key == Key.Delete && selectedText != null) DeleteText();
        else if (e.Key == Key.Space && mods == ModifierKeys.None) viewport.SpaceHeld = true;
        else if (Drawing) return;
        else if (e.Key is Key.Add or Key.OemPlus) viewport.ZoomBy(1.25);
        else if (e.Key is Key.Subtract or Key.OemMinus) viewport.ZoomBy(.8);
        else if (e.Key is Key.D0 or Key.NumPad0 && mods == ModifierKeys.Control || e.Key == Key.F && mods == ModifierKeys.None) viewport.Fit();
        else if (e.Key is Key.D1 or Key.NumPad1 && mods == ModifierKeys.Control) viewport.SetZoom(1);
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down && mods is ModifierKeys.None or ModifierKeys.Shift && viewport.IsKeyboardFocusWithin) {
            double step = mods == ModifierKeys.Shift ? 160 : 40;
            viewport.Pan(e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0, e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0);
        }
        else if (mods == ModifierKeys.None && toolKeys.TryGetValue(e.Key, out var button)) button.IsChecked = true;
        else return;
        e.Handled = true;
    }
    async void Save()
    {
        if (saving) return;
        CancelStroke(); CommitText();
        if (!history.IsDirty) { Close(); return; }
        saving = true; IsEnabled = false;
        try {
            var bitmap = Flatten();
            if (MediaTools.NeedsSeparateEdit(photo.Path)) {
                var target = FileRules.UniquePath(Settings.Inbox, System.IO.Path.GetFileNameWithoutExtension(photo.Path) + " edited.png"); MediaTools.SavePng(bitmap, target); await app.Store.Add(target);
            } else { MediaTools.SavePng(bitmap, photo.Path); await app.Store.Reload(photo); }
            history.MarkSaved(); saving = false; Close();
        } catch (Exception e) { MessageBox.Show(this, "保存失败，原图已保留。\n" + e.Message, "晾图"); }
        finally { saving = false; IsEnabled = true; }
    }
}
