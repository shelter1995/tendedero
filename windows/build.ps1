param([switch]$Publish)
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
Push-Location $PSScriptRoot
try {
 & (Join-Path $PSScriptRoot 'scripts/build-native.ps1')
 dotnet run --project tests/Core.Tests -c Release
 if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
 & (Join-Path $PSScriptRoot 'scripts/package-portable.ps1')
} finally { Pop-Location }
