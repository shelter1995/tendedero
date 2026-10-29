param([string]$Variant='baseline',[string]$Theme='color-play',[string]$SourceRoot='',[string]$Experiment='default')
$ErrorActionPreference='Stop'
$idleRoot=Split-Path $PSScriptRoot -Parent
if(-not $SourceRoot){$SourceRoot=$idleRoot}
$env:DOTNET_CLI_HOME=Join-Path $idleRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$idleOutput=Join-Path $idleRoot ('artifacts/idle-memory/'+$Variant)
$idleData=Join-Path $idleRoot ('artifacts/idle-memory/data-'+$Variant+'-'+$Theme+'-'+[guid]::NewGuid().ToString('N'))
Push-Location $idleRoot
try {
 dotnet build (Join-Path $SourceRoot 'src/Liangtu.Core') -c Release -p:IntermediateOutputPath=obj/IdleMemory/ -p:UseSharedCompilation=false -o $idleOutput
 if($LASTEXITCODE -ne 0){throw 'Idle benchmark core build failed'}
 dotnet build (Join-Path $SourceRoot 'src/Liangtu') -c Release -p:StartupObject=Liangtu.IdleMemoryChecks -p:BuildProjectReferences=false -p:IntermediateOutputPath=obj/IdleMemory/ -p:UseSharedCompilation=false -o $idleOutput
 if($LASTEXITCODE -ne 0){throw 'Idle benchmark build failed'}
 $idleProcess=Start-Process -FilePath (Join-Path $idleOutput 'Liangtu.exe') -ArgumentList ('"'+$idleData+'" '+$Theme+' '+$Experiment) -WindowStyle Hidden -Wait -PassThru
 Get-Content -LiteralPath (Join-Path $idleData 'results.txt')
 Write-Host ('Idle evidence: '+$idleData)
 if($idleProcess.ExitCode -ne 0){throw 'Idle checks failed'}
} finally {Pop-Location}
