using System.Globalization;
using System.Text.RegularExpressions;
namespace Liangtu;
public static class Localization
{
    static readonly Dictionary<string, Dictionary<string, string>> languages = Read();
    static Dictionary<string, Dictionary<string, string>> Read()
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        var root = Path.Combine(AppContext.BaseDirectory, "Languages");
        if (!Directory.Exists(root))
            return result;
        foreach (var path in Directory.GetFiles(root, "*.strings", SearchOption.AllDirectories))
        {
            var language = Path.GetFileName(Path.GetDirectoryName(path)!).Replace(".lproj", "");
            var map = new Dictionary<string, string>();
            foreach (Match m in Regex.Matches(File.ReadAllText(path), "\"((?:\\\\.|[^\"])*)\"\\s*=\\s*\"((?:\\\\.|[^\"])*)\"\\s*;"))
                map[Regex.Unescape(m.Groups[1].Value)] = Regex.Unescape(m.Groups[2].Value);
            result[language] = map;
        }
        return result;
    }
    public static string Text(string key, string? language = null)
    {
        language ??= CultureInfo.CurrentUICulture.Name;
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            language = "zh-Hans";
        if (!languages.TryGetValue(language, out var map))
            languages.TryGetValue(language.Split('-')[0], out map);
        var value = map != null && map.TryGetValue(key, out var translated) ? translated : key;
        return value.Replace("Tendedero", "晾图");
    }
    public static string FromChinese(string value)
    {
        if (languages.TryGetValue("zh-Hans", out var zh))
        {
            var key = zh.FirstOrDefault(pair => pair.Value == value).Key;
            if (key != null)
                return Text(key);
        }
        return value;
    }
    public static int LanguageCount => languages.Count;
}
