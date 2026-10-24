namespace Liangtu.Core;

public readonly record struct VideoSelection(double Start, double End);

public sealed class VideoSelectionHistory
{
    readonly List<VideoSelection> revisions = new();
    int index;
    public VideoSelection Current => revisions[index];
    public bool CanUndo => index > 0;
    public bool CanRedo => index < revisions.Count - 1;
    public VideoSelectionHistory(VideoSelection initial) { Validate(initial); revisions.Add(initial); }
    static void Validate(VideoSelection value)
    {
        if (!double.IsFinite(value.Start) || !double.IsFinite(value.End) || value.Start < 0 || value.End < value.Start) throw new ArgumentOutOfRangeException(nameof(value));
    }
    public void Commit(VideoSelection value)
    {
        Validate(value);
        if (Math.Abs(Current.Start-value.Start)<.000001 && Math.Abs(Current.End-value.End)<.000001) return;
        if (CanRedo) revisions.RemoveRange(index+1,revisions.Count-index-1);
        revisions.Add(value); if (revisions.Count>100) revisions.RemoveAt(0); index=revisions.Count-1;
    }
    public bool Undo() { if(!CanUndo)return false; index--;return true; }
    public bool Redo() { if(!CanRedo)return false; index++;return true; }
}
