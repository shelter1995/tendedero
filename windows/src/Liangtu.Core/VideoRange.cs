using System.Globalization;
namespace Liangtu.Core;

public sealed class VideoRange
{
    public double Duration { get; }
    public double Start { get; private set; }
    public double End { get; private set; }
    public double MinimumLength => Math.Min(.1, Duration);
    public VideoRange(double duration) { Duration = double.IsFinite(duration) ? Math.Max(0, duration) : 0; Reset(); }
    public void Reset() { Start = 0; End = Duration; }
    public void SetStart(double value) { if (double.IsFinite(value)) Start = Math.Clamp(value, 0, Math.Max(0, End - MinimumLength)); }
    public void SetEnd(double value) { if (double.IsFinite(value)) End = Math.Clamp(value, Math.Min(Duration, Start + MinimumLength), Duration); }
    public double ClampSelection(double value) => Math.Clamp(double.IsFinite(value) ? value : Start, Start, End);
    public static string Format(double seconds) => TimeSpan.FromSeconds(Math.Max(0, double.IsFinite(seconds) ? seconds : 0)).ToString(seconds >= 3600 ? @"hh\:mm\:ss\.fff" : @"mm\:ss\.fff", CultureInfo.InvariantCulture);
    public static bool TryParseTime(string text, out double seconds)
    {
        seconds = 0;
        var parts = text.Trim().Split(':');
        if (parts.Length is < 1 or > 3) return false;
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n) || n < 0 || (i > 0 && n >= 60) || (i < parts.Length - 1 && n != Math.Truncate(n))) return false;
            seconds = seconds * 60 + n;
        }
        return double.IsFinite(seconds) && seconds <= TimeSpan.MaxValue.TotalSeconds;
    }
}
