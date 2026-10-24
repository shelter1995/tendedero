using System.Globalization;
using System.Windows.Controls.Primitives;
namespace Liangtu;

public sealed partial class EditorWindow
{
    readonly List<TextAnnotation> texts = new();
    readonly Canvas textLayer = new() { ClipToBounds = false };
    readonly Dictionary<Guid, (Border frame, TextBox input, Border handle)> textViews = new();
    Guid? selectedText;
    string textFont = "Microsoft YaHei UI";
    double textSize = 28;
    bool textBold, textItalic, updatingTextProperties;
    FrameworkElement? textProperties;
    ComboBox? fontChoice, fontSizeChoice;
    ToggleButton? boldChoice, italicChoice;
    Button? deleteTextButton;
    internal IReadOnlyList<TextAnnotation> Texts => texts;
    internal Guid? SelectedText => selectedText;

    void InitializeTextDefaults()
    {
        var s = app.Settings;
        textFont = ValidFont(s.TextFontFamily);
        textSize = double.IsFinite(s.TextFontSize) ? Math.Clamp(s.TextFontSize, 6, 512) : 28;
        textBold = s.TextBold; textItalic = s.TextItalic;
    }
    static string ValidFont(string? name) => !string.IsNullOrWhiteSpace(name) && Fonts.SystemFontFamilies.Any(f => f.Source.Equals(name, StringComparison.OrdinalIgnoreCase)) ? name : "Microsoft YaHei UI";

