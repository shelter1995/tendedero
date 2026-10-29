using Microsoft.Win32;
namespace Liangtu;
public sealed partial class App
{
    ContextMenu Menu()
    {
        var m = new ContextMenu();
        return m;
    }
    static MenuItem Item(string text, Action action, bool? check = null, bool enabled = true)
    {
        var i = new MenuItem { Header = Localization.FromChinese(text), IsEnabled = enabled };
        if (check != null)
        {
            i.IsCheckable = true;
            i.IsChecked = check.Value;
        }
        i.Click += (_, e) => { action(); e.Handled = true; };
        return i;
    }
    static MenuItem Sub(string text) => new() { Header = Localization.FromChinese(text) };
    public ContextMenu PhotoMenu(Photo p)
    {
        var m = Menu();
        m.Items.Add(Item("复制", async () => { try { await Store.Copy(p); } catch { } }));
        m.Items.Add(Item("打开", () => Open(p)));
        m.Items.Add(Item("快速查看", () => Preview(p)));
        m.Items.Add(Item(p.Video ? "视频裁剪" : "图片标注", () => Edit(p)));
        if (!p.Video) m.Items.Add(Item("钉在桌面", () => Pin(p), enabled: !Settings.GameDoNotDisturb));
        m.Items.Add(Item("在资源管理器中显示", () => Theme.RevealFile(p.Path)));
        m.Items.Add(Item("保存副本…", () => SaveCopy(p)));
        m.Items.Add(new Separator());
        if (Store.Items.Contains(p)) m.Items.Add(Item("取下放篮子", () => Store.TakeDown(p)));
        else m.Items.Add(Item("挂回晾衣绳", () => ReturnToLine(p)));
        m.Items.Add(Item(Settings.ConfirmBeforeDelete ? "移到回收站…" : "移到回收站", () => DeletePhoto(p)));
        var index = Store.Items.IndexOf(p);
        m.Items.Add(Item("取下左侧全部", () => { _ = Store.TakeDownSide(p, true); }, enabled: index > 0));
        m.Items.Add(Item("取下右侧全部", () => { _ = Store.TakeDownSide(p, false); }, enabled: index < Store.Items.Count - 1));
        return m;
    }
    public ContextMenu CreateTrayMenu()
    {
        var m = Menu();
        m.Items.Add(Item("游戏勿扰模式", () => SetGameDoNotDisturb(!Settings.GameDoNotDisturb), Settings.GameDoNotDisturb));
        m.Items.Add(Item("图片篮子…", ShowBasket));
        m.Items.Add(Item(Line.Revealed ? "隐藏晾衣绳" : Settings.GameDoNotDisturb ? "显示晾衣绳（勿扰中仅限快捷键）" : "显示晾衣绳", Line.Toggle, enabled: !Settings.GameDoNotDisturb || Line.Revealed));
        m.Items.Add(Item("全部取下放篮子", () => { _ = Store.Clear(); }, enabled: Store.Items.Count > 0));
        m.Items.Add(Item("自动挂上新截图", () => { Settings.AutoHang = !Settings.AutoHang; SaveSettings(); }, Settings.AutoHang));
        m.Items.Add(new Separator());
        m.Items.Add(Item("接管截图", () => { Settings.HandleScreenshots = !Settings.HandleScreenshots; SaveSettings(true); }, Settings.HandleScreenshots));
        m.Items.Add(Item("挂上复制的图片", () => { Settings.ClipboardImages = !Settings.ClipboardImages; SaveSettings(); }, Settings.ClipboardImages));
        var bring = Sub("放下晾衣绳");
        bring.Items.Add(Item("指针停在屏幕顶边时", () => { Settings.HoverReveal = !Settings.HoverReveal; SaveSettings(); }, Settings.HoverReveal, !Settings.GameDoNotDisturb));
        bring.Items.Add(Item("有新内容挂上时", () => { Settings.NewReveal = !Settings.NewReveal; SaveSettings(); }, Settings.NewReveal, !Settings.GameDoNotDisturb));
        bring.Items.Add(Item("允许在全屏应用上自动弹出（含游戏）", () => { Settings.OverFullscreen = !Settings.OverFullscreen; SaveSettings(); }, Settings.OverFullscreen, !Settings.GameDoNotDisturb));
        m.Items.Add(bring);
        var folders = Sub("同时监视文件夹");
        foreach (var folder in Settings.ExtraFolders)
            folders.Items.Add(Item(System.IO.Path.GetFileName(folder), () => Theme.Shell(folder)));
        folders.Items.Add(Item("添加文件夹…", () => { using var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "选择要挂上截图和录屏的文件夹", UseDescriptionForTitle = true }; if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && !Settings.ExtraFolders.Contains(dlg.SelectedPath, StringComparer.OrdinalIgnoreCase)) { Settings.ExtraFolders.Add(dlg.SelectedPath); SaveSettings(true); } }, enabled: Settings.ExtraFolders.Count < 3));
        if (Settings.ExtraFolders.Count > 0)
        {
            var stop = Sub("停止监视");
            foreach (var folder in Settings.ExtraFolders.ToArray())
                stop.Items.Add(Item(System.IO.Path.GetFileName(folder), () => { Settings.ExtraFolders.Remove(folder); SaveSettings(true); }));
            folders.Items.Add(stop);
        }
        m.Items.Add(folders);
        var keep = Sub("绳上保留");
        foreach (var n in new[] { 0, 25, 50, 100 })
            keep.Items.Add(Item(n == 0 ? "放得下的数量" : n + " 张", async () => { Settings.Keep = n; await Store.ApplyKeepLimit(); Line.ResetScroll(); SaveSettings(); }, Settings.Keep == n));
        m.Items.Add(keep);
        var sizes = Sub("大小");
        foreach (var pair in new[] { ("小", .8), ("中", 1.0), ("大", 1.35) })
            sizes.Items.Add(Item(pair.Item1, () => { Settings.Scale = pair.Item2; SaveSettings(); Line.MoveTo(Line.Screen); }, Settings.Scale == pair.Item2));
        m.Items.Add(sizes);
        var shortcut = Sub("快捷键");
        shortcut.Items.Add(Item("显示 / 隐藏晾衣绳 · " + ShortcutText(), () => ChangeShortcut(false)));
        shortcut.Items.Add(Item("框选截图 · " + ShortcutText(true), () => ChangeShortcut(true)));
        m.Items.Add(shortcut);
        m.Items.Add(Item("打开截图文件夹", () => Theme.Shell(Settings.Inbox)));
        m.Items.Add(Item("框选截图", Capture, enabled: PresentationPolicy.CanCapture(Settings)));
        m.Items.Add(new Separator());
        m.Items.Add(Item(Settings.GameDoNotDisturb ? "声音（勿扰中暂停）" : "声音", () => { Settings.Sounds = !Settings.Sounds; if (!Settings.Sounds) sounds.Stop(); SaveSettings(); }, Settings.Sounds, !Settings.GameDoNotDisturb));
        m.Items.Add(Item("删除前确认", () => { Settings.ConfirmBeforeDelete = !Settings.ConfirmBeforeDelete; SaveSettings(); }, Settings.ConfirmBeforeDelete));
        m.Items.Add(Item("登录时打开", ToggleLogin, IsLogin()));
        m.Items.Add(Item("外观主题…", ShowThemes));
        var updates = Item("Windows 更新与升级…", ShowUpdates);
        updates.ToolTip = "检查晾图 Windows 版本，或导入本地升级包。";
        m.Items.Add(updates);
        m.Items.Add(Item("关于晾图 / 版本说明", ShowAbout));
        m.Items.Add(new Separator());
        m.Items.Add(Item("退出晾图", ExitAfterCopy));
        return m;
    }
    public void OpenTrayMenu() => NativeMenu.Show(CreateTrayMenu(), Line);
    string ShortcutText(bool capture = false) => ShortcutLabel(capture ? Settings.CaptureHotkeyModifiers : Settings.HotkeyModifiers, capture ? Settings.CaptureHotkeyKey : Settings.HotkeyKey);
    internal static string ShortcutLabel(uint mods, uint key) => key == 0 ? "未设置" :
        ((mods & 2) != 0 ? "Ctrl + " : "") + ((mods & 1) != 0 ? "Alt + " : "") + ((mods & 4) != 0 ? "Shift + " : "") + ((mods & 8) != 0 ? "Win + " : "") + KeyInterop.KeyFromVirtualKey((int)key);
    void ChangeShortcut(bool capture) => CreateShortcutDialog(capture).ShowDialog();
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    bool IsLogin()
    {
        if (TestMode)
            return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue("Liangtu") != null;
        }
        catch (System.Security.SecurityException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    void ToggleLogin()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (IsLogin())
            key.DeleteValue("Liangtu", false);
        else
            key.SetValue("Liangtu", "\"" + Environment.ProcessPath + "\"");
    }
}
static class FlagsExtension
{
    public static bool HasFlagValue(this uint value, uint flag) => (value & flag) != 0;
}





