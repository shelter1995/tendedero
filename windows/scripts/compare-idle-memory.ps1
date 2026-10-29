param([int]$Runs=2,[string]$Theme='color-play')
$ErrorActionPreference='Stop'
$idleCompareRoot=Split-Path $PSScriptRoot -Parent
$idleCompareResults=@()
foreach($idleRun in 1..$Runs) {
 foreach($idleKind in @('baseline','optimized')) {
  $idleCompareVariant='final-'+$idleKind+'-'+$idleRun
  $idleCompareSource=if($idleKind -eq 'baseline'){Join-Path $idleCompareRoot 'artifacts/idle-memory/baseline-source'}else{$idleCompareRoot}
  $idleCompareLog=Join-Path $idleCompareRoot ('artifacts/idle-memory/'+$idleCompareVariant+'.log')
  & (Join-Path $PSScriptRoot 'test-idle-memory.ps1') -Variant $idleCompareVariant -SourceRoot $idleCompareSource -Theme $Theme > $idleCompareLog
  $idleCompareData=(Get-ChildItem -LiteralPath (Join-Path $idleCompareRoot 'artifacts/idle-memory') -Directory -Filter ('data-'+$idleCompareVariant+'-'+$Theme+'-*') | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).FullName
  if(-not $idleCompareData){throw 'Missing idle comparison evidence'}
  $idleCompareSnapshots=Get-Content -LiteralPath (Join-Path $idleCompareData 'measurements.json') -Raw | ConvertFrom-Json
  $idleCompareResults+=[pscustomobject]@{kind=$idleKind;run=$idleRun;data=$idleCompareData;snapshots=$idleCompareSnapshots}
  $idleBlank=$idleCompareSnapshots | Where-Object stage -eq 'blank-hidden'
  $idleFirst=$idleCompareSnapshots | Where-Object stage -eq 'first-content-rendered'
  Write-Output ($idleKind+' run'+$idleRun+': blank commit='+[math]::Round($idleBlank.privateCommitMiB,2)+'MiB privateWS='+[math]::Round($idleBlank.privateWorkingSetMiB,2)+'MiB first='+[math]::Round($idleFirst.responseMs,2)+'ms')
 }
}
[IO.File]::WriteAllText((Join-Path $idleCompareRoot 'artifacts/idle-memory/final-comparison.json'),($idleCompareResults | ConvertTo-Json -Depth 8))
