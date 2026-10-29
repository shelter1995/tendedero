namespace Liangtu;

// Material detail is painted behind controls, never into the user's image canvas.
internal sealed class MaterialPanel : Border
{
    internal DrawingGroup CaptureChrome()
    {
        // Border.OnRender and this panel's OnRender draw only their own chrome,
        // never child controls or the photograph. No live property is changed.
        var drawing=new DrawingGroup();using(var dc=drawing.Open()) OnRender(dc);
        var snapshot=drawing.CloneCurrentValue();if(snapshot.CanFreeze)snapshot.Freeze();return snapshot;
    }
    static readonly Brush linenGrain = CreateGrain();
    static Brush CreateGrain()
    {
        var group=new DrawingGroup(); var random=new Random(317);
        using(var dc=group.Open()) for(int i=0;i<160;i++) {
            var x=random.NextDouble()*64; var y=random.NextDouble()*64;
            dc.DrawLine(new Pen(i%3==0?Theme.Brush("#448A7158"):Theme.Brush("#88FFFFFF"),.35),new Point(x,y),new Point(x+.8+random.NextDouble()*1.6,y+.4));
        }
        var brush=new DrawingBrush(group) { TileMode=TileMode.Tile, ViewportUnits=BrushMappingMode.Absolute, Viewport=new Rect(0,0,64,64), Stretch=Stretch.None };
        brush.Freeze(); return brush;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var w=ActualWidth; var h=ActualHeight;
        if(w<4 || h<4 || SystemParameters.HighContrast) return;
        var p=Theme.Current;
        dc.PushClip(new RectangleGeometry(new Rect(1,1,w-2,h-2),CornerRadius.TopLeft,CornerRadius.TopLeft));
        if(p.Id=="linen") {
            // A caller may supply the generated seamless paper tile as an
            // ImageBrush resource; fallback grain remains deterministic.
            dc.PushOpacity(.22); dc.DrawRectangle(TryFindResource("LinenPaperTexture") as Brush ?? linenGrain,null,new Rect(1,1,w-2,h-2)); dc.Pop();
            dc.DrawRoundedRectangle(null,new Pen(Theme.Brush("#BFFFFFF9"),1),new Rect(1.5,1.5,w-3,h-3),Math.Max(0,CornerRadius.TopLeft-1),Math.Max(0,CornerRadius.TopLeft-1));
            if(w>20 && h>8) dc.DrawLine(new Pen(Theme.Brush("#22A17F5B"),.8),new(10,h-1.5),new(w-10,h-1.5));
        } else if(p.Id=="darkroom") {
            dc.DrawLine(new Pen(Theme.Token("FrameEdge"),.6),new(1,1),new(w-1,1));
            foreach(var x in new[]{5d,w-5}) dc.DrawEllipse(Theme.Muted,null,new(x,5),.8,.8);
        } else if(p.Id=="porcelain") {
            dc.DrawLine(new Pen(Theme.Token("BasketHighlight"),1),new(5,2),new(w-5,2));
            dc.DrawLine(new Pen(Theme.Token("Selection"),1),new(5,h-2),new(w-5,h-2));
        } else if(p.Id=="color-play") {
            if(TryFindResource("ColorPlaySurfaceTexture") is Brush material) { dc.PushOpacity(.09); dc.DrawRectangle(material,null,new Rect(1,1,w-2,h-2)); dc.Pop(); }
            dc.DrawRoundedRectangle(null,new Pen(Theme.Brush("#F2FFFFFF"),1),new Rect(1.5,1.5,w-3,h-3),Math.Max(0,CornerRadius.TopLeft-1),Math.Max(0,CornerRadius.TopLeft-1));
            if(w>24 && h>8) dc.DrawLine(new Pen(Theme.Brush("#203C526C"),.65),new(12,h-1.5),new(w-12,h-1.5));
        }
        dc.Pop();
    }
}
