namespace Liangtu;

// Production sprites generated from the approved MiniMax Design concept boards.
// Decode, crop and freeze once; animation only reuses retained drawing resources.
internal static class RasterThemeArt
{
    static readonly Dictionary<string, BitmapSource> images = new();
    static readonly Dictionary<string, ImageBrush> surfaces = new();
    static readonly Dictionary<string, BitmapSource[]> frames = new();
    static readonly string[] basicPegs = { "peg-closed", "peg-open" };
    static readonly string[] coloredPegs = { "peg-closed", "peg-open", "peg-closed-peach", "peg-open-peach", "peg-closed-butter", "peg-open-butter" };
    public static bool Has(ThemeProfile profile) => profile.Id is "linen" or "color-play";
    internal static int DecodedCount => images.Count;
    internal static int DecodeOperations { get; private set; }
    internal static bool HasDecodedPegs(string theme) => images.Keys.Any(key => key.StartsWith(theme + "/peg-", StringComparison.Ordinal));
    internal static bool HasDecodedFrame(string theme) => images.ContainsKey(theme+"/frame");
    public static void RetainTheme(ThemeProfile profile)
    {
        // Live drawings keep their own immutable references. The static cache
        // only needs the selected theme; switching back decodes it once.
        foreach (var key in images.Keys.Where(key => !key.StartsWith(profile.Id + "/", StringComparison.Ordinal)).ToArray()) images.Remove(key);
        foreach (var key in surfaces.Keys.Where(key => key != profile.Id).ToArray()) surfaces.Remove(key);
        foreach (var key in frames.Keys.Where(key => key != profile.Id).ToArray()) frames.Remove(key);
        foreach (var key in ropes.Keys.Where(key => key != profile.Id).ToArray()) ropes.Remove(key);
    }
    public static void WarmShelf(ThemeProfile profile)
    {
        if (!Has(profile)) return;
        _ = Image(profile.Id, "basket"); _ = Image(profile.Id, "rope");
        if (profile.Id == "linen") _ = Image(profile.Id, "frame");
    }
    public static void WarmPhotos(ThemeProfile profile)
    {
        if (!Has(profile)) return;
        WarmShelf(profile);
        foreach (var part in profile.Id == "color-play" ? coloredPegs : basicPegs) _ = Image(profile.Id, part);
    }
    public static void ReleasePhotoArtwork(ThemeProfile profile)
    {
        foreach (var key in images.Keys.Where(key => key.StartsWith(profile.Id + "/peg-", StringComparison.Ordinal)).ToArray()) images.Remove(key);
    }
    internal static BitmapSource Image(string theme, string part)
    {
        string key=theme+"/"+part;
        if(images.TryGetValue(key,out var image)) return image;
        var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad;
        bitmap.UriSource=new Uri("pack://application:,,,/Liangtu;component/Assets/Themes/"+key+".png");
        bitmap.EndInit(); bitmap.Freeze(); images.Add(key,bitmap); DecodeOperations++; return bitmap;
    }
    public static ImageBrush Surface(string theme)
    {
        if(surfaces.TryGetValue(theme,out var brush)) return brush;
        brush=new ImageBrush(Image(theme,"surface")) { TileMode=TileMode.Tile, ViewportUnits=BrushMappingMode.Absolute,
            Viewport=new Rect(0,0,240,240), Stretch=Stretch.Fill };
        brush.Freeze(); surfaces.Add(theme,brush); return brush;
    }
    public static Rect BasketMouth(ThemeProfile p) => p.Id=="linen" ? new Rect(14,26,36,16) : new Rect(14,23,36,15);
    public static void Basket(DrawingContext dc, ThemeProfile p)
    {
        var image=Image(p.Id,"basket"); double height=64d*image.PixelHeight/image.PixelWidth;
        dc.DrawImage(image,new Rect(0,4,64,height));
    }
    public static void Peg(DrawingContext dc, ThemeProfile p, double opening, int variant = 0)
    {
        var suffix=p.Id=="color-play" ? (((variant%3+3)%3) switch { 1=>"-peach",2=>"-butter",_=>"" }) : "";
        _=Image(p.Id,"peg-closed"+suffix); _=Image(p.Id,"peg-open"+suffix);
        opening=Math.Clamp(opening,0,1);
        void Draw(string part,double opacity) {
            if(opacity<=0) return;
            var image=Image(p.Id,part+suffix); double width=38d*image.PixelWidth/image.PixelHeight;
            dc.PushOpacity(opacity); dc.DrawImage(image,new Rect(13-width/2,1,width,38)); dc.Pop();
        }
        Draw("peg-closed",1-opening); Draw("peg-open",opening);
    }
    static readonly Dictionary<string, Pen> ropes=new();
    public static void Rope(DrawingContext dc, Geometry curve, ThemeProfile p)
    {
        if(!ropes.TryGetValue(p.Id,out var pen)) {
            double width=p.Id=="linen"?4.2:3.5;
            var image=Image(p.Id,"rope");
            // Use the continuous opaque fiber core: tiling the transparent outer
            // silhouette across a curved stroke would leave periodic gaps.
            var core=new CroppedBitmap(image,new Int32Rect(16,image.PixelHeight/2-8,image.PixelWidth-32,16)); core.Freeze();
            var brush=new ImageBrush(core) { TileMode=TileMode.Tile, ViewportUnits=BrushMappingMode.Absolute,
                Viewport=new Rect(0,0,42,width), Stretch=Stretch.Fill };
            brush.Freeze(); pen=new Pen(brush,width) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round };
            pen.Freeze(); ropes.Add(p.Id,pen);
        }
        dc.DrawGeometry(null,pen,curve);
    }
    static readonly Brush[] pastelLayers = { Frozen("#ECDCA9"), Frozen("#E8BFAE"), Frozen("#BBD4E6") };
    static readonly Brush paper=Frozen("#FFFFFF"), paperEdge=Frozen("#CFDCDD"), middlePaper=Frozen("#E7EEE9");
    static readonly Pen paperOutline=FrozenPen(paperEdge,.8);
    static Brush Frozen(string color) { var brush=Theme.Brush(color); brush.Freeze(); return brush; }
    static Pen FrozenPen(Brush brush,double width) { var pen=new Pen(brush,width); pen.Freeze(); return pen; }
    internal static Rect ColorPlayPaperBounds(Rect rect) => new(rect.X+.5,rect.Y+.5,Math.Max(0,rect.Width-5),Math.Max(0,rect.Height-7));
    static void ColorPlayFrame(DrawingContext dc, Rect rect, double radius, int variant)
    {
        // Axis-aligned original geometry: the source photo's tilted silhouette
        // cannot be nine-sliced without magnifying jagged slanted pixel edges.
        var dx=Math.Min(4,rect.Width*.1); var dy=Math.Min(6,rect.Height*.1);
        var corner=Math.Min(radius,Math.Min(rect.Width,rect.Height)/3);
        dc.DrawRoundedRectangle(pastelLayers[(variant%3+3)%3],null,new Rect(rect.X+dx,rect.Y+dy,rect.Width-dx,rect.Height-dy),corner,corner);
        dc.DrawRoundedRectangle(middlePaper,null,new Rect(rect.X+dx/2,rect.Y+dy/2,Math.Max(0,rect.Width-dx),Math.Max(0,rect.Height-dy)),corner,corner);
        var front=ColorPlayPaperBounds(rect);
        if(front.Width>0 && front.Height>0) dc.DrawRoundedRectangle(paper,paperOutline,front,corner,corner);
    }
    public static void Frame(DrawingContext dc, ThemeProfile p, Rect rect, double? radius = null, int variant = 0)
    {
        if(rect.Width<=0 || rect.Height<=0) return;
        if(p.Id=="color-play") { ColorPlayFrame(dc,rect,radius ?? p.FrameRadius,variant); return; }
        var source=Image(p.Id,"frame");
        // Insets measured at the transparent opening of each supplied frame.
        var inset=new Thickness(56,57,59,138);
        if(!frames.TryGetValue(p.Id,out var slices)) {
            var xs=new[]{0,(int)inset.Left,source.PixelWidth-(int)inset.Right,source.PixelWidth};
            var ys=new[]{0,(int)inset.Top,source.PixelHeight-(int)inset.Bottom,source.PixelHeight};
            slices=new BitmapSource[9];
            for(int y=0;y<3;y++) for(int x=0;x<3;x++) {
                var crop=new CroppedBitmap(source,new Int32Rect(xs[x],ys[y],xs[x+1]-xs[x],ys[y+1]-ys[y])); crop.Freeze(); slices[y*3+x]=crop;
            }
            frames.Add(p.Id,slices);
        }
        var mat=p.PhotoMat; double sx=Math.Min(1,rect.Width/(mat.Left+mat.Right)), sy=Math.Min(1,rect.Height/(mat.Top+mat.Bottom));
        var dx=new[]{rect.Left,rect.Left+mat.Left*sx,rect.Right-mat.Right*sx,rect.Right};
        var dy=new[]{rect.Top,rect.Top+mat.Top*sy,rect.Bottom-mat.Bottom*sy,rect.Bottom};
        // GlassBorder is also used for status labels. Preserve the original
        // opaque paper backing; a transparent sprite opening must not expose
        // arbitrary desktop pixels behind copied/empty-state text.
        if(dx[2]>dx[1] && dy[2]>dy[1]) dc.DrawRectangle(ThemeArtwork.Color(p,"FrameFill"),null,new Rect(dx[1],dy[1],dx[2]-dx[1],dy[2]-dy[1]));
        for(int y=0;y<3;y++) for(int x=0;x<3;x++) {
            if(x==1&&y==1) continue; // The screenshot owns the transparent center.
            if(dx[x+1]>dx[x]&&dy[y+1]>dy[y]) dc.DrawImage(slices[y*3+x],new Rect(dx[x],dy[y],dx[x+1]-dx[x],dy[y+1]-dy[y]));
        }
    }
}
