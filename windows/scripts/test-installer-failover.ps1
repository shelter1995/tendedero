param([string]$TestRoot='artifacts/installer-r2-failover')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$root=[IO.Path]::GetFullPath((Join-Path $projectRoot $TestRoot))
if(-not $root.StartsWith($projectRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'QA path escaped workspace'}
$installer=Join-Path $projectRoot 'artifacts/installer-test-output-r2/Liangtu-0.6.3-InstallerTest-x64-r2-failover.exe'
$appDir=Join-Path $root 'app'
if(Test-Path (Join-Path $appDir 'Liangtu.exe')){throw 'Previous test install must be removed first'}
$results=[Collections.Generic.List[string]]::new()
function Check([bool]$ok,[string]$message){
    $results.Add($(if($ok){'PASS '}else{'FAIL '})+$message)
    $results | Set-Content -LiteralPath (Join-Path $root 'results.txt') -Encoding utf8
    if(-not $ok){throw $message};Write-Output ('PASS '+$message)
}
function Install([string]$file,[string]$label,[switch]$Fail){
    $p=Start-Process -FilePath $file -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/COMPONENTS=core,video','/TASKS=""',('/DIR="'+$appDir+'"'),('/LOG="'+(Join-Path $root ($label+'.log'))+'"') -WindowStyle Hidden -PassThru
    if(-not $p.WaitForExit(120000)){throw ('Test timed out: '+$label)}
    if($Fail){Check ($p.ExitCode -ne 0) ($label+' rejected safely')}else{Check ($p.ExitCode -eq 0) ($label+' completed')}
}
function Uninstall([string]$label){
    $p=Start-Process -FilePath (Join-Path $appDir 'unins000.exe') -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -WindowStyle Hidden -Wait -PassThru
    Check ($p.ExitCode -eq 0 -and -not(Test-Path (Join-Path $appDir 'Liangtu.exe'))) ($label+' cleaned test installation')
}
Install $installer 'fallback'
$log=Get-Content (Join-Path $root 'fallback.log') -Raw
Check ($log -match 'Video source attempt 1:' -and $log -match 'Video source attempt 2:' -and $log -match 'Video source attempt 3:') 'timeout and corrupt sources advance to the third source'
Check (([regex]::Matches($log,'Video source failed:')).Count -eq 2) 'both failed sources are logged'
foreach($exe in @('ffmpeg.exe','ffprobe.exe')){
    $expected=(Get-FileHash -LiteralPath (Join-Path $projectRoot ('artifacts/jellyfin-component/'+$exe))).Hash
    Check ((Get-FileHash -LiteralPath (Join-Path $appDir ('tools/'+$exe))).Hash -eq $expected) ($exe+' matches verified upstream component')
}
Uninstall 'fallback'
$server=Get-Content (Join-Path $root 'server.json') -Raw | ConvertFrom-Json
$process=Get-Process -Id $server.pid -ErrorAction SilentlyContinue
if($process -and $process.ProcessName -eq 'node'){Stop-Process -Id $process.Id}
Install $installer 'all-sources-unreachable' -Fail
Check (-not(Test-Path (Join-Path $appDir 'Liangtu.exe'))) 'all failed sources leave no partial application'

$sidecarDir=Join-Path $root 'sidecar'
New-Item -ItemType Directory -Force $sidecarDir | Out-Null
$sidecarInstaller=Join-Path $sidecarDir ([IO.Path]::GetFileName($installer))
Copy-Item -LiteralPath $installer -Destination $sidecarInstaller -Force
$archiveName='jellyfin-ffmpeg_8.1.3-1_portable_win64-clang-gpl.zip'
Copy-Item -LiteralPath (Join-Path $projectRoot ('.tools/downloads/'+$archiveName)) -Destination (Join-Path $sidecarDir $archiveName) -Force
Install $sidecarInstaller 'offline-sidecar'
$log=Get-Content (Join-Path $root 'offline-sidecar.log') -Raw
Check ($log -match 'loaded from verified local archive' -and $log -notmatch 'Video source attempt') 'valid adjacent ZIP installs without any download attempt'
Uninstall 'offline-sidecar'
Set-Content -LiteralPath (Join-Path $sidecarDir $archiveName) -Value 'corrupted local test archive'
Install $sidecarInstaller 'corrupt-sidecar' -Fail
Check (-not(Test-Path (Join-Path $appDir 'Liangtu.exe'))) 'corrupted adjacent ZIP cannot install component'
Write-Output ('Failover QA complete: '+$results.Count+' checks passed')
