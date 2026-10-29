namespace Liangtu;

internal sealed record PinAppearance(DrawingGroup Body,DrawingGroup Controls,Size Size,Rect ImageBounds,
    double ScaleX,double ScaleY,double Radius,BitmapSource Image)
{
    internal Thickness PhysicalInsets => new(ImageBounds.Left*ScaleX,ImageBounds.Top*ScaleY,
        (Size.Width-ImageBounds.Right)*ScaleX,(Size.Height-ImageBounds.Bottom)*ScaleY);
}

internal readonly record struct FrameAppearanceState(Thickness Insets,double PinWeight,double Controls,
    double Radius,double ImageRadius,double ShadowOpacity,double ShadowBlur,double ShadowDepth);

internal sealed class FrameMorph
{
    internal PinAppearance Pin {get;private set;}
    internal readonly bool FromPin;
    internal readonly double LineScaleX,LineScaleY;
    internal FrameMorph(PinAppearance pin,bool fromPin,double lineScaleX,double lineScaleY)
    {Pin=pin;FromPin=fromPin;LineScaleX=lineScaleX;LineScaleY=lineScaleY;}
    internal void UpdatePin(PinAppearance pin)=>Pin=pin;
    internal FrameAppearanceState At(ThemeProfile profile,double progress)
    {
        progress=Math.Clamp(progress,0,1);
        var mix=FromPin?1-MotionCurve.Ease(Math.Clamp(progress/.35,0,1)):MotionCurve.Ease(Math.Clamp((progress-.65)/.35,0,1));
        var controls=FromPin?1-MotionCurve.Ease(Math.Clamp(progress/.14,0,1)):0;
        var mat=profile.PhotoMat;var pin=Pin.PhysicalInsets;
        double Blend(double a,double b)=>a+(b-a)*mix;
        var insets=new Thickness(Blend(mat.Left*LineScaleX,pin.Left),Blend(mat.Top*LineScaleY,pin.Top),Blend(mat.Right*LineScaleX,pin.Right),Blend(mat.Bottom*LineScaleY,pin.Bottom));
        return new(insets,mix,controls,Blend(profile.FrameRadius*LineScaleX,Pin.Radius*Pin.ScaleX),
            profile.PhotoImageRadius*LineScaleX*(1-mix),profile.ShadowOpacity*(1-mix),8*LineScaleX*(1-mix),3*LineScaleY*(1-mix));
    }
}
