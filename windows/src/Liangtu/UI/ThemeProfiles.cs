namespace Liangtu;

public sealed record ThemeProfile(string Id, string Name, string Description, bool Dark, string Material, double FrameRadius, double ShadowOpacity, IReadOnlyDictionary<string, string> Colors)
{
    public Color Color(string name) => (Color)ColorConverter.ConvertFromString(Colors[name]);
    public double ControlRadius => Id switch { "porcelain" => 6, "darkroom" => 1, "linen" => 8, "color-play" => 12, _ => Material == "woven" ? 4 : Material == "metal" ? 5 : 7 };
    public double ToolRadius => Id switch { "porcelain" => 4, "darkroom" => 0, "linen" => 7, "color-play" => 9, _ => Material == "woven" ? 3 : 5 };
    public Thickness PhotoMat => Id switch { "porcelain" => new(6, 6, 6, 12), "darkroom" => new(10, 7, 10, 15), "linen" => new(7, 7, 7, 21), "color-play" => new(8, 8, 12, 14), _ => new(4) };
    public double BasketArtWidth => Id is "linen" or "color-play" ? 104 : 72;
    public double BasketArtHeight => Id is "linen" or "color-play" ? 123.5 : 86;
    public double BasketButtonWidth => BasketArtWidth + 8;
    public double PhotoImageRadius => Id=="color-play" ? 4 : Math.Max(0,FrameRadius-4);
    public bool OriginalArtwork => Id is "porcelain" or "darkroom" or "linen" or "color-play";
}

