using System.Windows.Media.Animation;
namespace Liangtu;

internal static class LineMotion
{
    static readonly DependencyProperty ReflowProperty = DependencyProperty.RegisterAttached("Reflow", typeof(TranslateTransform), typeof(LineMotion));
    internal static TranslateTransform Reflow(UIElement card)
    {
        if (card.GetValue(ReflowProperty) is TranslateTransform existing) return existing;
        var motion = new TranslateTransform();
        var previous = card.RenderTransform;
        card.RenderTransform = new TransformGroup { Children = { previous, motion } };
        card.SetValue(ReflowProperty, motion);
        return motion;
    }
    public static void Place(UIElement card, double x, double y, bool animate)
    {
        var motion = Reflow(card);
        // Include the current presentation offset when a second removal interrupts
        // the first. Commit layout once; the compositor moves the retained visual.
        var oldX = Canvas.GetLeft(card) + motion.X; var oldY = Canvas.GetTop(card) + motion.Y;
        card.BeginAnimation(Canvas.LeftProperty, null); card.BeginAnimation(Canvas.TopProperty, null);
        motion.BeginAnimation(TranslateTransform.XProperty, null); motion.BeginAnimation(TranslateTransform.YProperty, null);
        motion.X = motion.Y = 0;
        Canvas.SetLeft(card, x); Canvas.SetTop(card, y);
        if (!animate || !SystemParameters.ClientAreaAnimation || !double.IsFinite(oldX) || !double.IsFinite(oldY)) return;
        Animate(TranslateTransform.XProperty, oldX - x); Animate(TranslateTransform.YProperty, oldY - y);
        void Animate(DependencyProperty property, double from)
        {
            if (Math.Abs(from) < .001) return;
            var animation = new DoubleAnimation(from, 0, TimeSpan.FromSeconds(.38)) {
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var clock = animation.CreateClock();
            motion.ApplyAnimationClock(property, clock);
            clock.Controller!.SeekAlignedToLastTick(TimeSpan.Zero, TimeSeekOrigin.BeginTime);
        }
    }
    public static void PrepareHidden(TranslateTransform slide, double hiddenY)
    {
        slide.BeginAnimation(TranslateTransform.XProperty, null); slide.X = 0;
        slide.BeginAnimation(TranslateTransform.YProperty, null); slide.Y = hiddenY;
    }
    public static AnimationClock Reveal(TranslateTransform slide, double hiddenY, bool restart)
    {
        var from = restart ? hiddenY : slide.Y;
        PrepareHidden(slide, 0);
        var animation = new DoubleAnimation(from, 0, TimeSpan.FromSeconds(SystemParameters.ClientAreaAnimation ? .44 : 0)) {
            FillBehavior = FillBehavior.Stop, EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };
        return Start(slide, animation);
    }
    public static AnimationClock Retract(TranslateTransform slide, double hiddenY)
    {
        var from = slide.Y; PrepareHidden(slide, hiddenY);
        var animation = new DoubleAnimation(from, hiddenY, TimeSpan.FromSeconds(SystemParameters.ClientAreaAnimation ? .24 : 0)) {
            FillBehavior = FillBehavior.Stop, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        return Start(slide, animation);
    }
    static AnimationClock Start(TranslateTransform slide, DoubleAnimation animation)
    {
        var clock = animation.CreateClock();
        slide.ApplyAnimationClock(TranslateTransform.YProperty, clock);
        // Applying a clock alone exposes the base (destination) value until the
        // next time-manager tick. Sample zero synchronously, including reversals.
        clock.Controller!.SeekAlignedToLastTick(TimeSpan.Zero, TimeSeekOrigin.BeginTime);
        return clock;
    }
}
