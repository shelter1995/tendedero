using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Diagnostics;
namespace Liangtu;
public sealed class LineWindow : Window
{
    readonly App app; readonly Canvas layer = new(); readonly Canvas line = new(); readonly TranslateTransform slide = new(); readonly Dictionary<string, Card> cards = new(StringComparer.OrdinalIgnoreCase); readonly Rope rope = new(); readonly Border hint;
    readonly Stopwatch clock = Stopwatch.StartNew(); readonly DispatcherTimer timer; double? atEdge, away; double peekUntil; bool pinned, entered; double scroll; bool wasMouseDown, suppressed;
    GlassBackdrop? glass;
    DisplayGeometry? geometry;
    bool changingDisplay;
    int visibilityGeneration;
    string? edgeDisplay;
    bool openedOnPurpose;
    double? emptySince;
    double nextGust = 7 + Random.Shared.NextDouble() * 9;
    public bool LiveGlassAvailable => glass?.Available == true;
    public bool Revealed
    {
        get; private set;
    }
    public bool Busy
    {
        get; set;
    }
    public bool MenuOpen
    {
        get; set;
    }
    public IntPtr Hwnd
    {
        get; private set;
    }
    public System.Windows.Forms.Screen Screen { get; private set; } = System.Windows.Forms.Screen.PrimaryScreen!;
    public double PanelHeight => Layout.Round(104 * app.Settings.Scale) + 106;
    public LineWindow(App app)
    {
        this.app = app;
        Title = "晾图 · 晾衣绳";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Content = layer;
        layer.Children.Add(line);
        line.RenderTransform = slide;
        line.Children.Add(rope);
        hint = Theme.Glass(new TextBlock { Text = Localization.Text("Take a screenshot and it will hang here"), FontSize = 12, FontWeight = FontWeights.Medium, Foreground = Theme.Ink }, 20, 9);
        hint.IsHitTestVisible = false;
        line.Children.Add(hint);
        SourceInitialized += (_, _) => { Hwnd = new WindowInteropHelper(this).Handle; Native.NonActivating(this); HwndSource.FromHwnd(Hwnd).AddHook(Hook); };
        new WindowInteropHelper(this).EnsureHandle();
        app.Store.Changed += _ => Refresh();
        app.Store.Copied += p => { foreach (var card in cards.Values) card.SetCopied(ReferenceEquals(card.Photo, p)); };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        DpiChanged += (_, _) => { if (!changingDisplay) Dispatcher.BeginInvoke(RefreshDisplay, DispatcherPriority.Loaded); };
        MouseWheel += (_, e) => { if (app.Settings.Keep == 0) return; scroll = Math.Clamp(scroll + e.Delta * .6, 0, Math.Max(0, (app.Store.Items.Count - Layout.Visible(Width, app.Settings.Scale)) * Layout.Spacing(app.Settings.Scale))); Position(); };
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / 30) };
        timer.Tick += (_, _) => Tick();
        timer.Start();
        CompositionTarget.Rendering += UpdateRopeMotion;
        Closed += (_, _) => { timer.Stop(); CompositionTarget.Rendering -= UpdateRopeMotion; glass?.Dispose(); Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged; };
        glass = new GlassBackdrop(this, Hwnd);
        MoveTo(Screen);
    }
    IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x1f)
        {
            foreach (var card in cards.Values)
                card.CancelInput();
        }
        if (msg == 0x312)
        {
            Settings.Log($"WM_HOTKEY id={w.ToInt32()}");
            if (w.ToInt32() == 1)
                Toggle();
            else if (w.ToInt32() == 2)
                app.Capture();
            handled = true;
        }
        else if (msg == 0x31D)
        {
            _ = app.Store.CaptureClipboard();
        }
        else if (msg == 0x84)
        {
            var p = PointFromScreen(new Point((short)(l.ToInt64() & 0xffff), (short)((l.ToInt64() >> 16) & 0xffff)));
            if (!cards.Values.Any(c => c.Contains(p)) && !Busy)
            {
                handled = true;
                return new IntPtr(-1);
            }
        }
        return IntPtr.Zero;
    }
    public void MoveTo(System.Windows.Forms.Screen screen)
    {
        if (changingDisplay)
            return;
        changingDisplay = true;
        try
        {
            if (geometry != null && (geometry.DeviceName != screen.DeviceName || geometry.PhysicalBounds != screen.Bounds)) glass?.Hide();
            Screen = screen;
            Native.SetWindowPos(Hwnd, new IntPtr(-1), screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height, 0x10);
            var dpi = Native.GetDpiForWindow(Hwnd) / 96.0;
            if (dpi <= 0)
                dpi = Native.Dpi(screen);
            ApplyDisplayLayout(new DisplayGeometry(screen.DeviceName, screen.Bounds, dpi, screen.Primary));
            Native.SetWindowPos(Hwnd, new IntPtr(-1), screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height, 0x10);
        }
        finally { changingDisplay = false; }
    }
    internal void ApplyDisplayLayout(DisplayGeometry display)
    {
        bool changed = geometry != null && (geometry.DeviceName != display.DeviceName || geometry.PhysicalBounds != display.PhysicalBounds || geometry.Scale != display.Scale);
        geometry = display;
        Width = geometry.LocalBounds.Width;
        Height = geometry.LocalBounds.Height;
        line.Width = Width;
        line.Height = Height;
        rope.Width = Width;
        rope.Height = 75;
        app.Store.Capacity = Layout.Visible(Width, app.Settings.Scale);
        if (changed)
        {
            scroll = 0;
            foreach (var card in cards.Values) card.FinishArrival();
        }
        Position(animate: false);
        if (changed)
        {
            if (Revealed) { LineMotion.Reveal(slide, -(PanelHeight + 12), restart: true); StartRopeBounce(); }
            else { LineMotion.PrepareHidden(slide, -(PanelHeight + 12)); rope.BeginAnimation(Rope.DeflectionProperty, null); rope.Deflection = 0; UpdateRopeMotion(null, EventArgs.Empty); }
        }
    }
    void DisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(RefreshDisplay);
    void RefreshDisplay()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        Native.GetCursorPos(out var p);
        var topology = screens.Select(s => new DisplayGeometry(s.DeviceName, s.Bounds, Native.Dpi(s), s.Primary)).ToArray();
        var current = DisplayGeometry.ResolveDisplay(topology, Screen.DeviceName, new System.Drawing.Point(p.X, p.Y));
        if (current == null)
        {
            Retract();
            return;
        }
        MoveTo(screens.Single(s => s.DeviceName == current.DeviceName));
    }
    public void ResetScroll()
    {
        scroll = 0;
        Position(animate: false);
    }
    public void Toggle()
    {
        if (Revealed)
            Retract();
        else
        {
            Native.GetCursorPos(out var p);
            MoveTo(System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(p.X, p.Y)));
            Reveal(true);
        }
    }
    public void Reveal(bool manual = false, double peek = 0)
    {
        if (!manual && !app.Settings.OverFullscreen && Native.Fullscreen(Screen))
            return;
        bool restart = !Revealed || !IsVisible;
        ++visibilityGeneration;
        scroll = 0;
        Position(animate: !restart);
        pinned = manual;
        openedOnPurpose = manual;
        entered = false;
        peekUntil = clock.Elapsed.TotalSeconds + peek;
        away = null;
        Revealed = true;
        if (!IsVisible)
        {
            LineMotion.PrepareHidden(slide, -(PanelHeight + 12));
            Show();
            MoveTo(Screen);
        }
        LineMotion.Reveal(slide, -(PanelHeight + 12), restart);
        if (restart || peek > 0) StartRopeBounce();
        Settings.Log($"Reveal manual={manual}, screen={Screen.DeviceName}, size={Width}x{Height}, hwnd={Hwnd}");
    }
    public void ShowFromPointer()
    {
        Native.GetCursorPos(out var p);
        MoveTo(System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(p.X, p.Y)));
        Reveal(true);
    }
    public async void Retract()
    {
        if (!Revealed || Busy || MenuOpen)
            return;
        Revealed = false;
        var generation = ++visibilityGeneration;
        openedOnPurpose = false;
        pinned = false;
        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-(PanelHeight + 12), TimeSpan.FromSeconds(.22)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
        await Task.Delay(300);
        if (!Revealed && generation == visibilityGeneration)
        {
            Hide(); glass?.Hide();
        }
    }
    public void Refresh()
    {
        var paths = app.Store.Items.Select(p => p.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in cards.Keys.Where(p => !paths.Contains(p)).ToArray())
        {
            var departing = cards[path];
            departing.StartFall();
            _ = RemoveAfterFall(departing);
            cards.Remove(path);
        }
        foreach (var p in app.Store.Items)
        {
            if (!cards.TryGetValue(p.Path, out var c))
            {
                c = new Card(p, app);
                cards[p.Path] = c;
                line.Children.Add(c);
            }
            c.Resize();
        }
        hint.Visibility = app.Store.Items.Count == 0 ? Visibility.Visible : Visibility.Hidden;
        emptySince = app.Store.Items.Count == 0 ? clock.Elapsed.TotalSeconds : null;
        Position();
    }
    void Position(bool animate = true)
    {
        if (Width <= 0)
            return;
        rope.InvalidateVisual();
        var visible = Layout.Visible(Width, app.Settings.Scale);
        for (int i = 0; i < app.Store.Items.Count; i++)
        {
            var p = app.Store.Items[i];
            if (!cards.TryGetValue(p.Path, out var c))
                continue;
            var x = Layout.X(i, app.Store.Items.Count, Width, visible, app.Settings.Keep > 0, scroll, app.Settings.Scale);
            var y = Layout.RopeY(x, Width) - 9.5;
            LineMotion.Place(c, x - 125, y, animate && Revealed && IsVisible && !changingDisplay);
            c.Opacity = Math.Clamp(Math.Min(x, Width - x) / 90, 0, 1);
        }
        hint.Measure(new Size(1000, 100));
        Canvas.SetLeft(hint, (Width - hint.DesiredSize.Width) / 2);
        Canvas.SetTop(hint, Layout.RopeY(Width / 2, Width) + 22);
    }
    void Tick()
    {
        Native.GetCursorPos(out var p);
        var current = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(p.X, p.Y));
        var now = clock.Elapsed.TotalSeconds;
        if (Revealed && !Busy && !MenuOpen && now >= nextGust)
        {
            foreach (var card in cards.Values)
                _ = Breeze(card);
            nextGust = now + 7 + Random.Shared.NextDouble() * 9;
        }
        var pointer = new System.Drawing.Point(p.X, p.Y);
        var pointerDisplay = new DisplayGeometry(current.DeviceName, current.Bounds, Native.Dpi(current), current.Primary);
        var edge = pointerDisplay.ContainsTopEdge(pointer);
        var mouseDown = (Native.GetAsyncKeyState(1) & 0x8000) != 0;
        if (edge && mouseDown && !wasMouseDown)
        {
            suppressed = true;
            Retract();
        }
        wasMouseDown = mouseDown;
        if (!edge)
            suppressed = false;
        if (!Revealed)
        {
            if (edge && !suppressed && app.Settings.HoverReveal && app.Store.Items.Count > 0)
            {
                if (edgeDisplay != current.DeviceName) { atEdge = null; edgeDisplay = current.DeviceName; }
                atEdge ??= now;
                if (now - atEdge >= .25)
                {
                    MoveTo(current);
                    Reveal();
                    atEdge = null;
                }
            }
            else
            {
                atEdge = null; edgeDisplay = null;
            }
            return;
        }
        if (Busy || MenuOpen || cards.Values.Any(c => c.Busy))
            return;
        if (!openedOnPurpose && !app.Settings.OverFullscreen && Native.Fullscreen(Screen))
        {
            Retract();
            return;
        }
        if (!openedOnPurpose && emptySince is double sinceEmpty && now - sinceEmpty >= .7 && now >= peekUntil)
        {
            Retract();
            return;
        }
        var inside = geometry?.ContainsTopBand(pointer, PanelHeight + 16) == true;
        if (inside)
        {
            entered = true;
            away = null;
            return;
        }
        if (now < peekUntil || pinned && !entered)
            return;
        away ??= now;
        if (now - away >= .5)
            Retract();
    }
    async Task Breeze(Card card)
    {
        await Task.Delay((int)(Random.Shared.NextDouble() * 350));
        if (!Busy && !card.Busy)
            card.Nudge(1.6 + Random.Shared.NextDouble() * 1.8);
    }
    internal AnimationClock StartRopeBounce()
    {
        foreach (var card in cards.Values) card.RevealSway();
        return rope.Bounce();
    }
    internal void UpdateRopeMotion(object? sender, EventArgs e)
    {
        foreach (var card in cards.Values)
            card.FollowRope(Rope.Offset(Canvas.GetLeft(card) + 125, Width, rope.Deflection));
    }
    async Task RemoveAfterFall(Card card)
    {
        await Task.Delay(570);
        line.Children.Remove(card);
    }
}
public sealed class Rope : FrameworkElement
{
    public static readonly DependencyProperty DeflectionProperty = DependencyProperty.Register(nameof(Deflection), typeof(double), typeof(Rope), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Deflection { get => (double)GetValue(DeflectionProperty); set => SetValue(DeflectionProperty, value); }
    public static double Offset(double x, double width, double deflection) { var f = width > 0 ? Math.Clamp(x / width, 0, 1) : 0; return 4 * deflection * f * (1 - f); }
    public AnimationClock Bounce()
    {
        BeginAnimation(DeflectionProperty, null); Deflection = 0;
        var animation = new DoubleAnimation(0, 24, TimeSpan.FromSeconds(1.1)) { EasingFunction = new RopeBounceEase { EasingMode = EasingMode.EaseIn }, FillBehavior = FillBehavior.Stop };
        var clock = animation.CreateClock(); ApplyAnimationClock(DeflectionProperty, clock); return clock;
    }
    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        if (w <= 0)
            return;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(0, 10), false, false);
            c.QuadraticBezierTo(new Point(w / 2, 10 + 2 * (Math.Min(30, w * .018) + Deflection)), new Point(w, 10), true, false);
        }
        var mask = new LinearGradientBrush(new GradientStopCollection { new(Colors.Transparent, 0), new(Colors.Black, .08), new(Colors.Black, .92), new(Colors.Transparent, 1) }, 0);
        dc.PushOpacityMask(mask);
        dc.PushTransform(new TranslateTransform(0, 1.2));
        dc.DrawGeometry(null, new Pen(Theme.Brush("#38000000"), 2.2), g);
        dc.Pop();
        dc.DrawGeometry(null, new Pen(Theme.Brush("#FF8C8C8C"), 1.2), g);
        dc.PushTransform(new TranslateTransform(0, -.35));
        dc.DrawGeometry(null, new Pen(Theme.Brush("#73FFFFFF"), .4), g);
        dc.Pop();
        dc.Pop();
    }
}
internal sealed class RopeBounceEase : EasingFunctionBase
{
    protected override double EaseInCore(double t) => t >= 1 ? 0 : Math.Sin(t * Math.PI * 4) * Math.Exp(-4.5 * t);
    protected override Freezable CreateInstanceCore() => new RopeBounceEase();
}
