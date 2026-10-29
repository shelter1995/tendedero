namespace Liangtu;

public sealed partial class App
{
    internal Window CreateAboutDialog()
    {
        var window = new Window { Title = "关于晾图", Width = 480, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
        Theme.WindowStyle(window);
        var panel = new StackPanel { Margin = new Thickness(28) };
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 24) };
        header.Children.Add(new Image { Source = Theme.AppImage, Width = 64, Height = 64, Margin = new Thickness(0, 0, 18, 0) });
        var heading = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        heading.Children.Add(Theme.Text("晾图", 28));
        heading.Children.Add(Theme.Text("截图，挂起来晾着。", 13, Theme.Muted));
        header.Children.Add(heading); panel.Children.Add(header);
        panel.Children.Add(Theme.Text($"版本 {typeof(App).Assembly.GetName().Version?.ToString(3)}  ·  Windows", 14));
        var description = Theme.Text("让截图留在手边。复制、标注、拖拽分享，\n都在屏幕上方的一根绳子上。", 13, Theme.Muted);
        description.LineHeight = 24; description.Margin = new Thickness(0, 12, 0, 18); panel.Children.Add(description);
        panel.Children.Add(new Border { Height = 1, Background = Theme.Outline, Margin = new Thickness(0, 0, 0, 18) });
        var credits = Theme.Text("基于 Alejandro Buján 的 Tendedero v1.6.0\nWindows 个人移植版 · MIT 许可\n图片本地处理，无账户或遥测；手动检查 Windows 更新。", 12, Theme.Muted);
        credits.LineHeight = 22; panel.Children.Add(credits);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };
        actions.Children.Add(Theme.Button("使用说明", () => { var readme = Path.Combine(AppContext.BaseDirectory, "README.md"); Theme.Shell(File.Exists(readme) ? readme : Path.Combine(AppContext.BaseDirectory, "使用说明.txt")); }));
        var close = Theme.Primary("关闭", window.Close); close.IsCancel = true; actions.Children.Add(close);
        panel.Children.Add(actions); window.Content = panel;
        return window;
    }
    void ShowAbout() => CreateAboutDialog().ShowDialog();

    internal Window CreateShortcutDialog(bool capture)
    {
        var window = new Window { Title = "快捷键设置", Width = 500, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
        Theme.WindowStyle(window);
        uint candidateKey = capture ? Settings.CaptureHotkeyKey : Settings.HotkeyKey;
        uint candidateMods = capture ? Settings.CaptureHotkeyModifiers : Settings.HotkeyModifiers;
        bool shortcutsSuspended = false;
        var panel = new StackPanel { Margin = new Thickness(28) };
        panel.Children.Add(Theme.Text(capture ? "框选截图" : "显示 / 隐藏晾衣绳", 22));
        var hint = Theme.Text("点击下方输入框，然后按下希望使用的组合键。", 13, Theme.Muted);
        hint.Margin = new Thickness(0, 10, 0, 20); panel.Children.Add(hint);
        var input = new TextBox { IsReadOnly = true, Text = ShortcutLabel(candidateMods, candidateKey), FontSize = 20, Padding = new Thickness(16), HorizontalContentAlignment = HorizontalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(input, "录制快捷键");
        panel.Children.Add(input);
        var error = Theme.Text("可使用 Ctrl / Alt / Win 组合键，或 F1–F24。", 12, Theme.Muted);
        error.MinHeight = 42; error.Margin = new Thickness(0, 12, 0, 4); panel.Children.Add(error);
        var secondary = new StackPanel { Orientation = Orientation.Horizontal };
        void Refresh() { input.Text = ShortcutLabel(candidateMods, candidateKey); error.Text = "点击保存后生效。Esc 取消，不更改原设置。"; error.Foreground = Theme.Muted; }
        secondary.Children.Add(Theme.Button("清除快捷键", () => { candidateKey = candidateMods = 0; Refresh(); }));
        secondary.Children.Add(Theme.Button("恢复默认", () => { candidateKey = capture ? 0x53u : 0x54u; candidateMods = 3; Refresh(); }));
        panel.Children.Add(secondary);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        var cancel = Theme.Button("取消", window.Close); cancel.IsCancel = true; actions.Children.Add(cancel);
        var save = Theme.Primary("保存", () => {
            uint otherKey = capture ? Settings.HotkeyKey : Settings.CaptureHotkeyKey;
            uint otherMods = capture ? Settings.HotkeyModifiers : Settings.CaptureHotkeyModifiers;
            if (candidateKey != 0 && candidateKey == otherKey && candidateMods == otherMods) { error.Text = "两项操作不能使用相同的快捷键。"; error.Foreground = Theme.Brush("#C74338"); return; }
            uint oldKey = capture ? Settings.CaptureHotkeyKey : Settings.HotkeyKey;
            uint oldMods = capture ? Settings.CaptureHotkeyModifiers : Settings.HotkeyModifiers;
            void Assign(uint key, uint mods) { if (capture) { Settings.CaptureHotkeyKey = key; Settings.CaptureHotkeyModifiers = mods; } else { Settings.HotkeyKey = key; Settings.HotkeyModifiers = mods; } }
            Assign(candidateKey, candidateMods);
            if (!RegisterShortcut(capture)) { Assign(oldKey, oldMods); error.Text = "该快捷键已被其他应用占用，请换一个组合。"; error.Foreground = Theme.Brush("#C74338"); return; }
            try { SaveSettings(); window.Close(); }
            catch (Exception e) { Assign(oldKey, oldMods); RegisterShortcut(capture); error.Text = "未能保存设置：" + e.Message; }
        });
        save.IsDefault = true; actions.Children.Add(save); panel.Children.Add(actions); window.Content = panel;
        input.PreviewKeyDown += (_, e) => {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key is Key.Tab or Key.Escape or Key.Enter) return;
            e.Handled = true;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
            var mods = Keyboard.Modifiers;
            uint flags = (mods.HasFlag(ModifierKeys.Control) ? 2u : 0) | (mods.HasFlag(ModifierKeys.Alt) ? 1u : 0) | (mods.HasFlag(ModifierKeys.Shift) ? 4u : 0) | (mods.HasFlag(ModifierKeys.Windows) ? 8u : 0);
            if (key == Key.Delete && flags == 0) { candidateKey = candidateMods = 0; Refresh(); return; }
            if ((flags & 11) == 0 && !(key >= Key.F1 && key <= Key.F24)) { error.Text = "请同时按住 Ctrl、Alt 或 Win，或使用功能键。"; return; }
            candidateKey = (uint)KeyInterop.VirtualKeyFromKey(key); candidateMods = flags; Refresh();
        };
        window.Loaded += (_, _) => { shortcutsSuspended = true; Native.UnregisterHotKey(Line.Hwnd, 1); Native.UnregisterHotKey(Line.Hwnd, 2); input.Focus(); };
        window.Closed += (_, _) => { if (shortcutsSuspended) { RegisterShortcut(); RegisterShortcut(true); } };
        return window;
    }
}
