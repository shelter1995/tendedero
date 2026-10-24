param([string]$Installer='artifacts/installer-test-output/Liangtu-0.6.3-InstallerTest-x64.exe')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$installerPath=(Resolve-Path -LiteralPath (Join-Path $projectRoot $Installer)).Path
if([IO.Path]::GetFileName($installerPath) -notlike '*-InstallerTest-*'){throw 'Only an isolated test installer may be used'}
$qaRoot=Join-Path $projectRoot 'artifacts/install-qa'
$appDir=Join-Path $qaRoot '程序 files'
$dataRoot=Join-Path $qaRoot 'data'
foreach($path in @($appDir,$dataRoot)){
    if(-not [IO.Path]::GetFullPath($path).StartsWith($projectRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Test path escaped workspace'}
}
if(Test-Path -LiteralPath (Join-Path $appDir 'Liangtu.exe')){throw 'Previous test install exists; uninstall it before retrying'}
New-Item -ItemType Directory -Force $qaRoot,(Join-Path $dataRoot 'Inbox') | Out-Null
$marker=Join-Path $dataRoot 'Inbox/preserved-user-data.txt'
Set-Content -LiteralPath $marker -Value 'installer must preserve this data' -Encoding utf8
$regRun='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runName='LiangtuInstallerTest'
$uninstallKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{9643F797-0668-459F-9BD8-F42EC65E1233}_is1'
$results=[Collections.Generic.List[string]]::new()
function Check([bool]$ok,[string]$name){if(-not $ok){$results.Add('FAIL '+$name);$results|Set-Content (Join-Path $qaRoot 'results.txt');throw $name};$results.Add('PASS '+$name);$results|Set-Content (Join-Path $qaRoot 'results.txt');Write-Output ('PASS '+$name)}
function Setup([string]$label,[string]$components,[string[]]$Extra=@(),[switch]$ExpectFailure,[switch]$CreateDesktop){
    $taskArg=if($CreateDesktop){'/TASKS=desktopicon'}else{'/TASKS=""'}
    $setupArgs=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/LANG=chinesesimplified',('/DIR="'+$appDir+'"'),('/LOG="'+(Join-Path $qaRoot ($label+'.log'))+'"'),('/COMPONENTS='+$components),$taskArg)+$Extra
    $p=Start-Process -FilePath $installerPath -ArgumentList $setupArgs -WindowStyle Hidden -PassThru
    if(-not $p.WaitForExit(240000)){throw ('Installer timed out: '+$label)}
    if($ExpectFailure){Check ($p.ExitCode -ne 0) ($label+' rejects installation')}else{Check ($p.ExitCode -eq 0) ($label+' exits successfully')}
}
function VerifyPackage([string]$label){
    $p=Start-Process -FilePath (Join-Path $appDir 'Liangtu.exe') -ArgumentList '--verify-package' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput (Join-Path $qaRoot ($label+'-verify.txt')) -RedirectStandardError (Join-Path $qaRoot ($label+'-verify-error.txt'))
    Check ($p.ExitCode -eq 0) ($label+' validates bundled runtime and all file hashes')
}
function Uninstall([string]$label){
    $p=Start-Process -FilePath (Join-Path $appDir 'unins000.exe') -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $qaRoot ($label+'.log'))+'"') -WindowStyle Hidden -Wait -PassThru
    Check ($p.ExitCode -eq 0) ($label+' exits successfully')
    Check (-not(Test-Path -LiteralPath (Join-Path $appDir 'Liangtu.exe'))) ($label+' removes installed application')
    Check (Test-Path -LiteralPath $marker) ($label+' preserves screenshot/settings directory')
    Check (-not(Test-Path -LiteralPath $uninstallKey)) ($label+' removes uninstall registration')
}
$runBefore=Get-ItemProperty -LiteralPath $regRun -Name $runName -ErrorAction SilentlyContinue
if($runBefore){throw 'Unexpected preexisting installer-test startup value'}

# A mismatched component must fail before writing application files.
$bad=Join-Path $qaRoot 'invalid-component.7z';Set-Content -LiteralPath $bad -Value 'not the pinned component'
Setup 'bad-component' 'core,video' @(('/VIDEOARCHIVE="'+$bad+'"')) -ExpectFailure
Check (-not(Test-Path -LiteralPath (Join-Path $appDir 'Liangtu.exe'))) 'bad component leaves no partial application install'

# Match the application single-instance algorithm exactly, without touching the real app.
$id=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($dataRoot).ToUpperInvariant()))).Substring(0,16)
$mutex=[Threading.Mutex]::new($false,('Local\Liangtu.'+$id))
try{Setup 'running-app' 'core' @() -ExpectFailure}finally{$mutex.Dispose()}
Check (-not(Test-Path -LiteralPath (Join-Path $appDir 'Liangtu.exe'))) 'running-app guard preserves existing session'

