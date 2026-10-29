using System.Windows.Media.Animation;
namespace Liangtu;

internal sealed class BasketVisual : FrameworkElement
{
    public ThemeProfile? Palette { get; init; }
    Brush Ink(string key) => ThemeArtwork.Color(Palette ?? Theme.Current, key);
    public bool Compact { get; init; }
    readonly RotateTransform rotation = new(0,32,4);
    DrawingGroup? artwork;
    string? artworkTheme;
    internal int ArtworkBuildCount { get; private set; }
    static void MotionChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e) {
        var basket=(BasketVisual)obj; basket.rotation.Angle=basket.Angle;
    }
    public static readonly DependencyProperty LoadProperty = DependencyProperty.Register(nameof(Load), typeof(double), typeof(BasketVisual), new FrameworkPropertyMetadata(0d, MotionChanged));
    public static readonly DependencyProperty SwayProperty = DependencyProperty.Register(nameof(Sway), typeof(double), typeof(BasketVisual), new FrameworkPropertyMetadata(0d, MotionChanged));
    public double Load { get => (double)GetValue(LoadProperty); set => SetValue(LoadProperty, value); }
    public double Sway { get => (double)GetValue(SwayProperty); set => SetValue(SwayProperty, value); }
    double Angle => Sway + 6 * Math.Sin(Load * Math.PI * 2.2) * Math.Exp(-3 * Load);
    public BasketVisual() { IsHitTestVisible = false; Width = 72; Height = 86; }
    internal void UseShelfSize(ThemeProfile profile) { Width=profile.BasketArtWidth; Height=profile.BasketArtHeight; }
    internal Rect NormalizedMouth => RasterThemeArt.Has(Palette ?? Theme.Current) ? RasterThemeArt.BasketMouth(Palette ?? Theme.Current) : new Rect(17,31,30,24);
    public Rect MouthBounds {
        get { var transform = new TransformGroup { Children = { new RotateTransform(Angle, 32, 4), new ScaleTransform(ActualWidth / 64, ActualHeight / 76) } }; return transform.TransformBounds(NormalizedMouth); }
    }
    public void Receive()
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        BeginAnimation(LoadProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(.85)) { FillBehavior = FillBehavior.Stop });
    }
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(ActualWidth / 64, ActualHeight / 76));
        dc.PushTransform(rotation);
        var profile=Palette ?? Theme.Current;
        if(artwork==null || artworkTheme!=profile.Id) {
            artwork=new DrawingGroup(); using(var art=artwork.Open()) DrawArtwork(art);
            if(artwork.CanFreeze) artwork.Freeze(); artworkTheme=profile.Id; ArtworkBuildCount++;
        }
        dc.DrawDrawing(artwork); dc.Pop(); dc.Pop();
    }
    void DrawArtwork(DrawingContext dc)
    {
        if ((Palette ?? Theme.Current).OriginalArtwork) { ThemeArtwork.Basket(dc, Palette ?? Theme.Current, Compact); return; }
        var outline = new Pen(Ink("BasketEdge"), Compact ? 3 : 1.3) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawGeometry(null, new Pen(Ink("BasketStrap"), Compact ? 3 : 2), Geometry.Parse("M9,35 C13,17 27,13 32,4 C37,13 51,17 55,35"));
        dc.DrawEllipse(Ink("BasketHighlight"), outline, new Point(32,4), 3,3);
        dc.DrawEllipse(Ink("BasketInside"), outline, new Point(32,35), 27,7);
        var material = (Palette ?? Theme.Current).Material;
        var body = Geometry.Parse(material == "canvas" ? "M6,35 Q32,42 58,35 L56,60 Q54,72 43,72 L21,72 Q10,72 8,60 Z" : material == "metal" ? "M5,35 Q32,43 59,35 L55,65 Q55,68 51,68 L13,68 Q9,68 9,65 Z" : "M5,35 Q32,44 59,35 L54,64 Q53,68 48,69 L16,69 Q11,68 10,64 Z");
        dc.DrawGeometry(Ink("BasketFill"), outline, body);
        if (!Compact && material == "woven") {
            dc.PushClip(body);
            for (int y = 45; y < 69; y += 5) dc.DrawGeometry(null, new Pen(Ink("BasketWeave"), .8), Geometry.Parse($"M8,{y} Q32,{y+5} 57,{y}"));
            for (int x = 17; x < 53; x += 8) dc.DrawLine(new Pen(Ink("BasketHighlight"), 1.7), new Point(x - 2, 40), new Point(x, 69));
            dc.Pop();
        }
        if (!Compact && material == "canvas") {
            dc.DrawGeometry(null, new Pen(Ink("BasketEdge"), .65) { DashStyle = new DashStyle(new[] { 2d, 2d }, 0) }, Geometry.Parse("M12,42 L15,59 Q17,66 24,66 L40,66 Q47,66 49,59 L52,42"));
            dc.DrawRoundedRectangle(Ink("BasketTrim"), null, new Rect(25,31,14,7), 2, 2);
        }
        if (!Compact && material == "metal") {
            for (int x = 18; x <= 46; x += 7) dc.DrawRoundedRectangle(Ink("BasketInside"), null, new Rect(x,45,2.2,13), 1, 1);
            dc.DrawLine(new Pen(Ink("BasketHighlight"), .8), new Point(13,64), new Point(51,64));
        }
        dc.DrawGeometry(null, new Pen(Ink("BasketTrim"), Compact ? 4 : 3.5), Geometry.Parse("M5,35 Q32,45 59,35"));
        dc.DrawGeometry(null, new Pen(Ink("BasketHighlight"), 1.2), Geometry.Parse("M7,34 Q32,42 57,34"));
        if (material != "metal") {
            dc.DrawRoundedRectangle(Ink("BasketHighlight"), new Pen(Ink("BasketEdge"), .6), new Rect(25,51,14,11),2,2);
            dc.DrawGeometry(null,new Pen(Ink("BasketTrim"),1.1),Geometry.Parse("M28,59 L31,55 L34,58 L36,56 M28,54 L36,54"));
        }
    }
}

