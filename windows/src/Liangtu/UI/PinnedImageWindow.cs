using System.Windows.Interop;
using System.Windows.Media.Animation;
namespace Liangtu;

public sealed class PinnedImageWindow : Window
{
    readonly App app;
    readonly Photo photo;
    readonly Image picture = new() { Stretch = Stretch.Uniform };
    readonly TextBlock feedback = Theme.Text("滚轮缩放 · 拖动图片移动", 11, Theme.Muted);
    readonly DockPanel tools = new() { LastChildFill=false };
    readonly MaterialPanel surface;
    BitmapSource thumbnail;
    int loadGeneration;
    bool closed;
    internal bool ReturnOnClose { get; private set; } = true;
    internal Rect? LastPictureBounds { get; private set; }
    double aspect;
    bool initialSizeApplied, userZoomed;
    bool returnQueued;
    bool transferControls;
    internal bool Returning { get; private set; }
    internal Photo Photo => photo;
    public PinnedImageWindow(App app, Photo photo)
    {
        this.app = app; this.photo = photo; thumbnail = photo.Image;
        Title = Path.GetFileName(photo.Path) + " · 贴图"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false; ShowActivated = false;
        Theme.WindowStyle(this); Background = Brushes.Transparent;
        aspect = (double)photo.ThumbnailWidth / photo.ThumbnailHeight;
        Width = Math.Clamp(photo.ThumbnailWidth, 280, 520); Height = Width / aspect + 68;
        var layout = new DockPanel(); surface = (MaterialPanel)Theme.Panel(layout); surface.SetResourceReference(Border.PaddingProperty, "PanelInset"); Content = surface;
        var returnButton = Theme.Button("挂回绳子", () => app.ReturnToLine(photo)); returnButton.Padding = new Thickness(8, 3, 8, 3); returnButton.MinHeight = 30; returnButton.FontSize = 12; returnButton.ToolTip = "挂回晾衣绳 · Esc"; tools.Children.Add(returnButton);
        var basketButton = ToolbarVisual.Button("放入篮子（不挂回）", "@basket", () => app.PutPinInBasket(photo));
        basketButton.Content = new BasketVisual { Width = 20, Height = 20, Compact = true }; tools.Children.Add(basketButton);
        tools.Children.Add(ToolbarVisual.Button("复制图片", "@copy", async () => await Copy()));
        tools.Children.Add(ToolbarVisual.Button("编辑图片", "@annotate", () => app.Edit(photo)));
        var close = ToolbarVisual.Button("关闭并挂回绳子 · Esc", "M6,6 L18,18 M18,6 L6,18", Close); DockPanel.SetDock(close, Dock.Right); tools.Children.Add(close);
        DockPanel.SetDock(tools, Dock.Top); layout.Children.Add(tools);
        feedback.Margin = new Thickness(6, 5, 6, 3); feedback.TextTrimming = TextTrimming.CharacterEllipsis; DockPanel.SetDock(feedback, Dock.Bottom); layout.Children.Add(feedback);
        picture.Source = thumbnail; RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality); layout.Children.Add(picture);
        picture.Cursor = Cursors.SizeAll;
        picture.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { app.Edit(photo); e.Handled = true; return; } try { DragMove(); } catch (InvalidOperationException) { } e.Handled = true; };
        MouseWheel += (_, e) => { userZoomed = true; Zoom(e.Delta > 0 ? 1.12 : 1 / 1.12); e.Handled = true; };
        MouseRightButtonUp += (_, e) => { NativeMenu.Show(app.PhotoMenu(photo), this); e.Handled = true; };
        KeyDown += async (_, e) => {
            if (e.Key == Key.Escape) Close();
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control) await Copy();
            else if (e.Key == Key.Add || e.Key == Key.OemPlus) { userZoomed = true; Zoom(1.12); }
            else if (e.Key == Key.Subtract || e.Key == Key.OemMinus) { userZoomed = true; Zoom(1 / 1.12); }
            else return; e.Handled = true;
        };
        app.Store.Changed += StoreChanged;
        Closing += (_, e) => {
            LastPictureBounds = PictureBounds();
            if(ReturnOnClose&&app.CanDeferPinReturn(photo)) {
                e.Cancel=true;
                if(returnQueued||Returning)return;
                returnQueued=true;
                Dispatcher.BeginInvoke(()=>{returnQueued=false;if(!closed)app.ReturnToLine(photo);},DispatcherPriority.Input);
            }
        };
        Closed += (_, _) => { closed = true; ++loadGeneration; app.Store.Changed -= StoreChanged; };
        _ = LoadPicture();
    }
    internal Rect? PictureBounds()
    {
        if (!IsVisible || picture.ActualWidth < 1 || picture.ActualHeight < 1) return null;
        // Image.Stretch=Uniform may letterbox; return the visible image rectangle.
        var w = Math.Min(picture.ActualWidth, picture.ActualHeight * aspect); var h = w / aspect;
        var top = new Point((picture.ActualWidth - w) / 2, (picture.ActualHeight - h) / 2);
        return new Rect(picture.PointToScreen(top), picture.PointToScreen(new Point(top.X + w, top.Y + h)));
    }
    internal PinAppearance? CaptureAppearance(bool includeControls=true)
    {
        UpdateLayout();
        if(PictureBounds() is not Rect physical||physical.Width<1||physical.Height<1)return null;
        var w=Math.Min(picture.ActualWidth,picture.ActualHeight*aspect);var h=w/aspect;
        var top=picture.TranslatePoint(new Point((picture.ActualWidth-w)/2,(picture.ActualHeight-h)/2),this);
        var imageRect=new Rect(top,new Size(w,h));var controls=new DrawingGroup();
        using(var dc=controls.Open()) {
            if(includeControls) { Capture(tools);Capture(feedback); }
            void Capture(FrameworkElement element) {
                if(element.ActualWidth<1||element.ActualHeight<1)return;
                var branch=new DrawingGroup();using(var paint=branch.Open()) {
                    paint.PushTransform(Matrix(element.TransformToAncestor(this)));CaptureBranch(element,paint);paint.Pop();
                }
                dc.DrawDrawing(branch);
            }
        }
        controls=controls.CloneCurrentValue();if(controls.CanFreeze)controls.Freeze();
        return new PinAppearance(surface.CaptureChrome(),controls,new Size(ActualWidth,ActualHeight),imageRect,
            physical.Width/w,physical.Height/h,surface.CornerRadius.TopLeft,(BitmapSource)picture.Source);
    }
    static MatrixTransform Matrix(GeneralTransform transform)
    {
        var origin=transform.Transform(new Point());var x=transform.Transform(new Point(1,0))-origin;var y=transform.Transform(new Point(0,1))-origin;
        return new MatrixTransform(new Matrix(x.X,x.Y,y.X,y.Y,origin.X,origin.Y));
    }
    static void CaptureBranch(Visual visual,DrawingContext dc)
    {
        if(visual is UIElement element&&element.Visibility!=Visibility.Visible)return;
        dc.PushOpacity(VisualTreeHelper.GetOpacity(visual));var mask=VisualTreeHelper.GetOpacityMask(visual);if(mask!=null)dc.PushOpacityMask(mask);var clip=VisualTreeHelper.GetClip(visual);
        if(clip!=null)dc.PushClip(clip);
        var drawing=VisualTreeHelper.GetDrawing(visual);if(drawing!=null)dc.DrawDrawing(drawing);
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(visual);i++) if(VisualTreeHelper.GetChild(visual,i) is Visual child) {
            dc.PushTransform(Matrix(child.TransformToAncestor(visual)));CaptureBranch(child,dc);dc.Pop();
        }
        if(clip!=null)dc.Pop();if(mask!=null)dc.Pop();dc.Pop();
    }
    internal void CloseWithoutReturn() { if(closed)return;ReturnOnClose = false; Close(); }
    internal void BeginReturn() { Returning=true;IsHitTestVisible=false;foreach(var element in new UIElement[]{tools,feedback}) {var opacity=element.Opacity;element.BeginAnimation(OpacityProperty,null);element.Opacity=opacity;} }
    internal void PresentTransferDestination() { if(!closed){BeginAnimation(OpacityProperty,null);Opacity=1;} }
    internal void FinishTransfer()
    {
        if(closed)return;PresentTransferDestination();IsHitTestVisible=true;
        if(!transferControls)return;transferControls=false;
        foreach(var element in new UIElement[]{tools,feedback}) {
            element.Opacity=1;
            if(!SystemParameters.ClientAreaAnimation)continue;
            var clock=new DoubleAnimation(0,1,TimeSpan.FromSeconds(.12)){FillBehavior=FillBehavior.Stop}.CreateClock();
            element.ApplyAnimationClock(OpacityProperty,clock);clock.Controller!.SeekAlignedToLastTick(TimeSpan.Zero,TimeSeekOrigin.BeginTime);
        }
    }
    public void ShowOn(System.Windows.Forms.Screen screen,bool deferPresentation=false)
    {
        var scale = Native.Dpi(screen); var area = screen.WorkingArea;
        // Read only the header here so the transfer endpoint uses the final
        // original-pixel size before Show; full decoding remains asynchronous.
        try {
            using var stream = File.OpenRead(photo.Path);
            var source = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            ApplyInitialSize(source.PixelWidth, source.PixelHeight, area.Width / scale, area.Height / scale, scale);
        } catch (Exception e) when (e is IOException or NotSupportedException or FileFormatException) { Settings.Log("Pin dimensions " + e.Message); }
        Width = Math.Min(Width, area.Width / scale - 32); Height = Math.Min(Height, area.Height / scale - 32);
        WindowStartupLocation = WindowStartupLocation.Manual;
        SourceInitialized += (_, _) => {
            var hwnd = new WindowInteropHelper(this).Handle;
            var bounds = ScreenPixels.WindowBounds(area, Width, Height, scale);
            Native.SetWindowPos(hwnd, new IntPtr(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x10);
        };
        if(deferPresentation) { Opacity=0;IsHitTestVisible=false;transferControls=true;tools.Opacity=feedback.Opacity=0; }
        Show();
        if (!deferPresentation&&SystemParameters.ClientAreaAnimation) BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(.18)));
    }
    void ApplyInitialSize(int pixelWidth, int pixelHeight, double workWidth, double workHeight, double dpi)
    {
        if (initialSizeApplied || userZoomed || pixelWidth <= 0 || pixelHeight <= 0) return;
        aspect = pixelWidth / (double)pixelHeight;
        var width = Math.Min(pixelWidth / dpi, Math.Min(workWidth * .8 - 24, (workHeight * .8 - 90) * aspect));
        Width = Math.Max(Math.Min(320, workWidth - 32), width + 24);
        Height = Math.Min(workHeight - 32, width / aspect + 90);
        initialSizeApplied = true;
    }
    internal void Zoom(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0) return;
        var area = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var maxWidth = Math.Max(280, Math.Min(area.Width / dpi - 20, (area.Height / dpi - 88) * aspect));
        var width = Math.Clamp(Width * factor, Math.Min(280, maxWidth), maxWidth);
        var left = Left - (width - Width) / 2; var height = (width - 12) / aspect + 68;
        Width = width; Height = Math.Min(height, area.Height / dpi - 20);
        Left = Math.Clamp(left, area.Left / dpi, Math.Max(area.Left / dpi, area.Right / dpi - Width));
        Top = Math.Clamp(Top, area.Top / dpi, Math.Max(area.Top / dpi, area.Bottom / dpi - Height));
    }
    async Task Copy()
    {
        feedback.Text = "正在复制…";
        try { await app.Store.Copy(photo); feedback.Text = "已复制 · 滚轮缩放"; }
        catch { feedback.Text = "复制失败，请重试"; }
    }
    void StoreChanged(Photo? changed)
    {
        if (!app.Store.Collection.Contains(photo) || app.Store.Items.Contains(photo)&&!Returning) { CloseWithoutReturn(); return; }
        if (!ReferenceEquals(thumbnail, photo.Image)) { thumbnail = photo.Image; _ = LoadPicture(); }
    }
    async Task LoadPicture()
    {
        var generation = ++loadGeneration;
        try {
            var bitmap = await Task.Run(() => MediaTools.LoadImage(photo.Path));
            if (closed || generation != loadGeneration) return;
            picture.Source = bitmap; aspect = (double)bitmap.PixelWidth / bitmap.PixelHeight;
            if (IsVisible) {
                var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
                var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
                ApplyInitialSize(bitmap.PixelWidth,bitmap.PixelHeight,screen.WorkingArea.Width/dpi,screen.WorkingArea.Height/dpi,dpi);
            }
        } catch (Exception e) { if (!closed) feedback.Text = "无法载入：" + e.Message; }
    }
}
