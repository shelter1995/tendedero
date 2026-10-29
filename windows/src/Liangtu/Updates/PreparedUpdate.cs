using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
namespace Liangtu.Updates;
internal sealed class PreparedUpdate
{
    public string Version { get; }
    public string WorkDirectory { get; }
    public string Kind { get; }
    readonly string stageDirectory;
    readonly string? installerFile;
    readonly Dictionary<string,string> files=new(StringComparer.OrdinalIgnoreCase);
    bool launched;
    internal void VerifyStaging()
    {
        var paths=Directory.EnumerateFiles(stageDirectory,"*",SearchOption.AllDirectories).ToArray();
        if(paths.Length!=files.Count)throw new InvalidDataException("已验证的更新包文件集合发生改变，请重新下载。");
        foreach(var path in paths) {
            var relative=Path.GetRelativePath(stageDirectory,path);
            if(!files.TryGetValue(relative,out var expected))throw new InvalidDataException("已验证的更新包被改动。");
            using var stream=File.OpenRead(path);
            if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("已验证的更新包被改动，请重新下载。");
        }
    }
    internal PreparedUpdate(StagedPackage package,string workDirectory)
    {
        Version=package.Version.ToString();Kind=package.Kind;stageDirectory=Path.GetFullPath(package.Directory);WorkDirectory=Path.GetFullPath(workDirectory);installerFile=package.InstallerFile;
        foreach(var path in Directory.EnumerateFiles(stageDirectory,"*",SearchOption.AllDirectories)) {
            var relative=Path.GetRelativePath(stageDirectory,path);UpdateService.ValidateAppPath(relative);
            using var stream=File.OpenRead(path);files.Add(relative,Convert.ToHexString(SHA256.HashData(stream)));
        }
    }
    public static string CreateWorkDirectory()
    {
        var path=Path.Combine(Path.GetTempPath(),"Liangtu-updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;
    }
    public void LaunchAfterExit()
    {
        if(launched)throw new InvalidOperationException("更新助手已经启动。");
        VerifyStaging();
        var helper=Path.Combine(AppContext.BaseDirectory,"apply-update.ps1");if(!File.Exists(helper))throw new FileNotFoundException("安装包缺少更新助手，请重新下载完整发行包。",helper);
        var copiedHelper=Path.Combine(WorkDirectory,"apply-update.ps1");File.Copy(helper,copiedHelper,true);
        var process=Process.GetCurrentProcess();
        var plan=new {schemaVersion=1,product="Liangtu",kind=Kind,version=Version,currentVersion=UpdateService.CurrentVersion.ToString(),parentId=Environment.ProcessId,parentStartUtc=process.StartTime.ToUniversalTime().ToString("o"),installRoot=Path.GetFullPath(AppContext.BaseDirectory),stageRoot=stageDirectory,installerFile,files,restart=true};
        var planPath=Path.Combine(WorkDirectory,"plan.json");File.WriteAllText(planPath,JsonSerializer.Serialize(plan));
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=WorkDirectory};
        foreach(var argument in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",copiedHelper,"-Plan",planPath})start.ArgumentList.Add(argument);
        using var child=Process.Start(start)??throw new IOException("无法启动更新助手。");
        var ready=Path.Combine(WorkDirectory,"helper-ready");
        for(int i=0;i<100;i++) {
            if(File.Exists(ready)){launched=true;return;}
            if(child.HasExited)throw new IOException("更新助手检查失败，请查看 "+Path.Combine(WorkDirectory,"update-result.log"));
            Thread.Sleep(100);
        }
        // Do not shut the app down if the helper has not acknowledged the plan.
        try{child.Kill();}catch(InvalidOperationException){}
        throw new IOException("更新助手启动超时；应用仍然运行，请稍后重试。");
    }
}
