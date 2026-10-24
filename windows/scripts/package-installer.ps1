param([string]$Version='0.6.3',[switch]$TestInstall,[string]$TestDataRoot='', [string]$FFmpegUrl='')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$source=Join-Path $projectRoot ('dist/Liangtu-'+$Version)
$compiler=Join-Path $projectRoot '.tools/InnoSetup/ISCC.exe'
if(-not(Test-Path -LiteralPath $compiler)){throw 'Missing Inno Setup compiler in .tools/InnoSetup'}
if(-not(Test-Path -LiteralPath (Join-Path $source 'SHA256SUMS.json'))){throw 'Build and verify the portable release first'}
$suffix=if($TestInstall){'test'}else{'release'}
$stage=Join-Path $projectRoot ('artifacts/installer-payload-'+$Version+'-'+$suffix)
if(Test-Path -LiteralPath $stage){throw "Payload staging directory already exists: $stage"}
New-Item -ItemType Directory -Force $stage | Out-Null
$manifest=Get-Content -LiteralPath (Join-Path $source 'SHA256SUMS.json') -Raw | ConvertFrom-Json
foreach($entry in $manifest.PSObject.Properties){
    $from=[IO.Path]::GetFullPath((Join-Path $source $entry.Name))
    if(-not $from.StartsWith($source+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe manifest path'}
    if((Get-FileHash -LiteralPath $from).Hash -ne $entry.Value){throw ('Release checksum mismatch: '+$entry.Name)}
    if($entry.Name -in @('README.txt','README.md','Start.cmd')){continue}
    $to=Join-Path $stage $entry.Name
    New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
    Copy-Item -LiteralPath $from -Destination $to
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/friends-readme.txt') -Destination (Join-Path $stage '使用说明.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'installer/video-component.json') -Destination (Join-Path $stage 'licenses/video-component.json')
$hashes=[ordered]@{}
Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object {$hashes[[IO.Path]::GetRelativePath($stage,$_.FullName)]=(Get-FileHash -LiteralPath $_.FullName).Hash}
[IO.File]::WriteAllText((Join-Path $stage 'SHA256SUMS.json'),($hashes|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
$output=Join-Path $projectRoot $(if($TestInstall){'artifacts/installer-test-output'}else{'dist/Share-Liangtu-'+$Version})
New-Item -ItemType Directory -Force $output | Out-Null
$arguments=@(('/DAppVersion='+$Version),('/DPayloadDir='+$stage),('/DOutputPath='+$output))
if($TestInstall){
    if(-not $TestDataRoot){throw 'TestDataRoot is required for a test installer'}
    $arguments+=('/DTestInstall=1',('/DTestDataRoot='+[IO.Path]::GetFullPath($TestDataRoot)))
}
if($FFmpegUrl){$arguments+=('/DFFmpegURL='+$FFmpegUrl)}
& $compiler @arguments (Join-Path $projectRoot 'installer/Liangtu.iss')
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
$base=if($TestInstall){'Liangtu-'+$Version+'-InstallerTest-x64.exe'}else{'Liangtu-'+$Version+'-Setup-x64.exe'}
$exe=Join-Path $output $base
$sha=(Get-FileHash -LiteralPath $exe).Hash
[IO.File]::WriteAllText(($exe+'.sha256'),($sha+'  '+$base+[Environment]::NewLine),[Text.Encoding]::ASCII)
if(-not $TestInstall){Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/friends-readme.txt') -Destination (Join-Path $output '使用说明.txt')}
Write-Output ('Installer: '+$exe)
Write-Output ('SHA256: '+$sha)
