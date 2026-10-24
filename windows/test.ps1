$ErrorActionPreference='Stop'
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
Push-Location $PSScriptRoot
try {
 & (Join-Path $PSScriptRoot 'scripts/build-native.ps1')
 New-Item -ItemType Directory -Path 'artifacts' -Force | Out-Null
 dotnet run --project tests/Core.Tests -c Release | Tee-Object -FilePath 'artifacts/core-tests.txt'
 if($LASTEXITCODE -ne 0){throw 'Core tests failed'}
 dotnet publish src/Liangtu -c Release -o dist/Liangtu
 if($LASTEXITCODE -ne 0){throw 'Publish failed'}
 $ffmpeg=Get-Command ffmpeg -ErrorAction SilentlyContinue
 if($ffmpeg){
  & $ffmpeg.Source -hide_banner -loglevel error -f lavfi -i 'color=c=red:s=320x240:r=30:d=2' -f lavfi -i 'color=c=lime:s=320x240:r=30:d=2' -f lavfi -i 'color=c=blue:s=320x240:r=30:d=2' -filter_complex '[0:v][1:v][2:v]concat=n=3:v=1:a=0[v]' -map '[v]' -c:v libx264 -g 180 -keyint_min 180 -sc_threshold 0 -y 'dist/trim-test.mp4'
  if($LASTEXITCODE -ne 0){throw 'Video fixture generation failed'}
 }
 $data=Join-Path $PSScriptRoot ('artifacts/release-test-'+[guid]::NewGuid().ToString('N'))
 $exe=Join-Path $PSScriptRoot 'dist/Liangtu/Liangtu.exe'
 $process=Start-Process -FilePath $exe -ArgumentList '--self-test','--data',('"' + $data + '"') -WindowStyle Hidden -PassThru
 if(-not $process.WaitForExit(45000)){throw 'Self-test did not finish'}
 Get-Content -LiteralPath (Join-Path $data 'results.txt')
 if($process.ExitCode -ne 0 -or (Test-Path -LiteralPath (Join-Path $data 'failure.txt'))){throw 'Windows self-test failed'}
 New-Item -ItemType Directory -Path 'artifacts/release-test' -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $data 'results.txt') -Destination 'artifacts/release-test/results.txt'
 Copy-Item -LiteralPath (Join-Path $data 'line-preview.png') -Destination 'artifacts/release-test/line-preview.png'
 Copy-Item -LiteralPath (Join-Path $data 'menu-preview.png') -Destination 'artifacts/release-test/menu-preview.png'
} finally { Pop-Location }
