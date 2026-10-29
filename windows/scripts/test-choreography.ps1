param([string]$RunName='choreography-final')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME=Join-Path $root '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$output=Join-Path $root 'artifacts/choreography-checks'
$data=Join-Path $root ('artifacts/'+$RunName)
dotnet build (Join-Path $root 'src/Liangtu') -c Release -p:StartupObject=Liangtu.BasketChecks -p:UseSharedCompilation=false -o $output > (Join-Path $root 'artifacts/choreography-build.log')
if($LASTEXITCODE -ne 0){Get-Content (Join-Path $root 'artifacts/choreography-build.log') -Tail 20;throw 'Build failed'}
$assembly=Join-Path $output 'Liangtu.dll'
$process=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList ('"'+$assembly+'" "'+$data+'"') -WindowStyle Hidden -PassThru -RedirectStandardOutput ($data+'-stdout.log') -RedirectStandardError ($data+'-stderr.log')
if(-not $process.WaitForExit(45000)){$process.Kill();throw 'Owned WPF test process did not exit within45seconds'}
$code=$process.ExitCode
$results=Join-Path $data 'results.txt'
Get-Content $results | Where-Object {$_ -match '^FAIL'}
Write-Output ('Pass='+(Select-String '^PASS' $results).Count+'; Fail='+(Select-String '^FAIL' $results).Count+'; Exit='+$code)
if($code -ne 0){throw ('WPF test exit='+$code)}
