using System;
using System.Linq;
using System.Text.RegularExpressions;
namespace Liangtu.Updates;
internal sealed record UpdateVersion(int Major, int Minor, int Patch, string Prerelease = "") : IComparable<UpdateVersion>
{
    public static UpdateVersion Parse(string text)
    {
        var m = Regex.Match(text, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z.-]+)?$");
        if (!m.Success) throw new FormatException("无效的 Windows 版本号：" + text);
        return new(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), m.Groups[4].Value);
    }
    public int CompareTo(UpdateVersion? other)
    {
        if (other == null) return 1;
        var n = Major.CompareTo(other.Major); if (n != 0) return n;
        n = Minor.CompareTo(other.Minor); if (n != 0) return n;
        n = Patch.CompareTo(other.Patch); if (n != 0) return n;
        if (Prerelease == other.Prerelease) return 0;
        if (Prerelease.Length == 0) return 1;
        if (other.Prerelease.Length == 0) return -1;
        var a = Prerelease.Split('.'); var b = other.Prerelease.Split('.');
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++) {
            bool an = a[i].All(char.IsDigit), bn = b[i].All(char.IsDigit);
            n = an && bn ? a[i].TrimStart('0').Length.CompareTo(b[i].TrimStart('0').Length) : an != bn ? (an ? -1 : 1) : 0;
            if (n == 0) n = string.CompareOrdinal(a[i], b[i]);
            if (n != 0) return n;
        }
        return a.Length.CompareTo(b.Length);
    }
    public override string ToString() => $"{Major}.{Minor}.{Patch}" + (Prerelease.Length == 0 ? "" : "-" + Prerelease);
}
