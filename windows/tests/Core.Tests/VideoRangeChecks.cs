using Liangtu.Core;
static class VideoRangeChecks
{
    public static void Run(Action<string, Action> test)
    {
        void Near(double expected, double actual) { if (Math.Abs(expected - actual) > .00001) throw new Exception($"Expected {expected}, got {actual}"); }
        test("video handles cannot cross or leave media bounds", () => {
            var r = new VideoRange(12); r.SetStart(2.5); r.SetEnd(7.5); r.SetStart(10); Near(7.4, r.Start);
            r.SetEnd(-2); Near(7.5, r.End); r.SetStart(-20); Near(0, r.Start); r.SetEnd(40); Near(12, r.End);
        });
        test("short recording retains a valid full range", () => { var r = new VideoRange(.04); r.SetStart(.03); Near(0, r.Start); r.SetEnd(0); Near(.04, r.End); });
        test("range reset restores full duration and seeks clamp", () => { var r = new VideoRange(80); r.SetStart(10); r.SetEnd(20); Near(10, r.ClampSelection(0)); Near(20, r.ClampSelection(30)); r.Reset(); Near(0, r.Start); Near(80, r.End); });
        test("time input accepts fractional seconds and rejects malformed values", () => {
            if (!VideoRange.TryParseTime("01:02.500", out var s)) throw new Exception("Clock time rejected"); Near(62.5,s);
            if (!VideoRange.TryParseTime("2.5", out s)) throw new Exception("Seconds rejected"); Near(2.5,s);
            foreach(var text in new[]{"-1","NaN","Infinity","1:70","a",""}) if(VideoRange.TryParseTime(text,out _)) throw new Exception("Invalid input accepted: "+text);
        });
        test("invalid duration and updates do not poison range", () => { var r = new VideoRange(double.NaN); Near(0,r.Duration); r = new VideoRange(5); r.SetStart(double.NaN); r.SetEnd(double.PositiveInfinity); Near(0,r.Start); Near(5,r.End); });
    }
}
