using System.Windows.Media.Animation;
namespace Liangtu;

// Render-only travel in the line's own DIP coordinates. No data/file operation
// is delayed by a flight; the parent removes clocks on hide/display changes.
internal sealed class MediaTransferVisual : FrameworkElement
{
    readonly BitmapSource bitmap;
    readonly Rect from, to;
    readonly bool intoBasket;
    readonly double lip;
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(MediaTransferVisual), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    internal Rect CurrentBounds => Travel(from, to, Progress);
    public MediaTransferVisual(BitmapSource bitmap, Rect from, Rect to, bool intoBasket, double lip)
    { this.bitmap = bitmap; this.from = from; this.to = to; this.intoBasket = intoBasket; this.lip = lip; IsHitTestVisible = false; }
    internal static Rect Travel(Rect from, Rect to, double progress)
    {
        if (progress <= 0) return from;
        if (progress >= 1) return to;
        var t = MotionCurve.Ease(progress);
        var a = new Point(from.X + from.Width / 2, from.Y + from.Height / 2); var d = new Point(to.X + to.Width / 2, to.Y + to.Height / 2);
        var dx = d.X - a.X; var lift = Math.Min(100, 35 + Math.Abs(dx) * .07);
        var point = MotionCurve.Bezier(a, new Point(a.X + dx * .3, a.Y - lift), new Point(d.X - dx * .18, d.Y - lift * .7), d, t);
        var width = from.Width + (to.Width - from.Width) * t; var height = from.Height + (to.Height - from.Height) * t;
        return new Rect(point.X - width / 2, point.Y - height / 2, width, height);
    }
    public void Play(Action completed, bool releaseDelay = true)
    {
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(.78)) { BeginTime = TimeSpan.FromSeconds(releaseDelay ? .14 : 0), FillBehavior = FillBehavior.HoldEnd };
        animation.Completed += (_, _) => completed(); BeginAnimation(ProgressProperty, animation);
    }
    public void Stop() => BeginAnimation(ProgressProperty, null);
    protected override void OnRender(DrawingContext dc)
    {
        var t = Progress;
        if (t <= 0 || t >= 1) return;
        var rect = Travel(from, to, t);
        if (intoBasket) {
            var full = new RectangleGeometry(new Rect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight)));
            var front = new RectangleGeometry(new Rect(Math.Max(0, to.X - 12), Math.Max(0, lip), to.Width + 24, Math.Max(1, ActualHeight - lip)));
            dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude, full, front));
        }
        dc.PushTransform(new RotateTransform(Math.Sin(t * Math.PI) * (intoBasket ? 9 : -4), rect.X + rect.Width / 2, rect.Y + rect.Height / 2));
        dc.DrawRoundedRectangle(Theme.Brush("#F8FBFF"), new Pen(Theme.Brush("#7195AC"), .8), rect, 6, 6);
        var imageRect = rect; imageRect.Inflate(-Math.Min(3, rect.Width / 8), -Math.Min(3, rect.Height / 8));
        dc.DrawImage(bitmap, imageRect);
        dc.Pop(); if (intoBasket) dc.Pop();
    }
}
