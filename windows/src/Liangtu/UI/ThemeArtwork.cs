namespace Liangtu;

// Original vector artwork, designed in normalized shelf coordinates. No reference
// application's bitmap assets are embedded. The mouth and suspension point retain
// the shelf animation's established coordinates (32,35) and (32,4).
internal static class ThemeArtwork
{
    static readonly Dictionary<string,Geometry> geometries=new();
    static readonly Dictionary<string,Brush> colors=new();
    static readonly Dictionary<string,LinearGradientBrush> gradients=new();
    internal static Brush Color(ThemeProfile p,string key) {
        var id=p.Id+":"+key; if(!colors.TryGetValue(id,out var b)) { var value=new SolidColorBrush(p.Color(key)); value.Freeze(); colors.Add(id,b=value); } return b;
    }
    static Pen P(Brush brush, double width) => new(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
    static void Path(DrawingContext dc, Brush? fill, Pen? pen, string path) {
        if(!geometries.TryGetValue(path,out var geometry)) { geometry=Geometry.Parse(path); geometry.Freeze(); if(geometries.Count<512) geometries.Add(path,geometry); }
        dc.DrawGeometry(fill,pen,geometry);
    }
    static void InvariantPath(DrawingContext dc, Brush? fill, Pen? pen, FormattableString path) => Path(dc,fill,pen,FormattableString.Invariant(path));
    static LinearGradientBrush Glaze(ThemeProfile p, string top, string bottom, double angle = 90) {
        var key=p.Id+":"+top+":"+bottom+":"+angle; if(!gradients.TryGetValue(key,out var brush)) { brush=new LinearGradientBrush(p.Color(top),p.Color(bottom),angle); brush.Freeze(); gradients.Add(key,brush); } return brush;
    }
    public static void Basket(DrawingContext dc, ThemeProfile p, bool compact)
    {
        if(RasterThemeArt.Has(p)) { RasterThemeArt.Basket(dc,p); return; }
        Brush B(string k) => Color(p,k);
        var edge = P(B("BasketEdge"), compact ? 1.5 : .8);
        switch (p.Id) {
            case "porcelain":
                // A bent steel hook and two narrow back brackets support the caddy.
                Path(dc, null, P(B("BasketStrap"), 1.8), "M33,18 L33,10 C33,6 27,7 27,3 C27,-1 33,-1 34,2 M33,15 L17,30 M33,15 L48,30");
                dc.DrawRoundedRectangle(Glaze(p,"BasketHighlight","BasketTrim"), edge, new Rect(6,28,52,40), 7,7);
                dc.DrawRoundedRectangle(B("BasketInside"), null, new Rect(9,30,46,12), 4,4);
                Photos(dc,p,compact);
                Path(dc, Glaze(p,"BasketHighlight","BasketFill"), edge, "M6,36 Q32,41 58,36 L57,60 Q57,69 49,69 L15,69 Q7,69 7,60 Z");
                Path(dc,null,P(B("BasketTrim"),2.4),"M7,36 Q32,42 57,36");
                Path(dc,null,P(B("BasketHighlight"),1),"M8,35 Q32,40 56,35 M10,44 L11,59 Q11,64 15,65");
                if (!compact) {
                    dc.DrawRoundedRectangle(B("BasketTrim"),null,new Rect(24,51,16,9),2,2);
                    Path(dc,null,P(B("BasketHighlight"),.8),"M28,55 L36,55 M30,57 L34,57");
                    dc.DrawLine(P(B("BasketEdge"),.5),new(16,65),new(48,65));
                }
                break;
            case "darkroom":
                Path(dc,null,P(B("BasketStrap"),1.8),"M29,7 L29,3 C29,-1 35,-1 35,3 L35,9 M32,7 L32,22 M32,19 L10,31 M32,19 L54,31");
                dc.DrawRoundedRectangle(B("BasketTrim"),edge,new Rect(28,17,8,6),1,1);
                Path(dc,B("BasketInside"),edge,"M5,31 L53,28 L60,35 L10,40 Z");
                Photos(dc,p,compact);
                Path(dc,Glaze(p,"BasketTrim","BasketFill"),edge,"M5,35 L55,35 L54,66 L8,66 Z");
                Path(dc,B("BasketEdge"),null,"M55,35 L60,31 L58,61 L54,66 Z");
                dc.DrawRectangle(B("BasketInside"),null,new Rect(11,43,38,17));
                if (!compact) for(int y=46;y<59;y+=4) for(int x=14;x<48;x+=5) dc.DrawEllipse(B("BasketWeave"),null,new Point(x+(y%8==2?1.5:0),y),.85,.85);
                Path(dc,null,P(B("BasketHighlight"),.65),"M8,37 L53,37 M10,63 L51,63");
                dc.DrawRoundedRectangle(B("Accent"),null,new Rect(23,38,17,3),.5,.5);
                foreach (var x in new[]{9d,51d}) { dc.DrawEllipse(B("BasketHighlight"),null,new(x,40),.9,.9); dc.DrawEllipse(B("BasketHighlight"),null,new(x,62),.9,.9); }
                break;
            case "linen":
                // Broad leather suspension with visible stitching, then the rear lip.
                Path(dc,B("BasketStrap"),edge,"M27,5 Q29,0 34,2 L43,32 L35,35 Z M29,4 L20,32 L13,30 L25,5 Z");
                if(!compact) Path(dc,null,P(B("BasketHighlight"),.5),"M29,8 L36,29 M26,9 L19,28");
                dc.DrawEllipse(B("BasketTrim"),edge,new(32,34),28,10);
                dc.DrawEllipse(B("BasketInside"),null,new(32,34),24.5,7);
                Photos(dc,p,compact);
                var wicker=Geometry.Parse("M4,35 Q32,48 60,35 L56,56 Q53,68 43,70 Q32,73 21,70 Q11,68 8,57 Z");
                dc.DrawGeometry(Glaze(p,"BasketFill","BasketTrim"),edge,wicker);
                if(!compact) {
                    dc.PushClip(wicker);
                    // Separate curved stakes and staggered over-under bands produce real weave.
                    for(int x=9;x<60;x+=6) InvariantPath(dc,null,P(B("BasketWeave"),2.1),$"M{x},36 Q{x-2},56 {x+2},72");
                    for(int y=43,row=0;y<71;y+=5,row++) for(int x=-1;x<62;x+=10) {
                        var start=x+(row%2)*5;
                        InvariantPath(dc,null,P(B("BasketWeave"),3.2),$"M{start},{y} Q{start+5},{y+2} {start+9},{y+1}");
                        InvariantPath(dc,null,P(B("BasketHighlight"),1.8),$"M{start},{y-.7} Q{start+5},{y+1.3} {start+9},{y+.3}");
                    }
                    dc.Pop();
                }
                Path(dc,null,P(B("BasketEdge"),4.4),"M5,35 Q32,48 59,35");
                Path(dc,null,P(B("BasketHighlight"),2.7),"M5,34 Q32,47 59,34");
                if(!compact) for(int x=8;x<59;x+=4) { var y=34+12*(1-Math.Pow((x-32)/27d,2))/2; dc.DrawLine(P(B("BasketTrim"),.8),new(x,y-1.5),new(x+1.5,y+1.8)); }
                dc.DrawRoundedRectangle(B("FrameFill"),P(B("BasketEdge"),.5),new Rect(23,53,19,12),1,1);
                Path(dc,null,P(B("BasketStrap"),.7),"M27,57 L38,57 M29,60 L36,60");
                dc.DrawEllipse(B("BasketHighlight"),edge,new(30,5),2.3,2.3);
                break;
            case "color-play":
                // Twin soft loops merge into the bucket, unlike the single-hook caddy.
                Path(dc,null,P(B("BasketStrap"),5),"M13,31 L23,9 Q27,1 31,3 M51,31 L41,9 Q37,1 33,3");
                Path(dc,null,P(B("BasketHighlight"),1),"M12,27 L23,6 M51,27 L41,6");
                dc.DrawEllipse(B("BasketTrim"),edge,new(32,34),28,11);
                dc.DrawEllipse(B("BasketInside"),null,new(32,35),24,8);
                Photos(dc,p,compact);
                Path(dc,Glaze(p,"BasketTrim","BasketFill",110),edge,"M4,35 Q32,50 60,35 L56,57 Q54,70 43,72 Q32,75 21,72 Q10,70 8,57 Z");
                Path(dc,null,P(B("BasketTrim"),4),"M5,35 Q32,49 59,35");
                Path(dc,null,P(B("BasketHighlight"),.85),"M7,35 Q32,47 57,35 M10,46 Q10,63 20,67");
                foreach(var x in new[]{13d,51d}) { dc.DrawEllipse(B("BasketEdge"),null,new(x,38),2.4,3); dc.DrawEllipse(B("BasketStrap"),null,new(x,37),1.6,2.2); }
                if(!compact) { dc.DrawRoundedRectangle(B("BasketHighlight"),null,new Rect(26,53,12,9),3,3); Path(dc,null,P(B("BasketEdge"),1),"M29,57 L31,59 L35,55"); }
                break;
        }
    }
    static void Photos(DrawingContext dc, ThemeProfile p, bool compact)
    {
        if(compact) return;
        Brush B(string k)=>Color(p,k);
        foreach(var (x,y,angle,key) in new[]{(12d,25d,-11d,"Selection"),(29d,23d,9d,"CanvasSurface")}) {
            dc.PushTransform(new RotateTransform(angle,x+10,y+12));
            dc.DrawRoundedRectangle(B("FrameFill"),P(B("FrameEdge"),.6),new Rect(x,y,23,23),1,1);
            dc.DrawRectangle(B(key),null,new Rect(x+2,y+2,19,15));
            InvariantPath(dc,B("Accent"),null,$"M{x+3},{y+15} L{x+9},{y+7} L{x+13},{y+11} L{x+17},{y+8} L{x+20},{y+15} Z");
            dc.Pop();
        }
    }
    public static void Peg(DrawingContext dc, ThemeProfile p, double opening, int variant = 0)
    {
        if(RasterThemeArt.Has(p)) { RasterThemeArt.Peg(dc,p,opening,variant); return; }
        Brush B(string k)=>p.Id=="color-play" && k=="PegFace" ? PlayColor(variant) : Color(p,k);
        var round=p.Id=="color-play"?4:p.Id=="porcelain"?1.3:.65;
        // Rear jaw sits to the right and behind the front; both pivot around the
        // same spring. The jaw gap opens below the coil, never sideways pincers.
        dc.PushTransform(new RotateTransform(p.Id=="linen"?7:3,13,18));
        dc.PushTransform(new RotateTransform(-8*opening,16,20));
        Path(dc,B("PegSide"),null,"M13,3 L20,5 L20,36 L14,38 L12,33 Z");
        dc.DrawRoundedRectangle(Glaze(p,"PegHighlight","PegBack",0),P(B("PegEdge"),.6),new Rect(10,2,9,34),round,round);
        dc.Pop();
        dc.PushTransform(new RotateTransform(10*opening,16,20));
        Path(dc,B("PegSide"),null,"M14,4 L18,6 L18,35 L14,38 L12,33 Z");
        dc.DrawRoundedRectangle(p.Id=="color-play"?B("PegFace"):Glaze(p,"PegHighlight","PegFace",0),P(B("PegEdge"),.65),new Rect(5,3,10,35),round,round);
        if(p.Id=="linen") {
            Path(dc,null,P(B("PegEdge"),.35),"M8,7 Q6,16 9,20 M11,5 Q13,14 11,18 M8,28 L9,35 M12,27 L12,34");
            dc.DrawLine(P(B("PegEdge"),.7),new(5.5,25),new(14.5,25));
        } else if(p.Id=="darkroom") {
            dc.DrawRoundedRectangle(B("PegBack"),null,new Rect(8,6,4,10),.5,.5);
            for(int y=28;y<35;y+=2) dc.DrawLine(P(B("PegEdge"),.6),new(7,y),new(13,y));
            dc.DrawRectangle(B("Accent"),null,new Rect(5,22,10,2));
        } else if(p.Id=="porcelain") {
            dc.DrawLine(P(B("PegHighlight"),1.2),new(7,6),new(7,33));
            dc.DrawRectangle(B("PegBack"),null,new Rect(5,22,10,3));
        } else {
            dc.DrawLine(P(B("PegHighlight"),.8),new(7,8),new(7,16));
            dc.DrawEllipse(B("PegBack"),P(B("PegEdge"),.5),new(10,29),2.2,2.2);
        }
        dc.Pop();
        if(p.Id!="color-play") {
            dc.DrawEllipse(B("PegHighlight"),P(B("PegEdge"),.8),new(16,20),2.2,3);
            dc.DrawEllipse(B("PegEdge"),null,new(16,20),.8,1.6);
            Path(dc,null,P(B("PegHighlight"),1.2),"M16,23 L13,25 L6,25");
        } else dc.DrawEllipse(B("PegBack"),P(B("PegEdge"),.7),new(16,20),1.6,2.4);
        dc.Pop();
    }
    public static void Rope(DrawingContext dc, Geometry curve, ThemeProfile p)
    {
        if(RasterThemeArt.Has(p)) { RasterThemeArt.Rope(dc,curve,p); return; }
        Brush B(string k)=>Color(p,k);
        var width=p.Id switch { "linen"=>3.8,"color-play"=>3,"porcelain"=>1.9,"darkroom"=>2.2,_=>p.Material=="woven"?1.8:1.2 };
        dc.DrawGeometry(null,P(B("Rope"),width),curve);
        if(p.OriginalArtwork) {
            var twist=P(B(p.Id=="darkroom"?"PegBack":"PegHighlight"),width*.65);
            twist.DashStyle=new DashStyle(p.Id=="darkroom"?new[]{.3,1.7}:new[]{1.2,1.8},0);
            dc.DrawGeometry(null,twist,curve);
        }
        dc.PushTransform(new TranslateTransform(0,-width*.22));
        dc.DrawGeometry(null,P(B("PegHighlight"),.45),curve); dc.Pop();
    }
    static Brush PlayColor(int variant) => Theme.Brush(((variant%3+3)%3) switch { 0=>"#F4DFA4", 1=>"#EDC3B2", _=>"#BED8EA" });
    public static void Frame(DrawingContext dc, ThemeProfile p, Rect rect, double radius, int variant = 0)
    {
        if(RasterThemeArt.Has(p)) { RasterThemeArt.Frame(dc,p,rect,radius,variant); return; }
        if(rect.Width<=0 || rect.Height<=0) return;
        Brush B(string k)=>Color(p,k);
        if(p.Id=="color-play") {
            dc.DrawRoundedRectangle(PlayColor(variant),null,rect,radius,radius);
            var paper=new Rect(rect.X,rect.Y,Math.Max(1,rect.Width-4),Math.Max(1,rect.Height-4));
            dc.DrawRoundedRectangle(B("FrameFill"),P(B("FrameEdge"),.6),paper,radius,radius);
            dc.DrawLine(P(PlayColor(variant),2),new(rect.X+radius,rect.Bottom-2),new(rect.Right-radius,rect.Bottom-2));
        } else {
            dc.DrawRoundedRectangle(p.Id=="porcelain"?Glaze(p,"BasketHighlight","FrameFill"):B("FrameFill"),P(B("FrameEdge"),.8),rect,radius,radius);
            if(p.Id=="darkroom" && rect.Width>55 && rect.Height>45) {
                for(double y=rect.Y+10;y<rect.Bottom-8;y+=12) foreach(var x in new[]{rect.X+2,rect.Right-6}) dc.DrawRoundedRectangle(B("WindowSurface"),P(B("FrameEdge"),.35),new Rect(x,y,4,6),.6,.6);
                dc.DrawLine(P(B("Accent"),1),new(rect.X+13,rect.Bottom-7),new(rect.X+34,rect.Bottom-7));
            }
            if(p.Id=="linen" && rect.Width>55 && rect.Height>45) {
                dc.PushClip(new RectangleGeometry(rect));
                for(double x=rect.X+3;x<rect.Right;x+=4) dc.DrawLine(P(B("Outline"),.25),new(x,rect.Bottom-17),new(x+1,rect.Bottom-2));
                InvariantPath(dc,B("ControlSurface"),P(B("FrameEdge"),.5),$"M{rect.Right-13},{rect.Bottom} L{rect.Right},{rect.Bottom-10} L{rect.Right},{rect.Bottom} Z"); dc.Pop();
            }
        }
    }
}
