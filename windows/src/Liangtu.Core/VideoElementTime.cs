namespace Liangtu.Core;

/// <summary>Source-video time, with an inclusive start and exclusive end.</summary>
public readonly record struct VideoElementTime
{
    public double Start { get; }
    public double End { get; }
    public double Length => End - Start;
    public VideoElementTime(double start, double end)
    {
        if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start) throw new ArgumentOutOfRangeException(nameof(end));
        Start = start; End = end;
    }
    public bool Contains(double position) => position >= Start && position < End;
    public VideoElementTime Move(double delta, double duration)
    {
        if (!double.IsFinite(delta) || !double.IsFinite(duration) || duration < End) return this;
        var start = Math.Clamp(Start + delta, 0, duration - Length);
        return new(start, start + Length);
    }
    public VideoElementTime ResizeStart(double value) => double.IsFinite(value) ? new(Math.Clamp(value, 0, End - Math.Min(.1, Length)), End) : this;
    public VideoElementTime ResizeEnd(double value, double duration) => double.IsFinite(value) && double.IsFinite(duration) && duration >= End
        ? new(Start, Math.Clamp(value, Start + Math.Min(.1, Length), duration)) : this;
    public VideoSelection? Clip(VideoSelection trim)
    {
        var start = Math.Max(Start, trim.Start); var end = Math.Min(End, trim.End);
        return end > start ? new(start - trim.Start, end - trim.Start) : null;
    }
}
