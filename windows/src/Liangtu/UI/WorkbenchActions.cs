using Liangtu.Updates;
namespace Liangtu;

public sealed partial class App
{
    ThemeWindow? appearanceWindow;
    UpdateWindow? updateWindow;
    public void SetTheme(string id)
    {
        Settings.ThemeId = ThemeProfiles.Valid(id) ? id : "system";
        Theme.Select(Settings.ThemeId); Settings.Save(); Line?.Refresh(); UpdateTray();
    }
    public void ShowThemes()
    {
        if (WorkBlocked) return;
        if (appearanceWindow != null) { appearanceWindow.Activate(); return; }
        Line.Retract(); appearanceWindow = new ThemeWindow(this); appearanceWindow.Closed += (_, _) => appearanceWindow = null; WindowPresenter.Show(appearanceWindow, Line.Screen);
    }
    public void ShowUpdates()
    {
        if (WorkBlocked) return;
        if (updateWindow != null) { updateWindow.Activate(); return; }
        Line.Retract(); updateWindow = new UpdateWindow(ApplyPreparedUpdate, Settings.IncludePreviewUpdates, value => { Settings.IncludePreviewUpdates = value; Settings.Save(); });
        updateWindow.Closed += (_, _) => updateWindow = null; WindowPresenter.Show(updateWindow, Line.Screen);
    }
    Task<bool> ApplyPreparedUpdate(PreparedUpdate prepared) => ApplyPreparedUpdateCore(() => Task.Run(prepared.LaunchAfterExit), Shutdown);
    internal async Task<bool> ApplyPreparedUpdateCore(Func<Task> launchAfterExit, Action shutdown)
    {
        if (WorkBlocked) return false;
        updateHandoff = true; ++captureGeneration;
        var disabled = new List<(Window window, bool enabled)>();
        try {
            foreach (var window in Windows.Cast<Window>().Where(w => w is EditorWindow or TrimWindow).ToArray()) {
                if (window is TrimWindow { HasPendingSelection: true } && MessageBox.Show(window, "视频编辑内容尚未保存。继续升级会放弃标注和裁剪选区，原视频保持不变。", "更新前确认", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) != MessageBoxResult.OK) return false;
                window.Close(); if (Windows.Cast<Window>().Contains(window)) return false;
            }
            foreach (Window window in Windows) { disabled.Add((window, window.IsEnabled)); window.IsEnabled = false; }
            if (!await Captures.Settle()) throw new InvalidOperationException("截图尚未保存，更新已暂缓。截图仍保留在内存中，请检查保存位置或磁盘空间后重试。");
            if (!await Store.FinishCopyBeforeExit()) throw new InvalidOperationException("剪贴板仍被占用，更新已暂缓，请完成粘贴后重试。");
            Store.Persist(); Settings.Save();
            await launchAfterExit();
            exiting = true; shutdown(); return true;
        } finally {
            updateHandoff = false;
            if (!exiting) {
                Store.ResumeAfterCancelledExit();
                foreach (var (window, enabled) in disabled) if (Windows.Cast<Window>().Contains(window)) window.IsEnabled = enabled;
            }
        }
    }
}
