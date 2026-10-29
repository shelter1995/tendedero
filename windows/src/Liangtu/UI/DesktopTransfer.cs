using System.Diagnostics;
namespace Liangtu;

// A stationary, bounded desktop surface. The shelf HWND never grows for pins.
internal sealed class DesktopTransfer
{
    readonly ArrivalFlight surface;
    readonly ArrivalPose from;
    ArrivalPose to;
    readonly Func<(PinAppearance? appearance,Rect? bounds)>? refreshTarget;
    bool targetChanged;
    readonly Size imageSize;
    readonly Stopwatch clock=new();
    Action? presented,landed,retired;
    bool running,rendering,hasPresented,hasLanded,stopped;
    int landingFrames;
    TimeSpan? lastFrame;
    TimeSpan? sourceFrame;
    double? motionStarted;
    internal DesktopTransfer(BitmapSource image,LineWindow owner,ArrivalPose from,ArrivalPose to,Size imageSize,int variant,PinAppearance? appearance=null,Func<(PinAppearance? appearance,Rect? bounds)>? refreshTarget=null)
    {
        this.from=from;this.to=to;this.imageSize=imageSize;this.refreshTarget=refreshTarget;
        surface=new ArrivalFlight(image,owner,from,to,imageSize,variant,appearance);
    }
    internal void Play(Action presented,Action landed,Action retired)
    {
        this.presented=presented;this.landed=landed;this.retired=retired;
        surface.Prepare();if(stopped)return;clock.Start();running=true;CompositionTarget.Rendering+=Render;if(refreshTarget!=null)Theme.Changed+=TargetChanged;
    }
    void TargetChanged()=>targetChanged=true;
    void Render(object? sender,EventArgs args)
    {
        if(!running||rendering)return;
        var frame=(args as RenderingEventArgs)?.RenderingTime;
        if(frame!=null&&lastFrame==frame)return;
        lastFrame=frame;rendering=true;
        try {
            if(targetChanged&&refreshTarget!=null) {
                targetChanged=false;var target=refreshTarget();
                if(target.appearance is PinAppearance appearance&&target.bounds is Rect bounds) {to=ArrivalPose.FromRect(bounds);surface.UpdatePinAppearance(appearance);if(hasLanded)surface.Present(to,imageSize,1);}
            }
            if(hasLanded) {
                // Opacity/layout changes on the destination need a compositor
                // submission before the last flight surface can be destroyed.
                if(++landingFrames>=2) retired?.Invoke();
                return;
            }
            if(!hasPresented) {
                surface.Present(from,imageSize);
                if(!surface.HasPresented)return;
                if(sourceFrame==null) { sourceFrame=frame;return; }
                if(frame==null||frame==sourceFrame)return;
                hasPresented=true;motionStarted=clock.Elapsed.TotalSeconds;presented?.Invoke();
            }
            if(!running)return;
            var progress=Math.Clamp((clock.Elapsed.TotalSeconds-motionStarted!.Value)/.65,0,1);
            surface.Present(ArrivalPose.Travel(from,to,progress),imageSize,progress);
            if(progress>=1&&surface.HasPresented) { hasLanded=true;landed?.Invoke(); }
        } finally { rendering=false; }
    }
    internal void Stop()
    {
        if(stopped)return;stopped=true;
        if(running) { running=false;CompositionTarget.Rendering-=Render; }
        if(refreshTarget!=null)Theme.Changed-=TargetChanged;
        presented=landed=retired=null;surface.Close();
    }
}
