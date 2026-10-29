using System.Windows.Controls.Primitives;
namespace Liangtu;

public sealed partial class EditorWindow
{
    readonly Border currentColor = new() { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), BorderBrush = Theme.Outline };
    readonly List<(Color color, RadioButton button)> swatches = new();
    StackPanel? brushProperties;
    internal Dock NativeToolDock { get; private set; } = Dock.Top;
    internal bool NativeToolLabelsVisible { get; private set; }
    internal void SelectTool(string value)
    {
        CancelStroke(); CommitText(); editingText = null; selectedText = selectedShape = null; tool = value; viewport.PanMode = value == "平移"; RebuildTextViews(); UpdateSelectionView();
        foreach (var button in toolKeys.Values) {var selected=((string?)button.ToolTip)?.StartsWith((value=="裁剪"?"图片裁剪":value) + " ·", StringComparison.Ordinal) == true;if(button.IsChecked!=selected)button.IsChecked=selected;}
        status.Text = value == "选择" ? "点击标注选择 · 拖动移动 · 右键复制 / 删除 · 方向键微调" : value == "平移" ? "拖动画布 · 方向键平移" : value == "文字" ? "点击图片添加文字" : value == "裁剪" ? "拖动选择保留区域" : value == "矩形" ? "拖动绘制矩形 · 按住 Shift 画正方形" : value == "圆形" ? "拖动绘制椭圆 · 按住 Shift 画正圆" : value is "直线" or "箭头" ? "按住 Shift 限制为水平、垂直或 45°" : value == "橡皮擦" ? "擦除标注，保留原图" : "滚轮缩放 · 空格 / 中键拖动画布";
    }
    UIElement BuildLayout()
    {
        var root = new DockPanel();
        var workArea = new DockPanel();
        var toolFaces = new List<(Control button, StackPanel face, FrameworkElement glyph, TextBlock label, bool command)>();
        void LabelTool(Control button, string label, bool command = false)
        {
            var content = button is ContentControl control ? control : throw new InvalidOperationException();
            var glyph = (FrameworkElement)content.Content; content.Content = null;
            var face = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var caption = new TextBlock { Text = label, FontSize = 11, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
            face.Children.Add(glyph); face.Children.Add(caption); content.Content = face;
            toolFaces.Add((button, face, glyph, caption, command));
        }
        var tools = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10, 8, 10, 8) };
        var definitions = AnnotationControls.DrawingTools;
        foreach (var (name, key, geometry) in definitions)
        {
            var button = new RadioButton { GroupName = "Tools", Content = Glyph(geometry), Width = 32, Height = 32, Padding = new Thickness(6), Margin = new Thickness(2, 0, 2, 0), IsChecked = tool == name };
            button.SetResourceReference(StyleProperty, "ToolbarTool");
            Hint(button, (name == "裁剪" ? "图片裁剪" : name) + " · " + key + (name == "矩形" ? " · Shift 正方形" : name == "圆形" ? " · Shift 正圆" : name is "直线" or "箭头" ? " · Shift 水平 / 垂直 / 45°" : ""));
            button.Checked += (_, _) => SelectTool(name); toolKeys[key] = button; tools.Children.Add(button);
            LabelTool(button,name);
        }
        var divider1=Divider(); tools.Children.Add(divider1);
        undoButton = IconButton("撤销 · Ctrl + Z", ToolbarVisual.Undo, Undo); tools.Children.Add(undoButton);
        redoButton = IconButton("重做 · Ctrl + Y / Ctrl + Shift + Z", ToolbarVisual.Redo, Redo); tools.Children.Add(redoButton);
        LabelTool(undoButton,"撤销",true); LabelTool(redoButton,"重做",true);
        var rotate=IconButton("顺时针旋转 90°", "M19,9 A8,8 0 1 0 20,15 M19,3 L19,9 L13,9", Rotate); tools.Children.Add(rotate); LabelTool(rotate,"旋转",true);
        var divider2=Divider(); tools.Children.Add(divider2);
        var save = IconButton("保存标注 · Ctrl + S", "@save", Save); tools.Children.Add(save);
        LabelTool(save,"保存",true);
        var toolScroll=new ScrollViewer { Content=tools, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled, VerticalScrollBarVisibility=ScrollBarVisibility.Disabled, Focusable=false };
        var toolBar = Theme.Panel(toolScroll, "ToolbarMargin");
        DockPanel.SetDock(toolBar, Dock.Top); root.Children.Add(toolBar);

        var properties = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(12, 5, 12, 5) };
        var palette = AnnotationControls.Palette(currentColor, () => { CancelStroke(); var chosen = AnnotationColors.Choose(this, color, app.Settings); if (chosen is Color value) SetColor(value); });
        properties.Children.Add(palette); currentColor.Background = new SolidColorBrush(color);
        foreach (var hex in AnnotationControls.Colors)
        {
            var value = AnnotationColors.Parse(hex);
            var button = AnnotationControls.Swatch(hex,"Colors",value==color); button.Checked += (_, _) => SetColor(value); swatches.Add((value, button)); properties.Children.Add(button);
        }
        properties.Children.Add(Divider());
        brushProperties = AnnotationControls.Stroke(brushWidth,value=>brushWidth=value,out _);
        properties.Children.Add(brushProperties); properties.Children.Add(BuildTextProperties());
        var propertyBar = Theme.Panel(properties, "PropertyMargin");
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
        viewport.Changed += () => { zoomLabel.Text = $"{viewport.Zoom * 100:0.#}%"; foreach (var text in texts) ApplyTextView(text); UpdateSelectionView(); };
        DockPanel.SetDock(viewControls, Dock.Right); footer.Children.Add(viewControls);
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        dimensions.FontSize = 11; dimensions.Foreground = Theme.Muted; info.Children.Add(dimensions);
        status.Text = "滚轮缩放 · 空格 / 中键拖动画布"; status.FontSize = 11; status.Foreground = Theme.Muted; status.TextTrimming = TextTrimming.CharacterEllipsis; info.Children.Add(status); footer.Children.Add(info);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var pictureMat=Theme.Panel(viewport); pictureMat.Background=Theme.CanvasSurface;
        workArea.Children.Add(pictureMat); root.Children.Add(workArea);
        string layoutMode="";
        void ApplyThemeLayout()
        {
            var id=Theme.Current.Id; var vertical=id=="color-play"; var linen=id=="linen"; var labeled=vertical||linen;
            NativeToolDock=vertical?Dock.Left:Dock.Top; NativeToolLabelsVisible=labeled;
            if(layoutMode!=id) {
                if(toolBar.Parent is Panel previous) previous.Children.Remove(toolBar);
                DockPanel.SetDock(toolBar,NativeToolDock);
                if(vertical) workArea.Children.Insert(0,toolBar); else root.Children.Insert(0,toolBar);
                layoutMode=id;
            }
            toolBar.Margin=vertical?new Thickness(12,0,8,10):linen?new Thickness(12,10,12,3): (Thickness)Resources["ToolbarMargin"];
            toolBar.Width=vertical?124:double.NaN;
            toolScroll.VerticalScrollBarVisibility=vertical?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;
            tools.Margin=vertical?new Thickness(5,7,5,7):linen?new Thickness(7,5,7,5):new Thickness(10,8,10,8);
            tools.Width=vertical?94:double.NaN;
            var linenWidth=root.ActualWidth>920?52d:44d;
            foreach(var (button,face,glyph,label,command) in toolFaces) {
                AnnotationControls.ToolFace(button,face,glyph,label,command,root.ActualWidth,button==save);
            }
            foreach(var divider in new[]{divider1,divider2}) {
                divider.Width=vertical?80:1; divider.Height=vertical?1:20;
                divider.Margin=vertical?new Thickness(6,7,6,7):new Thickness(6,0,6,0);
            }
            propertyBar.Margin=vertical?new Thickness(12,10,12,8):linen?new Thickness(12,3,12,8):(Thickness)Resources["PropertyMargin"];
            pictureMat.Margin=vertical?new Thickness(0,0,12,10):linen?new Thickness(12,0,12,8):new Thickness(0);
            pictureMat.Padding=new Thickness(labeled?10:0);
            pictureMat.BorderThickness=new Thickness(labeled?1:0);
            pictureMat.CornerRadius=new CornerRadius(vertical?14:linen?5:0);
            // Reflow the same hosts and controls: no canvas, selection, history or
            // text-editor recreation occurs during a theme change.
            InvalidateMeasure();
        }
        Theme.Changed+=ApplyThemeLayout; Closed+=(_,_)=>Theme.Changed-=ApplyThemeLayout;
        root.SizeChanged+=(_,_)=>ApplyThemeLayout(); ApplyThemeLayout();
        return root;
    }
    void SetColor(Color value)
    {
        color = value; currentColor.Background = new SolidColorBrush(value); currentColor.ToolTip = value.ToString();
        if (!updatingTextProperties && (tool == "文字" || selectedText != null)) { ChangeTextStyle(textFont, textSize, value, textBold, textItalic); return; }
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

