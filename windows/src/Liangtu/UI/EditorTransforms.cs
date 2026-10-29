namespace Liangtu;

public sealed partial class EditorWindow
{
    // Handles live in image coordinates; their drawn and hit areas are divided by
    // zoom so they retain the same DIP size as the document zooms.
    enum ObjectHandle { TopLeft, TopRight, BottomRight, BottomLeft, Rotate }
    readonly Dictionary<ObjectHandle, Point> transformHandles = new();
    ObjectHandle? transformHandle;
    Rect transformBounds;
    bool ObjectTransformsAvailable => tool is not ("裁剪" or "橡皮擦");

    Rect SelectedBounds => selectedShape is Guid id ? shapes.FirstOrDefault(s => s.Id == id)?.Bounds ?? Rect.Empty
        : texts.FirstOrDefault(t => t.Id == selectedText) is TextAnnotation text ? TextBounds(text) : Rect.Empty;

    Rect ViewportArea(double inset)
    {
        var area = new Rect(0, 0, canvas.Width, canvas.Height);
        if (viewport.ActualWidth > 0 && viewport.ActualHeight > 0) {
            area = new Rect(viewport.TranslatePoint(new Point(), canvas), viewport.TranslatePoint(new Point(viewport.ActualWidth, viewport.ActualHeight), canvas));
        } else {
            // Before the first layout the viewport has no dimensions. Keep the
            // chrome stable until its real viewport arrives on Changed.
            area.Inflate(Math.Max(0, (220 / viewport.Zoom - area.Width) / 2), Math.Max(0, (160 / viewport.Zoom - area.Height) / 2));
        }
        if (area.IsEmpty) return area;
        area.Inflate(-Math.Min(inset, area.Width / 3), -Math.Min(inset, area.Height / 3));
        return area;
    }
    Rect ReachableArea(double inset)
    {
        var area = ViewportArea(inset);
        if (area.IsEmpty) return area;
        // Prefer image edges when they leave space for distinct controls. At
        // very low zoom or extreme aspect ratios use the surrounding viewport.
        if (canvas.Width * viewport.Zoom >= 80) {
            double left = Math.Max(area.Left, inset), right = Math.Min(area.Right, canvas.Width - inset);
            if (right - left >= 40 / viewport.Zoom) { area.X = left; area.Width = right - left; }
        }
        if (canvas.Height * viewport.Zoom >= 100) {
            double top = Math.Max(area.Top, inset), bottom = Math.Min(area.Bottom, canvas.Height - inset);
            if (bottom - top >= 40 / viewport.Zoom) { area.Y = top; area.Height = bottom - top; }
        }
        return area;
    }
    static Point ClampPoint(Point point, Rect area) => new(Math.Clamp(point.X, area.Left, area.Right), Math.Clamp(point.Y, area.Top, area.Bottom));

