using System.Windows.Media.Animation;
namespace Liangtu;

internal static class LineMotion
{
    public static void Place(UIElement card, double x, double y, bool animate)
    {
        var oldX = Canvas.GetLeft(card); var oldY = Canvas.GetTop(card);
        // Retire previous clocks before setting the new display's local layout.
        card.BeginAnimation(Canvas.LeftProperty, null); card.BeginAnimation(Canvas.TopProperty, null);
        Canvas.SetLeft(card, x); Canvas.SetTop(card, y);
        if (!animate || !double.IsFinite(oldX) || !double.IsFinite(oldY)) return;
        Animate(Canvas.LeftProperty, oldX, x); Animate(Canvas.TopProperty, oldY, y);
        void Animate(DependencyProperty property, double from, double to)
        {
            if (Math.Abs(from - to) < .001) return;
            card.BeginAnimation(property, new DoubleAnimation(from, to, TimeSpan.FromSeconds(.55)) {
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new SpringEase { Response = .55, Damping = .78, Duration = .55, EasingMode = EasingMode.EaseIn }
            });
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
        var animation = new DoubleAnimation(from, 0, TimeSpan.FromSeconds(.8)) {
            FillBehavior = FillBehavior.Stop, EasingFunction = new SpringEase { EasingMode = EasingMode.EaseIn }
        };
        var clock = animation.CreateClock(); slide.ApplyAnimationClock(TranslateTransform.YProperty, clock); return clock;
    }
}
