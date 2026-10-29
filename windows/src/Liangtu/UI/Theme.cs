using System.Windows.Media.Animation;
namespace Liangtu;
public static class Theme
{
    sealed class PaletteValues : System.ComponentModel.INotifyPropertyChanged
    {
        public readonly Dictionary<string, Color> Values = new();
        public Color this[string key] => Values.TryGetValue(key, out var value) ? value : Colors.Transparent;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public void Notify() => PropertyChanged?.Invoke(this, new("Item[]"));
    }
    static readonly PaletteValues values = new();
    static readonly Dictionary<string, SolidColorBrush> brushes = new();
    static string selected = "system", lastPalette = "";
    static ThemeProfile? active;
    public static string SelectedId => selected;
    public static ThemeProfile Current => active ??= ThemeProfiles.Resolve(selected, SystemDark);
    public static bool SystemDark => Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int v && v == 0;
    public static event Action? Changed;
    public static void Select(string? id)
    {
        selected = ThemeProfiles.Valid(id) ? id! : "system"; active = ThemeProfiles.Resolve(selected, SystemDark); RasterThemeArt.RetainTheme(active); lastPalette = ""; EnsurePalette();
        if (Application.Current != null) foreach (Window window in Application.Current.Windows) { RefreshResources(window); InvalidateTree(window); if (window.Content is DependencyObject content) InvalidateTree(content); }
        Changed?.Invoke();
    }
    static void EnsurePalette()
    {
        var profile = Current; var key = profile.Id + ":" + SystemParameters.HighContrast;
        if (lastPalette == key) return; lastPalette = key;
        foreach (var pair in profile.Colors) values.Values[pair.Key] = profile.Color(pair.Key);
        if (SystemParameters.HighContrast) {
            foreach (var name in new[] { "WindowSurface", "CanvasSurface", "Surface", "FrameFill" }) values.Values[name] = SystemColors.WindowColor;
            values.Values["ControlSurface"] = SystemColors.ControlColor;
            values.Values["ActionFill"] = SystemColors.ControlColor; values.Values["OnAccent"] = SystemColors.HighlightTextColor;
            foreach (var name in new[] { "Ink", "Muted", "Outline", "ActionInk" }) values.Values[name] = SystemColors.WindowTextColor;
            values.Values["Accent"] = values.Values["Selection"] = values.Values["Hover"] = SystemColors.HighlightColor;
            values.Values["SelectionInk"] = SystemColors.HighlightTextColor;
        }
        values.Notify();
        foreach (var brush in brushes.Values) System.Windows.Data.BindingOperations.GetBindingExpression(brush, SolidColorBrush.ColorProperty)?.UpdateTarget();
    }
    public static SolidColorBrush Token(string name)
    {
        EnsurePalette();
        if (!brushes.TryGetValue(name, out var brush)) {
            brush = new SolidColorBrush();
            System.Windows.Data.BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty, new System.Windows.Data.Binding("[" + name + "]") { Source = values });
            brushes[name] = brush;
        }
        return brush;
    }
    public static Brush Token(string name, ThemeProfile? preview) => preview == null ? Token(name) : new SolidColorBrush(preview.Color(name));
    public static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
    public static bool Dark => SystemParameters.HighContrast ? SystemColors.WindowColor.R < 128 : Current.Dark;
    public static Brush Ink => Token("Ink");
    public static Brush Surface => Token("Surface");
    public static Brush WindowSurface => Token("WindowSurface");
    public static Brush ControlSurface => Token("ControlSurface");
    public static Brush Muted => Token("Muted");
    public static Brush Accent => Token("Accent");
    public static Brush Outline => Token("Outline");
    public static Brush CanvasSurface => Token("CanvasSurface");
    public static FontFamily Font => new("Microsoft YaHei UI, Segoe UI");
    public static TextBlock Text(string text, double size = 13, Brush? brush = null) => new() { Text = text, FontSize = size, Foreground = brush ?? Ink, TextWrapping = TextWrapping.Wrap };
    public static Button Primary(string label, Action action)
    {
        var button = Button(label, action);
        button.Background = Accent;
        button.Foreground = Token("OnAccent");
        button.BorderBrush = button.Background;
        return button;
    }
    public static BitmapSource AppImage => ThemeIcons.AppImage(Current);
    public static Border Glass(UIElement child, double radius = 16, double inset = 4) => new GlassBorder { Child = child, Padding = new Thickness(inset), CornerRadius = new CornerRadius(radius == 16 ? Current.FrameRadius : radius) };
    public static UIElement DiscardIcon(bool inbox)
    {
        var path = new System.Windows.Shapes.Path { Data = Geometry.Parse(inbox ? "M3,3 L13,13 M13,3 L3,13" : "M1,8 Q8,-1 15,8 Q8,17 1,8 M2,2 L14,14 M10,8 A2,2 0 1 1 6,8 A2,2 0 1 1 10,8"), Stroke = Ink, StrokeThickness = 1.8, Width = 10, Height = 10, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        return path;
    }
    public static Button Button(string label, Action action)
    {
        var b = new Button { Content = label, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(3), MinHeight = 32, Cursor = Cursors.Hand };
        b.Click += (_, _) => action();
        return b;
    }
    public static void Animate(IAnimatable target, DependencyProperty property, double to, double seconds = .22, IEasingFunction? easing = null)
    {
        target.BeginAnimation(property, new DoubleAnimation(to, TimeSpan.FromSeconds(seconds)) { EasingFunction = easing ?? new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
    public static void Shell(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception e) { MessageBox.Show(e.Message, "晾图"); }
    }
    public static void RevealFile(string path)
    {
        var p = new System.Diagnostics.ProcessStartInfo("explorer.exe") { UseShellExecute = true };
        p.ArgumentList.Add("/select,");
        p.ArgumentList.Add(path);
        try
        {
            System.Diagnostics.Process.Start(p);
        }
        catch (Exception e) { MessageBox.Show(e.Message, "晾图"); }
    }
    public static void WindowStyle(Window w)
    {
        w.Background = WindowSurface;
        w.Foreground = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Ink;
        w.FontFamily = Font;
        w.FontSize = 13;
        w.UseLayoutRounding = true;
        w.SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(w, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(w, TextRenderingMode.ClearType);
        w.Icon = AppImage;
        RefreshResources(w);
        w.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Liangtu;component/UI/Controls.xaml", UriKind.Relative) });
        w.WindowStartupLocation = w.Owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        w.SourceInitialized += (_, _) => {
            RefreshResources(w);
        };
    }
    static void RefreshResources(Window window)
    {
        if(!window.AllowsTransparency || window.ShowInTaskbar) window.Icon=AppImage;
        foreach (var key in Current.Colors.Keys) window.Resources[key] = Token(key);
        window.Resources["ControlRadius"] = new CornerRadius(Current.ControlRadius);
        window.Resources["ToolRadius"] = new CornerRadius(Current.ToolRadius);
        window.Resources["InputRadius"] = new CornerRadius(Current.Id is "linen" or "color-play"?Current.ControlRadius:0);
        window.Resources["PanelRadius"] = new CornerRadius(Current.Id == "darkroom" ? 1 : Current.Id == "color-play" ? 17 : Current.Id == "linen" ? 11 : 9);
        window.Resources["PanelBorder"] = new Thickness(Current.Id == "darkroom" ? 1 : Current.Id == "porcelain" ? 1 : .7);
        window.Resources["ToolPadding"] = Current.Id == "color-play" ? new Thickness(10, 7, 10, 7) : Current.Id == "darkroom" ? new Thickness(7, 5, 7, 5) : new Thickness(8, 6, 8, 6);
        window.Resources["PanelInset"] = new Thickness(Current.Id == "color-play" ? 12 : Current.Id == "darkroom" ? 6 : 9);
        window.Resources["ToolbarMargin"] = Current.Id == "darkroom" ? new Thickness(0) : Current.Id == "color-play" ? new Thickness(14, 10, 14, 3) : Current.Id == "linen" ? new Thickness(12, 8, 12, 3) : new Thickness(8, 5, 8, 2);
        window.Resources["PropertyMargin"] = Current.Id == "darkroom" ? new Thickness(0) : new Thickness(14, 3, 14, 8);
        window.Resources["SelectedToolBorder"] = new Thickness(Current.Id == "darkroom" ? 0 : 1, Current.Id == "darkroom" ? 0 : 1, Current.Id == "darkroom" ? 0 : 1, Current.Id == "darkroom" ? 3 : 1);
        var materialControls=Current.Id is "linen" or "color-play";
        // The concept toolbars use one continuous tray; only the active tool
        // has a filled face. Separate boxed buttons make the rail too busy.
        window.Resources["ToolRestSurface"] = Brushes.Transparent;
        window.Resources["ToolRestOutline"] = Brushes.Transparent;
        window.Resources["ToolSelectedSurface"] = Current.Id=="linen" ? Accent : Current.Id=="color-play" ? Brush("#D9EFFB") : Token("Selection");
        window.Resources["ToolSelectedInk"] = Current.Id=="linen" ? Token("OnAccent") : Current.Id=="color-play" ? Brush("#255D7D") : Token("SelectionInk");
        window.Resources["ToolSelectedOutline"] = Current.Id=="color-play" ? Brush("#ABD2E9") : Accent;
        window.Resources["ControlHighlight"] = materialControls ? Brush("#BBFFFFFF") : Brushes.Transparent;
        var materialWindow = window is not LineWindow && window is not ArrivalFlight && window is not CaptureWindow;
        window.Resources["LinenPaperTexture"] = materialWindow && Current.Id == "linen" ? RasterThemeArt.Surface("linen") : Brushes.Transparent;
        window.Resources["ColorPlaySurfaceTexture"] = materialWindow && Current.Id == "color-play" ? RasterThemeArt.Surface("color-play") : Brushes.Transparent;
        if(SystemParameters.HighContrast) {
            window.Resources["ToolRestSurface"]=SystemColors.ControlBrush; window.Resources["ToolRestOutline"]=SystemColors.WindowTextBrush;
            window.Resources["ToolSelectedSurface"]=SystemColors.HighlightBrush; window.Resources["ToolSelectedInk"]=SystemColors.HighlightTextBrush;
            window.Resources["ToolSelectedOutline"]=SystemColors.HighlightBrush; window.Resources["ControlHighlight"]=Brushes.Transparent;
        }
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero) ApplyNativeChrome(window);
    }
    internal static void ApplyNativeChrome(Window window)
    {
        var hwnd=new System.Windows.Interop.WindowInteropHelper(window).Handle;if(hwnd==IntPtr.Zero)return;
        if(window.AllowsTransparency && window.WindowStyle==System.Windows.WindowStyle.None) {
            // Per-pixel overlays have no rectangular system surface or frame.
            // A rounded DWM decoration belongs only to normal application windows.
            var noCorners=1;DwmSetWindowAttribute(hwnd,33,ref noCorners,4);
            var noBackdrop=1;DwmSetWindowAttribute(hwnd,38,ref noBackdrop,4);
            var noNonClient=1;DwmSetWindowAttribute(hwnd,2,ref noNonClient,4);
            return;
        }
        var dark=Dark?1:0;DwmSetWindowAttribute(hwnd,20,ref dark,4);
        var corners=Current.Id=="darkroom"?1:Current.Id=="porcelain"?3:2;DwmSetWindowAttribute(hwnd,33,ref corners,4);
    }
    public static Border Panel(UIElement child, string? marginResource = null)
    {
        var panel = new MaterialPanel { Child = child, Background = ControlSurface, BorderBrush = Outline };
        panel.SetResourceReference(Border.CornerRadiusProperty, "PanelRadius");
        panel.SetResourceReference(Border.BorderThicknessProperty, "PanelBorder");
        if (marginResource != null) panel.SetResourceReference(FrameworkElement.MarginProperty, marginResource);
        return panel;
    }
    static void InvalidateTree(DependencyObject root)
    {
        if (root is UIElement element) element.InvalidateVisual();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) InvalidateTree(VisualTreeHelper.GetChild(root, i));
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
public sealed class SpringEase : EasingFunctionBase
{
    public double Response { get; set; } = .42; public double Damping { get; set; } = .82; public double Duration { get; set; } = .8;
    protected override double EaseInCore(double t)
    {
        if (t >= 1)
            return 1;
        var omega = 2 * Math.PI / Response;
        var damp = Math.Min(.999, Damping);
        var wd = omega * Math.Sqrt(1 - damp * damp);
        var time = t * Duration;
        return 1 - Math.Exp(-damp * omega * time) * (Math.Cos(wd * time) + damp * omega / wd * Math.Sin(wd * time));
    }
    protected override Freezable CreateInstanceCore() => new SpringEase { Response = Response, Damping = Damping, Duration = Duration };
}

