$ErrorActionPreference='Stop'
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
Push-Location $PSScriptRoot
try {
 & (Join-Path $PSScriptRoot 'scripts/build-native.ps1')
 dotnet run --project tests/Core.Tests -c Release -p:UseSharedCompilation=false
 if($LASTEXITCODE -ne 0){throw 'Core tests failed'}
 dotnet build src/Liangtu.Core -c Release -p:IntermediateOutputPath=obj/SafeChecks/ -p:UseSharedCompilation=false -o artifacts/safety-v2
 if($LASTEXITCODE -ne 0){throw 'Core build failed'}
 dotnet build src/Liangtu -c Release -p:StartupObject=Liangtu.SafeChecks -p:BuildProjectReferences=false -p:IntermediateOutputPath=obj/SafeChecks/ -p:UseSharedCompilation=false -o artifacts/safety-v2
 if($LASTEXITCODE -ne 0){throw 'Safety build failed'}
 dotnet artifacts/safety-v2/Liangtu.dll artifacts/safety-v2-final-data
 if($LASTEXITCODE -ne 0){throw 'Safety checks failed'}
} finally { Pop-Location }