    void DrawTransformControls()
    {
        transformHandles.Clear();
        var bounds = SelectedBounds;
        if (!ObjectTransformsAvailable || bounds.IsEmpty || editingText != null) return;
        var zoom = viewport.Zoom;
        var visible = ReachableArea(10 / zoom);
        if (visible.IsEmpty) return;
        var frameBounds = bounds; frameBounds.Inflate(4 / zoom, 4 / zoom);
        frameBounds.Inflate(Math.Max(0, (40 / zoom - frameBounds.Width) / 2), Math.Max(0, (40 / zoom - frameBounds.Height) / 2));
        var frame = new System.Windows.Shapes.Rectangle { Width = frameBounds.Width, Height = frameBounds.Height,
            Stroke = Theme.Accent, StrokeThickness = 1 / zoom, StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false };
        Canvas.SetLeft(frame, frameBounds.X); Canvas.SetTop(frame, frameBounds.Y); selectionLayer.Children.Add(frame);
        transformHandles[ObjectHandle.TopLeft] = ClampPoint(frameBounds.TopLeft, visible);
        transformHandles[ObjectHandle.TopRight] = ClampPoint(frameBounds.TopRight, visible);
        transformHandles[ObjectHandle.BottomRight] = ClampPoint(frameBounds.BottomRight, visible);
        transformHandles[ObjectHandle.BottomLeft] = ClampPoint(frameBounds.BottomLeft, visible);
        var rotation = ClampPoint(new Point(frameBounds.X + frameBounds.Width / 2, frameBounds.Top - 28 / zoom), visible);
        // At the top edge put the rotation target inside the image, below the top
        // handles. The pivot still stays at the original object's center.
        if (Math.Abs(rotation.Y - transformHandles[ObjectHandle.TopLeft].Y) < 18 / zoom)
            rotation = ClampPoint(new Point(rotation.X, transformHandles[ObjectHandle.TopLeft].Y + 28 / zoom), visible);
        transformHandles[ObjectHandle.Rotate] = rotation;
        var stem = new System.Windows.Shapes.Line { X1 = rotation.X, Y1 = rotation.Y, X2 = rotation.X,
            Y2 = Math.Clamp(frameBounds.Top, visible.Top, visible.Bottom), Stroke = Theme.Accent, StrokeThickness = 1 / zoom, IsHitTestVisible = false };
        selectionLayer.Children.Add(stem);
        foreach (var (kind, point) in transformHandles) {
            var target = new Border { Width = 18 / zoom, Height = 18 / zoom, Background = Brushes.Transparent,
                Cursor = kind == ObjectHandle.Rotate ? Cursors.Hand : kind is ObjectHandle.TopLeft or ObjectHandle.BottomRight ? Cursors.SizeNWSE : Cursors.SizeNESW };
            target.MouseLeftButtonDown += (_, e) => {
                if (!ObjectTransformsAvailable || viewport.SpaceHeld || viewport.PanMode || Drawing) return;
                var point = e.GetPosition(canvas);
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) BeginStroke(point, true);
                else StartTransform(point, kind);
                viewport.Focus(); if (Drawing) canvas.CaptureMouse(); e.Handled = true;
            };
            Canvas.SetLeft(target, point.X - 9 / zoom); Canvas.SetTop(target, point.Y - 9 / zoom); selectionLayer.Children.Add(target);
            System.Windows.Shapes.Shape handle = kind == ObjectHandle.Rotate ? new System.Windows.Shapes.Ellipse() : new System.Windows.Shapes.Rectangle();
            double size = kind == ObjectHandle.Rotate ? 12 : 10;
            handle.Width = handle.Height = size / zoom; handle.Stroke = Theme.Accent; handle.Fill = Theme.ControlSurface;
            handle.StrokeThickness = 1.5 / zoom; handle.IsHitTestVisible = false;
            System.Windows.Automation.AutomationProperties.SetName(handle, "对象手柄 " + kind);
            Canvas.SetLeft(handle, point.X - size / 2 / zoom); Canvas.SetTop(handle, point.Y - size / 2 / zoom); selectionLayer.Children.Add(handle);
        }
        if (selectionStart != null) return;
        var actionsArea = ViewportArea(10 / zoom);
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal };
        var actions = new Border { Child = actionRow, Padding = new Thickness(6), CornerRadius = new CornerRadius(6),
            Background = Theme.ControlSurface, RenderTransform = new ScaleTransform(1 / zoom, 1 / zoom) };
        void Action(string label, string tip, Action action) {
            var button = new Button { Content = label, Width = 48, Height = 30, MinHeight = 0, MinWidth = 0,
                Margin = new Thickness(actionRow.Children.Count == 0 ? 0 : 8, 0, 0, 0), Padding = new Thickness(0), FontSize = 12, ToolTip = tip, Focusable = false };
            System.Windows.Automation.AutomationProperties.SetName(button, tip);
            button.Click += (_, e) => { action(); e.Handled = true; }; actionRow.Children.Add(button);
        }
        Action("复制", "复制标注 · 立即创建一份偏移副本 · Ctrl+D", DuplicateSelection);
        Action("删除", "删除标注 · Delete", DeleteSelection);
        Action("完成", "取消选择 · Esc", ClearSelection);
        actions.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var actionSize = actions.DesiredSize;
        var y = frameBounds.Bottom + 14 / zoom;
        if (y + actionSize.Height / zoom > actionsArea.Bottom) y = frameBounds.Top - (actionSize.Height + 14) / zoom;
        Canvas.SetLeft(actions, Math.Clamp(frameBounds.Left, actionsArea.Left, Math.Max(actionsArea.Left, actionsArea.Right - actionSize.Width / zoom)));
        Canvas.SetTop(actions, Math.Clamp(y, actionsArea.Top, Math.Max(actionsArea.Top, actionsArea.Bottom - actionSize.Height / zoom)));
        selectionLayer.Children.Add(actions);
    }
    bool TryBeginTransform(Point point)
    {
        if (!ObjectTransformsAvailable || editingText != null || !HasSelection) return false;
        var radius = 9 / viewport.Zoom;
        foreach (var handle in transformHandles.Reverse()) {
            if ((point - handle.Value).Length > radius) continue;
            StartTransform(point, handle.Key); return true;
        }
        return false;
    }
    void UpdateObjectCursor(Point point)
    {
        if (!ObjectTransformsAvailable || viewport.SpaceHeld || viewport.PanMode) { canvas.Cursor = null; return; }
        if (editingText != null) { canvas.Cursor = null; return; }
        ObjectHandle? hover = transformHandle;
        if (hover == null) {
            var radius = 9 / viewport.Zoom;
            foreach (var handle in transformHandles.Reverse()) if ((point - handle.Value).Length <= radius) { hover = handle.Key; break; }
        }
        canvas.Cursor = hover switch {
            ObjectHandle.TopLeft or ObjectHandle.BottomRight => Cursors.SizeNWSE,
            ObjectHandle.TopRight or ObjectHandle.BottomLeft => Cursors.SizeNESW,
            ObjectHandle.Rotate => Cursors.Hand,
            _ => selectionStart != null || HasSelection && SelectedBounds.Contains(point) ? Cursors.SizeAll : null
        };
    }
    void StartTransform(Point point, ObjectHandle? handle)
    {
        selectionStart = point; transformHandle = handle; transformBounds = SelectedBounds;
        shapeBefore = shapes.FirstOrDefault(s => s.Id == selectedShape); textBefore = texts.FirstOrDefault(t => t.Id == selectedText);
    }
    void ApplyTransform(Point point, bool constrain)
    {
        if (selectionStart is not Point origin || transformHandle is not ObjectHandle handle || transformBounds.IsEmpty
            || !double.IsFinite(point.X) || !double.IsFinite(point.Y)) return;
        var matrix = Matrix.Identity;
        double factor = 1, angle = 0;
        if (handle == ObjectHandle.Rotate) {
            var center = new Point(transformBounds.X + transformBounds.Width / 2, transformBounds.Y + transformBounds.Height / 2);
            if ((point - center).Length < 1e-6 || (origin - center).Length < 1e-6) return;
            angle = (Math.Atan2(point.Y - center.Y, point.X - center.X) - Math.Atan2(origin.Y - center.Y, origin.X - center.X)) * 180 / Math.PI;
            if (constrain) angle = Math.Round(angle / 15) * 15;
            matrix.RotateAt(angle, center.X, center.Y);
        } else {
            bool left = handle is ObjectHandle.TopLeft or ObjectHandle.BottomLeft;
            bool top = handle is ObjectHandle.TopLeft or ObjectHandle.TopRight;
            var anchor = new Point(left ? transformBounds.Right : transformBounds.Left, top ? transformBounds.Bottom : transformBounds.Top);
            var delta = point - origin;
            var sx = Math.Clamp(1 + delta.X * (left ? -1 : 1) / Math.Max(1, transformBounds.Width), .02, 100);
            var sy = Math.Clamp(1 + delta.Y * (top ? -1 : 1) / Math.Max(1, transformBounds.Height), .02, 100);
            if (constrain || textBefore != null) {
                factor = Math.Abs(sx - 1) >= Math.Abs(sy - 1) ? sx : sy;
                if (textBefore != null) factor = Math.Clamp(textBefore.FontSize * factor, 6, 512) / textBefore.FontSize;
                sx = sy = factor;
            }
            matrix.ScaleAt(sx, sy, anchor.X, anchor.Y);
        }
        if (shapeBefore != null) {
            var index = shapes.FindIndex(s => s.Id == shapeBefore.Id);
            if (index >= 0) shapes[index] = matrix.IsIdentity ? shapeBefore : shapeBefore.Apply(new MatrixTransform(matrix));
            RebuildShapeViews();
        }
        if (textBefore != null) {
            var index = texts.FindIndex(t => t.Id == textBefore.Id);
            if (index < 0) return;
            var position = matrix.Transform(new Point(textBefore.X, textBefore.Y));
            var result = textBefore with { X = position.X, Y = position.Y, FontSize = textBefore.FontSize * factor, Angle = textBefore.Angle + angle };
            if (handle != ObjectHandle.Rotate) {
                // Native TextBox padding/minimum width do not scale with the font.
                // Re-anchor the opposite corner after measuring the actual layout.
                var actual = TextBounds(result);
                bool left = handle is ObjectHandle.TopLeft or ObjectHandle.BottomLeft, top = handle is ObjectHandle.TopLeft or ObjectHandle.TopRight;
                result = result with { X = result.X + (left ? transformBounds.Right - actual.Right : transformBounds.Left - actual.Left),
                    Y = result.Y + (top ? transformBounds.Bottom - actual.Bottom : transformBounds.Top - actual.Top) };
            }
            texts[index] = matrix.IsIdentity ? textBefore : result;
            ApplyTextView(texts[index]); ReadTextStyle(texts[index]); SyncTextProperties(); UpdateSelectionView();
        }
    }
}
