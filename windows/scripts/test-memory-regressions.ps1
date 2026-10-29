param([string[]]$Cases=@('CollectionChecks','BasketChecks','ThemeChecks','MotionPerformanceChecks'))
$ErrorActionPreference='Stop'
$memoryRoot=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME=Join-Path $memoryRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
Push-Location $memoryRoot
try {
 foreach($memoryCase in $Cases) {
  $memoryOutput=Join-Path $memoryRoot ('artifacts/memory-optimization/regression-'+$memoryCase)
  $memoryData=Join-Path $memoryRoot ('artifacts/memory-optimization/check-'+$memoryCase+'-'+[guid]::NewGuid().ToString('N'))
  $memoryBuildLog=$memoryData+'-build.log'
  dotnet build src/Liangtu -c Release ('-p:StartupObject=Liangtu.'+$memoryCase) -p:UseSharedCompilation=false -o $memoryOutput > $memoryBuildLog
  if($LASTEXITCODE -ne 0){Get-Content -LiteralPath $memoryBuildLog -Tail 20;throw ($memoryCase+' build failed')}
  $memoryArguments='"'+(Join-Path $memoryOutput 'Liangtu.dll')+'" "'+$memoryData+'"'
  $memoryProcess=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $memoryArguments -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput ($memoryData+'-stdout.log') -RedirectStandardError ($memoryData+'-stderr.log')
  $memoryResults=if(Test-Path -LiteralPath (Join-Path $memoryData 'results.txt')){Get-Content -LiteralPath (Join-Path $memoryData 'results.txt')}else{Get-Content -LiteralPath ($memoryData+'-stdout.log')}
  $memoryFailures=@($memoryResults | Where-Object {$_ -match '^FAIL'})
  Write-Output ($memoryCase+': Pass='+@($memoryResults | Where-Object {$_ -match '^PASS'}).Count+' Fail='+$memoryFailures.Count+' Exit='+$memoryProcess.ExitCode)
  $memoryFailures | Write-Output
  Write-Output ('Evidence: '+$memoryData)
  if($memoryProcess.ExitCode -ne 0 -or $memoryFailures.Count -ne 0){throw ($memoryCase+' failed')}
 }
} finally {Pop-Location}
