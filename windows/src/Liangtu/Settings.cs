using System.Text.Json;
namespace Liangtu;
public sealed class Settings
{
    public bool ClipboardImages
    {
        get; set;
    }
    public bool HandleScreenshots
    {
        get; set;
    }
    public bool HoverReveal { get; set; } = true;
    public bool NewReveal { get; set; } = true;
    public bool GameDoNotDisturb { get; set; }
    public bool OverFullscreen
    {
        get; set;
    }
    public int FullscreenProtectionRevision { get; set; }
    public bool Sounds { get; set; } = true;
    public bool ConfirmBeforeDelete { get; set; } = true;
    public bool Welcomed
    {
        get; set;
    }
    public double Scale { get; set; } = 1;
    public int Keep
    {
        get; set;
    }
    public uint HotkeyModifiers { get; set; } = 3;
    public uint HotkeyKey { get; set; } = 0x54;
    public uint CaptureHotkeyModifiers { get; set; } = 3;
    public uint CaptureHotkeyKey { get; set; } = 0x53;
    public string AnnotationColor { get; set; } = "#E24D40";
    public string TextFontFamily { get; set; } = "Microsoft YaHei UI";
    public double TextFontSize { get; set; } = 28;
    public bool TextBold { get; set; }
    public bool TextItalic { get; set; }
    public int[] AnnotationCustomColors { get; set; } = Enumerable.Repeat(0xFFFFFF, 16).ToArray();
    public List<string> ExtraFolders { get; set; } = new();
    public List<string> Items { get; set; } = new();
    public List<string> Collection { get; set; } = new();
    public int CollectionRevision { get; set; }
    public bool AutoHang { get; set; } = true;
    public string ThemeId { get; set; } = "system";
    public bool IncludePreviewUpdates { get; set; }
    public static string Root { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Liangtu");
    public static string Inbox => Path.Combine(Root, "Inbox");
    public static Settings Load()
    {
        Directory.CreateDirectory(Inbox);
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(Root, "settings.json"))) ?? new();
            if (!new[] { .8, 1.0, 1.35 }.Contains(settings.Scale))
                settings.Scale = 1;
            if (!new[] { 0, 25, 50, 100 }.Contains(settings.Keep))
                settings.Keep = 0;
            settings.Items ??= new();
            settings.Collection ??= new();
            if (!ThemeProfiles.Valid(settings.ThemeId)) settings.ThemeId = "system";
            settings.ExtraFolders ??= new();
            if (settings.FullscreenProtectionRevision < 1) {
                if (settings.OverFullscreen) Log("Fullscreen protection upgrade: automatic display over fullscreen disabled; manual reveal remains available.");
                settings.OverFullscreen = false;
                settings.FullscreenProtectionRevision = 1;
            }
            return settings;
        }
        catch { return new() { FullscreenProtectionRevision = 1 }; }
    }
    public void Save()
    {
        Directory.CreateDirectory(Root);
        var path = Path.Combine(Root, "settings.json");
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, path, true);
    }
    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(Path.Combine(Root, "app.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
        }
        catch { }
    }
}