internal sealed class ClothesPegVisual : FrameworkElement
{
    public ThemeProfile? Palette { get; init; }
    public int Variant { get; set; }
    Brush Ink(string key) => ThemeArtwork.Color(Palette ?? Theme.Current, key);
    public static readonly DependencyProperty OpeningProperty = DependencyProperty.Register(nameof(Opening), typeof(double), typeof(ClothesPegVisual), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Opening { get => (double)GetValue(OpeningProperty); set => SetValue(OpeningProperty, value); }
    public ClothesPegVisual() { Width = 26; Height = 40; IsHitTestVisible = false; }
    public void Open(bool open, double duration = .18) => BeginAnimation(OpeningProperty, new DoubleAnimation(open ? 1 : 0, TimeSpan.FromSeconds(SystemParameters.ClientAreaAnimation ? duration : 0)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(ActualWidth / 26, ActualHeight / 40));
        if ((Palette ?? Theme.Current).OriginalArtwork) { ThemeArtwork.Peg(dc, Palette ?? Theme.Current, Opening, Variant); dc.Pop(); return; }
        dc.PushTransform(new RotateTransform(12,13,18));
        // Two overlapping blades viewed obliquely: front/rear, not pincers.
        dc.PushTransform(new RotateTransform(-3 * Opening,18,19));
        dc.DrawGeometry(Ink("PegSide"),null,Geometry.Parse("M14,3 L22,5 Q24,6 23,9 L20,35 L13,37 Z"));
        dc.DrawRoundedRectangle(Ink("PegBack"),new Pen(Ink("PegEdge"),.6),new Rect(10,2,10,34),3,3); dc.Pop();
        dc.PushTransform(new RotateTransform(11 * Opening,18,19));
        dc.DrawRoundedRectangle(Ink("PegFace"),new Pen(Ink("PegEdge"),.65),new Rect(5,3,12,35),3,3);
        dc.DrawLine(new Pen(Ink("PegHighlight"),1),new Point(8,6),new Point(14,6));
        dc.DrawLine(new Pen(Ink("PegEdge"),.8),new Point(6,24),new Point(16,24)); dc.Pop();
        if ((Palette ?? Theme.Current).Material == "woven") dc.DrawGeometry(null,new Pen(Ink("PegEdge"),.35),Geometry.Parse("M8,8 Q10,15 9,21 M11,27 L10,34 M14,8 L13,14"));
        dc.DrawEllipse(Theme.Brush("#EDF0F1"),new Pen(Theme.Brush("#5D6D78"),.9),new Point(18,18),2.4,3.2);
        dc.DrawEllipse(Theme.Brush("#687681"),null,new Point(18,18),.9,1.7);
        dc.DrawGeometry(null,new Pen(Theme.Brush("#E8ECEE"),1.6){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},Geometry.Parse("M18,21 L15,24 L6,24"));
        dc.Pop(); dc.Pop();
    }
}

internal sealed class PegHold
{
    double? began;
    public bool Active => began != null;
    public void Begin(double now) => began = now;
    public void Cancel() => began = null;
    public void Move(double distance) { if (distance > 6) Cancel(); }
    public bool Complete(double now) { if (began is not double start || now - start < .45) return false; began = null; return true; }
}
