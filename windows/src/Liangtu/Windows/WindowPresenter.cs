using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace Liangtu;

internal static class WindowPresenter
{
    // Only called for explicit user actions, never for background screenshot arrival.
    public static void Show(Window window, System.Windows.Forms.Screen screen)
    {
        var desiredWidth = window.Width; var desiredHeight = window.Height;
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.ShowActivated = true;
        void Place()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var scale = Native.GetDpiForWindow(hwnd) / 96d;
            var area = ScreenPixels.WindowBounds(screen.WorkingArea, desiredWidth, desiredHeight, Native.Dpi(screen));
            Native.SetWindowPos(hwnd, IntPtr.Zero, area.X, area.Y, area.Width, area.Height, 0x14);
            scale = Native.GetDpiForWindow(hwnd) / 96d;
            if (scale <= 0) scale = Native.Dpi(screen);
            area = ScreenPixels.WindowBounds(screen.WorkingArea, desiredWidth, desiredHeight, scale);
            window.MinWidth = Math.Min(window.MinWidth, area.Width / scale); window.MinHeight = Math.Min(window.MinHeight, area.Height / scale);
            window.Width = area.Width / scale; window.Height = area.Height / scale;
            Native.SetWindowPos(hwnd, IntPtr.Zero, area.X, area.Y, area.Width, area.Height, 0x14);
        }
        window.SourceInitialized += (_, _) => Place();
        window.Loaded += (_, _) => {
            Place();
            var hwnd = new WindowInteropHelper(window).Handle;
            bool activated = SetForegroundWindow(hwnd); window.Activate();
            // Briefly raise above the nonactivating overlay; never stay always-on-top.
            window.Topmost = true;
            window.Dispatcher.BeginInvoke(() => { if (window.IsVisible) { window.Topmost = false; window.Activate(); } }, DispatcherPriority.ApplicationIdle);
            Settings.Log($"User window title={window.Title}, screen={screen.DeviceName}, dpi={Native.GetDpiForWindow(hwnd)}, foregroundRequest={activated}");
        };
        window.Show();
    }
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
}
