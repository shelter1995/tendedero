using System.Globalization;
using System.Windows.Controls.Primitives;

namespace Liangtu;

public sealed partial class TrimWindow
{
    readonly TextBox elementStart = new(), elementEnd = new();
    readonly StackPanel elementFields = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly Dictionary<Key, RadioButton> annotationKeys = new();
    readonly Border inkSample = new() { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), BorderBrush = Theme.Outline, BorderThickness = new Thickness(1) };
    readonly List<(Color color,RadioButton button)> videoSwatches = new();
    readonly StackPanel fontProperties = new() { Orientation = Orientation.Horizontal }, strokeProperties = new() { Orientation = Orientation.Horizontal };
    ComboBox fontChoice = null!, sizeChoice = null!;
    Slider widthSlider = null!;
    Button deleteTextChoice = null!;
    ToggleButton boldChoice = null!, italicChoice = null!;
    bool syncingProperties;
    Border annotationToolbar = null!;
    FrameworkElement annotationProperties = null!;
    Border? annotationMat;
    void InstallAnnotationToolbar(DockPanel root, DockPanel stage)
    {
        var tools = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10,8,10,8) };
        var faces = new List<(Control button, StackPanel face, FrameworkElement glyph, TextBlock label,bool command)>();
        void AddFace(Control button,string name,bool command=false) {
            var control = (ContentControl)button; var glyph = (FrameworkElement)control.Content; control.Content = null;
            var face = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var label = new TextBlock { Text = name, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
            face.Children.Add(glyph); face.Children.Add(label); control.Content = face; faces.Add((button,face,glyph,label,command)); tools.Children.Add(button);
        }
        foreach (var (name,key,geometry) in AnnotationControls.DrawingTools.Where(x=>x.Name is not ("裁剪" or "橡皮擦"))) {
            var button = new RadioButton { GroupName = "VideoAnnotationTools", Content = ToolbarVisual.Glyph(geometry), IsChecked = name == annotations.Tool };
            button.SetResourceReference(StyleProperty,"ToolbarTool"); VideoChrome.Hint(button,name + " · " + key);
            button.Checked += (_,_) => SelectVideoTool(name);
            annotationKeys[key] = button; AddFace(button,name);
        }
        var divider = VideoChrome.Divider(); tools.Children.Add(divider); AddFace(undo,"撤销",true); AddFace(redo,"重做",true);
        var fit = VideoChrome.Icon("适应窗口 · F / Ctrl+0","M3,9 L3,3 L9,3 M15,3 L21,3 L21,9 M21,15 L21,21 L15,21 M9,21 L3,21 L3,15",annotations.Fit); AddFace(fit,"适应",true);
        var divider2=VideoChrome.Divider();tools.Children.Add(divider2);var toolbarSave=VideoChrome.Icon("保存视频 · Ctrl+S","@save",Save);AddFace(toolbarSave,"保存",true);
        var scroller = new ScrollViewer { Content = tools, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        annotationToolbar = Theme.Panel(scroller,"ToolbarMargin"); DockPanel.SetDock(annotationToolbar,Dock.Top); root.Children.Add(annotationToolbar);
        void ApplyTheme() {
            var vertical = Theme.Current.Id == "color-play"; var labeled = vertical || Theme.Current.Id == "linen";
            if (annotationToolbar.Parent is Panel previous) previous.Children.Remove(annotationToolbar);
            DockPanel.SetDock(annotationToolbar,vertical ? Dock.Left : Dock.Top);
            if (vertical) stage.Children.Insert(0,annotationToolbar); else root.Children.Insert(0,annotationToolbar);
            AnnotationControls.ToolPanels(annotationToolbar,scroller,tools,new[]{divider,divider2},annotationProperties,annotationMat);
            foreach (var (button,face,glyph,label,command) in faces) AnnotationControls.ToolFace(button,face,glyph,label,command,root.ActualWidth,button==toolbarSave);
            elementTrack.SetElements(annotations.Elements,player.Duration,annotations.SelectedId);
        }
        Theme.Changed += ApplyTheme; Closed += (_,_) => Theme.Changed -= ApplyTheme; ApplyTheme();
        Loaded+=(_,_)=>ApplyTheme();
        root.SizeChanged += (_,_) => { foreach(var (button,face,glyph,label,command) in faces) AnnotationControls.ToolFace(button,face,glyph,label,command,root.ActualWidth,button==toolbarSave); };
    }
    UIElement BuildAnnotationProperties()
    {
        var row = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(12,5,12,5) };
        var palette = AnnotationControls.Palette(inkSample,() => { var value = AnnotationColors.Choose(this,annotations.Ink,app.Settings); if (value is Color color) { annotations.Ink = color; annotations.ApplyStyle(); SyncAnnotationProperties(); } }); row.Children.Add(palette);
        foreach (var hex in AnnotationControls.Colors) {
            var color=AnnotationColors.Parse(hex);var button=AnnotationControls.Swatch(hex,"VideoColors",color==annotations.Ink);
            button.Checked+=(_,_)=>{if(!syncingProperties){annotations.Ink=color;annotations.ApplyStyle();SyncAnnotationProperties();}};videoSwatches.Add((color,button));row.Children.Add(button);
        }
        row.Children.Add(VideoChrome.Divider());
        ComboBox Choice(string name,double width) { var choice = new ComboBox { Width = width, Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center, ToolTip = name,MaxDropDownHeight=260 }; choice.SetResourceReference(StyleProperty,"AttributeCombo"); System.Windows.Automation.AutomationProperties.SetName(choice,name); return choice; }
        strokeProperties.Children.Add(AnnotationControls.Stroke(annotations.StrokeWidth,value=>{if(!syncingProperties){annotations.StrokeWidth=value;annotations.ApplyStyle();}},out widthSlider));row.Children.Add(strokeProperties);
        double widthBeforeDrag=annotations.StrokeWidth;
        widthSlider.AddHandler(Thumb.DragStartedEvent,new DragStartedEventHandler((_,_)=>{player.Pause();widthBeforeDrag=annotations.StrokeWidth;editingAttributes=true;}),true);
        widthSlider.AddHandler(Thumb.DragCompletedEvent,new DragCompletedEventHandler((_,e)=>{if(e.Canceled){annotations.StrokeWidth=widthBeforeDrag;annotations.ApplyStyle();SyncAnnotationProperties();}editingAttributes=false;CommitSelection();}),true);
        fontChoice = Choice("文字字体",164); fontChoice.ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(x => x).ToArray(); fontProperties.Children.Add(fontChoice);
        sizeChoice = Choice("文字字号（像素）",68); sizeChoice.IsEditable = true; sizeChoice.ItemsSource = new[] { "12","16","20","24","28","32","40","48","64","96","128" }; fontProperties.Children.Add(sizeChoice);
        boldChoice = new ToggleButton { Content = "B", FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.Bold, FontSize = 16 };
        italicChoice = new ToggleButton { Content = "I", FontFamily = new FontFamily("Georgia"), FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, FontSize = 18 };
        foreach (var button in new[] { boldChoice,italicChoice }) { button.SetResourceReference(StyleProperty,"ToolbarToggle"); fontProperties.Children.Add(button); }
        VideoChrome.Hint(boldChoice,"粗体"); VideoChrome.Hint(italicChoice,"斜体");
        deleteTextChoice=VideoChrome.Icon("删除选中文字","M5,7 L19,7 M9,4 L15,4 M7,7 L8,21 L16,21 L17,7 M10,10 L10,18 M14,10 L14,18",annotations.DeleteSelected);fontProperties.Children.Add(deleteTextChoice);row.Children.Add(fontProperties);
        void ApplyTextStyle() {
            if (syncingProperties) return;
            if (!double.TryParse(sizeChoice.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out var size) || !double.IsFinite(size) || size < 6 || size > 512) { sizeChoice.Text = annotations.TextSize.ToString(CultureInfo.InvariantCulture); status.Text = "字号请输入 6–512 之间的数字"; return; }
            annotations.TextFont = fontChoice.SelectedItem as string ?? "Microsoft YaHei UI"; annotations.TextSize = size; annotations.Bold = boldChoice.IsChecked == true; annotations.Italic = italicChoice.IsChecked == true; annotations.ApplyStyle();
        }
        fontChoice.SelectionChanged += (_,_) => ApplyTextStyle(); sizeChoice.LostKeyboardFocus += (_,_) => ApplyTextStyle(); sizeChoice.PreviewKeyDown += (_,e) => { if (e.Key == Key.Enter) { ApplyTextStyle(); e.Handled = true; } };
        sizeChoice.SelectionChanged += (_,_) => { if (!syncingProperties && sizeChoice.SelectedItem is string value) { sizeChoice.Text = value; ApplyTextStyle(); } };
        boldChoice.Click += (_,_) => ApplyTextStyle(); italicChoice.Click += (_,_) => ApplyTextStyle();
        elementFields.Margin = new Thickness(0,0,0,6);elementFields.Height=30;elementFields.HorizontalAlignment=HorizontalAlignment.Right; elementFields.Children.Add(VideoChrome.Label("显示"));
        foreach (var (box,start) in new[] { (elementStart,true),(elementEnd,false) }) {
            box.Width = 92; box.Height = 30; box.MinHeight = 0; box.Margin = new Thickness(5,0,0,0); box.Padding = new Thickness(5,3,5,3); box.VerticalContentAlignment = VerticalAlignment.Center; VideoChrome.Hint(box,start ? "元素出现时间" : "元素结束时间");
            box.LostKeyboardFocus += (_,_) => CommitElementTime(box,start);
            box.PreviewKeyDown += (_,e) => { if (e.Key == Key.Enter) { CommitElementTime(box,start); e.Handled = true; } };
            if (!start) elementFields.Children.Add(VideoChrome.Label("—")); elementFields.Children.Add(box);
        }
        SyncAnnotationProperties(); annotationProperties = Theme.Panel(row,"PropertyMargin"); return annotationProperties;
    }
    void SyncAnnotationProperties()
    {
        if (fontChoice == null) return;
        syncingProperties = true;
        try {
            inkSample.Background = new SolidColorBrush(annotations.Ink); fontChoice.SelectedItem = annotations.TextFont; sizeChoice.Text = annotations.TextSize.ToString(CultureInfo.InvariantCulture); boldChoice.IsChecked = annotations.Bold; italicChoice.IsChecked = annotations.Italic; widthSlider.Value = annotations.StrokeWidth;deleteTextChoice.IsEnabled=annotations.Selected?.Text!=null;
            foreach(var (color,button) in videoSwatches)button.IsChecked=color==annotations.Ink;
            var text = annotations.IsEditingText || annotations.Selected?.Text != null || annotations.Tool == "文字"; fontProperties.Visibility = text ? Visibility.Visible : Visibility.Collapsed; strokeProperties.Visibility = text ? Visibility.Collapsed : Visibility.Visible;
        } finally { syncingProperties = false; }
    }
    void SelectVideoTool(string tool)
    {
        player.Pause();annotations.SelectTool(tool);
        foreach(var button in annotationKeys.Values){var selected=(button.ToolTip as string)?.StartsWith(tool+" ·",StringComparison.Ordinal)==true;if(button.IsChecked!=selected)button.IsChecked=selected;}
        SyncAnnotationProperties();
    }
    void UpdateElementFields()
    {
        elementFields.Visibility = annotations.Selected == null ? Visibility.Hidden : Visibility.Visible;
        if (annotations.Selected is not { } element) return;
        if (!elementStart.IsKeyboardFocusWithin) elementStart.Text = VideoRange.Format(element.Time.Start);
        if (!elementEnd.IsKeyboardFocusWithin) elementEnd.Text = VideoRange.Format(element.Time.End);
    }
    bool CommitElementTime(TextBox input,bool start)
    {
        if (saving || annotations.Selected is not { } element) return true;
        if (!VideoRange.TryParseTime(input.Text,out var value)) { status.Text = "请输入有效的元素显示时间"; return false; }
        player.Pause(); annotations.CommitText(); var time = start ? element.Time.ResizeStart(value) : element.Time.ResizeEnd(value,player.Duration); annotations.SetTime(element.Id,time); UpdateElementFields();
        input.Text = VideoRange.Format(start ? time.Start : time.End);
        return true;
    }
    bool CommitElementFields()
    {
        if (annotations.Selected is not { } element) return true;
        if (!VideoRange.TryParseTime(elementStart.Text,out var start) || !VideoRange.TryParseTime(elementEnd.Text,out var end)) { status.Text = "请输入有效的元素显示时间"; return false; }
        player.Pause(); annotations.SetTime(element.Id,element.Time.ResizeEnd(end,player.Duration).ResizeStart(start)); return true;
    }
    bool HandleAnnotationKey(KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox) {
            if (e.Key == Key.Escape && annotations.IsEditingText) { annotations.CommitText(); e.Handled = true; return true; }
            return false;
        }
        if (e.OriginalSource is ComboBox or ToggleButton || saving) return false;
        var mods = Keyboard.Modifiers;
        if (e.Key == Key.Escape && annotations.IsGesturing) annotations.CancelGesture();
        else if (e.Key == Key.Escape && annotations.SelectedId != null) annotations.ClearSelection();
        else if (e.Key == Key.Delete && annotations.SelectedId != null) annotations.DeleteSelected();
        else if (e.Key == Key.D && mods == ModifierKeys.Control) annotations.DuplicateSelected();
        else if (e.Key == Key.C && mods == ModifierKeys.Control) annotations.CopySelected();
        else if (e.Key == Key.V && mods == ModifierKeys.Control) annotations.Paste();
        else if(e.Key==Key.Q&&mods==ModifierKeys.None)SelectVideoTool("选择");
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down && annotations.SelectedId != null && !elementTrack.IsKeyboardFocusWithin && mods is ModifierKeys.None or ModifierKeys.Shift) { double step = mods == ModifierKeys.Shift ? 10 : 1; annotations.Nudge(new Vector(e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0,e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0)); }
        else if (e.Key == Key.F && mods == ModifierKeys.None || e.Key is Key.D0 or Key.NumPad0 && mods == ModifierKeys.Control) annotations.Fit();
        else if (mods == ModifierKeys.None && annotationKeys.TryGetValue(e.Key,out var button)) button.IsChecked = true;
        else return false;
        e.Handled = true; return true;
    }
}
