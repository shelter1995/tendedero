param([ValidateSet('baseline','optimized')][string]$Variant='optimized',[string]$SourceRoot='')
$ErrorActionPreference='Stop'
$memoryProjectRoot=Split-Path $PSScriptRoot -Parent
if(-not $SourceRoot){$SourceRoot=$memoryProjectRoot}
$env:DOTNET_CLI_HOME=Join-Path $memoryProjectRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$memoryOutput=Join-Path $memoryProjectRoot ('artifacts/memory-optimization/'+$Variant)
$memoryData=Join-Path $memoryProjectRoot ('artifacts/memory-optimization/data-'+$Variant+'-'+[guid]::NewGuid().ToString('N'))
Push-Location $memoryProjectRoot
try {
 dotnet build (Join-Path $SourceRoot 'src/Liangtu.Core') -c Release -p:IntermediateOutputPath=obj/MemoryChecks/ -p:UseSharedCompilation=false -o $memoryOutput
 if($LASTEXITCODE -ne 0){throw 'Memory core build failed'}
 dotnet build (Join-Path $SourceRoot 'src/Liangtu') -c Release -p:StartupObject=Liangtu.MemoryChecks -p:BuildProjectReferences=false -p:IntermediateOutputPath=obj/MemoryChecks/ -p:UseSharedCompilation=false -o $memoryOutput
 if($LASTEXITCODE -ne 0){throw 'Memory harness build failed'}
 $memoryProcess=Start-Process -FilePath (Join-Path $memoryOutput 'Liangtu.exe') -ArgumentList ('"'+$memoryData+'"') -WindowStyle Hidden -PassThru
 if(-not $memoryProcess.WaitForExit(60000)){throw ('Memory checks timed out; pid='+$memoryProcess.Id)}
 Get-Content -LiteralPath (Join-Path $memoryData 'results.txt')
 Write-Host ('Memory evidence: '+$memoryData)
 if($memoryProcess.ExitCode -ne 0){throw 'Memory checks failed'}
} finally {Pop-Location}