public static class ThemeProfiles
{
    public static readonly ThemeProfile Sky = new("sky", "晴空", "冷白帆布 · 珊瑚夹子 · 清透蓝灰", false, "canvas", 13, .12, Palette(
        "#F2F6FA", "#FFFFFF", "#203344", "#596F82", "#28699A", "#D5E0E9", "#E3EFF8", "#DDE8F0",
        "#6A8295", "#F28D70", "#C3654C", "#E27858", "#FFE2C9", "#EAF3F7", "#779AAC", "#B8D2E1", "#435F72", "#93ADBE", "#CADCE7", "#FFFFFF", "#EAF7FBFE", "#9CB6C8", "#F7FAFC"));
    public static readonly ThemeProfile Paper = new("paper", "纸与木", "暖纸肌理 · 橡木夹子 · 编织收纳", false, "woven", 7, .09, Palette(
        "#F3EEE5", "#FFFCF6", "#42392F", "#746858", "#996342", "#DCD1BF", "#EADAC5", "#E5DDCF",
        "#A18C70", "#D9AA70", "#946A3E", "#BD8B53", "#F1D7B4", "#E9DBC2", "#9B8365", "#B99870", "#6F5841", "#B89B78", "#CCB593", "#FAF0DB", "#FFFFFAF1", "#C6B49A", "#FFF9EE"));
    public static readonly ThemeProfile Night = new("night", "夜航", "石墨金属 · 冷青细节 · 安静工作台", true, "metal", 9, .2, Palette(
        "#151E29", "#202D3B", "#E6EFF6", "#9BAFBE", "#9CCAD7", "#3B4D5E", "#304B5F", "#101923",
        "#879BAA", "#ABBCC8", "#46596B", "#758C9F", "#E8F2F8", "#26394A", "#6C8598", "#547286", "#101E2B", "#91A9B9", "#425C70", "#92AABD", "#FF273A4B", "#6E8DA4", "#273A4B"));
    public static readonly ThemeProfile Porcelain = new("porcelain", "白瓷工作室", "白釉收纳 · 银夹 · 精细描边", false, "porcelain", 6, .08, Palette(
        "#EEF3F7", "#FFFFFF", "#243747", "#526679", "#2C6586", "#C5D5DF", "#DCEBF3", "#E4EDF2",
        "#A0B4C4", "#F3F7FA", "#6F879C", "#ADBECD", "#FFFFFF", "#EDF4F8", "#7894AA", "#BACFDD", "#71899A", "#91A7B9", "#C6D8E3", "#FFFFFF", "#FFFAFCFE", "#A6BDCD", "#FFFFFF"));
    public static readonly ThemeProfile Darkroom = new("darkroom", "暗房实验室", "冲孔钢篮 · 金属夹 · 底片相框", true, "perforated", 1, .13, Palette(
        "#151618", "#202124", "#F0EEE9", "#B6B4AD", "#F3A453", "#4D4D50", "#473522", "#101113",
        "#73777D", "#9DA2A9", "#35383E", "#555B65", "#E3E6EB", "#393C42", "#767C85", "#5A6069", "#17191D", "#B3BAC4", "#606670", "#A4ABB4", "#FF1D1F22", "#64666B", "#2B2C30"));
    public static readonly ThemeProfile Linen = new("linen", "棉麻画室", "藤编篮 · 木夹 · 棉纸留白", false, "wicker", 2, .09, Palette(
        "#F3EADF", "#FFF9F0", "#493A2D", "#76624F", "#AF573F", "#DDCCB9", "#F0DCCC", "#EDE3D6",
        "#B4936B", "#D7A368", "#876039", "#AA7747", "#F3D7B0", "#C59864", "#795332", "#BC8B53", "#62472F", "#815438", "#976D43", "#E6C391", "#FFFFFAEE", "#C5B292", "#FFF8EC"));
    public static readonly ThemeProfile ColorPlay = new("color-play", "柔彩积木", "软胶桶 · 圆头夹 · 叠色相框", false, "silicone", 12, .07, Palette(
        "#F1F3F6", "#FFFFFF", "#293F42", "#566E70", "#397565", "#D8DFE4", "#E0EFE8", "#F7F8FA",
        "#B3B7A5", "#A6CABB", "#648878", "#7FA391", "#E1F2E9", "#9BB8A0", "#5C7F68", "#C4DAC4", "#5E7C64", "#EEDBA4", "#8AA98F", "#E7F2DF", "#FFFFFFFF", "#BCD0CF", "#FFFFFF"));
    public static IReadOnlyList<ThemeProfile> All { get; } = new[] { Porcelain, Darkroom, Linen, ColorPlay, Sky, Paper, Night };
    public static ThemeProfile Resolve(string? id, bool systemDark) => All.FirstOrDefault(p => p.Id == id) ?? (systemDark ? Night : Sky);
    public static bool Valid(string? id) => id == "system" || All.Any(p => p.Id == id);
    static IReadOnlyDictionary<string, string> Palette(string window, string control, string ink, string muted, string accent, string outline, string selection, string canvas,
        string rope, string pegFace, string pegEdge, string pegBack, string pegHighlight, string basketFill, string basketEdge, string basketTrim, string basketInside, string strap, string weave, string basketHighlight, string frameFill, string frameEdge, string actionFill)
        => new Dictionary<string, string> {
            ["WindowSurface"]=window,["ControlSurface"]=control,["Ink"]=ink,["Muted"]=muted,["Accent"]=accent,["Outline"]=outline,["Selection"]=selection,["Hover"]=selection,["CanvasSurface"]=canvas,
            ["Rope"]=rope,["PegFace"]=pegFace,["PegEdge"]=pegEdge,["PegBack"]=pegBack,["PegSide"]=pegEdge,["PegHighlight"]=pegHighlight,
            ["BasketFill"]=basketFill,["BasketEdge"]=basketEdge,["BasketTrim"]=basketTrim,["BasketInside"]=basketInside,["BasketStrap"]=strap,["BasketWeave"]=weave,["BasketHighlight"]=basketHighlight,
            ["FrameFill"]=frameFill,["FrameEdge"]=frameEdge,["ActionFill"]=actionFill,["ActionInk"]=ink,["SelectionInk"]=accent,["Surface"]=control,
            ["OnAccent"]=((Color)ColorConverter.ConvertFromString(window)).R < 128 ? window : "#FFFFFF"
        };
}