    FrameworkElement BuildTextProperties()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        fontChoice = new ComboBox { ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToArray(), SelectedItem = textFont, Width = 164, Margin = new Thickness(0, 0, 8, 0), ToolTip = "文字字体", MaxDropDownHeight = 260 };
        fontChoice.SetResourceReference(StyleProperty, "AttributeCombo");
        System.Windows.Automation.AutomationProperties.SetName(fontChoice, "文字字体");
        row.Children.Add(fontChoice);
        fontSizeChoice = new ComboBox { ItemsSource = new[] { "12", "16", "20", "24", "28", "32", "40", "48", "64", "96", "128" }, IsEditable = true, Text = textSize.ToString(CultureInfo.InvariantCulture), Width = 68, Margin = new Thickness(0, 0, 8, 0), ToolTip = "文字大小 · 图像像素（6–512）" };
        fontSizeChoice.SetResourceReference(StyleProperty, "AttributeCombo");
        System.Windows.Automation.AutomationProperties.SetName(fontSizeChoice, "文字字号（像素）");
        row.Children.Add(fontSizeChoice);
        boldChoice = new ToggleButton { Content = "B", FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.Bold, FontSize = 16, IsChecked = textBold };
        italicChoice = new ToggleButton { Content = "I", FontFamily = new FontFamily("Georgia"), FontStyle = FontStyles.Italic, FontWeight = FontWeights.Bold, FontSize = 18, IsChecked = textItalic };
        boldChoice.SetResourceReference(StyleProperty, "ToolbarToggle"); italicChoice.SetResourceReference(StyleProperty, "ToolbarToggle");
        Hint(boldChoice, "粗体"); Hint(italicChoice, "斜体");
        row.Children.Add(boldChoice); row.Children.Add(italicChoice);
        deleteTextButton = IconButton("删除选中文字", "M5,7 L19,7 M9,4 L15,4 M7,7 L8,21 L16,21 L17,7 M10,10 L10,18 M14,10 L14,18", DeleteText); deleteTextButton.IsEnabled = false; row.Children.Add(deleteTextButton);
        fontChoice.SelectionChanged += (_, _) => { if (!updatingTextProperties && fontChoice.SelectedItem is string font) ChangeTextStyle(font, textSize, color, textBold, textItalic); };
        void SizeChanged()
        {
            if (updatingTextProperties) return;
            if (double.TryParse(fontSizeChoice.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var size) && double.IsFinite(size) && size >= 6 && size <= 512)
                ChangeTextStyle(textFont, size, color, textBold, textItalic);
            else { fontSizeChoice.Text = textSize.ToString(CultureInfo.InvariantCulture); status.Text = "字号请输入 6–512 之间的数字"; }
        }
        fontSizeChoice.SelectionChanged += (_, _) => {
            if (!updatingTextProperties && fontSizeChoice.SelectedItem is string value && double.TryParse(value, out var size)) ChangeTextStyle(textFont, size, color, textBold, textItalic);
        };
        fontSizeChoice.LostKeyboardFocus += (_, _) => SizeChanged();
        fontSizeChoice.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { SizeChanged(); e.Handled = true; } };
        boldChoice.Click += (_, _) => ChangeTextStyle(textFont, textSize, color, boldChoice.IsChecked == true, textItalic);
        italicChoice.Click += (_, _) => ChangeTextStyle(textFont, textSize, color, textBold, italicChoice.IsChecked == true);
        textProperties = row; row.Visibility = Visibility.Collapsed;
        return textProperties;
    }

    void SyncTextProperties()
    {
        updatingTextProperties = true;
        try {
            if (fontChoice != null) fontChoice.SelectedItem = textFont;
            if (fontSizeChoice != null) fontSizeChoice.Text = textSize.ToString(CultureInfo.InvariantCulture);
            if (boldChoice != null) boldChoice.IsChecked = textBold;
            if (italicChoice != null) italicChoice.IsChecked = textItalic;
            if (deleteTextButton != null) deleteTextButton.IsEnabled = selectedText != null;
            SetColor(color);
        } finally { updatingTextProperties = false; }
    }
    internal void ChangeTextStyle(string font, double size, Color ink, bool bold, bool italic)
    {
        if (updatingTextProperties || !double.IsFinite(size)) return;
        CommitText();
        textFont = ValidFont(font); textSize = Math.Clamp(size, 6, 512); color = ink; textBold = bold; textItalic = italic;
        int index = texts.FindIndex(t => t.Id == selectedText);
        if (index >= 0) {
            texts[index] = texts[index] with { FontFamily = textFont, FontSize = textSize, Color = color, Bold = bold, Italic = italic };
            ApplyTextView(texts[index]); CommitText();
        }
        SyncTextProperties();
        app.Settings.TextFontFamily = textFont; app.Settings.TextFontSize = textSize; app.Settings.TextBold = bold; app.Settings.TextItalic = italic;
        try { app.Settings.Save(); } catch (Exception e) { Settings.Log("Text defaults " + e.Message); }
    }
    void AddText(Point position)
    {
        CommitText();
        texts.RemoveAll(t => string.IsNullOrWhiteSpace(t.Text));
        var text = new TextAnnotation(Guid.NewGuid(), "", Math.Min(position.X, Math.Max(0, canvas.Width - 72)), Math.Min(position.Y, Math.Max(0, canvas.Height - textSize - 8)), textFont, textSize, color, textBold, textItalic);
        texts.Add(text); selectedText = text.Id; RebuildTextViews();
        var input = textViews[text.Id].input;
        // Newly inserted WPF controls become focusable only after layout/load.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => {
            if (selectedText == text.Id && tool == "文字" && IsVisible) { input.Focus(); Keyboard.Focus(input); }
        }));
        status.Text = "直接输入 · Enter 换行 · 拖动左侧移动柄 · Esc 完成编辑";
    }
    internal void SelectText(Guid id)
    {
        if (!texts.Any(t => t.Id == id)) return;
        if (selectedText != id) CommitText();
        selectedText = id;
        var text = texts.Single(t => t.Id == id);
        textFont = text.FontFamily; textSize = text.FontSize; color = text.Color; textBold = text.Bold; textItalic = text.Italic;
        foreach (var item in texts) ApplyTextView(item);
        SyncTextProperties();
    }
    internal void CommitText()
    {
        var meaningful = texts.Where(t => !string.IsNullOrWhiteSpace(t.Text)).ToArray();
        if (!meaningful.SequenceEqual(history.Current.Texts))
            history.Commit(history.Current.Image, history.Current.Baseline, meaningful);
        undoButton.IsEnabled = history.CanUndo; redoButton.IsEnabled = history.CanRedo;
    }
    internal void MoveTextTo(Point position, bool commit = true)
    {
        int index = texts.FindIndex(t => t.Id == selectedText);
        if (index < 0) return;
        var label = texts[index].CreateLabel(); label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var bounds = new RotateTransform(texts[index].Angle).TransformBounds(new Rect(label.DesiredSize));
        texts[index] = texts[index] with {
            X = Math.Clamp(position.X, 8 - bounds.Right, canvas.Width - 8 - bounds.Left),
            Y = Math.Clamp(position.Y, 8 - bounds.Bottom, canvas.Height - 8 - bounds.Top)
        };
        ApplyTextView(texts[index]);
        if (commit) CommitText();
    }
    internal void DeleteText()
    {
        if (selectedText == null) return;
        CommitText(); texts.RemoveAll(t => t.Id == selectedText); selectedText = null;
        CommitText(); RebuildTextViews(); viewport.Focus();
    }
    void RebuildTextViews()
    {
        textLayer.Children.Clear(); textViews.Clear();
        textLayer.Width = canvas.Width; textLayer.Height = canvas.Height;
        Panel.SetZIndex(textLayer, 100);
        if (!canvas.Children.Contains(textLayer)) canvas.Children.Add(textLayer);
        textLayer.IsHitTestVisible = tool == "文字";
        if (textProperties != null) textProperties.Visibility = tool == "文字" ? Visibility.Visible : Visibility.Collapsed;
        if (brushProperties != null) brushProperties.Visibility = tool == "文字" ? Visibility.Collapsed : Visibility.Visible;
        foreach (var text in texts.ToArray()) {
            var id = text.Id;
            var input = new TextBox { Text = text.Text, AcceptsReturn = true, AcceptsTab = false, TextWrapping = TextWrapping.NoWrap,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), MinWidth = 64,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden };
            System.Windows.Automation.AutomationProperties.SetName(input, "图片内文字");
            TextOptions.SetTextFormattingMode(input, TextFormattingMode.Ideal);
            var frame = new Border { Child = input, Background = Brushes.Transparent, BorderThickness = new Thickness(1), Padding = new Thickness(3), UseLayoutRounding = false, SnapsToDevicePixels = false };
            var handle = new Border { Width = 20, Height = 22, Background = Theme.ControlSurface, BorderBrush = Theme.Accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
                Cursor = Cursors.SizeAll, ToolTip = "拖动调整文字位置", Child = new TextBlock { Text = "✥", Foreground = Theme.Ink, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            textViews[id] = (frame, input, handle); textLayer.Children.Add(frame); textLayer.Children.Add(handle);
            input.TextChanged += (_, _) => {
                var index = texts.FindIndex(t => t.Id == id);
                if (index >= 0) texts[index] = texts[index] with { Text = input.Text };
            };
            input.LostKeyboardFocus += (_, _) => CommitText();
            frame.PreviewMouseLeftButtonDown += (_, e) => {
                if (viewport.SpaceHeld || viewport.PanMode) return;
                if (selectedText != id) { SelectText(id); input.Focus(); input.CaretIndex = input.Text.Length; e.Handled = true; }
            };
            // A text click must never bubble into the canvas's new-text gesture.
            frame.MouseLeftButtonDown += (_, e) => e.Handled = true;
            Point? dragStart = null; TextAnnotation? before = null;
            handle.MouseLeftButtonDown += (_, e) => { SelectText(id); CommitText(); before = texts.Single(t => t.Id == id); dragStart = e.GetPosition(canvas); handle.CaptureMouse(); e.Handled = true; };
            handle.MouseMove += (_, e) => { if (dragStart is Point start && before != null) { var delta = e.GetPosition(canvas) - start; MoveTextTo(new Point(before.X + delta.X, before.Y + delta.Y), false); e.Handled = true; } };
            handle.MouseLeftButtonUp += (_, e) => { if (dragStart != null) { dragStart = null; CommitText(); handle.ReleaseMouseCapture(); viewport.Focus(); e.Handled = true; } };
            handle.LostMouseCapture += (_, _) => { if (dragStart != null && before != null) { dragStart = null; var index = texts.FindIndex(t => t.Id == id); if (index >= 0) { texts[index] = before; ApplyTextView(before); } } };
            ApplyTextView(text);
        }
        SyncTextProperties();
    }
    void ApplyTextView(TextAnnotation text)
    {
        if (!textViews.TryGetValue(text.Id, out var view)) return;
        bool selected = selectedText == text.Id && tool == "文字";
        view.frame.BorderBrush = selected ? Theme.Accent : Brushes.Transparent;
        view.frame.RenderTransform = new RotateTransform(text.Angle);
        Canvas.SetLeft(view.frame, text.X); Canvas.SetTop(view.frame, text.Y);
        var zoom = Math.Max(.05, viewport.Zoom);
        view.handle.RenderTransform = new ScaleTransform(1 / zoom, 1 / zoom);
        view.handle.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        view.input.IsReadOnly = !selected; view.input.IsHitTestVisible = selected;
        view.input.FontFamily = new FontFamily(text.FontFamily); view.input.FontSize = text.FontSize;
        view.input.FontWeight = text.Bold ? FontWeights.Bold : FontWeights.Normal;
        view.input.FontStyle = text.Italic ? FontStyles.Italic : FontStyles.Normal;
        view.input.Foreground = new SolidColorBrush(text.Color); view.input.CaretBrush = new SolidColorBrush(text.Color);
        TextOptions.SetTextRenderingMode(view.input, TextRenderingMode.Grayscale);
        var label = text.CreateLabel(); label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var bounds = new RotateTransform(text.Angle).TransformBounds(new Rect(label.DesiredSize));
        Canvas.SetLeft(view.handle, Math.Clamp(text.X + bounds.Left - 22 / zoom, 0, Math.Max(0, canvas.Width - 20 / zoom)));
        Canvas.SetTop(view.handle, Math.Clamp(text.Y + bounds.Top, 0, Math.Max(0, canvas.Height - 22 / zoom)));
    }
}