Setup 'basic-install' 'core'
VerifyPackage 'basic'
Check (-not(Test-Path -LiteralPath (Join-Path $appDir 'tools/ffmpeg.exe'))) 'basic install does not download video component'
Check (Test-Path -LiteralPath $uninstallKey) 'per-user uninstall registration exists'
Check ($null -eq (Get-ItemProperty -LiteralPath $regRun -Name $runName -ErrorAction SilentlyContinue)) 'installation does not enable startup by default'
$startMenu=Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)) '晾图安装验证'
Check (Test-Path -LiteralPath (Join-Path $startMenu '晾图安装验证.lnk')) 'start menu shortcut exists'
$shortcut=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $startMenu '晾图安装验证.lnk'))
Check ($shortcut.TargetPath -eq (Join-Path $appDir 'Liangtu.exe') -and $shortcut.Arguments -eq '--show') 'shortcut targets installed executable'

New-ItemProperty -LiteralPath $regRun -Name $runName -Value '"C:\OldPortable\Liangtu.exe"' -PropertyType String -Force | Out-Null
# This run deliberately uses the network path, not the predownloaded QA archive.
Setup 'online-video-upgrade' 'core,video'
VerifyPackage 'video-upgrade'
Check (Test-Path -LiteralPath $marker) 'upgrade preserves existing data'
Check ((Get-ItemPropertyValue -LiteralPath $regRun -Name $runName) -eq ('"'+(Join-Path $appDir 'Liangtu.exe')+'"')) 'upgrade preserves opt-in and updates startup path'
Check (Test-Path -LiteralPath (Join-Path $appDir 'licenses/FFmpeg/LICENSE.txt')) 'video GPL license is installed'
Check (Test-Path -LiteralPath (Join-Path $appDir 'licenses/FFmpeg/README.txt')) 'video upstream build details are installed'

$oldPath=$env:PATH
try{
    $env:PATH=Join-Path $env:WINDIR 'System32'
    $ffmpeg=Join-Path $appDir 'tools/ffmpeg.exe';$ffprobe=Join-Path $appDir 'tools/ffprobe.exe'
    & $ffmpeg -version | Set-Content (Join-Path $qaRoot 'ffmpeg-version.txt')
    Check ($LASTEXITCODE -eq 0) 'downloaded FFmpeg runs without PATH dependencies'
    $fixture=Join-Path $projectRoot 'artifacts/video-fixtures/source.mp4'
    & $ffmpeg -v error -nostdin -y -i $fixture -frames:v 1 -vf 'scale=600:-1' (Join-Path $qaRoot 'thumbnail.png')
    Check ($LASTEXITCODE -eq 0 -and (Test-Path (Join-Path $qaRoot 'thumbnail.png'))) 'downloaded component generates video thumbnail'
    & $ffmpeg -v error -nostdin -y -ss 2.5 -i $fixture -t 2 -map '0:v:0' -map '0:a?' -c:v libx264 -preset fast -crf 18 -c:a aac -movflags +faststart (Join-Path $qaRoot 'trimmed.mp4')
    Check ($LASTEXITCODE -eq 0) 'downloaded component performs exact reencoded trim'
    $json=& $ffprobe -v error -show_entries 'format=duration:stream=codec_type' -of json (Join-Path $qaRoot 'trimmed.mp4')
    $json | Set-Content (Join-Path $qaRoot 'trim-probe.json')
    $probe=$json | ConvertFrom-Json
    Check ([Math]::Abs([double]$probe.format.duration-2) -lt .1 -and 'audio' -in $probe.streams.codec_type) 'trim has expected duration and retained audio'
}finally{$env:PATH=$oldPath}

Uninstall 'uninstall-full'
Check (-not(Test-Path -LiteralPath (Join-Path $startMenu '晾图安装验证.lnk'))) 'uninstall removes owned shortcut'
Check ($null -eq (Get-ItemProperty -LiteralPath $regRun -Name $runName -ErrorAction SilentlyContinue)) 'uninstall removes only owned startup value'
Setup 'reinstall-basic' 'core' @() -CreateDesktop
VerifyPackage 'reinstall'
Check (Test-Path -LiteralPath $marker) 'reinstall retains preexisting user data'
$desktopShortcut=Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)) '晾图安装验证.lnk'
Check (Test-Path -LiteralPath $desktopShortcut) 'optional desktop shortcut is created when selected'
Uninstall 'uninstall-reinstalled'
Check (-not(Test-Path -LiteralPath $desktopShortcut)) 'uninstall removes optional desktop shortcut'
$results | Set-Content -LiteralPath (Join-Path $qaRoot 'results.txt') -Encoding utf8
Write-Output ('Installer QA complete: '+$results.Count+' checks passed')
