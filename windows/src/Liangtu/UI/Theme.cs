using System.Windows.Media.Animation;
namespace Liangtu;
public static class Theme
{
    public static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
    public static bool Dark => Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int v && v == 0;
    public static Brush Ink => SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Dark ? Brush("#F2F2F4") : Brush("#252527");
    public static Brush Surface => Dark ? Brush("#F02B2B30") : Brush("#F0F1F1F4");
    public static Brush WindowSurface => SystemParameters.HighContrast ? SystemColors.WindowBrush : Dark ? Brush("#202124") : Brush("#F5F6F8");
    public static Brush ControlSurface => SystemParameters.HighContrast ? SystemColors.ControlBrush : Dark ? Brush("#2B2D31") : Brush("#FFFFFF");
    public static Brush Muted => SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Dark ? Brush("#B6BAC4") : Brush("#626775");
    public static Brush Accent => SystemParameters.HighContrast ? SystemColors.HighlightBrush : Dark ? Brush("#9BB8FF") : Brush("#315FC4");
    public static Brush Outline => SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Dark ? Brush("#454850") : Brush("#D6D9E1");
    public static FontFamily Font => new("Microsoft YaHei UI, Segoe UI");
    public static TextBlock Text(string text, double size = 13, Brush? brush = null) => new() { Text = text, FontSize = size, Foreground = brush ?? Ink, TextWrapping = TextWrapping.Wrap };
    public static Button Primary(string label, Action action)
    {
        var button = Button(label, action);
        button.Background = SystemParameters.HighContrast ? SystemColors.HighlightBrush : Brush("#3868CE");
        button.Foreground = SystemParameters.HighContrast ? SystemColors.HighlightTextBrush : Brushes.White;
        button.BorderBrush = button.Background;
        return button;
    }
    public static BitmapSource AppImage => BitmapFrame.Create(new Uri("pack://application:,,,/Liangtu;component/Assets/app.png"));
    public static Border Glass(UIElement child, double radius = 16, double inset = 4) => new GlassBorder { Child = child, Padding = new Thickness(inset), CornerRadius = new CornerRadius(radius) };
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
        w.Resources["Ink"] = w.Foreground;
        w.Resources["ControlSurface"] = ControlSurface;
        w.Resources["Outline"] = Outline;
        w.Resources["Accent"] = Accent;
        w.Resources["SelectionInk"] = SystemParameters.HighContrast ? SystemColors.HighlightTextBrush : Accent;
        w.Resources["Hover"] = Dark ? Brush("#363940") : Brush("#E9ECF2");
        w.Resources["Selection"] = SystemParameters.HighContrast ? SystemColors.HighlightBrush : Dark ? Brush("#2E3C59") : Brush("#E3EBFC");
        w.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Liangtu;component/UI/Controls.xaml", UriKind.Relative) });
        w.WindowStartupLocation = w.Owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        w.SourceInitialized += (_, _) => {
            var dark = Dark ? 1 : 0;
            DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(w).Handle, 20, ref dark, 4);
        };
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
