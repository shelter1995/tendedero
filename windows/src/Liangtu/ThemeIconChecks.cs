namespace Liangtu;
internal static class ThemeIconChecks
{
 [STAThread] public static int Main(string[] args)
 {
  Directory.CreateDirectory(args[0]);var app=new Application();var results=new List<string>();
  void Check(string name,Action run){try{run();results.Add("PASS "+name);}catch(Exception e){results.Add("FAIL "+name+": "+e.Message);}}
  app.Startup+=(_,_)=>{
   Check("every selectable theme produces a frozen application icon and valid multi-size ICO",()=>{
    foreach(var p in ThemeProfiles.All){var image=ThemeIcons.AppImage(p);if(!image.IsFrozen)throw new Exception("Mutable icon "+p.Id);
     var bytes=ThemeIcons.AppIco(p);using var stream=new MemoryStream(bytes);var decoder=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
     foreach(var size in new[]{16,20,32,48,256})if(!decoder.Frames.Any(f=>f.PixelWidth==size&&f.PixelHeight==size))throw new Exception("ICO resolution missing "+p.Id+" "+size);
     File.WriteAllBytes(Path.Combine(args[0],p.Id+".ico"),bytes);
    }
   });
   Check("16 and 20 pixel tray icons retain transparent exterior and visible light dark contrast",()=>{
    foreach(var p in ThemeProfiles.All.Where(ThemeIcons.Custom))foreach(var light in new[]{true,false})foreach(var size in new[]{16,20}){
     var bitmap=ThemeIcons.Render(p,size,true,light);var pixels=new byte[size*size*4];bitmap.CopyPixels(pixels,size*4,0);var backdrop=light?230:30;var visible=0;
     for(int i=0;i<pixels.Length;i+=4){var a=pixels[i+3]/255d;if(a<.3)continue;var luminance=.0722*pixels[i]+.7152*pixels[i+1]+.2126*pixels[i+2]+backdrop*(1-a);if(Math.Abs(luminance-backdrop)>60)visible++;}
     if(pixels[3]!=0||visible<size)throw new Exception("Unclear tray silhouette "+p.Id+" "+size+" light="+light);
     using var icon=ThemeIcons.Tray(p,light);if(icon.Width<16||icon.Handle==IntPtr.Zero)throw new Exception("Invalid tray handle");
    }
   });
   Check("theme switching updates the existing window icon without recreating controls",()=>{
    var window=new Window();Theme.WindowStyle(window);var text=new TextBox{Text="保留编辑"};window.Content=text;
    foreach(var p in ThemeProfiles.All){Theme.Select(p.Id);if(!ReferenceEquals(window.Content,text)||!ReferenceEquals(window.Icon,ThemeIcons.AppImage(p)))throw new Exception("Window icon did not follow "+p.Id);}
    window.Close();
   });
   var board=new Canvas{Width=1080,Height=ThemeProfiles.All.Count*148+70,Background=Theme.Brush("#F3F5F6")};
   void Add(UIElement child,double x,double y){Canvas.SetLeft(child,x);Canvas.SetTop(child,y);board.Children.Add(child);}
   Add(new TextBlock{Text="晾图 · 主题配套图标 / 原生渲染",FontFamily=Theme.Font,FontSize=24,Foreground=Theme.Brush("#203344")},24,18);
   for(int i=0;i<ThemeProfiles.All.Count;i++){
    var p=ThemeProfiles.All[i];double y=70+i*148;Add(new Image{Source=ThemeIcons.Render(p,104,false,false),Width=104,Height=104},28,y);
    Add(new TextBlock{Text=p.Name,FontFamily=Theme.Font,FontSize=19,Foreground=Theme.Brush("#203344")},152,y+14);
    Add(new TextBlock{Text="窗口图标随主题切换",FontFamily=Theme.Font,FontSize=12,Foreground=Theme.Brush("#526679")},152,y+47);
    foreach(var light in new[]{true,false}){
     var sample=new Canvas{Width=270,Height=100,Background=Theme.Brush(light?"#E4E6EB":"#1C1F26")};Add(sample,light?400:700,y);
     var label=new TextBlock{Text=light?"浅色托盘 · 16 / 20 / 32px":"深色托盘 · 16 / 20 / 32px",Foreground=light?Brushes.Black:Brushes.White,FontFamily=Theme.Font,FontSize=12};Canvas.SetLeft(label,12);Canvas.SetTop(label,10);sample.Children.Add(label);
     double x=25;foreach(var size in new[]{16,20,32}){var icon=new Image{Source=ThemeIcons.Render(p,size,true,light),Width=size,Height=size};Canvas.SetLeft(icon,x);Canvas.SetTop(icon,47);sample.Children.Add(icon);x+=64;}
    }
   }
   board.Measure(new Size(board.Width,board.Height));board.Arrange(new Rect(0,0,board.Width,board.Height));board.UpdateLayout();var render=new RenderTargetBitmap((int)board.Width,(int)board.Height,96,96,PixelFormats.Pbgra32);render.Render(board);MediaTools.SavePng(render,Path.Combine(args[0],"theme-icons.png"));
   File.WriteAllLines(Path.Combine(args[0],"results.txt"),results);foreach(var line in results)Console.WriteLine(line);app.Shutdown(results.Any(s=>s.StartsWith("FAIL"))?1:0);
  };return app.Run();
 }
}
