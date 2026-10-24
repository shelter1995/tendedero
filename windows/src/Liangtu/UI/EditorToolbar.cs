using System.Windows.Controls.Primitives;
namespace Liangtu;

public sealed partial class EditorWindow
{
    readonly Border currentColor = new() { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), BorderBrush = Theme.Outline };
    readonly List<(Color color, RadioButton button)> swatches = new();
    StackPanel? brushProperties;
    internal void SelectTool(string value)
    {
        CancelStroke(); CommitText(); selectedText = null; tool = value; viewport.PanMode = value == "平移"; RebuildTextViews();
        foreach (var button in toolKeys.Values) if (((string?)button.ToolTip)?.StartsWith(value + " ·", StringComparison.Ordinal) == true && button.IsChecked != true) button.IsChecked = true;
        status.Text = value == "平移" ? "拖动画布 · 方向键平移" : value == "文字" ? "点击图片添加文字" : value == "裁剪" ? "拖动选择保留区域" : value == "矩形" ? "拖动绘制矩形 · 按住 Shift 画正方形" : value == "圆形" ? "拖动绘制椭圆 · 按住 Shift 画正圆" : value is "直线" or "箭头" ? "按住 Shift 限制为水平、垂直或 45°" : value == "橡皮擦" ? "擦除标注，保留原图" : "滚轮缩放 · 空格 / 中键拖动画布";
    }
    UIElement BuildLayout()
    {
        var root = new DockPanel();
        var tools = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10, 8, 10, 8) };
        var definitions = new[] {
            ("矩形", Key.R, "M3,5 L21,5 L21,19 L3,19 Z"),
            ("圆形", Key.O, "M12,3 A9,9 0 1 1 12,21 A9,9 0 1 1 12,3"),
            ("直线", Key.L, "M4,20 L20,4"),
            ("箭头", Key.A, "M4,20 L20,4 M9,4 L20,4 L20,15"),
            ("画笔", Key.B, "@pencil"),
            ("荧光笔", Key.H, "@marker"),
            ("文字", Key.T, "M3,5 L21,5 M12,5 L12,21 M7,21 L17,21"),
            ("裁剪", Key.C, "@crop"),
            ("橡皮擦", Key.E, "@eraser"),
            ("平移", Key.V, "@hand") };
        foreach (var (name, key, geometry) in definitions)
        {
            var button = new RadioButton { GroupName = "Tools", Content = Glyph(geometry), Width = 32, Height = 32, Padding = new Thickness(6), Margin = new Thickness(2, 0, 2, 0), IsChecked = tool == name };
            button.SetResourceReference(StyleProperty, "ToolbarTool");
            Hint(button, (name == "裁剪" ? "图片裁剪" : name) + " · " + key + (name == "矩形" ? " · Shift 正方形" : name == "圆形" ? " · Shift 正圆" : name is "直线" or "箭头" ? " · Shift 水平 / 垂直 / 45°" : ""));
            button.Checked += (_, _) => SelectTool(name); toolKeys[key] = button; tools.Children.Add(button);
        }
        tools.Children.Add(Divider());
        undoButton = IconButton("撤销 · Ctrl + Z", ToolbarVisual.Undo, Undo); tools.Children.Add(undoButton);
        redoButton = IconButton("重做 · Ctrl + Y / Ctrl + Shift + Z", ToolbarVisual.Redo, Redo); tools.Children.Add(redoButton);
        tools.Children.Add(IconButton("顺时针旋转 90°", "M19,9 A8,8 0 1 0 20,15 M19,3 L19,9 L13,9", Rotate));
        tools.Children.Add(Divider());
        var save = IconButton("保存标注 · Ctrl + S", "@save", Save); tools.Children.Add(save);
        var toolBar = new Border { Background = Theme.ControlSurface, BorderBrush = Theme.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Child = tools };
        DockPanel.SetDock(toolBar, Dock.Top); root.Children.Add(toolBar);

        var properties = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(12, 5, 12, 5) };
        var palette = Theme.Button("", () => { CancelStroke(); var chosen = AnnotationColors.Choose(this, color, app.Settings); if (chosen is Color value) SetColor(value); });
        var paletteContent = new StackPanel { Orientation = Orientation.Horizontal }; paletteContent.Children.Add(currentColor);
        var chevron = Glyph("M2,7 L12,18 L22,7"); chevron.Width = chevron.Height = 10; chevron.Margin = new Thickness(6, 0, 1, 0); paletteContent.Children.Add(chevron);
        palette.Content = paletteContent; palette.Padding = new Thickness(5); palette.Margin = new Thickness(2, 0, 10, 0); palette.Width = 48; palette.Height = 34; Hint(palette, "更多颜色… · Windows 调色盘 / RGB / 自定义色槽");
        properties.Children.Add(palette); currentColor.Background = new SolidColorBrush(color);
        foreach (var hex in new[] { "#E24D40", "#F09B32", "#F3CC46", "#218368", "#267BDC", "#8B5FC7", "#202624", "#FFFFFF" })
        {
            var value = AnnotationColors.Parse(hex);
            var button = new RadioButton { GroupName = "Colors", Padding = new Thickness(4), Margin = new Thickness(1, 0, 1, 0), VerticalAlignment = VerticalAlignment.Center, IsChecked = value == color,
                Content = new Border { Width = 16, Height = 16, Background = new SolidColorBrush(value), CornerRadius = new CornerRadius(8), BorderBrush = Theme.Outline, BorderThickness = new Thickness(1) } };
            Hint(button, "颜色 " + hex); button.Checked += (_, _) => SetColor(value); swatches.Add((value, button)); properties.Children.Add(button);
        }
        properties.Children.Add(Divider());
        brushProperties = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        brushProperties.Children.Add(new TextBlock { Text = "笔宽", Foreground = Theme.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 4, 0) });
        var sizeLabel = new TextBlock { Text = "4 px", Width = 42, Foreground = Theme.Muted, VerticalAlignment = VerticalAlignment.Center };
        var size = new Slider { Minimum = 1, Maximum = 24, Value = brushWidth, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 104, Margin = new Thickness(8, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        Hint(size, "笔宽 · 图像像素"); size.ValueChanged += (_, _) => { brushWidth = size.Value; sizeLabel.Text = $"{brushWidth:0} px"; }; brushProperties.Children.Add(size); brushProperties.Children.Add(sizeLabel);
        properties.Children.Add(brushProperties); properties.Children.Add(BuildTextProperties());
        var propertyBar = new Border { BorderBrush = Theme.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Child = properties };
        DockPanel.SetDock(propertyBar, Dock.Top); root.Children.Add(propertyBar);

        var footer = new DockPanel { Margin = new Thickness(14, 6, 14, 6) };
        var viewControls = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var arrow in new[] { ("向左平移 · ←", "M15,5 L8,12 L15,19", -60.0, 0.0), ("向右平移 · →", "M9,5 L16,12 L9,19", 60.0, 0.0), ("向上平移 · ↑", "M5,15 L12,8 L19,15", 0.0, -60.0), ("向下平移 · ↓", "M5,9 L12,16 L19,9", 0.0, 60.0) })
            viewControls.Children.Add(Small(IconButton(arrow.Item1, arrow.Item2, () => viewport.Pan(arrow.Item3, arrow.Item4))));
        viewControls.Children.Add(Divider());
        viewControls.Children.Add(Small(IconButton("缩小 · −", "M5,12 L19,12", () => viewport.ZoomBy(.8))));
        zoomLabel.Text = "100%"; zoomLabel.Width = 52; zoomLabel.TextAlignment = TextAlignment.Center; zoomLabel.VerticalAlignment = VerticalAlignment.Center; zoomLabel.Foreground = Theme.Ink; viewControls.Children.Add(zoomLabel);
        viewControls.Children.Add(Small(IconButton("放大 · +", "M5,12 L19,12 M12,5 L12,19", () => viewport.ZoomBy(1.25))));
        var actual = Theme.Button("100%", () => viewport.SetZoom(1)); actual.Padding = new Thickness(8, 3, 8, 3); actual.MinHeight = 30; Hint(actual, "实际大小 · Ctrl + 1"); viewControls.Children.Add(actual);
        viewControls.Children.Add(Small(IconButton("适应窗口 · F / Ctrl + 0", "M3,9 L3,3 L9,3 M15,3 L21,3 L21,9 M21,15 L21,21 L15,21 M9,21 L3,21 L3,15", viewport.Fit)));
        viewport.Changed += () => { zoomLabel.Text = $"{viewport.Zoom * 100:0.#}%"; foreach (var text in texts) ApplyTextView(text); };
        DockPanel.SetDock(viewControls, Dock.Right); footer.Children.Add(viewControls);
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        dimensions.FontSize = 11; dimensions.Foreground = Theme.Muted; info.Children.Add(dimensions);
        status.Text = "滚轮缩放 · 空格 / 中键拖动画布"; status.FontSize = 11; status.Foreground = Theme.Muted; status.TextTrimming = TextTrimming.CharacterEllipsis; info.Children.Add(status); footer.Children.Add(info);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); root.Children.Add(viewport);
        return root;
    }
    void SetColor(Color value)
    {
        color = value; currentColor.Background = new SolidColorBrush(value); currentColor.ToolTip = value.ToString();
        if (!updatingTextProperties && tool == "文字") { ChangeTextStyle(textFont, textSize, value, textBold, textItalic); return; }
        foreach (var swatch in swatches) swatch.button.IsChecked = swatch.color == value;
        app.Settings.AnnotationColor = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
        try { app.Settings.Save(); } catch (Exception e) { Settings.Log("Annotation colors " + e.Message); }
    }
    static FrameworkElement Glyph(string geometry) => ToolbarVisual.Glyph(geometry);
    static Button IconButton(string name, string geometry, Action action) => ToolbarVisual.Button(name, geometry, action);
    static Button Small(Button button) { button.Width = button.Height = 30; button.MinHeight = 30; button.Padding = new Thickness(5); if (button.Content is FrameworkElement glyph) glyph.Width = glyph.Height = 16; return button; }
    static Border Divider() => ToolbarVisual.Divider();
    static void Hint(FrameworkElement element, string text)
    {
        ToolbarVisual.Hint(element, text);
    }
}
