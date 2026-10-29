using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace Liangtu.Updates;
internal sealed record UpdateAsset(string Name,string Url,long Size,string Sha256);
internal sealed record UpdateRelease(UpdateVersion Version,string Notes,UpdateAsset Asset);
internal sealed record StagedPackage(string Directory,UpdateVersion Version,string Kind,string? InstallerFile);
internal sealed class UpdateService(HttpClient client)
{
    static readonly HashSet<string> RuntimeCultures=new(CultureInfo.GetCultures(CultureTypes.AllCultures).Select(c=>c.Name),StringComparer.OrdinalIgnoreCase);
    public const string ReleasesUrl = "https://github.com/shelter1995/tendedero/releases";
    public static UpdateVersion CurrentVersion => UpdateVersion.Parse(typeof(UpdateService).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0");
    public static bool Installed => File.Exists(Path.Combine(AppContext.BaseDirectory,"unins000.exe"));
    public static HttpClient CreateHttpClient() { var client=new HttpClient { Timeout=TimeSpan.FromMinutes(20) }; client.DefaultRequestHeaders.UserAgent.ParseAdd("Liangtu-Windows-Updater/1.0"); client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json"); return client; }
    public async Task<UpdateRelease?> CheckAsync(UpdateVersion current,bool prerelease,bool installed,CancellationToken token)
    {
        using var response=await client.GetAsync("https://api.github.com/repos/shelter1995/tendedero/releases?per_page=100",token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return SelectRelease(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false),current,prerelease,installed);
    }
    public static UpdateRelease? SelectRelease(string json,UpdateVersion current,bool includePrerelease,bool installed)
    {
        using var doc=JsonDocument.Parse(json); if(doc.RootElement.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("更新源返回了无效的版本列表。");
        var found=new List<UpdateRelease>();
        foreach(var r in doc.RootElement.EnumerateArray()) {
            var tag=r.GetProperty("tag_name").GetString()??"";
            if(!tag.StartsWith("windows-v",StringComparison.Ordinal)||r.GetProperty("draft").GetBoolean())continue;
            UpdateVersion version; try{version=UpdateVersion.Parse(tag[9..]);}catch(FormatException){continue;}
            if((r.GetProperty("prerelease").GetBoolean()||version.Prerelease.Length>0)&&!includePrerelease || version.CompareTo(current)<=0)continue;
            var versionName=Regex.Escape(version.ToString());
            var pattern=installed ? "^Liangtu-"+versionName+@"-Setup-x64(?:-r\d+)?\.exe$" : "^Liangtu-"+versionName+@"-Windows-x64-portable\.zip$";
            foreach(var a in r.GetProperty("assets").EnumerateArray()) {
                var name=a.GetProperty("name").GetString()??""; if(!Regex.IsMatch(name,pattern,RegexOptions.CultureInvariant))continue;
                var url=a.GetProperty("browser_download_url").GetString()??""; var digest=a.TryGetProperty("digest",out var d)?d.GetString()??"":"";
                // Missing checksums are a release error, never silently claim that the app is up to date.
                if(!digest.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)||!Regex.IsMatch(digest[7..],"^[0-9a-fA-F]{64}$"))throw new InvalidDataException("发现新版，但发布资产缺少 GitHub SHA256。请联系发布者修复后重试。");
                ValidateUrl(url); var size=a.GetProperty("size").GetInt64(); if(size<=0||size>2L*1024*1024*1024)throw new InvalidDataException("发布包大小无效。");
                found.Add(new(version,r.TryGetProperty("body",out var b)?b.GetString()??"":"",new(name,url,size,digest[7..])));
            }
        }
        return found.OrderByDescending(x=>x.Version).FirstOrDefault();
    }
    static void ValidateUrl(string url)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/shelter1995/tendedero/releases/download/",StringComparison.Ordinal)||uri.UserInfo.Length!=0)throw new InvalidDataException("更新资产不属于指定的 Windows 发布源。");
    }
    public async Task DownloadAsync(UpdateAsset asset,string destination,IProgress<double>? progress,CancellationToken token)
    {
        ValidateUrl(asset.Url); if(!Regex.IsMatch(asset.Sha256,"^[0-9a-fA-F]{64}$")||asset.Size<=0)throw new InvalidDataException("缺少有效的包校验信息。");
        var partial=destination+".partial";
        try {
            token.ThrowIfCancellationRequested();
            using var response=await client.GetAsync(asset.Url,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false); response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength is long length && length!=asset.Size)throw new InvalidDataException("下载大小与发布信息不符。");
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using(var input=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true)) {
                var buffer=new byte[65536]; long total=0; int read;
                while((read=await input.ReadAsync(buffer,token).ConfigureAwait(false))>0) {
                    total+=read; if(total>asset.Size)throw new InvalidDataException("下载超过预期大小。");
                    hash.AppendData(buffer,0,read); await output.WriteAsync(buffer.AsMemory(0,read),token).ConfigureAwait(false); progress?.Report(total*100d/asset.Size);
                }
                if(total!=asset.Size)throw new InvalidDataException("下载不完整。");
            }
            if(!Convert.ToHexString(hash.GetHashAndReset()).Equals(asset.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA256 校验失败，未使用下载文件。");
            token.ThrowIfCancellationRequested(); File.Move(partial,destination);
        } finally { if(File.Exists(partial))File.Delete(partial); }
    }
    public static string SafeRelative(string name)
    {
        var normalized=name.Replace('\\','/');
        if(string.IsNullOrWhiteSpace(normalized)||normalized.StartsWith('/')||normalized.Contains(':')||normalized.Split('/').Any(p=>p is "" or "." or ".." || p.EndsWith('.') || p.EndsWith(' ') || Regex.IsMatch(p,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",RegexOptions.IgnoreCase)))throw new InvalidDataException("更新包包含不安全路径："+name);
        return normalized;
    }
    public static void ValidateAppPath(string name)
    {
        var path=SafeRelative(name); var first=path.Split('/')[0];
        var parts=path.Split('/');
        var runtimeResource=parts.Length==2 && RuntimeCultures.Contains(first) && parts[1].EndsWith(".resources.dll",StringComparison.OrdinalIgnoreCase);
        if(path.Contains('/') && !new[]{"languages","licenses","assets","runtimes"}.Contains(first.ToLowerInvariant()) && !runtimeResource)throw new InvalidDataException("更新包试图覆盖非程序目录："+first);
        if(new[]{"settings.json","config.json","unins000.exe","unins000.dat"}.Contains(first.ToLowerInvariant()) || first.Equals("data",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包试图覆盖用户设置或卸载程序。");
    }
    public static StagedPackage StagePackage(string archive,string destination,UpdateVersion current,bool installed)
    {
        if(Directory.Exists(destination))throw new InvalidDataException("更新暂存目录必须为空且尚未创建。");
        Directory.CreateDirectory(destination); var root=Path.GetFullPath(destination)+Path.DirectorySeparatorChar;
        using var zip=ZipFile.OpenRead(archive); if(zip.Entries.Count>10000)throw new InvalidDataException("更新包文件过多。");
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total=0;
        foreach(var entry in zip.Entries) {
            if(entry.FullName.EndsWith('/')||entry.FullName.EndsWith('\\'))continue;
            var name=SafeRelative(entry.FullName); ValidateAppPath(name);
            if(!seen.Add(name)||(entry.ExternalAttributes & 0x400)!=0||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("更新包包含重复文件或符号链接。");
            total+=entry.Length; if(total>2L*1024*1024*1024)throw new InvalidDataException("更新包解压大小过大。");
            var target=Path.GetFullPath(Path.Combine(destination,name)); if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包路径越界。");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); entry.ExtractToFile(target,false);
        }
        var manifestPath=Path.Combine(destination,"SHA256SUMS.json"); var metadataPath=Path.Combine(destination,"update-info.json");
        if(!File.Exists(manifestPath)||!File.Exists(metadataPath))throw new InvalidDataException("更新包缺少 update-info.json 或 SHA256SUMS.json；旧版包不能直接导入。");
        var manifest=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(manifestPath))??throw new InvalidDataException("校验清单无效。");
        if(!manifest.ContainsKey("update-info.json")||manifest.Count!=seen.Count-1)throw new InvalidDataException("校验清单未覆盖全部程序文件。");
        var hashed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var (relative,expected) in manifest) {
            var name=SafeRelative(relative); if(!hashed.Add(name)||!seen.Contains(name)||!Regex.IsMatch(expected,"^[0-9a-fA-F]{64}$"))throw new InvalidDataException("校验清单文件无效。");
            using var stream=File.OpenRead(Path.Combine(destination,name));
            if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包文件损坏："+name);
        }
        using var info=JsonDocument.Parse(File.ReadAllText(metadataPath)); var m=info.RootElement;
        if(m.GetProperty("schemaVersion").GetInt32()!=1 || m.GetProperty("product").GetString()!="Liangtu" || m.GetProperty("runtime").GetString()!="win-x64")throw new InvalidDataException("此更新包不是晾图 Windows x64 更新。");
        var version=UpdateVersion.Parse(m.GetProperty("version").GetString()??""); if(version.CompareTo(current)<=0)throw new InvalidDataException("此更新包版本不高于当前版本，已阻止降级或重复安装。");
        var kind=m.GetProperty("kind").GetString()??""; if(kind!=(installed?"installer":"portable"))throw new InvalidDataException(installed?"安装版需要安装器更新 ZIP 包。":"便携版需要 Windows x64 portable ZIP 包。");
        string? installer=null;
        if(installed) { installer=SafeRelative(m.GetProperty("installerFile").GetString()??""); if(installer.Contains('/')||!installer.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!seen.Contains(installer))throw new InvalidDataException("安装器文件无效。"); }
        else if(!seen.Contains("Liangtu.exe"))throw new InvalidDataException("更新包缺少 Liangtu.exe。");
        return new(destination,version,kind,installer);
    }
}
