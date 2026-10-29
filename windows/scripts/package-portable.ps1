param([string]$RuntimeVersion='8.0.28',[string]$PackageSource='')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME=Join-Path $projectRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$project=Join-Path $projectRoot 'src/Liangtu/Liangtu.csproj'
$version=([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version
$output=Join-Path $projectRoot ('dist/Liangtu-'+$version)
$archive=Join-Path $projectRoot ('dist/Liangtu-'+$version+'-Windows-x64-portable.zip')
if(Test-Path -LiteralPath $output){throw "Release directory already exists: $output. Use a new version or move the old directory first; release packaging requires a clean folder."}
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'artifacts') -Force | Out-Null
$restoreArgs=@()
if($PackageSource){$restoreArgs+=('-p:RestoreSources='+$PackageSource)}
dotnet publish $project -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=$RuntimeVersion -p:StartupObject=Liangtu.App -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -p:UseSharedCompilation=false -p:NuGetAudit=false @restoreArgs -o $output
if($LASTEXITCODE -ne 0){throw 'Portable publish failed'}
$licenses=Join-Path $output 'licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'reference/tendedero/LICENSE') -Destination (Join-Path $licenses 'Tendedero-MIT.txt')
$packageCache=((dotnet nuget locals global-packages --list) -replace '^global-packages:\s*','').Trim()
Copy-Item -LiteralPath (Join-Path $packageCache "microsoft.netcore.app.runtime.win-x64/$RuntimeVersion/LICENSE.TXT") -Destination (Join-Path $licenses 'NET-runtime-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $packageCache "microsoft.netcore.app.runtime.win-x64/$RuntimeVersion/THIRD-PARTY-NOTICES.TXT") -Destination (Join-Path $licenses 'NET-THIRD-PARTY-NOTICES.txt')
Copy-Item -LiteralPath (Join-Path $packageCache "microsoft.windowsdesktop.app.runtime.win-x64/$RuntimeVersion/LICENSE") -Destination (Join-Path $licenses 'WindowsDesktop-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/portable-readme.txt') -Destination (Join-Path $output 'README.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $output 'README.md')
$launch="@echo off`r`nstart `"`" /D `"%~dp0`" `"%~dp0Liangtu.exe`" --show`r`n"
[IO.File]::WriteAllText((Join-Path $output 'Start.cmd'),$launch,[Text.Encoding]::ASCII)
$manifest=[ordered]@{}
$updateInfo=[ordered]@{schemaVersion=1;product='Liangtu';version=[string]$version;runtime='win-x64';kind='portable'}
[IO.File]::WriteAllText((Join-Path $output 'update-info.json'),($updateInfo|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Get-ChildItem -LiteralPath $output -File -Recurse | Where-Object {$_.Name -ne 'SHA256SUMS.json'} | Sort-Object FullName | ForEach-Object {
 $relative=[IO.Path]::GetRelativePath($output,$_.FullName)
 $manifest[$relative]=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
}
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.json'),($manifest | ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
$verify=Start-Process -FilePath (Join-Path $output 'Liangtu.exe') -ArgumentList '--verify-package' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput (Join-Path $projectRoot 'artifacts/portable-verification.txt') -RedirectStandardError (Join-Path $projectRoot 'artifacts/portable-verification-error.txt')
if($verify.ExitCode -ne 0){Get-Content (Join-Path $projectRoot 'artifacts/portable-verification-error.txt');throw 'Published package verification failed'}
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -Force
$hash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
[IO.File]::WriteAllText(($archive+'.sha256'),($hash+'  '+[IO.Path]::GetFileName($archive)+[Environment]::NewLine),[Text.Encoding]::ASCII)
Write-Host "Ready: $archive"
Get-Content (Join-Path $projectRoot 'artifacts/portable-verification.txt')
