param([string]$RunName = ('video-' + [guid]::NewGuid().ToString('N')))
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $projectRoot
try {
    $fixture = Join-Path $projectRoot 'artifacts/video-fixtures'
    New-Item -ItemType Directory -Force $fixture | Out-Null
    $source = Join-Path $fixture 'source.mp4'
    ffmpeg -v error -f lavfi -i 'testsrc2=size=640x360:rate=30:duration=12' -f lavfi -i 'sine=frequency=440:duration=12' -c:v libx264 -pix_fmt yuv420p -g 180 -c:a aac -y $source
    if ($LASTEXITCODE -ne 0) { throw 'Fixture generation failed; FFmpeg must be installed' }
    foreach ($ext in @('mov', 'm4v')) {
        ffmpeg -v error -i $source -c copy -y (Join-Path $fixture ('source.' + $ext))
        if ($LASTEXITCODE -ne 0) { throw "Fixture failed: $ext" }
    }
    ffmpeg -v error -i $source -c:v mpeg4 -c:a mp3 -y (Join-Path $fixture 'source.avi')
    if ($LASTEXITCODE -ne 0) { throw 'AVI fixture failed' }
    ffmpeg -v error -i $source -c:v wmv2 -c:a wmav2 -y (Join-Path $fixture 'source.wmv')
    if ($LASTEXITCODE -ne 0) { throw 'WMV fixture failed' }
    dotnet build src/Liangtu -c Release -p:StartupObject=Liangtu.VideoFlowChecks -p:UseSharedCompilation=false -o artifacts/video-checks
    if ($LASTEXITCODE -ne 0) { throw 'Video checks build failed' }
    $data = Join-Path $projectRoot ('artifacts/' + $RunName)
    # Invoke dotnet explicitly so the test always uses the selected entry point.
    dotnet artifacts/video-checks/Liangtu.dll $data $source
    $result = $LASTEXITCODE
    Get-Content -LiteralPath (Join-Path $data 'results.txt')
    if ($result -ne 0) { throw 'Video flow checks failed' }
} finally { Pop-Location }
