namespace Liangtu;

internal enum RevealTrigger { Automatic, UserAction, Hotkey }
internal static class PresentationPolicy
{
    public static bool CanReveal(Settings settings, RevealTrigger trigger) => !settings.GameDoNotDisturb || trigger == RevealTrigger.Hotkey;
    public static bool CanPlaySound(Settings settings, bool testMode) => settings.Sounds && !settings.GameDoNotDisturb && !testMode;
    public static bool CanNotify(Settings settings) => !settings.GameDoNotDisturb;
    public static bool CanCapture(Settings settings) => !settings.GameDoNotDisturb;
}
