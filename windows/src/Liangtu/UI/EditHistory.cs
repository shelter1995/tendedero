namespace Liangtu;

internal sealed record EditState(BitmapSource Image, BitmapSource Baseline, int Revision, IReadOnlyList<TextAnnotation> Texts);

internal sealed class EditHistory
{
    readonly Stack<EditState> undo = new(), redo = new();
    int serial, saved;
    public EditState Current { get; private set; }
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public bool IsDirty => Current.Revision != saved;
    public EditHistory(BitmapSource image) => Current = new(image, image, 0, Array.Empty<TextAnnotation>());
    public void Commit(BitmapSource image, BitmapSource baseline, IReadOnlyList<TextAnnotation>? texts = null)
    {
        undo.Push(Current); redo.Clear(); Current = new(image, baseline, ++serial, (texts ?? Current.Texts).ToArray());
        if (undo.Count > 30) { var keep = undo.Take(30).Reverse().ToArray(); undo.Clear(); foreach (var state in keep) undo.Push(state); }
    }
    public bool Undo()
    {
        if (!undo.TryPop(out var state)) return false;
        redo.Push(Current); Current = state; return true;
    }
    public bool Redo()
    {
        if (!redo.TryPop(out var state)) return false;
        undo.Push(Current); Current = state; return true;
    }
    public void MarkSaved() => saved = Current.Revision;
}
