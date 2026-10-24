param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$SourceVideo,
    [string]$RecordingFolder = (Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::MyVideos)) '屏幕录制')
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testId = [guid]::NewGuid().ToString('N')
$data = Join-Path $projectRoot ('artifacts/intake-' + $testId)
$testFile = Join-Path $RecordingFolder ('Liangtu-intake-test-' + $testId + '.mp4')
$process = $null
New-Item -ItemType Directory -Force -Path $data, $RecordingFolder | Out-Null
@{Welcomed=$true; Sounds=$false; HandleScreenshots=$true; ClipboardImages=$false; HotkeyKey=0; CaptureHotkeyKey=0; Keep=100} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $data 'settings.json') -Encoding utf8
try {
    $process = Start-Process -FilePath (Resolve-Path -LiteralPath $Executable).Path -ArgumentList '--data',('"'+$data+'"') -WindowStyle Hidden -PassThru
    $deadline = (Get-Date).AddSeconds(10)
    do {
        Start-Sleep -Milliseconds 200
        if ($process.HasExited) { throw 'Test application exited before starting watcher' }
        $log = Get-Content -LiteralPath (Join-Path $data 'app.log') -Raw -ErrorAction SilentlyContinue
    } until ($log -like '*Started pid=*' -or (Get-Date) -ge $deadline)
    if ($log -notlike '*Started pid=*') { throw 'Watcher startup timed out' }
    Copy-Item -LiteralPath $SourceVideo -Destination $testFile
    $deadline = (Get-Date).AddSeconds(8)
    do {
        Start-Sleep -Milliseconds 250
        $settings = Get-Content -LiteralPath (Join-Path $data 'settings.json') -Raw -Encoding utf8 | ConvertFrom-Json
    } until ($testFile -in $settings.Items -or (Get-Date) -ge $deadline)
    if ($testFile -notin $settings.Items) { throw 'Recording in the system folder was not collected' }
    $result = 'PASS real default recording folder intake: ' + $RecordingFolder
    Set-Content -LiteralPath (Join-Path $data 'result.txt') -Value $result -Encoding utf8
    Write-Output $result
} catch {
    Set-Content -LiteralPath (Join-Path $data 'result.txt') -Value ('FAIL ' + $_.Exception.Message) -Encoding utf8
    throw
} finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
    if (Test-Path -LiteralPath $testFile) { Remove-Item -LiteralPath $testFile }
    Write-Output ('Evidence: ' + $data)
}
