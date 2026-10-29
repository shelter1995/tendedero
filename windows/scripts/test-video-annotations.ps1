param([string]$RunName = 'video-annotations-final', [switch]$Interactive)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$component = Join-Path $projectRoot 'artifacts/jellyfin-component'
if (Test-Path -LiteralPath (Join-Path $component 'ffmpeg.exe')) { $env:PATH = $component + ';' + $env:PATH }
Push-Location $projectRoot
try {
    $fixture = Join-Path $projectRoot 'artifacts/video-fixtures/source.mp4'
    if (-not (Test-Path -LiteralPath $fixture)) { throw 'Run scripts/test-video.ps1 once to generate owned video fixtures.' }
    dotnet build src/Liangtu -c Release -p:StartupObject=Liangtu.VideoAnnotationChecks -p:UseSharedCompilation=false -o artifacts/video-annotations-checks
    if ($LASTEXITCODE -ne 0) { throw 'Video annotation checks build failed' }
    $data = Join-Path $projectRoot ('artifacts/' + $RunName)
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    if ($Interactive) {
        $copy = Join-Path $data 'video-example.mp4'
        Copy-Item -LiteralPath $fixture -Destination $copy
        dotnet artifacts/video-annotations-checks/Liangtu.dll $data $copy --interactive
    } else {
        dotnet artifacts/video-annotations-checks/Liangtu.dll $data $fixture
        $result = $LASTEXITCODE
        Get-Content -LiteralPath (Join-Path $data 'results.txt')
        if ($result -ne 0) { throw 'Video annotation checks failed' }
    }
} finally { Pop-Location }
