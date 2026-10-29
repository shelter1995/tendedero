using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
namespace Liangtu;

// Passive observation on a dedicated message thread. Never consumes input,
// captures desktop pixels, replaces Win+Shift+S, or reads ordinary typed keys.
internal sealed class WindowsSnipOrigin : IDisposable
{
    readonly Func<bool> enabled;
    readonly Action<string>? diagnostics;
    readonly SnipOriginSession session=new();
    readonly object sourceGate=new();
    readonly Dictionary<uint,string> trustedSequences=new();
    readonly HookProc mouseCallback,keyboardCallback;
    readonly Thread thread;
    readonly ManualResetEventSlim ready=new();
    readonly System.Threading.Timer expiry;
    IntPtr mouseHook,keyboardHook;
    uint threadId;
    volatile bool disposed;
    uint dragProcess;
    IntPtr dragWindow;
    double dragScale=1;
    string dragOwner = "";
    bool windowsDrag;
    public WindowsSnipOrigin(Func<bool> enabled,Action<string>? diagnostics=null)
    {
        this.enabled=enabled;this.diagnostics=diagnostics;
        mouseCallback=Mouse;keyboardCallback=Keyboard;
        thread=new Thread(Run) { IsBackground=true,Name="Liangtu Windows snip origin" }; thread.SetApartmentState(ApartmentState.STA); thread.Start();
        ready.Wait(2000);
        expiry=new System.Threading.Timer(_=>{lock(sourceGate) { if(disposed)return;session.Expire(Environment.TickCount64);if(!session.Pending) { RemoveKeyboard();trustedSequences.Clear(); } }},null,1000,1000);
    }
    public void ObserveClipboardSource(string owner,uint sequence)
    {
        lock(sourceGate) {
        if(disposed || !enabled() || !session.Pending) return;
        if(owner.Length>0 && !SourceNameMatches(owner)) { RejectClipboard(sequence);return; }
        if(SourceNameMatches(owner) && ClipboardBelongsToSelection()) {
            if(trustedSequences.Count>=16) trustedSequences.Clear();trustedSequences[sequence]=owner;
            Trace("trusted clipboard event sequence="+sequence);
        }
        }
    }
    public void RejectClipboard(uint sequence)
    {
        lock(sourceGate) { session.RejectClipboard(sequence);if(!session.Pending) { RemoveKeyboard();trustedSequences.Clear(); } }
    }
    public string? ObservedClipboardOwner(uint sequence)
    {
        lock(sourceGate) { return !disposed && enabled() && session.Pending && trustedSequences.TryGetValue(sequence,out var owner) ? owner : null; }
    }
    public Rect? TryMatch(string owner,uint sequence,int width,int height)
    {
        lock(sourceGate) {
        if(disposed || !enabled()) { session.Cancel();RemoveKeyboard();return null; }
        var observed=trustedSequences.ContainsKey(sequence);
        var trusted=SourceNameMatches(owner) && (observed||ClipboardBelongsToSelection());
        Trace($"try owner={owner} trusted={trusted} seq={sequence} size={width}x{height} state={session.Diagnostic}");
        var result=session.Match(trusted,sequence,width,height,Environment.TickCount64);
        if(!session.Pending) { RemoveKeyboard();trustedSequences.Clear(); }
        if(result is Rect rect) Trace($"match {rect.X},{rect.Y} {rect.Width}x{rect.Height}");
        return result;
        }
    }
    bool SourceNameMatches(string owner) => windowsDrag ? ClipboardImage.IsWindowsScreenshotOwner(owner) : owner.Length>0 && owner.Equals(dragOwner,StringComparison.OrdinalIgnoreCase);
    bool ClipboardBelongsToSelection()
    {
        var owner=GetClipboardOwner();
        if(windowsDrag) return TrustedWindow(owner,out _);
        return owner!=IntPtr.Zero && GetWindowThreadProcessId(owner,out var pid)!=0 && pid==dragProcess;
    }
    void Run()
    {
        threadId=GetCurrentThreadId();
        PeekMessage(out _,IntPtr.Zero,0,0,0);
        lock(sourceGate) {
        if(disposed) { ready.Set();return; }
        mouseHook=SetWindowsHookEx(14,mouseCallback,IntPtr.Zero,0);
        if(mouseHook==IntPtr.Zero) Trace("mouse observer unavailable: "+Marshal.GetLastWin32Error());
        ready.Set();
        }
        try { while(GetMessage(out var message,IntPtr.Zero,0,0)>0) { TranslateMessage(ref message);DispatchMessage(ref message); } }
        finally { lock(sourceGate) { if(mouseHook!=IntPtr.Zero) UnhookWindowsHookEx(mouseHook);mouseHook=IntPtr.Zero;RemoveKeyboard();session.Cancel();trustedSequences.Clear(); } }
    }
    IntPtr Mouse(int code,IntPtr message,IntPtr data)
    {
        if(code<0 || message.ToInt32() is not (0x201 or 0x202)) return CallNextHookEx(mouseHook,code,message,data);
        try { lock(sourceGate) {
            var kind=message.ToInt32();
            if(code>=0 && kind is 0x201 or 0x202) {
                if(disposed || !enabled()) { session.Cancel();RemoveKeyboard(); }
                else {
                    var info=Marshal.PtrToStructure<MouseData>(data);
                    var root=GetAncestor(WindowFromPoint(info.Point),2);
                    if(kind==0x201) {
                        var windows=Overlay(root,info.Point,out var pid);
                        var source="";
                        if(windows || GenericOverlay(root,info.Point,out pid,out source)) {
                            if(session.Pending && (pid!=dragProcess || windows!=windowsDrag)) session.Cancel();
                            dragProcess=pid;dragWindow=root;dragScale=Math.Max(96,GetDpiForWindow(root))/96d;
                            windowsDrag=windows;dragOwner=source;
                            trustedSequences.Clear();
                            session.Begin(new Point(info.Point.X,info.Point.Y),GetClipboardSequenceNumber(),Environment.TickCount64,dragScale,windows);
                            RemoveKeyboard();keyboardHook=SetWindowsHookEx(13,keyboardCallback,IntPtr.Zero,0);
                            if(keyboardHook==IntPtr.Zero) { session.Cancel();Trace("keyboard cancellation observer unavailable"); }
                            else Trace($"begin {info.Point.X},{info.Point.Y} window={root}");
                        } else if(session.Dragging) { session.Cancel();RemoveKeyboard(); }
                    } else if(session.Dragging) {
                        Trace($"up window={root} startWindow={dragWindow}");
                        // Capture overlays can disappear during mouse-up. Trust
                        // the overlay measured at down; clipboard owner, sequence
                        // and exact dimensions still have to match before use.
                        session.End(new Point(info.Point.X,info.Point.Y),Environment.TickCount64);RemoveKeyboard();Trace($"end {info.Point.X},{info.Point.Y}");
                    }
                }
            }
        } } catch { lock(sourceGate) { session.Cancel();RemoveKeyboard(); } }
        return CallNextHookEx(mouseHook,code,message,data);
    }
    bool GenericOverlay(IntPtr root,Point32 point,out uint pid,out string owner)
    {
        pid=0;owner="";
        if(root==IntPtr.Zero || !GetWindowRect(root,out var rect)) return false;
        GetWindowThreadProcessId(root,out pid);
        if(pid==0 || pid==Environment.ProcessId) return false;
        var name=new StringBuilder(128);GetClassName(root,name,name.Capacity);
        if(name.ToString() is "WorkerW" or "Progman" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(point.X,point.Y)).Bounds;
        var desktop=System.Windows.Forms.SystemInformation.VirtualScreen;
        bool Same(System.Drawing.Rectangle area)=>Math.Abs(rect.Left-area.Left)<=2&&Math.Abs(rect.Top-area.Top)<=2&&Math.Abs(rect.Right-area.Right)<=2&&Math.Abs(rect.Bottom-area.Bottom)<=2;
        if(!Same(screen)&&!Same(desktop)) return false;
        // Capture surfaces are borderless topmost overlays. Ordinary maximized
        // windows must not turn an unrelated image copy into a screenshot.
        if((GetWindowLongPtr(root,-20).ToInt64()&8)==0 || (GetWindowLongPtr(root,-16).ToInt64()&0x00C00000)!=0) return false;
        try { using var process=System.Diagnostics.Process.GetProcessById((int)pid);owner=process.ProcessName; }
        catch { return false; }
        Trace("third-party selection owner="+owner);
        return owner.Length>0;
    }
    IntPtr Keyboard(int code,IntPtr message,IntPtr data)
    {
        // Any key-down during this short selection invalidates the candidate:
        // includes cancellation and keyboard translation. We never read data/vk.
        lock(sourceGate) { if(code>=0 && message.ToInt32() is 0x100 or 0x104 && session.Dragging) { Trace("cancel: keyboard event (no key data read)");session.Cancel();RemoveKeyboard(); } }
        return CallNextHookEx(IntPtr.Zero,code,message,data);
    }
    bool Overlay(IntPtr root,Point32 point,out uint pid)
    {
        pid=0;if(root==IntPtr.Zero || !TrustedWindow(root,out pid)) return false;
        var name=new StringBuilder(128);GetClassName(root,name,name.Capacity);
        if(name.ToString() is not ("XamlWindow" or "Windows.UI.Core.CoreWindow")) { Trace("reject overlay class="+name);return false; }
        if(!GetWindowRect(root,out var rect)) return false;
        var bounds=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(point.X,point.Y)).Bounds;
        // Current Windows uses caption style bits on its capture XamlWindow too.
        // Require exact screen/virtual-desktop coverage instead of style guesses;
        // a maximized editor only covers the work area plus resize borders.
        var desktop=System.Windows.Forms.SystemInformation.VirtualScreen;
        bool Same(System.Drawing.Rectangle area)=>Math.Abs(rect.Left-area.Left)<=2 && Math.Abs(rect.Top-area.Top)<=2 && Math.Abs(rect.Right-area.Right)<=2 && Math.Abs(rect.Bottom-area.Bottom)<=2;
        var covers=Same(bounds)||Same(desktop);
        Trace($"overlay {rect.Left},{rect.Top},{rect.Right},{rect.Bottom} dpi={GetDpiForWindow(root)} accepted={covers}");
        if(!covers) Trace($"reject bounds {rect.Left},{rect.Top},{rect.Right},{rect.Bottom}");
        return covers;
    }
    static bool TrustedWindow(IntPtr window,out uint pid)
    {
        pid=0;if(window==IntPtr.Zero) return false;GetWindowThreadProcessId(window,out pid);
        var process=OpenProcess(0x1000,false,pid);if(process==IntPtr.Zero) return false;
        try {
            uint length=512;var family=new StringBuilder(512);
            if(GetPackageFamilyName(process,ref length,family)==0 && family.ToString() is "Microsoft.ScreenSketch_8wekyb3d8bbwe" or "MicrosoftWindows.Client.CBS_cw5n1h2txyewy") return true;
            var path=new StringBuilder(1024);length=1024;
            if(!QueryFullProcessImageName(process,0,path,ref length)) return false;
            var system=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            return path.ToString().Equals(Path.Combine(system,"System32","SnippingTool.exe"),StringComparison.OrdinalIgnoreCase)
                || path.ToString().Equals(Path.Combine(system,"SystemApps","MicrosoftWindows.Client.CBS_cw5n1h2txyewy","ScreenClippingHost.exe"),StringComparison.OrdinalIgnoreCase);
        } finally { CloseHandle(process); }
    }
    void RemoveKeyboard() { var hook=Interlocked.Exchange(ref keyboardHook,IntPtr.Zero);if(hook!=IntPtr.Zero) UnhookWindowsHookEx(hook); }
    void Trace(string message) { if(diagnostics!=null) ThreadPool.QueueUserWorkItem(_=>{try{diagnostics(message);}catch{}}); }
    public void Dispose() { lock(sourceGate) { if(disposed)return;disposed=true;expiry.Dispose();session.Cancel();RemoveKeyboard();trustedSequences.Clear();if(threadId!=0)PostThreadMessage(threadId,0x12,IntPtr.Zero,IntPtr.Zero); } thread.Join(1000); }
    delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
    [StructLayout(LayoutKind.Sequential)] struct Point32 { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] struct Rect32 { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MouseData { public Point32 Point;public uint Data,Flags,Time;public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct Message { public IntPtr Hwnd;public uint Id;public UIntPtr W;public IntPtr L;public uint Time;public Point32 Point;public uint Private; }
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,HookProc proc,IntPtr module,uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point32 point);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Rect32 rect);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder text,int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern int GetPackageFamilyName(IntPtr process,ref uint count,StringBuilder name);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder name,ref uint length);
    [DllImport("user32.dll")] static extern bool PeekMessage(out Message message,IntPtr window,uint first,uint last,uint remove);
    [DllImport("user32.dll")] static extern int GetMessage(out Message message,IntPtr window,uint first,uint last);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref Message message);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread,uint message,IntPtr w,IntPtr l);
}
