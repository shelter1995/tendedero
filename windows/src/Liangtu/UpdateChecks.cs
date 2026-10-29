using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.IO.Compression;
using Liangtu.Updates;
namespace Liangtu;
internal static class UpdateChecks
{
    public static async Task<int> Main()
    {
        try {
            Assert(UpdateVersion.Parse("0.9.0").CompareTo(UpdateVersion.Parse("0.8.2")) > 0, "upgrade ordering");
            Assert(UpdateVersion.Parse("1.0.0-beta.10").CompareTo(UpdateVersion.Parse("1.0.0-beta.2")) > 0, "numeric prerelease ordering");
            Assert(UpdateVersion.Parse("1.0.0").CompareTo(UpdateVersion.Parse("1.0.0-rc.1")) > 0, "stable ordering");
            Console.WriteLine("PASS update semantic versions");
            string Release(string tag, bool pre=false, string? name=null) => JsonSerializer.Serialize(new { tag_name=tag, prerelease=pre, draft=false, body="notes", assets=new[]{new {name=name??("Liangtu-"+tag.Replace("windows-v", "")+"-Windows-x64-portable.zip"),size=3,digest="sha256:"+new string('A',64),browser_download_url="https://github.com/shelter1995/tendedero/releases/download/"+tag+"/"+(name??"package.zip")}}});
            var releases="["+string.Join(",",Release("v9.0.0"),Release("windows-v1.1.0"),Release("windows-v1.2.0",true),Release("windows-v1.3.0",false,"Tendedero.dmg"))+"]";
            Assert(UpdateService.SelectRelease(releases,UpdateVersion.Parse("1.0.0"),false,false)?.Version.ToString()=="1.1.0","Windows stable filter");
            Assert(UpdateService.SelectRelease(releases,UpdateVersion.Parse("1.0.0"),true,false)?.Version.ToString()=="1.2.0","prerelease opt-in");
            Assert(UpdateService.SelectRelease(releases,UpdateVersion.Parse("2.0.0"),true,false)==null,"no downgrade");
            Assert(UpdateService.SelectRelease("["+Release("windows-v2.0.0",false,"Liangtu-0.7.2-Setup-x64-r1.exe")+"]",UpdateVersion.Parse("1.0.0"),true,true)==null,"asset version mismatch accepted");
            Reject(()=>UpdateService.SelectRelease(releases.Replace("sha256:","missing:"),UpdateVersion.Parse("1.0.0"),false,false),"missing release digest silently treated as current");
            Console.WriteLine("PASS release channel, asset and downgrade filters");
            var root=Path.Combine(Environment.CurrentDirectory,"artifacts","update-checks","case-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var content=Encoding.UTF8.GetBytes("abc"); var digest=Convert.ToHexString(SHA256.HashData(content));
            using var client=new HttpClient(new ReplyHandler(content)); var service=new UpdateService(client);
            using var offline=new HttpClient(new ReplyHandler(content,HttpStatusCode.ServiceUnavailable));
            try{await new UpdateService(offline).CheckAsync(UpdateVersion.Parse("1.0.0"),false,false,CancellationToken.None);throw new Exception("HTTP failure treated as current");}catch(HttpRequestException){}
            var asset=new UpdateAsset("package.zip", "https://github.com/shelter1995/tendedero/releases/download/windows-v1.0.0/package.zip",3,digest);
            await service.DownloadAsync(asset,Path.Combine(root,"good.zip"),null,CancellationToken.None);
            await RejectAsync(()=>service.DownloadAsync(asset with {Sha256=new string('0',64)},Path.Combine(root,"bad.zip"),null,CancellationToken.None),"wrong digest accepted");
            await RejectAsync(()=>service.DownloadAsync(asset with {Size=4},Path.Combine(root,"short.zip"),null,CancellationToken.None),"short download accepted");
            using var cts=new CancellationTokenSource(); cts.Cancel();
            await RejectAsync(()=>service.DownloadAsync(asset,Path.Combine(root,"cancel.zip"),null,cts.Token),"cancel ignored");
            Assert(!File.Exists(Path.Combine(root,"bad.zip")) && !File.Exists(Path.Combine(root,"cancel.zip")),"invalid file retained");
            Console.WriteLine("PASS download size/hash/cancellation");
            var zip=MakePackage(root,"valid",false,false); var staged=UpdateService.StagePackage(zip,Path.Combine(root,"valid-stage"),UpdateVersion.Parse("0.9.0"),false);
            Assert(File.Exists(Path.Combine(staged.Directory,"Liangtu.exe")),"valid package not staged");
            var prepared=new PreparedUpdate(staged,root);
            File.WriteAllText(Path.Combine(staged.Directory,"Liangtu.exe"),"changed after verification");
            Reject(()=>prepared.VerifyStaging(),"changed prepared payload accepted");
            Reject(()=>UpdateService.StagePackage(MakePackage(root,"traversal",true,false),Path.Combine(root,"traversal-stage"),UpdateVersion.Parse("0.9.0"),false),"ZipSlip accepted");
            Reject(()=>UpdateService.StagePackage(MakePackage(root,"corrupt",false,true),Path.Combine(root,"corrupt-stage"),UpdateVersion.Parse("0.9.0"),false),"corrupt manifest accepted");
            Reject(()=>UpdateService.StagePackage(MakePackage(root,"alias",false,false,true),Path.Combine(root,"alias-stage"),UpdateVersion.Parse("0.9.0"),false),"duplicate manifest aliases accepted");
            Assert(!File.Exists(Path.Combine(root,"escape.txt")),"ZipSlip created a file outside staging");
            Reject(()=>UpdateService.StagePackage(zip,Path.Combine(root,"old-stage"),UpdateVersion.Parse("9.0.0"),false),"local downgrade accepted");
            Reject(()=>UpdateService.StagePackage(zip,Path.Combine(root,"edition-stage"),UpdateVersion.Parse("0.9.0"),true),"wrong edition accepted");
            Console.WriteLine("PASS local metadata, edition, manifest and ZipSlip");
            foreach(var culture in new[]{"cs","de","es","fr","it","ja","ko","pl","pt-BR","ru","tr","zh-Hans","zh-Hant"})UpdateService.ValidateAppPath(culture+"/PresentationCore.resources.dll");
            foreach(var protectedPath in new[]{"tools/ffmpeg.exe","data/photo.jpg","config/settings.json","settings.json","config.json","zh-Hans/settings.json","de/nested/resource.resources.dll"})Reject(()=>UpdateService.ValidateAppPath(protectedPath),"protected update path accepted: "+protectedPath);
            var realSource=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--real-payload=",StringComparison.Ordinal));
            if(realSource!=null)CheckRealPayload(realSource[15..],root);
            return 0;
        } catch(Exception ex) { Console.Error.WriteLine("FAIL " + ex); return 1; }
    }
    static void CheckRealPayload(string source,string root)
    {
        var copy=Path.Combine(root,"real-payload");Directory.CreateDirectory(copy);
        foreach(var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories)) {
            var relative=Path.GetRelativePath(source,file);var target=Path.Combine(copy,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
        }
        // Keep the real binary version consistent; never change the published source payload.
        var binaryVersion=System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(copy,"Liangtu.exe")).ProductVersion??throw new InvalidDataException("Real payload has no product version");
        var version=UpdateVersion.Parse(binaryVersion).ToString();
        File.WriteAllText(Path.Combine(copy,"update-info.json"),JsonSerializer.Serialize(new{schemaVersion=1,product="Liangtu",version,runtime="win-x64",kind="portable"}));
        File.Copy(Path.Combine(Environment.CurrentDirectory,"scripts","apply-update.ps1"),Path.Combine(copy,"apply-update.ps1"),true);
        var hashes=Directory.EnumerateFiles(copy,"*",SearchOption.AllDirectories).Where(f=>Path.GetFileName(f)!="SHA256SUMS.json").ToDictionary(f=>Path.GetRelativePath(copy,f),f=>{using var stream=File.OpenRead(f);return Convert.ToHexString(SHA256.HashData(stream));});
        File.WriteAllText(Path.Combine(copy,"SHA256SUMS.json"),JsonSerializer.Serialize(hashes));
        var archive=Path.Combine(root,"real-payload.zip");ZipFile.CreateFromDirectory(copy,archive,CompressionLevel.Fastest,false);
        var staged=UpdateService.StagePackage(archive,Path.Combine(root,"real-payload-stage"),UpdateVersion.Parse("0.0.0"),false);
        foreach(var relative in hashes.Keys)Assert(File.Exists(Path.Combine(staged.Directory,relative)),"Real payload missing "+relative);
        Assert(Directory.Exists(Path.Combine(staged.Directory,"zh-Hans")),"Real .NET culture payload missing");
        Console.WriteLine($"PASS real release payload staging: {hashes.Count} verified files at {staged.Directory}");
    }
    static string MakePackage(string root,string name,bool traversal,bool corrupt,bool alias=false)
    {
        var path=Path.Combine(root,name+".zip"); using var zip=ZipFile.Open(path,ZipArchiveMode.Create);
        var metadata=JsonSerializer.Serialize(new {schemaVersion=1,product="Liangtu",version="1.0.0",runtime="win-x64",kind="portable"});
        var files=new System.Collections.Generic.Dictionary<string,string>{{"Liangtu.exe","synthetic application"},{"update-info.json",metadata}};
        if(alias){files["languages/a.txt"]="a";files["languages/b.txt"]="b";}
        var hashes=files.ToDictionary(p=>p.Key,p=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p.Value)))); if(corrupt)hashes["Liangtu.exe"]=new string('0',64);
        if(alias){hashes.Remove("languages/b.txt");hashes["languages\\a.txt"]=hashes["languages/a.txt"];}
        files["SHA256SUMS.json"]=JsonSerializer.Serialize(hashes); if(traversal)files["../escape.txt"]="bad";
        foreach(var pair in files){using var writer=new StreamWriter(zip.CreateEntry(pair.Key).Open(),new UTF8Encoding(false));writer.Write(pair.Value);}
        return path;
    }
    static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    static void Reject(Action action,string message){try{action();}catch(InvalidDataException){return;}throw new Exception(message);}
    static async Task RejectAsync(Func<Task> action,string message){try{await action();}catch(Exception ex) when(ex is InvalidDataException or OperationCanceledException){return;}throw new Exception(message);}
    sealed class ReplyHandler(byte[] bytes,HttpStatusCode status=HttpStatusCode.OK):HttpMessageHandler {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(new HttpResponseMessage(status){Content=new ByteArrayContent(bytes)});}
}
