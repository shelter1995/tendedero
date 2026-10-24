using System.Windows.Automation;
using System.Windows.Shapes;
using System.Windows.Data;
namespace Liangtu;

internal static class VideoChrome
{
    public const string Play = "M7,4 L20,12 L7,20 Z";
    public const string Pause = "M8,4 L8,20 M16,4 L16,20";
    public const string Back = "M14,5 L7,12 L14,19 M4,5 L4,19";
    public const string Forward = "M10,5 L17,12 L10,19 M20,5 L20,19";
    public const string Trim = "@scissors";
    public const string Annotate = "@annotate";
    public const string Open = "M9,4 L3,4 L3,21 L20,21 L20,15 M13,3 L21,3 L21,11 M10,14 L21,3";
    public static FrameworkElement Glyph(string geometry) => ToolbarVisual.Glyph(geometry);
    public static void Hint(FrameworkElement element, string name)
    {
        ToolbarVisual.Hint(element, name);
    }
    public static Button Icon(string name, string geometry, Action action)
    {
        return ToolbarVisual.Button(name, geometry, action);
    }
    public static Border Divider() => ToolbarVisual.Divider();
    public static Border Bar(UIElement content, bool bottom = false) => new() { Background = Theme.ControlSurface, BorderBrush = Theme.Outline, BorderThickness = new Thickness(0, bottom ? 1 : 0, 0, bottom ? 0 : 1), Child = content };
    public static TextBlock Label(string text, double size = 12, bool muted = true) => new() { Text = text, FontSize = size, Foreground = muted ? Theme.Muted : Theme.Ink, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    public static void Window(Window w)
    {
        Theme.WindowStyle(w); w.Width = Math.Min(1120, SystemParameters.WorkArea.Width - 40); w.Height = Math.Min(800, SystemParameters.WorkArea.Height - 40); w.MinWidth = 700; w.MinHeight = 480;
    }
}
