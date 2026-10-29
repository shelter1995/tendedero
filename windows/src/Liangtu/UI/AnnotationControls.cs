namespace Liangtu;

// Shared native attributes and text editing chrome for image and video documents.
internal static class AnnotationControls
{
    public static readonly (string Name,Key Key,string Geometry)[] DrawingTools = {
        ("矩形",Key.R,"M3,5 L21,5 L21,19 L3,19 Z"),("圆形",Key.O,"M12,3 A9,9 0 1 1 12,21 A9,9 0 1 1 12,3"),
        ("直线",Key.L,"M4,20 L20,4"),("箭头",Key.A,"M4,20 L20,4 M9,4 L20,4 L20,15"),("画笔",Key.B,"@pencil"),("荧光笔",Key.H,"@marker"),
        ("文字",Key.T,"M3,5 L21,5 M12,5 L12,21 M7,21 L17,21"),("裁剪",Key.C,"@crop"),("橡皮擦",Key.E,"@eraser"),("平移",Key.V,"@hand")};
    public static bool IsAttributeInput(DependencyObject? source)
    {
        for(var node=source;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):null)
            if(node is ComboBox or System.Windows.Controls.Primitives.RangeBase || node is System.Windows.Controls.Primitives.ToggleButton and not RadioButton)return true;
        return false;
    }
    public static bool IsRangeInput(DependencyObject? source)
    {
        for(var node=source;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):null)if(node is System.Windows.Controls.Primitives.RangeBase)return true;
        return false;
    }
    public static void ToolFace(Control button,StackPanel face,FrameworkElement glyph,TextBlock label,bool command,double width,bool save=false)
    {
        var vertical=Theme.Current.Id=="color-play";var linen=Theme.Current.Id=="linen";var labeled=vertical||linen;
        button.Width=vertical?(command?43:88):linen?(width>920?52:44):32;button.Height=linen?52:vertical?(command?36:34):32;
        button.Padding=new Thickness(labeled?3:6);button.Margin=vertical?new Thickness(1):new Thickness(2,0,2,0);
        face.Orientation=vertical?Orientation.Horizontal:Orientation.Vertical;glyph.Width=glyph.Height=labeled?20:18;
        label.Visibility=labeled&&!(vertical&&command)?Visibility.Visible:Visibility.Collapsed;label.Margin=vertical?new Thickness(8,0,0,0):new Thickness(0,3,0,0);label.Width=vertical?42:double.NaN;label.TextAlignment=TextAlignment.Center;
        if(save){button.SetResourceReference(Control.ForegroundProperty,labeled?"OnAccent":"Ink");button.SetResourceReference(Control.BackgroundProperty,labeled?"Accent":"ToolRestSurface");button.SetResourceReference(Control.BorderBrushProperty,labeled?"Accent":"ToolRestOutline");}
    }
    public static void ToolPanels(Border bar,ScrollViewer scroll,WrapPanel tools,IEnumerable<Border> dividers,FrameworkElement? properties,Border? mat)
    {
        var vertical=Theme.Current.Id=="color-play";var linen=Theme.Current.Id=="linen";
        if(vertical||linen)bar.Margin=vertical?new Thickness(12,0,8,10):new Thickness(12,10,12,3);else bar.SetResourceReference(FrameworkElement.MarginProperty,"ToolbarMargin");
        bar.Width=vertical?124:double.NaN;scroll.VerticalScrollBarVisibility=vertical?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;
        tools.Margin=vertical?new Thickness(5,7,5,7):linen?new Thickness(7,5,7,5):new Thickness(10,8,10,8);tools.Width=vertical?94:double.NaN;
        foreach(var divider in dividers){divider.Width=vertical?80:1;divider.Height=vertical?1:20;divider.Margin=vertical?new Thickness(6,7,6,7):new Thickness(6,0,6,0);}
        if(properties!=null){if(vertical||linen)properties.Margin=vertical?new Thickness(12,10,12,8):new Thickness(12,3,12,8);else properties.SetResourceReference(FrameworkElement.MarginProperty,"PropertyMargin");}
        if(mat!=null){mat.Margin=vertical?new Thickness(0,0,12,10):linen?new Thickness(12,0,12,8):new Thickness(0);mat.Padding=new Thickness(vertical||linen?10:0);mat.BorderThickness=new Thickness(vertical||linen?1:0);mat.CornerRadius=new CornerRadius(vertical?14:linen?5:0);}
    }
    public static readonly string[] Colors = { "#E24D40", "#F09B32", "#F3CC46", "#218368", "#267BDC", "#8B5FC7", "#202624", "#FFFFFF" };
    public static Button Palette(Border sample,Action choose)
    {
        var button=Theme.Button("",choose); var face=new StackPanel{Orientation=Orientation.Horizontal};face.Children.Add(sample);
        var arrow=ToolbarVisual.Glyph("M2,7 L12,18 L22,7");arrow.Width=arrow.Height=10;arrow.Margin=new Thickness(6,0,1,0);face.Children.Add(arrow);
        button.Content=face;button.Padding=new Thickness(5);button.Margin=new Thickness(2,0,10,0);button.Width=48;button.Height=34;ToolbarVisual.Hint(button,"更多颜色… · Windows 调色盘 / RGB / 自定义色槽");return button;
    }
    public static RadioButton Swatch(string hex,string group,bool selected)
    {
        var button=new RadioButton {GroupName=group,Padding=new Thickness(4),Margin=new Thickness(1,0,1,0),VerticalAlignment=VerticalAlignment.Center,IsChecked=selected,
            Content=new Border{Width=16,Height=16,Background=new SolidColorBrush(AnnotationColors.Parse(hex)),CornerRadius=new CornerRadius(8),BorderBrush=Theme.Outline,BorderThickness=new Thickness(1)}};
        ToolbarVisual.Hint(button,"颜色 "+hex);return button;
    }
    public static StackPanel Stroke(double initial,Action<double> changed,out Slider slider)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        row.Children.Add(new TextBlock{Text="笔宽",Foreground=Theme.Muted,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,4,0)});
        var label=new TextBlock{Text=$"{initial:0} px",Width=42,Foreground=Theme.Muted,VerticalAlignment=VerticalAlignment.Center};
        var value=new Slider{Minimum=1,Maximum=24,Value=initial,TickFrequency=1,IsSnapToTickEnabled=true,Width=104,Margin=new Thickness(8,0,10,0),VerticalAlignment=VerticalAlignment.Center};
        ToolbarVisual.Hint(value,"笔宽 · 图像像素");value.ValueChanged+=(_,_)=>{label.Text=$"{value.Value:0} px";changed(value.Value);};row.Children.Add(value);row.Children.Add(label);slider=value;return row;
    }
}

