param([Parameter(Mandatory)][string]$Installer, [Parameter(Mandatory)][string]$TestRoot)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$qa=[IO.Path]::GetFullPath((Join-Path $workspace $TestRoot))
if(-not $qa.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Test root escaped workspace'}
$exe=(Resolve-Path -LiteralPath $Installer).Path
if([IO.Path]::GetFileName($exe) -notlike '*-InstallerTest-*'){throw 'Only isolated test installers are allowed'}
$appDir=Join-Path $qa 'app'
$dataDir=Join-Path $qa 'data'
$outside=Join-Path $qa 'external-original'
New-Item -ItemType Directory -Force $qa,$outside | Out-Null
Set-Content -LiteralPath (Join-Path $outside 'original.txt') -Value 'external file must remain'
$results=[Collections.Generic.List[string]]::new()
function Check([bool]$ok,[string]$name){
    $results.Add($(if($ok){'PASS '}else{'FAIL '})+$name)
    $results | Set-Content -LiteralPath (Join-Path $qa 'results.txt')
    Write-Output $results[$results.Count-1]
    if(-not $ok){throw $name}
}
foreach($remove in @($false,$true)){
    $label=if($remove){'remove'}else{'keep'}
    if(Test-Path -LiteralPath (Join-Path $appDir 'Liangtu.exe')){throw 'Existing test install must be uninstalled before retry'}
    $installArgs=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/COMPONENTS=core','/TASKS=""',('/DIR="'+$appDir+'"'),('/LOG="'+(Join-Path $qa ($label+'-install.log'))+'"'))
    $process=Start-Process -FilePath $exe -ArgumentList $installArgs -WindowStyle Hidden -Wait -PassThru
    Check ($process.ExitCode -eq 0) ($label+' installation succeeds')
    $verify=Start-Process -FilePath (Join-Path $appDir 'Liangtu.exe') -ArgumentList '--verify-package' -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput (Join-Path $qa ($label+'-verify.txt')) -RedirectStandardError (Join-Path $qa ($label+'-verify-error.txt'))
    Check ($verify.ExitCode -eq 0) ($label+' installed package verifies runtime and file hashes')
    New-Item -ItemType Directory -Force (Join-Path $dataDir 'Inbox') | Out-Null
    Set-Content -LiteralPath (Join-Path $dataDir 'settings.json') -Value ('{"Items":["'+($outside.Replace('\','\\'))+'/original.txt"]}')
    Set-Content -LiteralPath (Join-Path $dataDir 'Inbox/image.txt') -Value 'test image'
    $link=Join-Path $dataDir 'external-link'
    if(-not(Test-Path -LiteralPath $link)){New-Item -ItemType Junction -Path $link -Target $outside | Out-Null}
    $uninstallArgs=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $qa ($label+'-uninstall.log'))+'"'))
    if($remove){$uninstallArgs+='/REMOVEDATA=yes'}
    $process=Start-Process -FilePath (Join-Path $appDir 'unins000.exe') -ArgumentList $uninstallArgs -WindowStyle Hidden -Wait -PassThru
    Check ($process.ExitCode -eq 0) ($label+' uninstall succeeds')
    Check (-not(Test-Path -LiteralPath (Join-Path $appDir 'Liangtu.exe'))) ($label+' removes program')
    if($remove){Check (-not(Test-Path -LiteralPath $dataDir)) 'explicit removal deletes settings and inbox'}
    else {Check ((Test-Path -LiteralPath (Join-Path $dataDir 'settings.json')) -and (Test-Path -LiteralPath (Join-Path $dataDir 'Inbox/image.txt'))) 'default preserves settings and images'}
    Check (Test-Path -LiteralPath (Join-Path $outside 'original.txt')) ($label+' preserves external original and junction target')
}
