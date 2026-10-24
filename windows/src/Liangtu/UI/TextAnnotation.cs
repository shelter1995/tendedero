namespace Liangtu;

internal sealed record TextAnnotation(Guid Id, string Text, double X, double Y,
    string FontFamily, double FontSize, Color Color, bool Bold = false, bool Italic = false, double Angle = 0)
{
    public Border CreateLabel()
    {
        // Use the exact same text layout as the editor, without focus/selection.
        var input = new TextBox { Text = Text, FontFamily = new FontFamily(FontFamily), FontSize = FontSize,
            Foreground = new SolidColorBrush(Color), FontWeight = Bold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = Italic ? FontStyles.Italic : FontStyles.Normal, Padding = new Thickness(0), BorderThickness = new Thickness(0),
            Background = Brushes.Transparent, MinWidth = 64, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            IsReadOnly = true, IsHitTestVisible = false, Focusable = false };
        TextOptions.SetTextFormattingMode(input, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(input, TextRenderingMode.Grayscale);
        return new Border { Child = input, Padding = new Thickness(3), BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent, IsHitTestVisible = false, UseLayoutRounding = false, SnapsToDevicePixels = false };
    }
    public TextAnnotation Crop(double x, double y) => this with { X = X - x, Y = Y - y };
    public TextAnnotation Rotate(double oldHeight) => this with { X = oldHeight - Y, Y = X, Angle = (Angle + 90) % 360 };
}
