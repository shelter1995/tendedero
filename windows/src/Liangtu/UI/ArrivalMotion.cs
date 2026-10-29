namespace Liangtu;

internal sealed class VisibleArrivalClock
{
    double? start;
    public bool Started => start.HasValue;
    public double Sample(double now, bool visible, double leadIn = 0)
    {
        if (!visible) return -1;
        start ??= now + Math.Max(0, leadIn);
        return Math.Max(0, now - start.Value);
    }
}
internal readonly record struct ArrivalPhase(double PegOffset, double PegOpen, double Picture, bool PictureVisible, bool Finished)
{
    public const double Duration = 1.32;
    public static ArrivalPhase At(double time)
    {
        double Ratio(double from, double duration) => Math.Clamp((time - from) / duration, 0, 1);
        var peg = -22 * (1 - MotionCurve.Ease(Ratio(0, .18)));
        var opening = time < 1.12 ? MotionCurve.Ease(Ratio(.08, .16)) : 1 - MotionCurve.Ease(Ratio(1.12, .20));
        return new(peg, opening, Ratio(.24, .88), time >= .24, time >= Duration);
    }
    internal static ArrivalPhase PinReturn(double time)
    {
        double Ratio(double from,double duration)=>Math.Clamp((time-from)/duration,0,1);
        var opening=time<.65?MotionCurve.Ease(Ratio(0,.12)):1-MotionCurve.Ease(Ratio(.65,.18));
        return new(-22*(1-MotionCurve.Ease(Ratio(0,.18))),opening,Ratio(0,.65),true,time>=.83);
    }
}
internal static class MotionCurve
{
    // CSS-style cubic-bezier(.25,.1,.25,1), evaluated without a JS runtime.
    public static double Ease(double t)
    {
        if (t <= 0) return 0; if (t >= 1) return 1;
        double lo = 0, hi = 1;
        for (var i = 0; i < 24; i++) { var u = (lo + hi) / 2; var x = Cubic(0, .25, .25, 1, u); if (x < t) lo = u; else hi = u; }
        return Cubic(0, .1, 1, 1, (lo + hi) / 2);
    }
    static double Cubic(double a, double b, double c, double d, double t) { var v = 1 - t; return v * v * v * a + 3 * v * v * t * b + 3 * v * t * t * c + t * t * t * d; }
    public static Point Bezier(Point a, Point b, Point c, Point d, double t) => new(Cubic(a.X, b.X, c.X, d.X, t), Cubic(a.Y, b.Y, c.Y, d.Y, t));
}
