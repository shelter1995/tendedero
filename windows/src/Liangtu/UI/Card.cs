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
    readonly ClothesPegVisual peg = new();
    readonly PegHold pegHold = new();
    readonly DispatcherTimer pegTimer = new() { Interval = TimeSpan.FromMilliseconds(20) };
    Point pegDown;
    int pegGeneration;
    bool pegCommitted, transferring;
    Rect? arrivalOrigin;
    bool arrivalOriginMeasured;
    readonly VisibleArrivalClock arrivalClock = new();
    PinAppearance? returnAppearance;
    readonly bool quickReturn;
    double? returnMotionStarted;
    readonly TranslateTransform pegArrival = new();
    bool arrivalPending;
    ArrivalFlight? arrivalFlight;
    bool retiringArrivalFlight;
    TimeSpan? arrivalLandingFrame;
    int arrivalLandingFrames;
    readonly RectangleGeometry laneClip = new();
    double displayScale;
    (double left, double right, double height)? lane;
    readonly Border imageEdge;
    readonly int frameVariant;
    readonly ScaleTransform crossScale = new(.6, .6);
    readonly App app; readonly GestureScheduler gesture; readonly Border frame; readonly Image image; readonly Border cross; readonly Border copied; readonly Border trash;
    readonly ScaleTransform flightScale = new(1, 1); readonly RotateTransform rotate = new(); readonly TranslateTransform fall = new(); readonly ScaleTransform scale = new(1, 1); readonly Grid group = new();
    readonly TranslateTransform ropeFollow = new();
    readonly ScaleTransform density = new(1, 1);
    readonly Stopwatch clock = Stopwatch.StartNew(); Point down; double nudgeStart, nudgeAmount = 4, nudgeFrom; bool arriving = true; double copiedUntil;
    bool hover, pressed, dragging, falling, cancelDragRequested, arrivalCancelled, copying; int copyRequest; double? fallingAt; public bool Busy => pressed || dragging || copying || pegHold.Active || pegCommitted || ContextMenu?.IsOpen == true;
    internal bool RenderingAttached { get; private set; }
    public double CardWidth => frame.Width; public double CardHeight => frame.Height;
    public Card(Photo photo, App app)
    {
        RasterThemeArt.WarmPhotos(Theme.Current);
        Photo = photo;
        arrivalOrigin = photo.Origin; photo.Origin = null;
        returnAppearance=photo.ReturnAppearance;photo.ReturnAppearance=null;quickReturn=returnAppearance!=null;
        arrivalOriginMeasured = photo.ValidatedWindowsCapture || photo.UserInitiatedCapture || quickReturn;
        arrivalPending = photo.ArrivalPending || arrivalOrigin != null; photo.ArrivalPending = false; displayScale = app.Settings.Scale;
        if (!arrivalPending) photo.ArrivalStarted = true;
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
        imageEdge = new Border { CornerRadius = new CornerRadius(9), BorderBrush = Theme.Token("FrameEdge"), BorderThickness = new Thickness(.5), IsHitTestVisible = false };
        content.Children.Add(imageEdge);
        frame = Theme.Glass(content);
        var variant = frameVariant = (int)(Photo.Path.Aggregate(0u, (hash, c) => hash * 31 + c) % 3);
        if(frame is GlassBorder artwork) artwork.ArtVariant = variant; peg.Variant = variant;
        frame.Shadow();
        frame.RenderTransform = new TransformGroup { Children = { density, scale, flightScale, fall } };
        frame.RenderTransformOrigin = new Point(.5, 0);
        frame.HorizontalAlignment = HorizontalAlignment.Center;
        frame.VerticalAlignment = VerticalAlignment.Top;
        frame.Margin = new Thickness(0, 14, 0, 0);
        group.Children.Add(frame);
        Children.Add(group);
        group.RenderTransform = new TransformGroup { Children = { rotate } };
        rotate.CenterX = 125;
        rotate.CenterY = 0;
        pin = new Border { Width = 32, Height = 42, Background = Brushes.Transparent, Margin = new Thickness(0, -10, 0, 0), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, Child = peg };
        pin.RenderTransform = pegArrival;
        group.Children.Add(pin);
        pin.IsHitTestVisible = !photo.Video;
        pin.Cursor = Cursors.Hand;
        pin.ToolTip = "长按夹子 · 松开并钉在桌面";
        pin.MouseLeftButtonDown += (_, e) => { if (!transferring && !app.Settings.GameDoNotDisturb) { gesture.Cancel(); pegDown = e.GetPosition(pin); pegHold.Begin(clock.Elapsed.TotalSeconds); pin.CaptureMouse(); pegTimer.Start(); } e.Handled = true; };
        pin.MouseLeftButtonUp += (_, e) => { if (!pegCommitted) CancelPegHold(); pin.ReleaseMouseCapture(); e.Handled = true; };
        pin.MouseMove += (_, e) => { if (pegHold.Active) { pegHold.Move((e.GetPosition(pin) - pegDown).Length); if (!pegHold.Active) CancelPegHold(); } };
        pin.LostMouseCapture += (_, _) => { if (!pegCommitted) CancelPegHold(); };
        pegTimer.Tick += (_, _) => { if (pegHold.Complete(clock.Elapsed.TotalSeconds)) { pegTimer.Stop(); CommitPegHold(); } };
        cross = CornerAction("M4,14 L4,20 L20,20 L20,14 M12,3 L12,15 M7,10 L12,15 L17,10");
        cross.ToolTip = "取下放篮子（保留文件）";
        cross.Width = 24;
        cross.Height = 24;
        cross.HorizontalAlignment = HorizontalAlignment.Left;
        cross.VerticalAlignment = VerticalAlignment.Top;
        cross.Margin = new Thickness(3);
        cross.Opacity = 0;
        cross.IsHitTestVisible = false;
        crossScale.ScaleX = crossScale.ScaleY = 1;
        cross.MouseLeftButtonDown += (_, e) => { gesture.Cancel(); app.Discard(this); e.Handled = true; };
        cross.RenderTransform = crossScale;
        cross.RenderTransformOrigin = new Point(.5, .5);
        group.Children.Add(cross);
        trash = CornerAction("M6,6 L18,18 M18,6 L6,18");
        trash.ToolTip = "丢弃 · 移到回收站"; trash.HorizontalAlignment = HorizontalAlignment.Left; trash.VerticalAlignment = VerticalAlignment.Top;
        trash.MouseLeftButtonDown += (_, e) => { e.Handled = true; gesture.Cancel(); app.Line.MenuOpen = true; try { app.DeletePhoto(Photo, app.Line); } finally { app.Line.MenuOpen = false; } };
        group.Children.Add(trash);
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
        MouseEnter += (_, _) => { hover = true; VisualState(); };
        MouseLeave += (_, _) => { hover = false; VisualState(); };
        frame.MouseLeftButtonDown += (_, e) => { var p = e.GetPosition(frame); if (cross.Opacity > .1 && HitSurface.Contains(cross, frame, p, 14)) { app.Discard(this); e.Handled = true; return; } down = e.GetPosition(this); pressed = true; frame.CaptureMouse(); Act(gesture.Down(clock.Elapsed.TotalSeconds, e.ClickCount)); VisualState(); e.Handled = true; };
        frame.MouseLeftButtonUp += (_, e) => { if (pressed) { var action = gesture.Up(clock.Elapsed.TotalSeconds, Native.GetDoubleClickTime() / 1000.0); pressed = false; frame.ReleaseMouseCapture(); Act(action); VisualState(); } e.Handled = true; };
        frame.MouseMove += (_, e) => { if (pressed) { var v = e.GetPosition(this) - down; Act(gesture.Move(v.Length)); } };
        frame.LostMouseCapture += (_, _) => { if (pressed && !dragging) { pressed = false; gesture.Cancel(); VisualState(); } };
        frame.QueryContinueDrag += (_, e) => { if (e.EscapePressed || cancelDragRequested) { e.Action = DragAction.Cancel; e.Handled = true; } };
        frame.MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { gesture.Cancel(); app.Preview(Photo); e.Handled = true; } };
        frame.MouseRightButtonUp += (_, e) => { gesture.Cancel(); e.Handled = true; NativeMenu.Show(app.PhotoMenu(Photo), app.Line); };
        Resize();
        if (arrivalPending) { frame.Opacity = 0; IsHitTestVisible = false; }
        Loaded += (_, _) => SetRenderingActive(IsVisible);
        IsVisibleChanged += (_, _) => { if (!IsVisible) { if(quickReturn)FinishArrival();else CancelArrivalFlight(); } SetRenderingActive(IsLoaded && IsVisible); };
        Unloaded += (_, _) => { CancelArrivalFlight(); returnAppearance=null; CancelInput(); SetRenderingActive(false); gesture.Cancel(); };
    }
    void SetRenderingActive(bool active)
    {
        if (RenderingAttached == active) return;
        RenderingAttached = active;
        if (active) CompositionTarget.Rendering += Render; else CompositionTarget.Rendering -= Render;
    }
    public void Resize(double? adjustedScale = null)
    {
        peg.InvalidateVisual();
        var previousWidth = frame.Width; var previousHeight = frame.Height;
        if (adjustedScale is double value) displayScale = value;
        image.Source = Photo.Image;
        var s = Layout.Photo(Photo.ThumbnailWidth, Photo.ThumbnailHeight, displayScale);
        var mat = Theme.Current.PhotoMat; frame.Padding = mat;
        frame.Width = s.width + mat.Left + mat.Right;
        frame.Height = s.height + mat.Top + mat.Bottom;
        if (frame.Effect is DropShadowEffect shadow) shadow.Opacity = Theme.Current.ShadowOpacity;
        image.Width = s.width;
        image.Height = s.height;
        var radius = Theme.Current.PhotoImageRadius; frame.CornerRadius = new CornerRadius(Theme.Current.FrameRadius); imageEdge.CornerRadius = new CornerRadius(radius);
        image.Clip = new RectangleGeometry(new Rect(0, 0, s.width, s.height), radius, radius);
        group.Width = 250;
        group.Height = frame.Height + 14;
        Height = frame.Height + 34;
        var actionWidth = Math.Max(64, frame.Width);
        cross.Margin = new Thickness((250 - actionWidth) / 2 + 3, 17, 0, 0);
        trash.Margin = new Thickness((250 + actionWidth) / 2 - 27, 17, 0, 0);
        if (!arrivalPending && !transferring && IsLoaded && IsVisible && SystemParameters.ClientAreaAnimation && double.IsFinite(previousWidth) && previousWidth > 0 && Math.Abs(previousWidth - frame.Width) > .01) {
            density.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(previousWidth / frame.Width, 1, TimeSpan.FromSeconds(.38)) { FillBehavior = FillBehavior.Stop, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            density.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(previousHeight / frame.Height, 1, TimeSpan.FromSeconds(.38)) { FillBehavior = FillBehavior.Stop, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }
    public bool Contains(Point point)
    {
        if (lane is var bounds && bounds.HasValue && (point.X < bounds.Value.left || point.X > bounds.Value.right)) return false;
        return IsHitTestVisible && Opacity > .05 && (HitSurface.Contains(frame, (Visual)app.Line.Content, point, frame.CornerRadius.TopLeft) || cross.IsHitTestVisible && HitSurface.Contains(cross, (Visual)app.Line.Content, point, 5) || trash.IsHitTestVisible && HitSurface.Contains(trash, (Visual)app.Line.Content, point, 5) || !Photo.Video && HitSurface.Contains(pin, (Visual)app.Line.Content, point, 0));
    }
    void VisualState()
    {
        var size = pressed ? .975 : hover ? 1.025 : 1;
        Theme.Animate(scale, ScaleTransform.ScaleXProperty, size, .16);
        Theme.Animate(scale, ScaleTransform.ScaleYProperty, size, .16);
        var actionsVisible = hover && !dragging && !transferring && !arrivalPending;
        foreach (var button in new[] { cross, trash }) { button.IsHitTestVisible = actionsVisible; Theme.Animate(button, OpacityProperty, actionsVisible ? 1 : 0, .16); }
        frame.Effect = new DropShadowEffect { BlurRadius = hover ? 12 : 8, ShadowDepth = hover ? 5 : 3, Opacity = Theme.Current.ShadowOpacity * (hover ? 1.3 : 1), Color = Colors.Black, Direction = 270 };
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
        var request = ++copyRequest;
        SetCopying();
        try { await app.Store.Copy(Photo); }
        catch { /* Store publishes the final user-facing error exactly once. */ }
        finally { if (request == copyRequest && copying) SetCopied(false); }
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
        copying = false;
        ((TextBlock)copied.Child).Text = "✓  " + Localization.Text("Copied");
        copiedUntil = value ? clock.Elapsed.TotalSeconds + 1.2 : 0;
        if (value) { copied.BeginAnimation(OpacityProperty, null); copied.Opacity = 1; }
        else Theme.Animate(copied, OpacityProperty, 0, .2);
        if (value)
            Nudge(1.6);
    }
    internal void SetCopying()
    {
        copying = true; copiedUntil = 0;
        ((TextBlock)copied.Child).Text = "正在复制…";
        copied.BeginAnimation(OpacityProperty, null); copied.Opacity = 1;
    }
    public void CancelInput()
    {
        CancelPegHold();
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
        UpdateLaneClip();
        if (!falling && !transferring)
        {
            if (arrivalPending && SystemParameters.ClientAreaAnimation && !arrivalCancelled)
            {
                var elapsed = arrivalClock.Sample(now, true, quickReturn?0:app.Line.ArrivalLeadIn);
                if(quickReturn) { if(!app.Line.HoldingReturnSource(Photo))returnMotionStarted??=now;elapsed=returnMotionStarted is double begin?now-begin:0; }
                var phase = quickReturn?ArrivalPhase.PinReturn(elapsed):ArrivalPhase.At(elapsed);
                if (phase.PictureVisible) Photo.ArrivalStarted = true;
                pegArrival.Y = phase.PegOffset;
                peg.BeginAnimation(ClothesPegVisual.OpeningProperty, null); peg.Opening = phase.PegOpen;
                frame.Opacity = phase.PictureVisible ? 1 : 0;
                var destination = new Rect((250 - CardWidth) / 2, 14, CardWidth, CardHeight);
                Rect source;
                if (arrivalOrigin is Rect origin)
                {
                    // Source and endpoint both stay in physical desktop pixels.
                    // The flight has no ancestor reveal/tilt transform to distort it.
                    flightScale.ScaleX = flightScale.ScaleY = 1; fall.X = fall.Y = 0;
                    rotate.Angle = Photo.Tilt;
                    var target = ImageScreenPose();
                    frame.Opacity = phase.Picture >= 1 ? 1 : 0;
                    if (phase.Picture < 1 && app.Line.CanAnimateTransfer && !retiringArrivalFlight) {
                        arrivalFlight ??= new ArrivalFlight(returnAppearance?.Image??Photo.Image, app.Line, ArrivalPose.FromRect(origin), target, image.RenderSize, frameVariant,returnAppearance,fromPin:true);
                        arrivalFlight.Prepare();
                    }
                    if (phase.Picture < 1 && app.Line.CanAnimateTransfer && !retiringArrivalFlight) {
                        // The themed frame appears around the unchanged capture
                        // first; Picture stays zero while the peg opens, then flies.
                        arrivalFlight!.Present(ArrivalPose.Travel(origin, target, phase.Picture),image.RenderSize,phase.Picture);
                        if(arrivalFlight.HasPresented)Photo.ArrivalStarted=true;
                    } else if (phase.Picture >= 1 && arrivalFlight!=null) {
                        arrivalFlight.Present(target,image.RenderSize,1);
                        var frameTime=(e as RenderingEventArgs)?.RenderingTime;
                        if(frameTime!=null&&frameTime!=arrivalLandingFrame) {
                            arrivalLandingFrame=frameTime;
                            if(++arrivalLandingFrames>=3) CancelArrivalFlight();
                        }
                    }
                    source = destination;
                }
                else source = new Rect(destination.X, destination.Y + 105, destination.Width * .86, destination.Height * .86);
                var rect = MediaTransferVisual.Travel(source, destination, phase.Picture);
                flightScale.ScaleX = rect.Width / CardWidth; flightScale.ScaleY = rect.Height / CardHeight;
                fall.X = rect.X + rect.Width / 2 - 125; fall.Y = rect.Y - 14;
                IsHitTestVisible = false;
                if (phase.Finished) { FinishArrival(); Nudge(1.8); VisualState(); }
            }
            else
            {
                if (arrivalPending) FinishArrival();
                flightScale.ScaleX = flightScale.ScaleY = 1; fall.X = fall.Y = 0;
                pegArrival.Y = 0; IsHitTestVisible = true;
            }
        }
        var t = Math.Max(0, now - nudgeStart);
        if (!dragging && !transferring && !(arrivalPending && arrivalOrigin != null)) {
            var angle = Photo.Tilt + (!SystemParameters.ClientAreaAnimation || t > 2.5 ? 0 : arriving ? SpringDisplacement(4, 64, 7, t) : t < .16 ? nudgeFrom + (nudgeAmount - nudgeFrom) * (1 - Math.Pow(1 - t / .16, 3)) : SpringDisplacement(nudgeAmount, 64, 7, t - .16));
            if (Math.Abs(rotate.Angle - angle) > .001) rotate.Angle = angle;
        }
        if (copiedUntil > 0 && now > copiedUntil)
        {
            copiedUntil = 0;
            Theme.Animate(copied, OpacityProperty, 0, .2);
        }
        if (fallingAt is double start)
        {
            var v = SystemParameters.ClientAreaAnimation ? Math.Clamp((now - start) / .26, 0, 1) : 1;
            var ease = v * v;
            fall.Y = 42 * ease; fall.X = 12 * ease;
            rotate.Angle = Photo.Tilt + 6 * ease;
            Opacity = 1 - v * v * (3 - 2 * v);
        }
        if(arrivalFlight?.HasPresented==true || !arrivalPending&&frame.Opacity>.99) app.Line.ArrivalPresented(Photo,e);
    }
    static double SpringDisplacement(double amount, double stiffness, double damping, double t)
    {
        var d = damping / 2;
        var w = Math.Sqrt(stiffness - d * d);
        return amount * Math.Exp(-d * t) * (Math.Cos(w * t) + d / w * Math.Sin(w * t));
    }
    internal void FinishArrival()
    {
        CancelArrivalFlight();
        if (falling || transferring) return;
        arrivalCancelled = true; arrivalPending = false; arriving = false;
        returnAppearance=null;
        Photo.ArrivalStarted = true;
        flightScale.ScaleX = flightScale.ScaleY = 1; fall.X = fall.Y = 0;
        pegArrival.Y = 0; peg.Opening = 0; frame.Opacity = 1;
        nudgeStart = clock.Elapsed.TotalSeconds; nudgeFrom = nudgeAmount = 0; rotate.Angle = Photo.Tilt;
        IsHitTestVisible = true;
    }
    internal void CancelArrivalFlight()
    {
        var retired=arrivalFlight;arrivalFlight=null;
        arrivalLandingFrame=null;arrivalLandingFrames=0;
        if(retired==null)return;
        retiringArrivalFlight=true;
        try { retired.Close(); }
        finally { retiringArrivalFlight=false; }
    }
    internal Rect? ArrivalFlightBounds => arrivalFlight is { IsVisible: true, HasPresented: true } ? arrivalFlight.PhysicalBounds : null;
    internal Rect? ArrivalRenderedBounds => arrivalFlight is { IsVisible: true, HasPresented: true } ? arrivalFlight.RenderedScreenBounds : null;
    internal Rect? ArrivalFrameBounds => arrivalFlight is { IsVisible: true, HasPresented: true } ? arrivalFlight.RenderedFrameBounds : null;
    internal void DrawArrivalSnapshot(DrawingContext dc, Window target) => arrivalFlight?.DrawSnapshot(dc,target);
    internal ArrivalPose ImageScreenPose()
    {
        var transform=image.TransformToAncestor(app.Line);
        var screen=ArrivalPose.WindowToScreen(app.Line);
        var top=screen.Transform(transform.Transform(new Point()));
        var x=screen.Transform(transform.Transform(new Point(image.ActualWidth,0)));
        var y=screen.Transform(transform.Transform(new Point(0,image.ActualHeight)));
        return new ArrivalPose(top,x-top,y-top);
    }
    internal void FollowRope(double offset) { if (Math.Abs(ropeFollow.Y - offset) > .001) ropeFollow.Y = offset; }
    internal double RopeOffset => ropeFollow.Y;
    internal void RevealSway() { if (!falling && !dragging) Nudge(1.8 + Photo.Tilt * .2); }
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
    void CancelPegHold()
    {
        ++pegGeneration; pegTimer.Stop(); pegHold.Cancel(); pegCommitted = false;
        if (Mouse.Captured == pin) pin.ReleaseMouseCapture();
        if (!transferring) peg.Open(false);
    }
    void CommitPegHold()
    {
        if (!IsLoaded || app.Settings.GameDoNotDisturb || transferring) { CancelPegHold();return; }
        ++pegGeneration; pegCommitted = true; peg.Open(true);
        if (Mouse.Captured == pin) pin.ReleaseMouseCapture();
        pegCommitted = false; app.Pin(Photo);
    }
    internal Rect FrameBounds(Visual ancestor) => frame.TransformToAncestor(ancestor).TransformBounds(new Rect(frame.RenderSize));
    internal Size ImageSize => image.RenderSize;
    internal int FrameVariant => frameVariant;
    internal void HideTransferPicture()
    {
        foreach(var element in new FrameworkElement[]{frame,cross,trash,copied}) { element.BeginAnimation(OpacityProperty,null);element.Opacity=0; }
    }
    internal void BeginTransferOut(bool holdPicture=false)
    {
        CancelInput(); transferring = true; IsHitTestVisible = false; peg.Open(true);
        if(!holdPicture) foreach (var element in new FrameworkElement[] { frame, cross, trash, copied }) element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(.04)) { BeginTime = TimeSpan.FromSeconds(.14) });
        pin.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(.14)) { BeginTime = TimeSpan.FromSeconds(.2) });
    }
    static Border CornerAction(string path)
    {
        var glyph = new System.Windows.Shapes.Path { Data = Geometry.Parse(path), Stroke = Theme.Token("ActionInk"), StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Width = 14, Height = 14, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        return new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(5), Background = Theme.Token("ActionFill"), BorderBrush = Theme.Outline, BorderThickness = new Thickness(.7), Child = glyph, Cursor = Cursors.Hand, Opacity = 0, IsHitTestVisible = false };
    }
    internal bool AwaitingFirstArrival => arrivalPending && !arrivalClock.Started;
    internal bool ArrivalActive => arrivalPending;
    internal Rect? PendingArrivalOrigin => arrivalOrigin;
    internal void AdoptPendingOrigin(Rect origin)
    {
        if (!arrivalPending || arrivalCancelled || Photo.ArrivalStarted) return;
        if (arrivalOrigin != null && (arrivalOriginMeasured || !Photo.ValidatedWindowsCapture)) return;
        arrivalOrigin = origin; arrivalOriginMeasured = Photo.ValidatedWindowsCapture; Photo.Origin = null;
    }
    internal bool AllowsLiveGlassFor(FrameworkElement surface)
    {
        if (arrivalPending || lane == null) return true;
        try { var bounds = surface.TransformToAncestor((Visual)app.Line.Content).TransformBounds(new Rect(surface.RenderSize)); return bounds.Left >= lane.Value.left && bounds.Right <= lane.Value.right; }
        catch (InvalidOperationException) { return false; }
    }
    internal void SetLane(double left, double right, double height) { lane = (left, right, height); UpdateLaneClip(); }
    void UpdateLaneClip()
    {
        if (arrivalPending || lane is not { } bounds) { if (Clip != null) Clip = null; return; }
        var rect = new Rect(bounds.left - Canvas.GetLeft(this) - LineMotion.Reflow(this).X, -100, bounds.right - bounds.left, bounds.height + 100);
        if (laneClip.Rect != rect) laneClip.Rect = rect;
        if (Clip != laneClip) Clip = laneClip;
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
    public static void Shadow(this Border b) => b.Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 3, Opacity = Theme.Current.ShadowOpacity, Direction = 270 };
}





