namespace Liangtu;

public sealed partial class EditorWindow
{
    readonly List<ShapeAnnotation> shapes = new();
    readonly Canvas shapeLayer = new() { IsHitTestVisible = false };
    readonly Canvas selectionLayer = new();
    Guid? selectedShape;
    Point? selectionStart;
    ShapeAnnotation? shapeBefore;
    TextAnnotation? textBefore;
    ShapeAnnotation? copiedShape;
    TextAnnotation? copiedText;
    int pasteCount;
    internal IReadOnlyList<ShapeAnnotation> Shapes => shapes;
    internal Guid? SelectedShape => selectedShape;
    bool HasSelection => selectedShape != null || selectedText != null;

    void RebuildShapeViews()
    {
        shapeLayer.Children.Clear();
        foreach (var shape in shapes) shapeLayer.Children.Add(shape.CreateView());
        if (!canvas.Children.Contains(shapeLayer)) canvas.Children.Add(shapeLayer);
        viewport.AttachOverlay(selectionLayer);
        Panel.SetZIndex(selectionLayer, 200); UpdateSelectionView();
    }
    void UpdateSelectionView()
    {
        selectionLayer.Children.Clear();
        DrawTransformControls();
    }
    static Rect TextBounds(TextAnnotation text)
    {
        var label = text.CreateLabel(); label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var bounds = new RotateTransform(text.Angle).TransformBounds(new Rect(label.DesiredSize)); bounds.Offset(text.X, text.Y); return bounds;
    }
    void SelectAt(Point point)
    {
        var previousShape = selectedShape; var previousBounds = SelectedBounds;
        CommitText(); editingText = null; selectedText = null; selectedShape = null;
        // Text is rendered above drawn shapes, so test it first.
        foreach (var text in texts.AsEnumerable().Reverse()) if (TextBounds(text).Contains(point)) { selectedText = text.Id; break; }
        if (selectedText == null) foreach (var shape in shapes.AsEnumerable().Reverse())
            if (shape.Hit(point, 5 / viewport.Zoom)) { selectedShape = shape.Id; break; }
        if (!HasSelection && previousShape != null && previousBounds.Contains(point)) selectedShape = previousShape;
        if (texts.FirstOrDefault(t => t.Id == selectedText) is TextAnnotation selected) ReadTextStyle(selected);
        RebuildTextViews(); UpdateSelectionView();
        status.Text = !ObjectTransformsAvailable ? tool == "裁剪" ? "裁剪工具保持启用 · 拖动选择保留区域 · 右键操作选中标注" : "橡皮擦保持启用 · 擦除标注，保留原图 · 右键操作选中标注"
            : HasSelection ? "拖动移动 · 角点缩放 · 圆点旋转 · Shift 约束 · 双击文字编辑 · Alt 穿过标注绘制" : "空白处绘制 · 直接点击标注调整 · Alt 穿过标注绘制";
    }
    void BeginSelection(Point point)
    {
        SelectAt(point); if (!HasSelection) return;
        StartTransform(point, null);
    }
    void DragSelection(Point point, bool constrain = false)
    {
        if (selectionStart is not Point startPoint) return;
        if (transformHandle != null) { ApplyTransform(point, constrain); return; }
        var delta = point - startPoint;
        if (shapeBefore != null) { int index = shapes.FindIndex(s => s.Id == shapeBefore.Id); if (index >= 0) shapes[index] = delta.Length < .001 ? shapeBefore : shapeBefore.Move(delta); RebuildShapeViews(); }
        if (textBefore != null) { MoveTextTo(new Point(textBefore.X + delta.X, textBefore.Y + delta.Y), false); UpdateSelectionView(); }
    }
    void EndSelection()
    {
        selectionStart = null; shapeBefore = null; textBefore = null; transformHandle = null; CommitObjects(); UpdateSelectionView();
    }
    void CommitObjects()
    {
        var meaningful = texts.Where(t => !string.IsNullOrWhiteSpace(t.Text)).ToArray();
        if (!shapes.SequenceEqual(history.Current.Shapes) || !meaningful.SequenceEqual(history.Current.Texts))
            history.Commit(history.Current.Image, history.Current.Baseline, meaningful, shapes);
        undoButton.IsEnabled = history.CanUndo; redoButton.IsEnabled = history.CanRedo;
    }
    internal void DeleteSelection()
    {
        if (!HasSelection) return;
        CommitText(); shapes.RemoveAll(s => s.Id == selectedShape); texts.RemoveAll(t => t.Id == selectedText);
        selectedShape = selectedText = editingText = null; CommitObjects(); RebuildShapeViews(); RebuildTextViews(); viewport.Focus();
    }
    internal void CopySelection()
    {
        CommitText();
        if (!HasSelection) return;
        copiedShape = shapes.FirstOrDefault(s => s.Id == selectedShape); copiedText = texts.FirstOrDefault(t => t.Id == selectedText); pasteCount = 0;
        status.Text = "已复制标注 · Ctrl+V 在本编辑窗口粘贴";
    }
    internal void PasteSelection()
    {
        if (copiedShape == null && copiedText == null) return;
        CommitText(); editingText = null; selectedShape = selectedText = null; var delta = new Vector(16 * ++pasteCount, 16 * pasteCount);
        if (copiedShape != null) { var copy = copiedShape.Move(delta) with { Id = Guid.NewGuid() }; shapes.Add(copy); selectedShape = copy.Id; }
        if (copiedText != null) { var copy = copiedText with { Id = Guid.NewGuid(), X = copiedText.X + delta.X, Y = copiedText.Y + delta.Y }; texts.Add(copy); selectedText = copy.Id; }
        CommitObjects(); RebuildShapeViews(); RebuildTextViews(); UpdateSelectionView(); viewport.Focus();
    }
    internal void DuplicateSelection() { CopySelection(); PasteSelection(); }
    internal void NudgeSelection(Vector delta)
    {
        if (shapes.FindIndex(s => s.Id == selectedShape) is int index && index >= 0) shapes[index] = shapes[index].Move(delta);
        if (texts.FirstOrDefault(t => t.Id == selectedText) is TextAnnotation text) MoveTextTo(new Point(text.X + delta.X, text.Y + delta.Y), false);
        CommitObjects(); RebuildShapeViews(); UpdateSelectionView();
    }
    void InstallObjectMenu()
    {
        var menu = new ContextMenu();
        MenuItem Item(string title, string shortcut, Action action) { var item = new MenuItem { Header = title, InputGestureText = shortcut }; item.Click += (_, _) => action(); menu.Items.Add(item); return item; }
        var copy = Item("复制标注", "Ctrl+C", CopySelection); var paste = Item("粘贴标注", "Ctrl+V", PasteSelection);
        var duplicate = Item("重复标注", "Ctrl+D", DuplicateSelection); var delete = Item("删除标注", "Delete", DeleteSelection);
        var edit = Item("编辑文字", "双击", () => { if (selectedText is Guid textId) SelectText(textId); });
        canvas.ContextMenu = selectionLayer.ContextMenu = menu;
        canvas.PreviewMouseRightButtonDown += (_, e) => { if (Drawing) CancelStroke(); SelectAt(e.GetPosition(canvas)); };
        menu.Opened += (_, _) => { copy.IsEnabled = duplicate.IsEnabled = delete.IsEnabled = HasSelection; paste.IsEnabled = copiedShape != null || copiedText != null; edit.IsEnabled = selectedText != null; };
    }
    void ClearSelection()
    {
        CommitText(); editingText = selectedText = selectedShape = null; RebuildTextViews(); UpdateSelectionView(); viewport.Focus();
    }
    void EraseObjects(Point from, Point to)
    {
        for (int i = 0; i < shapes.Count; i++) shapes[i] = shapes[i].Erase(from, to, brushWidth * 5);
        RebuildShapeViews();
    }
}
