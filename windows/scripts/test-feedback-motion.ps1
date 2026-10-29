param([string]$RunName='feedback104-final')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME=Join-Path $projectRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$feedbackOutput=Join-Path $projectRoot 'artifacts/feedback-motion-bin'
$feedbackLog=Join-Path $projectRoot ('artifacts/'+$RunName+'-build.log')
dotnet build (Join-Path $projectRoot 'src/Liangtu.Core') -c Release -p:IntermediateOutputPath=obj/FeedbackMotion/ -p:UseSharedCompilation=false -o $feedbackOutput *> $feedbackLog
if($LASTEXITCODE -ne 0){Get-Content $feedbackLog -Tail 20;throw 'Core build failed'}
foreach($feedbackSuite in @('FeedbackMotionChecks','WindowsSnipOriginChecks','WindowsSnipIntakeChecks','ClipboardFlightChecks','PinHandoffChecks','PinMorphChecks')) {
    dotnet build (Join-Path $projectRoot 'src/Liangtu') -c Release ('-p:StartupObject=Liangtu.'+$feedbackSuite) -p:BuildProjectReferences=false -p:IntermediateOutputPath=obj/FeedbackMotion/ -p:UseSharedCompilation=false -o $feedbackOutput *> $feedbackLog
    if($LASTEXITCODE -ne 0){Get-Content $feedbackLog -Tail 20;throw 'Build failed'}
    $feedbackData=Join-Path $projectRoot ('artifacts/'+$RunName+'-'+$feedbackSuite)
    dotnet (Join-Path $feedbackOutput 'Liangtu.dll') $feedbackData *> ($feedbackData+'.log')
    $feedbackExit=$LASTEXITCODE
    $feedbackResults=Join-Path $feedbackData 'results.txt'
    Get-Content $feedbackResults | Where-Object {$_ -match '^FAIL'}
    Write-Output ($feedbackSuite+': PASS='+@(Select-String '^PASS' $feedbackResults).Count+' FAIL='+@(Select-String '^FAIL' $feedbackResults).Count+' exit='+$feedbackExit)
    if($feedbackExit -ne 0){throw ('Suite failed: '+$feedbackSuite)}
}
