namespace Liangtu;
// Gesture deadlines must run while the overlay is hidden, independently of rendering.
public sealed class GestureScheduler : IDisposable
{
    readonly Gesture policy = new();
    readonly DispatcherTimer timer;
    readonly Func<double> now;
    readonly Action<GestureAction> dispatch;
    public GestureScheduler(Action<GestureAction> dispatch, Func<double>? now = null)
    {
        this.dispatch = dispatch;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        this.now = now ?? (() => clock.Elapsed.TotalSeconds);
        timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(12) };
        timer.Tick += (_, _) => { var action = policy.Tick(this.now()); if (action != GestureAction.None) dispatch(action); if (!policy.HasPending) timer.Stop(); };
    }
    public GestureAction Down(double time, int clicks = 1)
    {
        var a = policy.Down(time, clicks);
        timer.Start();
        return a;
    }
    public GestureAction Move(double distance) => policy.Move(distance);
    public GestureAction Up(double time, double interval)
    {
        var a = policy.Up(time, interval);
        if (policy.HasPending)
            timer.Start();
        else timer.Stop();
        return a;
    }
    public void Cancel()
    {
        policy.Cancel();
        timer.Stop();
    }
    public void Dispose() => Cancel();
}
