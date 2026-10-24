using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Diagnostics;
namespace Liangtu;
public sealed class Card : Canvas
{
    public Photo Photo
    {
        get;
    }
    readonly Border pin;
    readonly Border imageEdge;
    readonly ScaleTransform crossScale = new(.6, .6);
    readonly App app; readonly GestureScheduler gesture; readonly Border frame; readonly Image image; readonly Border cross; readonly Border copied;
    readonly ScaleTransform flightScale = new(1, 1, 125, 14); readonly RotateTransform rotate = new(); readonly TranslateTransform fall = new(); readonly ScaleTransform scale = new(1, 1); readonly Grid group = new();
    readonly TranslateTransform ropeFollow = new();
    readonly Stopwatch clock = Stopwatch.StartNew(); Point down; double nudgeStart, nudgeAmount = 16, nudgeFrom; bool arriving = true; double copiedUntil;
    bool hover, pressed, dragging, falling, landed, cancelDragRequested, arrivalCancelled; double? fallingAt; public bool Busy => pressed || dragging || ContextMenu?.IsOpen == true;
    internal bool RenderingAttached { get; private set; }
    public double CardWidth => frame.Width; public double CardHeight => frame.Height;
    public Card(Photo photo, App app)
    {
        Photo = photo;
        this.app = app;
        gesture = new GestureScheduler(Act, () => clock.Elapsed.TotalSeconds);
        Width = 250;
        Height = 250;
        ClipToBounds = false;
        RenderTransform = ropeFollow;
        image = new Image { Source = photo.Image, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        var content = new Grid();
        content.Children.Add(image);
        imageEdge = new Border { CornerRadius = new CornerRadius(12), BorderBrush = Theme.Brush("#2EFFFFFF"), BorderThickness = new Thickness(.5), IsHitTestVisible = false };
        content.Children.Add(imageEdge);
        frame = Theme.Glass(content);
        frame.Shadow();
        frame.RenderTransform = scale;
        frame.RenderTransformOrigin = new Point(.5, 0);
        frame.HorizontalAlignment = HorizontalAlignment.Center;
        frame.VerticalAlignment = VerticalAlignment.Top;
        frame.Margin = new Thickness(0, 14, 0, 0);
        group.Children.Add(frame);
        Children.Add(group);
        group.RenderTransform = new TransformGroup { Children = { flightScale, rotate, fall } };
        rotate.CenterX = 125;
        rotate.CenterY = 0;
        pin = new Border { Width = 9, Height = 26, CornerRadius = new CornerRadius(3.5), Background = new LinearGradientBrush(new GradientStopCollection { new(Color.FromRgb(179, 179, 179), 0), new(Color.FromRgb(237, 237, 237), .35), new(Color.FromRgb(209, 209, 209), .65), new(Color.FromRgb(158, 158, 158), 1) }, 0), BorderBrush = Theme.Brush("#DDEEEEEE"), BorderThickness = new Thickness(.6), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false, Effect = new DropShadowEffect { BlurRadius = 2, ShadowDepth = 1.5, Opacity = .3 } };
        pin.Child = new Border { Width = 5, Height = 1.4, CornerRadius = new CornerRadius(.7), Background = Theme.Brush("#52000000"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 0, 0) };
        group.Children.Add(pin);
        cross = Theme.Glass(Theme.DiscardIcon(photo.InInbox), 10, 0);
        cross.Width = 20;
        cross.Height = 20;
        cross.HorizontalAlignment = HorizontalAlignment.Left;
        cross.VerticalAlignment = VerticalAlignment.Top;
        cross.Margin = new Thickness(3);
        cross.Opacity = 0;
        cross.IsHitTestVisible = false;
        cross.RenderTransform = crossScale;
        cross.RenderTransformOrigin = new Point(.5, .5);
        group.Children.Add(cross);
        if (photo.Video)
        {
            var play = Theme.Glass(new TextBlock { Text = "▶", FontSize = 12, Foreground = Theme.Ink, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, 14, 0);
            play.Width = 28;
            play.Height = 28;
            play.IsHitTestVisible = false;
            content.Children.Add(play);
        }
        copied = Theme.Glass(new TextBlock { Text = "✓  " + Localization.Text("Copied"), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Theme.Ink }, 15, 6);
        copied.HorizontalAlignment = HorizontalAlignment.Center;
        copied.VerticalAlignment = VerticalAlignment.Bottom;
        copied.Margin = new Thickness(0, 0, 0, -16);
        copied.Opacity = 0;
        copied.IsHitTestVisible = false;
        group.Children.Add(copied);
        frame.MouseEnter += (_, _) => { hover = true; VisualState(); };
        frame.MouseLeave += (_, _) => { hover = false; VisualState(); };
        frame.MouseLeftButtonDown += (_, e) => { var p = e.GetPosition(frame); if (cross.Opacity > .1 && HitSurface.Contains(cross, frame, p, 10)) { app.Discard(this); e.Handled = true; return; } down = e.GetPosition(this); pressed = true; frame.CaptureMouse(); Act(gesture.Down(clock.Elapsed.TotalSeconds, e.ClickCount)); VisualState(); e.Handled = true; };
        frame.MouseLeftButtonUp += (_, e) => { if (pressed) { gesture.Up(clock.Elapsed.TotalSeconds, Native.GetDoubleClickTime() / 1000.0); pressed = false; frame.ReleaseMouseCapture(); VisualState(); } e.Handled = true; };
        frame.MouseMove += (_, e) => { if (pressed) { var v = e.GetPosition(this) - down; Act(gesture.Move(v.Length)); } };
        frame.LostMouseCapture += (_, _) => { if (pressed && !dragging) { pressed = false; gesture.Cancel(); VisualState(); } };
        frame.QueryContinueDrag += (_, e) => { if (e.EscapePressed || cancelDragRequested) { e.Action = DragAction.Cancel; e.Handled = true; } };
        frame.MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { gesture.Cancel(); app.Preview(Photo); e.Handled = true; } };
        frame.MouseRightButtonUp += (_, e) => { gesture.Cancel(); e.Handled = true; NativeMenu.Show(app.PhotoMenu(Photo), app.Line); };
        Resize();
        Loaded += (_, _) => SetRenderingActive(true);
        Unloaded += (_, _) => { CancelInput(); SetRenderingActive(false); gesture.Cancel(); };
    }
    void SetRenderingActive(bool active)
    {
        if (RenderingAttached == active) return;
        RenderingAttached = active;
        if (active) CompositionTarget.Rendering += Render; else CompositionTarget.Rendering -= Render;
    }
    public void Resize()
    {
        image.Source = Photo.Image;
        var s = Layout.Photo(Photo.Image.PixelWidth, Photo.Image.PixelHeight, app.Settings.Scale);
        frame.Width = s.width + 8;
        frame.Height = s.height + 8;
        image.Width = s.width;
        image.Height = s.height;
        image.Clip = new RectangleGeometry(new Rect(0, 0, s.width, s.height), 12, 12);
        group.Width = 250;
        group.Height = s.height + 22;
        Height = s.height + 42;
        cross.Margin = new Thickness((250 - frame.Width) / 2 + 3, 17, 0, 0);
    }
    public bool Contains(Point point)
    {
        return IsHitTestVisible && HitSurface.Contains(frame, (Visual)app.Line.Content, point, frame.CornerRadius.TopLeft);
    }
    void VisualState()
    {
        var size = pressed ? .95 : hover ? 1.035 : 1;
        Theme.Animate(scale, ScaleTransform.ScaleXProperty, size, pressed ? .45 : .3);
        Theme.Animate(scale, ScaleTransform.ScaleYProperty, size, pressed ? .45 : .3);
        Theme.Animate(cross, OpacityProperty, hover && !dragging ? 1 : 0, .18);
        Theme.Animate(crossScale, ScaleTransform.ScaleXProperty, hover ? 1 : .6, .18);
        Theme.Animate(crossScale, ScaleTransform.ScaleYProperty, hover ? 1 : .6, .18);
        frame.Effect = new DropShadowEffect { BlurRadius = hover ? 14 : 10, ShadowDepth = hover ? 8 : 5, Opacity = hover ? .26 : .18, Color = Colors.Black, Direction = 270 };
    }
    void Act(GestureAction action)
    {
        if (action == GestureAction.None)
            return;
        switch (action)
        {
            case GestureAction.Copy:
                _ = CopyPhoto();
                break;
            case GestureAction.Open:
                pressed = false;
                frame.ReleaseMouseCapture();
                app.Open(Photo);
                break;
            case GestureAction.Edit:
                pressed = false;
                frame.ReleaseMouseCapture();
                app.Edit(Photo);
                break;
            case GestureAction.Drag:
                Drag();
                break;
        }
        VisualState();
    }
    async Task CopyPhoto()
    {
        try { await app.Store.Copy(Photo); }
        catch { /* Store publishes the final user-facing error exactly once. */ }
    }
    public void Nudge(double degrees)
    {
        arriving = false;
        nudgeFrom = rotate.Angle - Photo.Tilt;
        nudgeAmount = degrees;
        nudgeStart = clock.Elapsed.TotalSeconds;
    }
    public void SetCopied(bool value)
    {
        copiedUntil = value ? clock.Elapsed.TotalSeconds + 1.2 : 0;
        Theme.Animate(copied, OpacityProperty, value ? 1 : 0, .2);
        if (value)
            Nudge(3);
    }
    public void CancelInput()
    {
        if (dragging)
            cancelDragRequested = true;
        pressed = false;
        gesture.Cancel();
        if (Mouse.Captured == frame)
            frame.ReleaseMouseCapture();
        Native.ReleaseOwnedCapture(frame);
        if (!dragging)
            VisualState();
    }
    void Render(object? sender, EventArgs e)
    {
        if (!IsVisible)
            return;
        var now = clock.Elapsed.TotalSeconds;
        if (!falling)
        {
            if (!arrivalCancelled && Photo.Origin is Rect origin && now < .65)
            {
                var tFly = now / .65;
                var eased = tFly < .5 ? 4 * tFly * tFly * tFly : 1 - Math.Pow(-2 * tFly + 2, 3) / 2;
                var originTopLeft = app.Line.PointFromScreen(origin.TopLeft);
                var originBottomRight = app.Line.PointFromScreen(origin.BottomRight);
                var originLocal = new Rect(originTopLeft, originBottomRight);
                var destinationX = Canvas.GetLeft(this) + 125;
                var destinationY = Canvas.GetTop(this) + 14;
                flightScale.ScaleX = 1 + (originLocal.Width / CardWidth - 1) * (1 - eased);
                flightScale.ScaleY = 1 + (originLocal.Height / CardHeight - 1) * (1 - eased);
                fall.X = (originLocal.X + originLocal.Width / 2 - destinationX) * (1 - eased);
                fall.Y = (originLocal.Y - destinationY) * (1 - eased) - Math.Sin(eased * Math.PI) * 30;
                var decoration = Math.Clamp((tFly - .35) / .65, 0, 1);
                pin.Opacity = decoration;
                imageEdge.Opacity = decoration;
                imageEdge.CornerRadius = new CornerRadius(12 * decoration);
                frame.Padding = new Thickness(4 * decoration);
                frame.CornerRadius = new CornerRadius(16 * decoration);
                frame.Width = image.Width + 8 * decoration;
                frame.Height = image.Height + 8 * decoration;
                image.Clip = new RectangleGeometry(new Rect(0, 0, image.Width, image.Height), 12 * decoration, 12 * decoration);
                IsHitTestVisible = false;
            }
            else
            {
                pin.Opacity = 1;
                imageEdge.Opacity = 1;
                imageEdge.CornerRadius = new CornerRadius(12);
                frame.Padding = new Thickness(4);
                frame.CornerRadius = new CornerRadius(16);
                frame.Width = image.Width + 8;
                frame.Height = image.Height + 8;
                image.Clip = new RectangleGeometry(new Rect(0, 0, image.Width, image.Height), 12, 12);
                flightScale.ScaleX = flightScale.ScaleY = 1;
                fall.X = 0;
                IsHitTestVisible = true;
                if (Photo.Origin != null)
                {
                    fall.Y = 0;
                    if (!landed)
                    {
                        landed = true;
                        Nudge(2.2);
                    }
                }
                else
                    fall.Y = !arrivalCancelled && now < .6 ? -46 * Math.Exp(-8 * now) * Math.Cos(8 * now) : 0;
            }
        }
        var t = Math.Max(0, now - nudgeStart);
        if (!dragging)
            rotate.Angle = Photo.Tilt + (arriving ? SpringDisplacement(16, 46, 2.6, t) : t < .3 ? nudgeFrom + (nudgeAmount - nudgeFrom) * (1 - Math.Pow(1 - t / .3, 3)) : SpringDisplacement(nudgeAmount, 38, 2.4, t - .3));
        if (copiedUntil > 0 && now > copiedUntil)
        {
            copiedUntil = 0;
            Theme.Animate(copied, OpacityProperty, 0, .2);
        }
        if (fallingAt is double start)
        {
            var v = Math.Clamp((now - start) / .55, 0, 1);
            var ease = v * v * v;
            fall.Y = 520 * ease;
            rotate.Angle = Photo.Tilt + (Photo.Tilt * 7 + 20) * ease;
            Opacity = 1 - ease;
        }
    }
    static double SpringDisplacement(double amount, double stiffness, double damping, double t)
    {
        var d = damping / 2;
        var w = Math.Sqrt(stiffness - d * d);
        return amount * Math.Exp(-d * t) * (Math.Cos(w * t) + d / w * Math.Sin(w * t));
    }
    internal void FinishArrival()
    {
        if (falling) return;
        arrivalCancelled = true; arriving = false; landed = true;
        flightScale.ScaleX = flightScale.ScaleY = 1; fall.X = fall.Y = 0;
        nudgeStart = clock.Elapsed.TotalSeconds; nudgeFrom = nudgeAmount = 0; rotate.Angle = Photo.Tilt;
        IsHitTestVisible = true;
    }
    internal void FollowRope(double offset) => ropeFollow.Y = offset;
    internal void RevealSway() { if (!falling && !dragging) Nudge(4 + Photo.Tilt * .4); }
    void ActTick(GestureAction a)
    {
        if (a != GestureAction.None)
            Act(a);
    }
    public void StartFall()
    {
        if (falling)
            return;
        gesture.Cancel();
        flightScale.ScaleX = flightScale.ScaleY = 1;
        fall.X = 0;
        falling = true;
        gesture.Cancel();
        fallingAt = clock.Elapsed.TotalSeconds;
        IsHitTestVisible = false;
    }
    public static DataObject DragData(Photo photo)
    {
        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, new[] { photo.Path });
        data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(2)));
        if (!photo.Video)
            data.SetImage(MediaTools.LoadImage(photo.Path));
        return data;
    }
    void Drag()
    {
        gesture.Cancel();
        pressed = false;
        frame.ReleaseMouseCapture();
        cancelDragRequested = false;
        dragging = true;
        app.Line.Busy = true;
        frame.Opacity = .45;
        try
        {
            var data = new ShellDataObject(DragData(Photo));
            using var preview = new DragImage(Photo.Image, data, image.ActualWidth, image.ActualHeight, VisualTreeHelper.GetDpi(this).DpiScaleX);
            var effect = DragDrop.DoDragDrop(frame, data, DragDropEffects.Copy | DragDropEffects.Move);
            Settings.Log($"Drag result={effect}, sourceExists={File.Exists(Photo.Path)}");
            if (effect == DragDropEffects.None)
            {
                Native.GetCursorPos(out var cursor);
                var extent = image.TransformToAncestor(app.Line).TransformBounds(new Rect(image.RenderSize));
                var destination = new Rect(app.Line.PointToScreen(extent.TopLeft), app.Line.PointToScreen(extent.BottomRight));
                var scale = VisualTreeHelper.GetDpi(image);
                var width = image.ActualWidth * scale.DpiScaleX; var height = image.ActualHeight * scale.DpiScaleY;
                new ReturnFlight(Photo.Image, new Rect(cursor.X - width / 2, cursor.Y - height / 2, width, height), destination).Show();
            }
            if ((effect & DragDropEffects.Move) != 0 && !File.Exists(Photo.Path))
                app.Store.TakeDown(Photo);
        }
        catch (Exception e) { Settings.Log("Drag " + e.Message); }
        finally { dragging = false; pressed = false; if (Mouse.Captured == frame) frame.ReleaseMouseCapture(); Native.ReleaseOwnedCapture(frame); app.Line.Busy = false; frame.Opacity = 1; app.Store.Prune(); _ = PruneLater(); }
    }
    async Task PruneLater()
    {
        await Task.Delay(650);
        app.Store.Prune();
    }
}
static class ShadowExtensions
{
    public static void Shadow(this Border b) => b.Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 5, Opacity = .18, Direction = 270 };
}
