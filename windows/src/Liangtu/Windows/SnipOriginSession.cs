namespace Liangtu;

// Only physical rectangle candidates; no pixels, titles or keyboard data.
internal sealed class SnipOriginSession
{
    readonly object gate=new();
    Point? start;
    Rect? candidate;
    long began,completed;
    uint beforeSequence;
    double pixelScale=1;
    bool ambiguous;
    bool windowsPixels = true;
    long lifetime = 5000;
    (Rect rect,long completed,uint sequence,double scale,bool ambiguous,long lifetime)? clickFallback;
    public bool Pending { get { lock(gate) return start!=null || candidate!=null; } }
    public bool Dragging { get { lock(gate) return start!=null; } }
    internal string Diagnostic { get { lock(gate) return $"start={start};candidate={candidate};before={beforeSequence};age={Environment.TickCount64-completed}"; } }
    public void Begin(Point point,uint sequence,long now,double scale=1,bool windows=true)
    {
        lock(gate) {
            var fallback=!windows && !windowsPixels && candidate is Rect rect
                ? ((Rect rect,long completed,uint sequence,double scale,bool ambiguous,long lifetime)?)(rect,completed,beforeSequence,pixelScale,ambiguous,lifetime) : null;
            var unresolved=candidate!=null;ClearCore();
            if(!double.IsFinite(scale)||scale<1||scale>8)return;
            clickFallback=fallback;start=point;began=now;beforeSequence=sequence;pixelScale=scale;ambiguous=unresolved;windowsPixels=windows;lifetime=windows?5000:15000;
        }
    }
    public void End(Point point,long now)
    {
        lock(gate) {
            if(start is not Point from || now<began || now-began>60000) { ClearCore(); return; }
            start=null;
            var rect=new Rect(from,point);
            if(rect.Width<3 || rect.Height<3) {
                var previous=clickFallback;ClearCore();
                if(previous is { } saved && now>=saved.completed && now-saved.completed<=saved.lifetime) {
                    candidate=saved.rect;completed=saved.completed;beforeSequence=saved.sequence;pixelScale=saved.scale;ambiguous=saved.ambiguous;windowsPixels=false;lifetime=saved.lifetime;
                }
                return;
            }
            if(!double.IsFinite(rect.Width+rect.Height) || new[]{rect.X,rect.Y,rect.Width,rect.Height}.Any(n=>n!=Math.Truncate(n))) { ClearCore(); return; }
            clickFallback=null;candidate=rect; completed=now;
        }
    }
    public Rect? Match(bool trustedSource,uint sequence,int width,int height,long now)
    {
        lock(gate) {
            if(candidate is not Rect rect) return null;
            // A stable pre-selection clipboard is never the result of this snip.
            if(sequence==beforeSequence) return null;
            // Pixel-coded Windows fixtures establish that the origin stays in
            // physical pixels while extents round outward to logical pixels.
            var expectedWidth=windowsPixels?Math.Ceiling(Math.Ceiling(rect.Width/pixelScale)*pixelScale):rect.Width;
            var expectedHeight=windowsPixels?Math.Ceiling(Math.Ceiling(rect.Height/pixelScale)*pixelScale):rect.Height;
            var okay=!ambiguous && trustedSource && now>=completed && now-completed<=lifetime && width==expectedWidth && height==expectedHeight;
            ClearCore(); return okay?new Rect(rect.X,rect.Y,width,height):null;
        }
    }
    public void Expire(long now) { lock(gate) { if(start!=null && (now<began || now-began>60000) || candidate!=null && (now<completed || now-completed>lifetime)) ClearCore(); } }
    public void Cancel() { lock(gate) ClearCore(); }
    public void RejectClipboard(uint sequence) { lock(gate) { if(sequence!=beforeSequence) ClearCore(); } }
    void ClearCore() { start=null; candidate=null;clickFallback=null; began=completed=0;ambiguous=false; }
}
