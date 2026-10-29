using System.Windows.Data;
namespace Liangtu;

// One 24-unit optical grid for every image/video toolbar. Geometry keeps its
// own proportions instead of stretching each path to fill the button.
internal sealed class ToolGlyph : FrameworkElement
{
    public static readonly DependencyProperty InkProperty = DependencyProperty.Register(nameof(Ink), typeof(Brush), typeof(ToolGlyph), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush Ink { get => (Brush)GetValue(InkProperty); set => SetValue(InkProperty, value); }
    public string Symbol { get; }
    static readonly Dictionary<string, Geometry> paths = new();
    public ToolGlyph(string symbol)
    {
        Symbol = symbol; Width = Height = 18; IsHitTestVisible = false;
        HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Center;
        SetBinding(InkProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) });
    }
    static Geometry Path(string data)
    {
        if (!paths.TryGetValue(data, out var path)) { path = Geometry.Parse(data); path.Freeze(); paths.Add(data, path); } return path;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); if (ActualWidth <= 0 || ActualHeight <= 0) return;
        dc.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        var line = new Pen(Ink, 1.65) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        void Draw(string data) => dc.DrawGeometry(null, line, Path(data));
        void Pencil()
        {
            Draw("M4,16 L16,4 Q17,3 18,4 L20,6 Q21,7 20,8 L8,20 L3,21 Z M14,6 L18,10 M4,16 L8,20");
        }
        switch (Symbol)
        {
            case "@basket": Draw("M3,9 L21,9 L19,20 L5,20 Z M8,9 L10,4 M16,9 L14,4 M9,13 L9,16 M15,13 L15,16"); break;
            case "@pin": Draw("M9,3 L17,3 M10,3 L10,9 L7,13 L7,15 L19,15 L19,13 L16,9 L16,3 M13,15 L13,22"); break;
            case "@copy": Draw("M8,8 L20,8 L20,21 L8,21 Z M16,5 L16,3 L3,3 L3,16 L5,16"); break;
            case "@pencil": Pencil(); break;
            case "@annotate":
                Draw("M17,5 L4,5 Q3,5 3,6 L3,19 Q3,20 4,20 L12,20 M5,15 L8,11 L11,14");
                Draw("M12,16 L19,9 L22,12 L15,19 L11,20 Z M17,11 L20,14"); break;
            case "@scissors":
                Draw("M8.5,9 L20,20 M8.5,15 L20,4");
                dc.DrawEllipse(null,line,new Point(6,7),3,3); dc.DrawEllipse(null,line,new Point(6,17),3,3); break;
            case "@crop":
                Draw("M7,3 L7,17 Q7,18 8,18 L21,18 M3,7 L17,7 Q18,7 18,8 L18,21");
                break;
            case "@marker":
                Draw("M6,15 L14,4 Q15,3 16,4 L20,7 Q21,8 20,9 L12,19 Z");
                Draw("M6,15 L12,19 L9,21 L3,21 Z M12,7 L18,11"); break;
            case "@eraser":
                Draw("M3,14 L12,4 Q13,3 14,4 L21,10 Q22,11 21,12 L13,21 L9,21 Z M8,9 L16,16 M13,21 L21,21"); break;
            case "@save":
                Draw("M5,3 L17,3 L21,7 L21,19 Q21,21 19,21 L5,21 Q3,21 3,19 L3,5 Q3,3 5,3 M7,3 L7,9 L16,9 L16,3 M7,21 L7,15 L17,15 L17,21 M14,5 L14,7"); break;
            case "@hand":
                Draw("M7,13 L7,7 Q7,4 9,5 L9,11 L9,4 Q11,2 12,5 L12,11 L12,5 Q14,3 15,6 L15,12 L15,8 Q18,6 18,9 L18,15 Q18,20 14,22 L8,22 L3,15 Q2,12 4,12 Z"); break;
            default: Draw(Symbol); break;
        }
        dc.Pop();
    }
}

internal static class ToolbarVisual
{
    public const string Undo = "M8,5 L3,10 L8,15 M3,10 L14,10 C19,10 21,13 21,18";
    public const string Redo = "M16,5 L21,10 L16,15 M21,10 L10,10 C5,10 3,13 3,18";
    public static FrameworkElement Glyph(string symbol) => new ToolGlyph(symbol);
    public static Button Button(string name, string symbol, Action action)
    {
        var b = Theme.Button("", action); b.Content = Glyph(symbol); b.Width = b.Height = 32; b.MinHeight = 32;
        b.Padding = new Thickness(6); b.Margin = new Thickness(2,0,2,0); b.SetResourceReference(FrameworkElement.StyleProperty,"ToolbarButton"); Hint(b,name); return b;
    }
    public static void Hint(FrameworkElement element, string name)
    {
        element.ToolTip = name; System.Windows.Automation.AutomationProperties.SetName(element,name);
        ToolTipService.SetInitialShowDelay(element,350); ToolTipService.SetShowDuration(element,10000); ToolTipService.SetShowOnDisabled(element,true);
    }
    public static Border Divider() => new() { Width=1, Height=18, Background=Theme.Outline, Margin=new Thickness(7,0,7,0), VerticalAlignment=VerticalAlignment.Center };
}
