namespace Liangtu;

internal readonly record struct DeleteDecision(bool Confirmed, bool DoNotAskAgain);

// One decision and one success cue for every user operation, including a batch.
internal static class DeleteOperation
{
    internal static int Execute(Settings settings, Photo[] photos, Func<string, DeleteDecision> ask,
        Func<Photo, bool> recycle, Action saveSettings, Action playCue)
    {
        if (photos.Length == 0) return 0;
        if (settings.ConfirmBeforeDelete) {
            var text = photos.Length == 1
                ? "将“" + Path.GetFileName(photos[0].Path) + "”移到回收站？\n原文件将从当前位置移走，可从系统回收站恢复。"
                : $"将所选 {photos.Length} 个原文件移到回收站？\n可从系统回收站恢复。只想清空绳子时，请使用“取下所选”。";
            var decision = ask(text);
            if (!decision.Confirmed) return 0;
            if (decision.DoNotAskAgain) { settings.ConfirmBeforeDelete = false; saveSettings(); }
        }
        var count = 0;
        foreach (var photo in photos) if (recycle(photo)) count++;
        if (count > 0) playCue();
        return count;
    }
}
