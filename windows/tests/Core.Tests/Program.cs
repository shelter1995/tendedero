using Liangtu.Core;
var failed=0;var passed=0;
void Test(string name,Action test){try{test();Console.WriteLine("PASS "+name);passed++;}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
void Eq<T>(T expected,T actual){if(!Equals(expected,actual))throw new Exception($"expected {expected}, got {actual}");}
void Near(double e,double a){if(Math.Abs(e-a)>0.001)throw new Exception($"expected {e}, got {a}");}
Test("rope anchors and sag",()=>{Near(10,Layout.RopeY(0,1920));Near(40,Layout.RopeY(960,1920));Near(10,Layout.RopeY(1920,1920));});
Test("portrait screenshot keeps aspect ratio",()=>{var s=Layout.Photo(100,1000);Near(10.4,s.width);Near(104,s.height);});
Test("wide screenshot keeps aspect ratio",()=>{var s=Layout.Photo(2000,100);Near(136,s.width);Near(6.8,s.height);});
Test("capacity clamps narrow and wide screens",()=>{Eq(3,Layout.Visible(320));Eq(12,Layout.Visible(6000));Eq(9,Layout.Visible(1920));});
Test("cards center with original spacing",()=>{Near(786,Layout.X(0,3,1920,9,false));Near(1134,Layout.X(2,3,1920,9,false));});
Test("single click waits for double click",()=>{var g=new Gesture();g.Down(0);g.Up(.1,.5);Eq(GestureAction.None,g.Tick(.59));Eq(GestureAction.Copy,g.Tick(.61));Eq(GestureAction.None,g.Tick(1));});
Test("double click never copies",()=>{var g=new Gesture();g.Down(0);g.Up(.1,.5);Eq(GestureAction.Open,g.Down(.2,2));g.Up(.3,.5);Eq(GestureAction.None,g.Tick(2));});
Test("450ms hold edits once without copying",()=>{var g=new Gesture();g.Down(0);Eq(GestureAction.None,g.Tick(.449));Eq(GestureAction.Edit,g.Tick(.451));Eq(GestureAction.None,g.Tick(.8));g.Up(1,.5);Eq(GestureAction.None,g.Tick(2));});
Test("drag cancels long press and copy",()=>{var g=new Gesture();g.Down(0);Eq(GestureAction.None,g.Move(4));Eq(GestureAction.Drag,g.Move(4.1));Eq(GestureAction.None,g.Tick(.6));g.Up(1,.5);Eq(GestureAction.None,g.Tick(2));});
Test("cancel clears pending clipboard write",()=>{var g=new Gesture();g.Down(0);g.Up(.1,.5);g.Cancel();Eq(GestureAction.None,g.Tick(1));});
Test("sibling folder is not inbox",()=>{var root=Path.Combine(Path.GetTempPath(),"inbox");Eq(true,FileRules.IsInside(Path.Combine(root,"a.png"),root));Eq(false,FileRules.IsInside(Path.Combine(root+"-other","a.png"),root));});
Test("media filter excludes incomplete and arbitrary files",()=>{Eq(true,FileRules.IsMedia("a.PNG"));Eq(true,FileRules.IsMedia("a.mp4"));Eq(false,FileRules.IsMedia("a.png.tmp"));Eq(false,FileRules.IsMedia(".trimming.mp4"));});
DpiChecks.Run(Test);
VideoRangeChecks.Run(Test);
VideoHistoryChecks.Run(Test);
Console.WriteLine($"{passed} passed, {failed} failed");return failed==0?0:1;
