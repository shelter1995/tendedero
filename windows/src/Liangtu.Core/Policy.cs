namespace Liangtu.Core;
public static class Layout {
 public static double Round(double n)=>Math.Round(n,MidpointRounding.AwayFromZero);
 public static double RopeY(double x,double width){if(width<=0)return 10;var f=x/width;return 10+4*Math.Min(30,width*.018)*f*(1-f);}
 public static (double width,double height) Photo(double w,double h,double scale=1){var mw=Round(150*scale)-14;var mh=Round(104*scale);if(w<=0||h<=0)return(mw,mh);var s=Math.Min(mw/w,mh/h);return(w*s,h*s);}
 public static double Spacing(double scale)=>Round(150*scale)+24;
 public static int Visible(double width,double scale=1)=>Math.Clamp((int)((width-200)/Spacing(scale)),3,12);
 public static double X(int index,int count,double width,int visible,bool retained,double scroll=0,double scale=1){var spacing=Spacing(scale);if(retained&&count>visible)return width/2+(visible-1)*spacing/2-(count-1-index)*spacing+scroll;var gaps=Math.Max(0,count-1);var step=gaps>0?Math.Min(spacing,Math.Max(0,width-200)/gaps):spacing;return width/2-gaps*step/2+index*step;}
}
public enum GestureAction { None,Copy,Open,Edit,Drag,Preview }
public class Gesture {
 double? down,copyAt;bool consumed;
 public bool HasPending=>down!=null||copyAt!=null;
 public GestureAction Down(double now,int clicks=1){if(clicks>1){Cancel();return GestureAction.Open;}down=now;consumed=false;return GestureAction.None;}
 public GestureAction Move(double distance){if(down!=null&&!consumed&&distance>4){consumed=true;copyAt=null;return GestureAction.Drag;}return GestureAction.None;}
 public GestureAction Up(double now,double doubleClickSeconds){if(down!=null&&!consumed)copyAt=now+doubleClickSeconds;down=null;consumed=false;return GestureAction.None;}
 public GestureAction Tick(double now){if(down!=null&&!consumed&&now-down>=.45){consumed=true;copyAt=null;return GestureAction.Edit;}if(copyAt!=null&&now>=copyAt){copyAt=null;return GestureAction.Copy;}return GestureAction.None;}
 public void Cancel(){down=null;copyAt=null;consumed=false;}
}
public static class FileRules {
 public static bool IsInside(string file,string folder){var root=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;return Path.GetFullPath(file).StartsWith(root,StringComparison.OrdinalIgnoreCase);}
 public static bool IsMedia(string file)=>!Path.GetFileName(file).StartsWith('.')&&new[]{".png",".jpg",".jpeg",".bmp",".gif",".webp",".tif",".tiff",".mp4",".mov",".m4v",".avi",".wmv"}.Contains(Path.GetExtension(file).ToLowerInvariant());
 public static string UniquePath(string folder,string filename){var dest=Path.Combine(folder,filename);for(int n=2;File.Exists(dest);n++)dest=Path.Combine(folder,Path.GetFileNameWithoutExtension(filename)+" "+n+Path.GetExtension(filename));return dest;}
}
