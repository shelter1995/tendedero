using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Diagnostics;
namespace Liangtu;
public sealed class LineWindow : Window
{
    readonly App app; readonly Canvas layer = new(); readonly Canvas line = new(); readonly TranslateTransform slide = new(); readonly Dictionary<string, Card> cards = new(StringComparer.OrdinalIgnoreCase); readonly Rope rope = new(); readonly Border hint;
    readonly Stopwatch clock = Stopwatch.StartNew(); readonly DispatcherTimer timer; double? atEdge, away; double peekUntil; bool pinned, entered; double scroll; bool wasMouseDown, suppressed;
    GlassBackdrop? glass;
    bool renderingAttached;
    internal int RenderingCallbackCount => (renderingAttached ? 1 : 0) + (glass?.RenderingAttached == true ? 1 : 0) + cards.Values.Count(c => c.RenderingAttached);
    DisplayGeometry? geometry;
    bool changingDisplay;
    bool extendedSurface;
    bool minimalSurface = true;
    int visibilityGeneration;
    string? edgeDisplay;
    bool openedOnPurpose;
    bool retractRequested;
    readonly FullscreenProtection fullscreen;
    readonly Button basketButton;
    readonly BasketVisual basketArt = new();
    readonly TranslateTransform basketFollow = new();
    double revealStarted = -10;
    internal double ArrivalLeadIn => Math.Max(0, .32 - (clock.Elapsed.TotalSeconds - revealStarted));
    readonly Dictionary<Photo, (Rect target, Action complete,PinAppearance? appearance,Action? landed,Func<(PinAppearance? appearance,Rect? bounds)>? refresh)> pinTransfers = new();
    readonly Dictionary<MediaTransferVisual, Action> flights = new();
    readonly Dictionary<DesktopTransfer, Action> desktopFlights = new();
    readonly Dictionary<Photo,(Action release,TimeSpan? frame)> returnSources = new();
    bool fullscreenManualOverride;
    double? emptySince;
    double nextGust = 7 + Random.Shared.NextDouble() * 9;
    public bool LiveGlassAvailable => glass?.Available == true;
    internal int NativeGlassUpdateCount => glass?.NativeUpdateCount ?? 0;
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
    internal System.Drawing.Size InitialSurfaceSize { get; private set; }
    public System.Windows.Forms.Screen Screen { get; private set; } = System.Windows.Forms.Screen.PrimaryScreen!;
    public double PanelHeight => Layout.Round(104 * app.Settings.Scale) + 106;
    double LayoutWidth => geometry?.LocalBounds.Width ?? Width;
    public LineWindow(App app) : this(app, new FullscreenProtection()) { }
    internal LineWindow(App app, FullscreenProtection fullscreen)
    {
        this.app = app;
        this.fullscreen = fullscreen;
        Title = "晾图 · 晾衣绳";
        FontFamily = Theme.Font;
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
        basketButton = new Button { Content = basketArt, Width = 80, Height = 90, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, ToolTip = "图片篮子 · 查看取下的图片", RenderTransform = basketFollow };
        basketButton.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='Transparent'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");
        basketButton.Click += (_, _) => app.ShowBasket();
        System.Windows.Automation.AutomationProperties.SetName(basketButton, "图片篮子");
        Panel.SetZIndex(basketButton, 1000);
        line.Children.Add(basketButton);
        SourceInitialized += (_, _) => {
            Hwnd = new WindowInteropHelper(this).Handle;
            Native.GetWindowRect(Hwnd, out var initial);
            InitialSurfaceSize = new System.Drawing.Size(initial.Right-initial.Left, initial.Bottom-initial.Top);
            Native.NonActivating(this); Theme.ApplyNativeChrome(this); HwndSource.FromHwnd(Hwnd).AddHook(Hook);
        };
        Width = Height = 1;
        new WindowInteropHelper(this).EnsureHandle();
        app.Store.Changed += _ => Refresh();
        app.Store.Copied += p => { foreach (var card in cards.Values) card.SetCopied(ReferenceEquals(card.Photo, p)); };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        DpiChanged += (_, _) => { if (!changingDisplay) Dispatcher.BeginInvoke(RefreshDisplay, DispatcherPriority.Loaded); };
        MouseWheel += (_, e) => { var layout = ShelfLayout.Create(LayoutWidth, app.Store.Items.Count, app.Settings.Scale, Theme.Current.BasketButtonWidth); scroll = Math.Clamp(scroll + e.Delta * .6, 0, layout.MaxScroll); Position(); };
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / 30) };
        timer.Tick += (_, _) => Tick();
        timer.Start();
        IsVisibleChanged += (_, _) => { SetRenderingActive(IsVisible); if(!IsVisible) ReleaseReturnSources(); };
        Closed += (_, _) => { CancelFlights(); timer.Stop(); SetRenderingActive(false); glass?.Dispose(); Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged; };
        glass = new GlassBackdrop(this, Hwnd);
        MoveTo(Screen, manual: true);
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
                ToggleFromHotkey();
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
            if (!cards.Values.Any(c => c.Contains(p)) && !HitSurface.Contains(basketButton, layer, p, 4) && !Busy)
            {
                handled = true;
                return new IntPtr(-1);
            }
        }
        return IntPtr.Zero;
    }
    public void MoveTo(System.Windows.Forms.Screen screen, bool manual = false)
    {
        if (changingDisplay)
            return;
        if (app.Settings.GameDoNotDisturb && !manual) return;
        // This must precede SetWindowPos: an already-visible line can otherwise
        // flash over the game before Reveal gets a chance to reject it.
        if (!manual && BlocksAutomaticDisplay(screen, fresh: true)) SuppressForFullscreen();
        if (Screen.DeviceName != screen.DeviceName) fullscreenManualOverride = false;
        changingDisplay = true;
        try
        {
            if (geometry != null && (geometry.DeviceName != screen.DeviceName || geometry.PhysicalBounds != screen.Bounds)) glass?.Hide();
            Screen = screen;
            var provisionalDpi = Native.Dpi(screen);
            Native.SetWindowPos(Hwnd, new IntPtr(-1), screen.Bounds.Left, screen.Bounds.Top, minimalSurface ? 1 : screen.Bounds.Width, SurfacePixels(screen.Bounds.Height, provisionalDpi), 0x10);
            var dpi = Native.GetDpiForWindow(Hwnd) / 96.0;
            if (dpi <= 0)
                dpi = Native.Dpi(screen);
            ApplyDisplayLayout(new DisplayGeometry(screen.DeviceName, screen.Bounds, dpi, screen.Primary));
            Native.SetWindowPos(Hwnd, new IntPtr(-1), screen.Bounds.Left, screen.Bounds.Top, minimalSurface ? 1 : screen.Bounds.Width, SurfacePixels(screen.Bounds.Height, dpi), 0x10);
        }
        finally { changingDisplay = false; }
    }
    internal void ApplyDisplayLayout(DisplayGeometry display)
    {
        bool changed = geometry != null && (geometry.DeviceName != display.DeviceName || geometry.PhysicalBounds != display.PhysicalBounds || geometry.Scale != display.Scale);
        geometry = display;
        Width = minimalSurface ? 1 : geometry.LocalBounds.Width;
        Height = minimalSurface ? 1 : SurfaceHeight;
        line.Width = geometry.LocalBounds.Width;
        line.Height = geometry.LocalBounds.Height;
        rope.Width = LayoutWidth;
        rope.Height = 75;
        app.Store.Capacity = ShelfLayout.Capacity(LayoutWidth, Theme.Current.BasketButtonWidth);
        if (changed)
        {
            CancelFlights();
            scroll = 0;
            foreach (var card in cards.Values) { card.CancelInput(); if (!card.AwaitingFirstArrival && card.Photo.ArrivalStarted) card.FinishArrival(); }
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
        if (app.Settings.GameDoNotDisturb) { SuspendForGameMode(); return; }
        var screens = System.Windows.Forms.Screen.AllScreens;
        Native.GetCursorPos(out var p);
        var topology = screens.Select(s => new DisplayGeometry(s.DeviceName, s.Bounds, Native.Dpi(s), s.Primary)).ToArray();
        var current = DisplayGeometry.ResolveDisplay(topology, Screen.DeviceName, new System.Drawing.Point(p.X, p.Y));
        if (current == null)
        {
            Retract();
            return;
        }
        // DPI maintenance of an explicitly opened shelf is not a new automatic
        // reveal. Preserve that intent only while staying on the same display.
        var manual = Revealed && fullscreenManualOverride && Screen.DeviceName == current.DeviceName;
        MoveTo(screens.Single(s => s.DeviceName == current.DeviceName),manual);
    }
    public void ResetScroll()
    {
        scroll = 0;
        Position(animate: false);
    }
    public void Toggle() => ToggleCore(false);
    internal void ToggleFromHotkey() => ToggleCore(true);
    void ToggleCore(bool viaHotkey)
    {
        if (Revealed && !retractRequested)
            Retract();
        else ShowFromPointer(viaHotkey);
    }
    public void Reveal(bool manual = false, double peek = 0, bool viaHotkey = false)
    {
        if (!PresentationPolicy.CanReveal(app.Settings, viaHotkey ? RevealTrigger.Hotkey : manual ? RevealTrigger.UserAction : RevealTrigger.Automatic)) return;
        var blocked = BlocksAutomaticDisplay(Screen, fresh: true);
        if (!manual && blocked) { SuppressForFullscreen(); Settings.Log($"Auto reveal suppressed for fullscreen, screen={Screen.DeviceName}"); return; }
        fullscreenManualOverride = manual && blocked;
        RasterThemeArt.WarmShelf(Theme.Current);
        var restoredSurface = minimalSurface;
        PrepareSurface(manual);
        if (minimalSurface) return;
        bool restart = !Revealed || !IsVisible;
        if (manual) retractRequested = false;
        if (!restart) {
            // New pictures and duplicate notifications extend the visible shelf.
            // They do not restart a running reveal or forget a manual opening.
            pinned |= manual; openedOnPurpose |= manual;
            peekUntil = Math.Max(peekUntil, clock.Elapsed.TotalSeconds + peek);
            away = null;
            return;
        }
        retractRequested = false;
        if (restart) revealStarted = clock.Elapsed.TotalSeconds;
        bool wasVisible = IsVisible;
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
            if (!restoredSurface) MoveTo(Screen, manual);
            if (!Revealed) return;
        }
        LineMotion.Reveal(slide, -(PanelHeight + 12), restart && !wasVisible);
        if (restart) StartRopeBounce();
        Settings.Log($"Reveal manual={manual}, screen={Screen.DeviceName}, size={Width}x{Height}, hwnd={Hwnd}");
    }
    public void ShowFromPointer(bool viaHotkey = false)
    {
        if (!PresentationPolicy.CanReveal(app.Settings, viaHotkey ? RevealTrigger.Hotkey : RevealTrigger.UserAction)) return;
        Native.GetCursorPos(out var p);
        MoveTo(System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(p.X, p.Y)), manual: true);
        Reveal(true, viaHotkey: viaHotkey);
    }
    public void Retract()
    {
        if (!Revealed) return;
        retractRequested = true;
        if (Busy || MenuOpen || ActiveTransferCount > 0 || cards.Values.Any(c => c.Busy)) return;
        retractRequested = false;
        suppressed = true; atEdge = null;
        Revealed = false;
        var generation = ++visibilityGeneration;
        openedOnPurpose = false;
        fullscreenManualOverride = false;
        pinned = false;
        var motion = LineMotion.Retract(slide, -(PanelHeight + 12));
        void Complete() {
            if (Revealed || generation != visibilityGeneration) return;
            Hide(); glass?.Hide(); CompactSurface();
        }
        if (!SystemParameters.ClientAreaAnimation) Complete();
        else motion.Completed += (_, _) => Complete();
    }
    public void Refresh()
    {
        if (app.Store.Items.Count > 0 || line.Children.OfType<Card>().Any() || ActiveTransferCount > 0) RasterThemeArt.WarmPhotos(Theme.Current);
        if (IsVisible) RasterThemeArt.WarmShelf(Theme.Current);
        var paths = app.Store.Items.Select(p => p.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in cards.Keys.Where(p => !paths.Contains(p)).ToArray())
        {
            var departing = cards[path];
            Depart(departing);
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
        }
        hint.Visibility = app.Store.Items.Count == 0 ? Visibility.Visible : Visibility.Hidden;
        emptySince = app.Store.Items.Count == 0 ? clock.Elapsed.TotalSeconds : null;
        Position();
        ReleaseUnusedPhotoArtwork();
    }
    void ReleaseUnusedPhotoArtwork()
    {
        if (app.Store.Items.Count == 0 && ActiveTransferCount == 0 && !line.Children.OfType<Card>().Any())
            RasterThemeArt.ReleasePhotoArtwork(Theme.Current);
    }
    internal void AdoptPendingOrigin(Photo photo, Rect origin)
    {
        if (cards.TryGetValue(photo.Path, out var card)) card.AdoptPendingOrigin(origin);
    }
    internal bool BlocksAutomaticDisplay(System.Windows.Forms.Screen screen, bool fresh = false) =>
        app.Settings.GameDoNotDisturb || !app.Settings.OverFullscreen && fullscreen.IsBlocked(screen.DeviceName, screen.Bounds, fresh);
    internal bool EnforceFullscreenProtection()
    {
        if (!BlocksAutomaticDisplay(Screen)) { fullscreenManualOverride = false; return false; }
        if (fullscreenManualOverride) return false;
        SuppressForFullscreen(); return true;
    }
    internal void SuspendForGameMode() => HideImmediately("game do-not-disturb mode");
    void SuppressForFullscreen() => HideImmediately("fullscreen");
    void HideImmediately(string reason)
    {
        CancelFlights();
        if (Revealed || IsVisible) Settings.Log($"Immediately hiding line for {reason}, screen={Screen.DeviceName}");
        ++visibilityGeneration;
        Revealed = false; openedOnPurpose = false; pinned = false; entered = false; fullscreenManualOverride = false; retractRequested = false;
        atEdge = away = null; edgeDisplay = null; peekUntil = 0;
        foreach (var card in cards.Values) card.CancelInput();
        glass?.Hide();
        if (IsVisible) Hide();
        CompactSurface();
        LineMotion.PrepareHidden(slide, -(PanelHeight + 12));
        rope.BeginAnimation(Rope.DeflectionProperty, null); rope.Deflection = 0; UpdateRopeMotion(null, EventArgs.Empty);
    }
    void Position(bool animate = true)
    {
        SetTransferSurface(extendedSurface);
        var width = LayoutWidth;
        if (width <= 0)
            return;
        rope.InvalidateVisual();
        basketArt.UseShelfSize(Theme.Current); basketArt.InvalidateVisual();
        basketButton.Width=Theme.Current.BasketButtonWidth; basketButton.Height=Theme.Current.BasketArtHeight+4;
        Canvas.SetLeft(basketButton, Math.Max(0, width - basketButton.Width - 14));
        var basketAnchor=Canvas.GetLeft(basketButton)+basketButton.Width/2;
        Canvas.SetTop(basketButton, Layout.RopeY(basketAnchor, width) - (2+basketArt.Height*4/76));
        var layout = ShelfLayout.Create(width, app.Store.Items.Count, app.Settings.Scale, basketButton.Width);
        app.Store.Capacity=ShelfLayout.Capacity(width,basketButton.Width);
        scroll = Math.Clamp(scroll, 0, layout.MaxScroll);
        for (int i = 0; i < app.Store.Items.Count; i++)
        {
            var p = app.Store.Items[i];
            if (!cards.TryGetValue(p.Path, out var c))
                continue;
            c.Resize(layout.Scale);
            var x = layout.Center(i, scroll);
            var y = Layout.RopeY(x, width) - 9.5;
            LineMotion.Place(c, x - 125, y, animate && Revealed && IsVisible && !changingDisplay);
            c.SetLane(layout.Left, layout.Right, line.Height);
            c.Opacity = Math.Clamp(Math.Min(x - layout.Left, layout.Right - x) / 24, 0, 1);
        }
        hint.Measure(new Size(1000, 100));
        Canvas.SetLeft(hint, (width - hint.DesiredSize.Width) / 2);
        Canvas.SetTop(hint, Layout.RopeY(width / 2, width) + 22);
    }
    void Tick()
    {
        Native.GetCursorPos(out var p);
        var pointer = new System.Drawing.Point(p.X, p.Y);
        System.Windows.Forms.Screen? current = null;
        foreach (var display in System.Windows.Forms.Screen.AllScreens)
            if (display.Bounds.Contains(pointer)) { current = display; break; }
        current ??= System.Windows.Forms.Screen.FromPoint(pointer);
        var now = clock.Elapsed.TotalSeconds;
        // Keep protection warm while hidden, so an overlay appearing just before
        // the pointer reaches the edge cannot erase the previous fullscreen state.
        var pointerBlocked = BlocksAutomaticDisplay(current);
        // Before busy/menu/pinned early returns: an old manual reveal must not
        // survive entering a new game session, nor wait for a retract animation.
        if (Revealed || IsVisible) EnforceFullscreenProtection();
        if (Revealed && !Busy && !MenuOpen && now >= nextGust)
        {
            foreach (var card in cards.Values)
                _ = Breeze(card);
            nextGust = now + 7 + Random.Shared.NextDouble() * 9;
        }
        var edge = DisplayGeometry.ContainsTopEdge(current.Bounds, pointer);
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
            if (edge && !suppressed && app.Settings.HoverReveal && (app.Store.Items.Count > 0 || app.Store.Collection.Count > 0) && !pointerBlocked)
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
        if (Busy || MenuOpen || ActiveTransferCount > 0 || cards.Values.Any(c => c.Busy))
            return;
        if (retractRequested) { Retract(); return; }
        if (cards.Values.Any(c => c.ArrivalActive)) return;
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
        basketArt.Receive();
        foreach (var card in cards.Values) card.RevealSway();
        return rope.Bounce();
    }
    internal void UpdateRopeMotion(object? sender, EventArgs e)
    {
        if (sender != null && !IsVisible) return;
        var anchor = Canvas.GetLeft(basketButton) + basketButton.Width/2;
        basketFollow.Y = Rope.Offset(anchor, LayoutWidth, rope.Deflection);
        basketArt.Sway = IsVisible && SystemParameters.ClientAreaAnimation ? Math.Sin(clock.Elapsed.TotalSeconds * 1.45) * 1.2 : 0;
        foreach (var card in cards.Values)
            card.FollowRope(Rope.Offset(Canvas.GetLeft(card) + LineMotion.Reflow(card).X + 125, LayoutWidth, rope.Deflection));
    }
    void SetRenderingActive(bool active)
    {
        if (renderingAttached == active) return;
        renderingAttached = active;
        if (active) CompositionTarget.Rendering += UpdateRopeMotion;
        else CompositionTarget.Rendering -= UpdateRopeMotion;
    }
    async Task RemoveAfterFall(Card card)
    {
        await Task.Delay(300);
        line.Children.Remove(card);
    }
    internal bool CanAnimateTransfer => IsVisible && Revealed && SystemParameters.ClientAreaAnimation && !app.Settings.GameDoNotDisturb;
    internal int ActiveTransferCount => flights.Count + desktopFlights.Count;
    internal bool CanPinTransfer(Photo photo) => CanAnimateTransfer && cards.ContainsKey(photo.Path);
    internal void HoldReturnSource(Photo photo,Action release) => returnSources[photo]=(release,null);
    internal bool HoldingReturnSource(Photo photo) => returnSources.ContainsKey(photo);
    internal void ArrivalPresented(Photo photo,EventArgs args)
    {
        if(!returnSources.TryGetValue(photo,out var source))return;
        var frame=(args as RenderingEventArgs)?.RenderingTime;
        if(frame==null)return;
        if(source.frame==null) { returnSources[photo]=(source.release,frame);return; }
        if(source.frame!=frame) ReleaseReturnSource(photo);
    }
    void ReleaseReturnSource(Photo photo)
    {
        if(returnSources.Remove(photo,out var source)) source.release();
    }
    void ReleaseReturnSources()
    {
        foreach(var photo in returnSources.Keys.ToArray()) ReleaseReturnSource(photo);
    }
    // Keep the layered HWND close to the actual shelf. A full-height transparent
    // window multiplies software raster/composition work on 4K/high-DPI displays.
    double SurfaceHeight => Math.Min(geometry?.LocalBounds.Height ?? PanelHeight + 140, extendedSurface ? double.MaxValue : PanelHeight + 140);
    int SurfacePixels(int fullHeight, double dpi) => minimalSurface ? 1 : extendedSurface ? fullHeight : Math.Min(fullHeight, (int)Math.Ceiling((PanelHeight + 140) * Math.Max(.01, dpi)));
    internal void PrepareSurface(bool manual = true)
    {
        if (!minimalSurface) return;
        minimalSurface = false;
        MoveTo(Screen, manual);
    }
    void CompactSurface()
    {
        if (IsVisible) return;
        minimalSurface = true;
        Width = Height = 1;
        Native.SetWindowPos(Hwnd, IntPtr.Zero, 0, 0, 1, 1, 0x16);
    }
    internal bool ExtendedTransferSurface => extendedSurface;
    void SetTransferSurface(bool extended)
    {
        extendedSurface = extended;
        var height = minimalSurface ? 1 : SurfaceHeight;
        if (Math.Abs(Height - height) < .01) return;
        Height = height;
        if (IsVisible && geometry != null && !changingDisplay)
            Native.SetWindowPos(Hwnd, new IntPtr(-1), Screen.Bounds.Left, Screen.Bounds.Top, minimalSurface ? 1 : Screen.Bounds.Width, SurfacePixels(Screen.Bounds.Height, geometry.Scale), 0x10);
    }
    void RetireFlight(MediaTransferVisual flight)
    {
        if (!flights.Remove(flight, out var done)) return;
        flight.Stop(); line.Children.Remove(flight);
        try { done(); }
        finally { if (flights.Count == 0) SetTransferSurface(false); ReleaseUnusedPhotoArtwork(); }
    }
    internal Rect BasketMouth {
        get {
            if(basketArt.ActualWidth>0) return basketArt.TransformToAncestor(line).TransformBounds(basketArt.MouthBounds);
            var mouth=basketArt.NormalizedMouth;
            return new(Canvas.GetLeft(basketButton)+4+mouth.X*basketArt.Width/64,Canvas.GetTop(basketButton)+2+mouth.Y*basketArt.Height/76,mouth.Width*basketArt.Width/64,mouth.Height*basketArt.Height/76);
        }
    }
    internal Rect? BasketScreenBounds()
    {
        if (!IsVisible) return null;
        var mouth = BasketMouth;
        return new Rect(line.PointToScreen(mouth.TopLeft), line.PointToScreen(mouth.BottomRight));
    }
    internal bool PreparePinTransfer(Photo photo, Rect destination, Action completed,PinAppearance? appearance=null,Action? landed=null,Func<(PinAppearance? appearance,Rect? bounds)>? refresh=null)
    {
        if (!CanAnimateTransfer || !cards.ContainsKey(photo.Path) || destination.IsEmpty) return false;
        pinTransfers[photo] = (destination, completed,appearance,landed,refresh); return true;
    }
    internal void ReceiveFromScreen(Photo photo, Rect source)
    {
        if (!CanAnimateTransfer) return;
        SetTransferSurface(true);
        var mouth = BasketMouth;
        var flight = new MediaTransferVisual(photo.Image, new Rect(line.PointFromScreen(source.TopLeft), line.PointFromScreen(source.BottomRight)), mouth, true, mouth.Y + 12) { Width = LayoutWidth, Height = line.Height };
        Panel.SetZIndex(flight, 900); line.Children.Add(flight); flights[flight] = basketArt.Receive;
        flight.Play(() => RetireFlight(flight), releaseDelay: false);
    }
    void Depart(Card card)
    {
        ReleaseReturnSource(card.Photo);
        var pinning = pinTransfers.Remove(card.Photo, out var pinTarget);
        if (!CanAnimateTransfer || (!pinning && (!app.Store.Collection.Contains(card.Photo) || !File.Exists(card.Photo.Path))))
        {
            line.Children.Remove(card); if (pinning) pinTarget.complete(); return;
        }
        var from = card.FrameBounds(line);
        var mouth = BasketMouth;
        var target = pinning ? new Rect(line.PointFromScreen(pinTarget.target.TopLeft), line.PointFromScreen(pinTarget.target.BottomRight)) : mouth;
        if (from.Width < 1 || from.Height < 1) { line.Children.Remove(card); if (pinning) pinTarget.complete(); return; }
        if(pinning) {
            var desktop=new DesktopTransfer(card.Photo.Image,this,card.ImageScreenPose(),ArrivalPose.FromRect(pinTarget.target),card.ImageSize,card.FrameVariant,pinTarget.appearance,pinTarget.refresh);
            var destinationReady=false;
            void CompleteDestination() { if(destinationReady)return;destinationReady=true;pinTarget.complete(); }
            desktopFlights[desktop]=()=>{line.Children.Remove(card);CompleteDestination();};
            desktop.Play(card.HideTransferPicture,pinTarget.landed??CompleteDestination,()=>RetireDesktopFlight(desktop));
            card.BeginTransferOut(holdPicture:true);
            return;
        }
        card.BeginTransferOut();
        var flight = new MediaTransferVisual(card.Photo.Image, from, target, !pinning, mouth.Y + 12) { Width = LayoutWidth, Height = line.Height };
        Panel.SetZIndex(flight, 900); line.Children.Add(flight);
        void Complete() { line.Children.Remove(card); if (pinning) pinTarget.complete(); else basketArt.Receive(); }
        flights[flight] = Complete;
        flight.Play(() => RetireFlight(flight));
    }
    void CancelFlights()
    {
        foreach (var card in cards.Values) card.CancelArrivalFlight();
        foreach(var desktop in desktopFlights.Keys.ToArray()) RetireDesktopFlight(desktop);
        ReleaseReturnSources();
        foreach (var (flight, done) in flights.ToArray()) { flight.Stop(); line.Children.Remove(flight); done(); }
        flights.Clear();
        foreach (var target in pinTransfers.Values.ToArray()) target.complete();
        pinTransfers.Clear();
        SetTransferSurface(false);
        ReleaseUnusedPhotoArtwork();
    }
    void RetireDesktopFlight(DesktopTransfer flight)
    {
        if(!desktopFlights.Remove(flight,out var complete)) return;
        flight.Stop();
        try { complete(); }
        finally { ReleaseUnusedPhotoArtwork(); }
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
        var animation = new DoubleAnimation(0, SystemParameters.ClientAreaAnimation ? 6 : 0, TimeSpan.FromSeconds(.62)) { EasingFunction = new RopeBounceEase { EasingMode = EasingMode.EaseIn }, FillBehavior = FillBehavior.Stop };
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
        ThemeArtwork.Rope(dc, g, Theme.Current);
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

