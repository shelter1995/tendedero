param(
    [Parameter(Mandatory=$true)][string]$SourceVideo,
    [string]$RunName = ('scrub-' + [guid]::NewGuid().ToString('N'))
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$source = (Resolve-Path -LiteralPath $SourceVideo).Path
Push-Location $projectRoot
try {
    dotnet build src/Liangtu -c Release -p:StartupObject=Liangtu.VideoScrubChecks -p:UseSharedCompilation=false -o artifacts/scrub-checks
    if ($LASTEXITCODE -ne 0) { throw 'Scrub checks build failed' }
    $data = Join-Path $projectRoot ('artifacts/' + $RunName)
    dotnet artifacts/scrub-checks/Liangtu.dll $data $source
    $result = $LASTEXITCODE
    Get-Content -LiteralPath (Join-Path $data 'results.txt')
    if ($result -ne 0) { throw 'Scrub checks failed' }
} finally { Pop-Location }
