using System.Runtime.InteropServices;
namespace Liangtu;

internal static class FullscreenWindows
{
    static readonly HashSet<string> ScreenshotProcesses = new(new[] { "SnippingTool", "ScreenClippingHost", "Snipaste", "ShareX", "Greenshot", "PicPick", "Lightshot" }, StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyList<ScreenWindow> Read()
    {
        var result = new List<ScreenWindow>(); var visited = new HashSet<IntPtr>();
        var screens = System.Windows.Forms.Screen.AllScreens;
        var name = new System.Text.StringBuilder(256);
        for (var handle = GetTopWindow(IntPtr.Zero); handle != IntPtr.Zero && visited.Count < 1024 && visited.Add(handle); handle = GetWindow(handle, 2))
        {
            if (!IsWindowVisible(handle) || IsIconic(handle)) continue;
            GetWindowThreadProcessId(handle, out var pid);
            if (pid == Environment.ProcessId) continue;
            var extendedStyle = Native.GetWindowLongPtr(handle, -20).ToInt64();
            if ((extendedStyle & 0x20) != 0) continue; // Click-through overlays are not the occupied surface.
            name.Clear(); GetClassName(handle, name, name.Capacity);
            if (name.ToString() is "WorkerW" or "Progman" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") continue;
            if (DwmGetWindowAttribute(handle, 14, out int cloaked, 4) == 0 && cloaked != 0) continue;
            if (!Native.GetWindowRect(handle, out var rect)) continue;
            if (DwmGetWindowAttribute(handle, 9, out Native.RECT visible, Marshal.SizeOf<Native.RECT>()) == 0) rect = visible;
            if (rect.Right <= rect.Left || rect.Bottom <= rect.Top) continue;
            var bounds = System.Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (screens.Any(screen => FullscreenPolicy.Covers(bounds, screen.Bounds)) && IsScreenshotOverlay(pid)) continue;
            bool transient = (extendedStyle & 0x80) != 0;
            result.Add(new ScreenWindow(bounds, transient));
        }
        return result;
    }
    static bool IsScreenshotOverlay(uint pid)
    {
        try { using var process = System.Diagnostics.Process.GetProcessById((int)pid); return IsScreenshotProcess(process.ProcessName); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }
    internal static bool IsScreenshotProcess(string name) => ScreenshotProcesses.Contains(name);
    [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr parent);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window, uint relation);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int count);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out Native.RECT value, int size);
}
