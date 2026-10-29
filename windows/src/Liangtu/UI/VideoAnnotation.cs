namespace Liangtu;

internal sealed record VideoAnnotation(Guid Id, VideoElementTime Time, string Name, TextAnnotation? Text = null, ShapeAnnotation? Shape = null)
{
    public Rect Bounds
    {
        get {
            if (Shape != null) return Shape.Bounds;
            if (Text == null) return Rect.Empty;
            var label = Text.CreateLabel(); label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return new RotateTransform(Text.Angle).TransformBounds(new Rect(label.DesiredSize)) with { X = new RotateTransform(Text.Angle).TransformBounds(new Rect(label.DesiredSize)).X + Text.X, Y = new RotateTransform(Text.Angle).TransformBounds(new Rect(label.DesiredSize)).Y + Text.Y };
        }
    }
    public UIElement CreateView()
    {
        if (Shape != null) return Shape.CreateView();
        if (Text == null) throw new InvalidOperationException("标注缺少内容");
        var label = Text.CreateLabel(); label.RenderTransform = new RotateTransform(Text.Angle);
        Canvas.SetLeft(label, Text.X); Canvas.SetTop(label, Text.Y); return label;
    }
    public VideoAnnotation Move(Vector delta) => this with { Text = Text is { } text ? text with { X = text.X + delta.X, Y = text.Y + delta.Y } : null, Shape = Shape?.Move(delta) };
    public VideoAnnotation Transform(Matrix matrix)
    {
        if (Shape != null) return this with { Shape = Shape.Apply(new MatrixTransform(matrix)) };
        if (Text == null) return this;
        var origin = matrix.Transform(new Point(Text.X, Text.Y));
        var scale = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
        var angle = Math.Atan2(matrix.M12, matrix.M11) * 180 / Math.PI;
        return this with { Text = Text with { X = origin.X, Y = origin.Y, FontSize = Math.Clamp(Text.FontSize * scale, 6, 512), Angle = Text.Angle + angle } };
    }
    public VideoAnnotation Duplicate() { var id = Guid.NewGuid(); var copy = Move(new Vector(16, 16)); return copy with { Id = id, Text = copy.Text is { } text ? text with { Id = id } : null, Shape = copy.Shape is { } shape ? shape with { Id = id } : null }; }
}

internal sealed record VideoEditState(VideoSelection Selection, IReadOnlyList<VideoAnnotation> Elements);

internal sealed class VideoEditHistory
{
    readonly List<VideoEditState> revisions = new();
    int index;
    public VideoEditState Current => revisions[index];
    public bool CanUndo => index > 0;
    public bool CanRedo => index < revisions.Count - 1;
    public bool IsDirty => CanUndo;
    public VideoEditHistory(VideoEditState initial) => revisions.Add(Copy(initial));
    static VideoEditState Copy(VideoEditState state) => state with { Elements = state.Elements.Where(e=>e.Text==null||!string.IsNullOrWhiteSpace(e.Text.Text)).ToArray() };
    public void Commit(VideoEditState state)
    {
        state=Copy(state);
        if (state.Selection == Current.Selection && state.Elements.SequenceEqual(Current.Elements)) return;
        if (CanRedo) revisions.RemoveRange(index + 1, revisions.Count - index - 1);
        revisions.Add(state); if (revisions.Count > 100) revisions.RemoveAt(0); index = revisions.Count - 1;
    }
    public bool Undo() { if (!CanUndo) return false; index--; return true; }
    public bool Redo() { if (!CanRedo) return false; index++; return true; }
}