internal sealed class AnnotationTextEditor
{
    static readonly DependencyProperty CompositionActiveProperty=DependencyProperty.RegisterAttached("CompositionActive",typeof(bool),typeof(AnnotationTextEditor),new PropertyMetadata(false));
    public static bool HasComposition(DependencyObject? source)
    {
        for(var node=source;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):null)
            if(node is TextBox)return (bool)node.GetValue(CompositionActiveProperty);
        return false;
    }
    public TextSelectionFrame Frame { get; }
    public TextBox Input { get; }
    public Border Handle { get; }
    public static TextAnnotation ClampPosition(TextAnnotation text,Point position,Size size)
    {
        var label=text.CreateLabel();label.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));var bounds=new RotateTransform(text.Angle).TransformBounds(new Rect(label.DesiredSize));
        return text with{X=Math.Clamp(position.X,8-bounds.Right,size.Width-8-bounds.Left),Y=Math.Clamp(position.Y,8-bounds.Bottom,size.Height-8-bounds.Top)};
    }
    public AnnotationTextEditor(TextAnnotation text,string accessibleName)
    {
        Input=new TextBox{Style=null,Text=text.Text,AcceptsReturn=true,AcceptsTab=false,TextWrapping=TextWrapping.NoWrap,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),MinWidth=64,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden};
        System.Windows.Automation.AutomationProperties.SetName(Input,accessibleName);TextOptions.SetTextFormattingMode(Input,TextFormattingMode.Ideal);
        InputMethod.SetIsInputMethodEnabled(Input,true);
        Input.AddHandler(TextCompositionManager.PreviewTextInputStartEvent,new TextCompositionEventHandler((_,_)=>Input.SetValue(CompositionActiveProperty,true)),true);
        Input.AddHandler(TextCompositionManager.PreviewTextInputUpdateEvent,new TextCompositionEventHandler((_,_)=>Input.SetValue(CompositionActiveProperty,true)),true);
        Input.AddHandler(TextCompositionManager.PreviewTextInputEvent,new TextCompositionEventHandler((_,_)=>Input.SetValue(CompositionActiveProperty,false)),true);
        Frame=new TextSelectionFrame{Child=Input,Background=Brushes.Transparent,BorderThickness=new Thickness(1),Padding=new Thickness(3),UseLayoutRounding=false,SnapsToDevicePixels=false};
        Handle=new Border{Width=20,Height=22,Background=Theme.ControlSurface,BorderBrush=Theme.Accent,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3),Cursor=Cursors.SizeAll,ToolTip="拖动调整文字位置",Child=new TextBlock{Text="✥",Foreground=Theme.Ink,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
    }
    public static void Apply(TextSelectionFrame frame,TextBox input,Border handle,TextAnnotation text,double zoom,Size size,bool editing)
    {
        frame.IsHitTestVisible=editing;frame.BorderBrush=editing?Theme.Accent:Brushes.Transparent;frame.RenderTransform=new RotateTransform(text.Angle);Canvas.SetLeft(frame,text.X);Canvas.SetTop(frame,text.Y);frame.Zoom=zoom;frame.InvalidateVisual();
        handle.RenderTransform=new ScaleTransform(1/zoom,1/zoom);handle.Visibility=editing?Visibility.Visible:Visibility.Hidden;
        input.IsReadOnly=!editing;input.IsHitTestVisible=editing;input.FontFamily=new FontFamily(text.FontFamily);input.FontSize=text.FontSize;input.FontWeight=text.Bold?FontWeights.Bold:FontWeights.Normal;input.FontStyle=text.Italic?FontStyles.Italic:FontStyles.Normal;input.Foreground=new SolidColorBrush(text.Color);input.CaretBrush=new SolidColorBrush(text.Color);TextOptions.SetTextRenderingMode(input,TextRenderingMode.Grayscale);
        var label=text.CreateLabel();label.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));var bounds=new RotateTransform(text.Angle).TransformBounds(new Rect(label.DesiredSize));
        Canvas.SetLeft(handle,Math.Clamp(text.X+bounds.Left-22/zoom,0,Math.Max(0,size.Width-20/zoom)));Canvas.SetTop(handle,Math.Clamp(text.Y+bounds.Top,0,Math.Max(0,size.Height-22/zoom)));
    }
}
