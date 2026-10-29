namespace Liangtu;

internal static class ThemeIcons
{
    static readonly Dictionary<string,(DrawingGroup drawing,double units)> drawings=new();
    static readonly Dictionary<string,byte[]> files=new();
    static readonly Dictionary<string,BitmapSource> appImages=new();
    static readonly int[] appSizes={16,20,24,32,40,48,64,128,256};
    static readonly int[] traySizes={16,20,24,32,40,48,64};
    internal static string Family(ThemeProfile p)=>p.Id switch { "paper"=>"linen","night"=>"darkroom",_=>p.Id };
    internal static bool Custom(ThemeProfile p)=>Family(p)!="sky";
    internal static BitmapSource AppImage(ThemeProfile p)
    {
        var key=Family(p);if(appImages.TryGetValue(key,out var image))return image;
        if(!Custom(p)) image=BitmapFrame.Create(new Uri("pack://application:,,,/Liangtu;component/Assets/app.png"));
        else {
            using var stream=new MemoryStream(AppIco(p));
            image=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames.Last();
        }
        image.Freeze();appImages.Add(key,image);return image;
    }
    internal static byte[] AppIco(ThemeProfile p)
    {
        var key=Family(p)+"-app";
        if(!files.TryGetValue(key,out var data)) { data=Ico(appSizes.Select(size=>Render(p,size,false,false)).ToArray());files.Add(key,data); }
        return data;
    }
    internal static System.Drawing.Icon Tray(ThemeProfile p,bool light)
    {
        if(!Custom(p)) {
            using var stream=Application.GetResourceStream(new Uri("pack://application:,,,/Liangtu;component/Assets/tray-"+(light?"light":"dark")+".ico")).Stream;
            using var original=new System.Drawing.Icon(stream,System.Windows.Forms.SystemInformation.SmallIconSize);
            return (System.Drawing.Icon)original.Clone();
        }
        var key=Family(p)+"-tray-"+(light?"light":"dark");
        if(!files.TryGetValue(key,out var bytes)) { bytes=Ico(traySizes.Select(size=>Render(p,size,true,light)).ToArray());files.Add(key,bytes); }
        using var memory=new MemoryStream(bytes);using var icon=new System.Drawing.Icon(memory,System.Windows.Forms.SystemInformation.SmallIconSize);
        return (System.Drawing.Icon)icon.Clone();
    }
    internal static BitmapSource Render(ThemeProfile p,int pixels,bool tray,bool light)
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) {
            if(!Custom(p)) {
                BitmapSource bitmap;
                if(tray) {
                    using var stream=Application.GetResourceStream(new Uri("pack://application:,,,/Liangtu;component/Assets/tray-"+(light?"light":"dark")+".ico")).Stream;
                    bitmap=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames.OrderBy(f=>Math.Abs(f.PixelWidth-pixels)).First();
                } else bitmap=BitmapFrame.Create(new Uri("pack://application:,,,/Liangtu;component/Assets/app.png"));
                dc.DrawImage(bitmap,new Rect(0,0,pixels,pixels));
            } else {
                var key=Family(p)+(tray?"-tray-"+(light?"light":"dark")+"-"+(pixels<=16?16:20):pixels<=48?"-small":"-app");
                if(!drawings.TryGetValue(key,out var art)) { art=ThemeIconDrawings.Create(key);drawings.Add(key,art); }
                dc.PushTransform(new ScaleTransform(pixels/art.units,pixels/art.units));dc.DrawDrawing(art.drawing);dc.Pop();
            }
        }
        var result=new RenderTargetBitmap(pixels,pixels,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;
    }
    static byte[] Ico(BitmapSource[] images)
    {
        var pngs=images.Select(image=>{var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var s=new MemoryStream();png.Save(s);return s.ToArray();}).ToArray();
        using var output=new MemoryStream();using var writer=new BinaryWriter(output);
        writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)images.Length);
        uint offset=(uint)(6+16*images.Length);
        for(int i=0;i<images.Length;i++) {
            writer.Write((byte)(images[i].PixelWidth==256?0:images[i].PixelWidth));writer.Write((byte)(images[i].PixelHeight==256?0:images[i].PixelHeight));
            writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write((uint)pngs[i].Length);writer.Write(offset);offset+=(uint)pngs[i].Length;
        }
        foreach(var bytes in pngs)writer.Write(bytes);return output.ToArray();
    }
}
