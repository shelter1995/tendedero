using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Liangtu;

/// Uses an actual HMENU: Windows owns typography, checkmarks, submenus and input.
internal static class NativeMenu
{
    public static void Show(ContextMenu model, Window host)
    {
        var commands = new Dictionary<uint, MenuItem>();
        var menu = Build(model.Items, commands);
        uint selected = 0;
        Native.GetCursorPos(out var point);
        using var owner = new HwndSource(new HwndSourceParameters("Liangtu menu owner") { WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x80, PositionX = point.X, PositionY = point.Y, Width = 1, Height = 1 });
        var line = host as LineWindow;
        if (line != null) line.MenuOpen = true;
        try
        {
            SetForegroundWindow(owner.Handle);
            selected = TrackPopupMenu(menu, 0x100 | 0x80 | 0x2, point.X, point.Y, 0, owner.Handle, IntPtr.Zero);
            PostMessage(owner.Handle, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally { if (line != null) line.MenuOpen = false; DestroyMenu(menu); }
        // Dispatch only after the native tracking loop releases mouse capture.
        if (commands.TryGetValue(selected, out var item))
            host.Dispatcher.BeginInvoke(() => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item)));
    }
    internal static IntPtr Build(ItemCollection items, Dictionary<uint, MenuItem> commands)
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        try
        {
            foreach (var entry in items)
            {
                if (entry is Separator) { Append(menu, 0x800, UIntPtr.Zero, null); continue; }
                if (entry is not MenuItem item) continue;
                uint flags = (item.IsEnabled ? 0u : 3u) | (item.IsChecked ? 8u : 0u);
                var label = (item.Header?.ToString() ?? "").Replace("&", "&&");
                if (!string.IsNullOrEmpty(item.InputGestureText)) label += "\t" + item.InputGestureText;
                if (item.HasItems)
                {
                    var child = Build(item.Items, commands);
                    try { Append(menu, flags | 0x10, (UIntPtr)(nuint)child, label); }
                    catch { DestroyMenu(child); throw; }
                }
                else
                {
                    uint id = (uint)commands.Count + 1;
                    commands.Add(id, item);
                    Append(menu, flags, (UIntPtr)id, label);
                }
            }
            return menu;
        }
        catch { DestroyMenu(menu); throw; }
    }
    static void Append(IntPtr menu, uint flags, UIntPtr id, string? label)
    {
        if (!AppendMenu(menu, flags, id, label)) throw new System.ComponentModel.Win32Exception();
    }
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);
    [DllImport("user32.dll")] internal static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr owner, IntPtr rect);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
}
