namespace Liangtu;
internal static class WindowsSnipOriginChecks
{
 [STAThread] public static int Main(string[] args)
 {
  var results=new List<string>();void Test(string name,Action action){try{action();results.Add("PASS "+name);}catch(Exception e){results.Add("FAIL "+name+": "+e.Message);}}
  void Assert(bool yes){if(!yes)throw new Exception("Origin state invariant failed");}
  SnipOriginSession Ready(){var s=new SnipOriginSession();s.Begin(new Point(600,460),10,100);s.End(new Point(1398,938),400);return s;}
  Test("physical selection and current Windows clipboard dimensions match",()=>Assert(Ready().Match(true,11,798,478,500)==new Rect(600,460,798,478)));
  Test("pixel-verified Windows 200 percent forward capture keeps raw physical origin",()=>{var s=new SnipOriginSession();s.Begin(new Point(607,469),1,0,2);s.End(new Point(1393,928),20);Assert(s.Match(true,2,786,460,30)==new Rect(607,469,786,460));});
  Test("pixel-verified Windows 200 percent reverse capture keeps raw physical origin",()=>{var s=new SnipOriginSession();s.Begin(new Point(1393,928),1,0,2);s.End(new Point(609,471),20);Assert(s.Match(true,2,784,458,30)==new Rect(609,471,784,458));});
  Test("DPI extent validation is exact rather than accepting arbitrary near sizes",()=>{var s=new SnipOriginSession();s.Begin(new Point(607,469),1,0,2);s.End(new Point(1393,928),20);Assert(s.Match(true,2,785,460,30)==null);});
  Test("reverse rectangle preserves physical top-left",()=>{var s=new SnipOriginSession();s.Begin(new Point(1398,938),1,0);s.End(new Point(600,460),20);Assert(s.Match(true,2,798,478,30)==new Rect(600,460,798,478));});
  Test("negative monitor coordinates are retained",()=>{var s=new SnipOriginSession();s.Begin(new Point(-1600,-900),1,0);s.End(new Point(-800,-400),20);Assert(s.Match(true,2,800,500,30)==new Rect(-1600,-900,800,500));});
  Test("unchanged clipboard sequence does not consume the pending result",()=>{var s=Ready();Assert(s.Match(true,10,798,478,500)==null&&s.Pending);Assert(s.Match(true,11,798,478,600)!=null);});
  Test("one gesture can be consumed once only",()=>{var s=Ready();Assert(s.Match(true,11,798,478,500)!=null);Assert(s.Match(true,12,798,478,600)==null);});
  Test("untrusted clipboard source clears the candidate",()=>{var s=Ready();Assert(s.Match(false,11,798,478,500)==null);Assert(s.Match(true,12,798,478,600)==null);});
  Test("different image dimensions never borrow a pending origin",()=>{var s=Ready();Assert(s.Match(true,11,800,480,500)==null);Assert(!s.Pending);});
  Test("cancellation clears both drag and completion",()=>{var s=Ready();s.Cancel();Assert(s.Match(true,11,798,478,500)==null);s.Begin(new Point(1,1),11,500);s.Cancel();s.End(new Point(301,201),550);Assert(!s.Pending);});
  Test("intervening text privacy or non-Windows clipboard clears stale position",()=>{var s=Ready();s.RejectClipboard(10);Assert(s.Pending);s.RejectClipboard(11);Assert(!s.Pending&&s.Match(true,12,798,478,500)==null);});
  Test("late clipboard images cannot reuse expired positions",()=>{var s=Ready();Assert(s.Match(true,11,798,478,5401)==null&&!s.Pending);});
  Test("expiration retires a long abandoned mouse press",()=>{var s=new SnipOriginSession();s.Begin(new Point(1,1),1,0);s.Expire(60001);s.End(new Point(301,201),60002);Assert(!s.Pending);});
  Test("overlapping unresolved captures cannot borrow each other's origins",()=>{var s=Ready();s.Begin(new Point(20,30),11,500);s.End(new Point(220,130),600);Assert(s.Match(true,12,200,100,650)==null);s.Begin(new Point(20,30),12,700);s.End(new Point(220,130),800);Assert(s.Match(true,13,200,100,850)==new Rect(20,30,200,100));});
  Test("fractional DPI expects integer outward-rounded PNG dimensions",()=>{foreach(var (scale,w,h) in new[]{(1.25,787,458),(1.5,786,458),(1.75,788,459)}) {var s=new SnipOriginSession();s.Begin(new Point(20,30),1,0,scale);s.End(new Point(806,487),20);Assert(s.Match(true,2,w,h,30)==new Rect(20,30,w,h));}});
  Test("clicks and subpixel origins are rejected",()=>{var s=new SnipOriginSession();s.Begin(new Point(1,1),1,0);s.End(new Point(2,2),20);Assert(!s.Pending);s.Begin(new Point(1.5,1),1,40);s.End(new Point(301.5,201),50);Assert(!s.Pending);});
  Test("regressing clocks cannot validate old state",()=>{Assert(Ready().Match(true,11,798,478,399)==null);var s=new SnipOriginSession();s.Begin(new Point(1,1),1,50);s.End(new Point(301,201),49);Assert(!s.Pending);});
  Test("clipboard sequence wrap remains valid",()=>{var s=new SnipOriginSession();s.Begin(new Point(1,1),uint.MaxValue,0);s.End(new Point(301,201),20);Assert(s.Match(true,0,300,200,30)!=null);});
  Test("third-party selection preserves odd physical dimensions at 200 percent",()=>{var s=new SnipOriginSession();s.Begin(new Point(-901,201),1,0,2,windows:false);s.End(new Point(-116,660),20);Assert(s.Match(true,2,785,459,30)==new Rect(-901,201,785,459));});
  Test("third-party toolbar confirmation may follow the selection after five seconds",()=>{var s=new SnipOriginSession();s.Begin(new Point(100,200),1,0,2,windows:false);s.End(new Point(700,600),20);s.Expire(6020);Assert(s.Match(true,2,600,400,7020)==new Rect(100,200,600,400));});
  Test("clicking a third-party copy toolbar retains the completed selection",()=>{var s=new SnipOriginSession();s.Begin(new Point(100,200),1,0,2,windows:false);s.End(new Point(700,600),20);s.Begin(new Point(740,650),1,500,2,windows:false);s.End(new Point(740,650),550);Assert(s.Match(true,2,600,400,600)==new Rect(100,200,600,400));});
  Test("third-party selection rejects a different-sized clipboard export",()=>{var s=new SnipOriginSession();s.Begin(new Point(100,200),1,0,2,windows:false);s.End(new Point(700,600),20);Assert(s.Match(true,2,300,200,50)==null&&!s.Pending);});
  Test("concurrent consumers cannot replay a completed origin",()=>{var s=Ready();var hits=0;Parallel.For(0,20,_=>{if(s.Match(true,11,798,478,500)!=null)System.Threading.Interlocked.Increment(ref hits);});Assert(hits==1);});
  Directory.CreateDirectory(args[0]);File.WriteAllLines(Path.Combine(args[0],"results.txt"),results);foreach(var r in results)Console.WriteLine(r);return results.Any(r=>r.StartsWith("FAIL"))?1:0;
 }
}
