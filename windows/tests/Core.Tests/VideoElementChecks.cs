using Liangtu.Core;
using System.Reflection;

static class VideoElementChecks
{
    public static void Run(Action<string, Action> test)
    {
        // Reflection keeps the RED run executable before the new model exists.
        object Span(double start, double end) => Activator.CreateInstance(typeof(VideoRange).Assembly.GetType("Liangtu.Core.VideoElementTime") ?? throw new Exception("Timed video element model is missing"), start, end)!;
        object? Call(object span, string method, params object[] args) => span.GetType().GetMethod(method)!.Invoke(span, args);
        double Read(object span, string property) => (double)span.GetType().GetProperty(property)!.GetValue(span)!;
        void Near(double want, double actual) { if (Math.Abs(want - actual) > .000001) throw new Exception($"Expected {want}, got {actual}"); }
        test("timed element includes start but excludes end", () => { var span = Span(2, 5); if (!(bool)Call(span, "Contains", 2d)! || (bool)Call(span, "Contains", 5d)!) throw new Exception("Wrong visibility boundary"); });
        test("moving element preserves length at both video edges", () => { var span = Span(2, 5); var left = Call(span, "Move", -10d, 8d)!; Near(0, Read(left, "Start")); Near(3, Read(left, "End")); var right = Call(span, "Move", 20d, 8d)!; Near(5, Read(right, "Start")); Near(8, Read(right, "End")); });
        test("export intersects element and shifts from source time to trimmed time", () => { var clipped = (VideoSelection)Call(Span(2, 8), "Clip", new VideoSelection(5, 10))!; Near(0, clipped.Start); Near(3, clipped.End); });
        test("elements touching trim boundary have no export interval", () => { if (Call(Span(2, 5), "Clip", new VideoSelection(5, 10)) != null) throw new Exception("Zero-length element exported"); });
        test("timed element rejects nonfinite and inverted ranges", () => { foreach (var pair in new[] { (double.NaN, 3d), (0d, double.PositiveInfinity), (-1d, 3d), (5d, 2d), (2d, 2d) }) { try { Span(pair.Item1, pair.Item2); throw new Exception("Invalid range accepted"); } catch (TargetInvocationException ex) when (ex.InnerException is ArgumentOutOfRangeException) { } } });
    }
}
