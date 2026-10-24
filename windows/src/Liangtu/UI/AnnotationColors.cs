namespace Liangtu;

internal static class AnnotationColors
{
    public static Color Parse(string? hex)
    {
        try { if (hex?.Length == 7 && hex[0] == '#') return (Color)ColorConverter.ConvertFromString(hex); } catch (FormatException) { }
        return Color.FromRgb(226, 77, 64);
    }
    public static int[] Slots(int[]? colors) => Enumerable.Range(0, 16).Select(i => colors != null && i < colors.Length ? colors[i] & 0xFFFFFF : 0xFFFFFF).ToArray();
    public static Color? Choose(Window owner, Color current, Settings settings)
    {
        using var dialog = new System.Windows.Forms.ColorDialog {
            FullOpen = true, AnyColor = true,
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B),
            CustomColors = Slots(settings.AnnotationCustomColors)
        };
        if (dialog.ShowDialog(new WindowHandle(new System.Windows.Interop.WindowInteropHelper(owner).Handle)) != System.Windows.Forms.DialogResult.OK) return null;
        settings.AnnotationCustomColors = dialog.CustomColors;
        return Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
    }
    sealed class WindowHandle : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; }
        public WindowHandle(IntPtr handle) => Handle = handle;
    }
}
