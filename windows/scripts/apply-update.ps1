param([Parameter(Mandatory=$true)][string]$Plan)
# Detached helper: the plan and package are prepared and verified by Liangtu.
# All mutations are confined to explicit program files; unknown files and user directories are retained.
$ErrorActionPreference='Stop'
$planPath=[IO.Path]::GetFullPath($Plan)
$workRoot=Split-Path -Parent $planPath
$log=Join-Path $workRoot 'update-result.log'
$changed=New-Object 'System.Collections.Generic.List[string]'
$backed=@{}
$completed=$false
$runtimeCultures=@([Globalization.CultureInfo]::GetCultures([Globalization.CultureTypes]::AllCultures) | ForEach-Object {$_.Name})
function Write-Log([string]$Message) { Add-Content -LiteralPath $log -Value ((Get-Date -Format o)+' '+$Message) -Encoding UTF8 }
function Assert-NoLinks([string]$Path) {
    $cursor=[IO.Path]::GetFullPath($Path)
    while($cursor) {
        if(Test-Path -LiteralPath $cursor) { if(((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw ('Reparse point rejected: '+$cursor)} }
        $next=Split-Path -Parent $cursor
        if($next -eq $cursor){break}; $cursor=$next
    }
}
function Safe-File([string]$Root,[string]$Name) {
    $normalized=$Name.Replace('\','/')
    if([string]::IsNullOrWhiteSpace($normalized) -or $normalized.StartsWith('/') -or $normalized.Contains(':')){throw 'Invalid package path'}
    foreach($part in $normalized.Split('/')) { if($part -in @('','.','..') -or $part.EndsWith('.') -or $part.EndsWith(' ') -or $part -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)'){throw 'Invalid package path component'} }
    $first=$normalized.Split('/')[0]
    $parts=$normalized.Split('/')
    $runtimeResource=$parts.Count -eq 2 -and $first -in $runtimeCultures -and $parts[1].EndsWith('.resources.dll',[StringComparison]::OrdinalIgnoreCase)
    if($normalized.Contains('/') -and $first -notin @('languages','licenses','assets','runtimes') -and -not $runtimeResource){throw 'Non-application directory rejected'}
    if($first -in @('settings.json','config.json','data','unins000.exe','unins000.dat')){throw 'Protected file rejected'}
    $full=[IO.Path]::GetFullPath((Join-Path $Root $normalized))
    if(-not $full.StartsWith($Root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Path escaped root'}
    Assert-NoLinks $full
    return $full
}
function Compare-SemVer([string]$A,[string]$B) {
    $pattern='^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-([0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*))?(\+[0-9A-Za-z.-]+)?$'
    if($A -notmatch $pattern -or $B -notmatch $pattern){throw 'Invalid version'}
    $aa=$A.Split('+')[0].Split('-',2); $bb=$B.Split('+')[0].Split('-',2)
    $n=([version]$aa[0]).CompareTo([version]$bb[0]); if($n -ne 0){return $n}
    if($aa.Count -eq 1 -and $bb.Count -eq 1){return 0}; if($aa.Count -eq 1){return 1}; if($bb.Count -eq 1){return -1}
    $ap=$aa[1].Split('.');$bp=$bb[1].Split('.')
    for($i=0;$i -lt [Math]::Min($ap.Count,$bp.Count);$i++){
        $an=$ap[$i] -match '^\d+$';$bn=$bp[$i] -match '^\d+$'
        if($an -and $bn){$n=$ap[$i].Length.CompareTo($bp[$i].Length);if($n -eq 0){$n=[string]::CompareOrdinal($ap[$i],$bp[$i])}}
        elseif($an -ne $bn){if($an){$n=-1}else{$n=1}}
        else{$n=[string]::CompareOrdinal($ap[$i],$bp[$i])}
        if($n -ne 0){return $n}
    };return $ap.Count.CompareTo($bp.Count)
}
try {
    Assert-NoLinks $planPath
    $p=Get-Content -LiteralPath $planPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if($p.schemaVersion -ne 1 -or $p.product -cne 'Liangtu' -or $p.kind -notin @('portable','installer')){throw 'Invalid update plan'}
    if((Compare-SemVer $p.version $p.currentVersion) -le 0){throw 'Downgrade or repeated update rejected'}
    $install=[IO.Path]::GetFullPath([string]$p.installRoot).TrimEnd('\')
    $stage=[IO.Path]::GetFullPath([string]$p.stageRoot).TrimEnd('\')
    Assert-NoLinks $install;Assert-NoLinks $stage
    if(-not(Test-Path -LiteralPath (Join-Path $install 'Liangtu.exe') -PathType Leaf)){throw 'Installation root has no Liangtu.exe'}
    if($install -eq $stage -or $stage.StartsWith($install+'\',[StringComparison]::OrdinalIgnoreCase) -or $install.StartsWith($stage+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Staging and installation must be separate'}
    $entries=@($p.files.PSObject.Properties)
    if($entries.Count -eq 0 -or $entries.Count -gt 10000){throw 'Invalid update file count'}
    $seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in $entries) {
        $source=Safe-File $stage $entry.Name
        $target=Safe-File $install $entry.Name
        if(-not $seen.Add($entry.Name.Replace('\','/')) -or $entry.Value -notmatch '^[0-9A-Fa-f]{64}$'){throw 'Invalid hash manifest'}
        if(-not(Test-Path -LiteralPath $source -PathType Leaf) -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.Value){throw ('Staged checksum mismatch: '+$entry.Name)}
    }
    if($p.kind -eq 'portable' -and -not $seen.Contains('Liangtu.exe')){throw 'Portable package missing executable'}
    if($p.kind -eq 'installer' -and (-not $seen.Contains([string]$p.installerFile) -or [IO.Path]::GetExtension([string]$p.installerFile) -ne '.exe')){throw 'Missing installer'}
    [IO.File]::WriteAllText((Join-Path $workRoot 'helper-ready'),'ready')
    if([int]$p.parentId -gt 0) {
        $parent=Get-Process -Id ([int]$p.parentId) -ErrorAction SilentlyContinue
        if($parent) {
            if($parent.StartTime.ToUniversalTime().ToString('o') -ne [string]$p.parentStartUtc){throw 'Parent PID was reused'}
            if(-not $parent.WaitForExit(120000)){throw 'Application did not exit; update cancelled'}
        }
    }
    # Verify again after waiting. No plan-supplied commands or arguments are evaluated.
    foreach($entry in $entries){$source=Safe-File $stage $entry.Name;if((Get-FileHash -LiteralPath $source).Hash -ne $entry.Value){throw 'Staged file changed while waiting'}}
    if($p.kind -eq 'installer') {
        $installer=Safe-File $stage ([string]$p.installerFile)
        $result=Start-Process -FilePath $installer -ArgumentList @('/SILENT','/NORESTART','/KEEPVIDEO=1',('/DIR="'+$install+'"')) -WindowStyle Hidden -Wait -PassThru
        if($result.ExitCode -ne 0){throw ('Installer cancelled or failed with exit code '+$result.ExitCode)}
    } else {
        $backup=Join-Path $workRoot 'backup'
        New-Item -ItemType Directory -Path $backup | Out-Null
        # Back up everything before any replacement. Never remove unrelated or obsolete files.
        foreach($entry in $entries) {
            $target=Safe-File $install $entry.Name
            if(Test-Path -LiteralPath $target -PathType Leaf) {
                $saved=Safe-File $backup $entry.Name
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $saved) | Out-Null
                Copy-Item -LiteralPath $target -Destination $saved
                $backed[$entry.Name]=$saved
            }
        }
        foreach($entry in $entries) {
            $source=Safe-File $stage $entry.Name;$target=Safe-File $install $entry.Name
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
            $changed.Add($entry.Name)
            $temporary=$target+'.liangtu-update-'+[Guid]::NewGuid().ToString('N')+'.tmp'
            try {
                Copy-Item -LiteralPath $source -Destination $temporary
                if(Test-Path -LiteralPath $target){[IO.File]::Replace($temporary,$target,[NullString]::Value)}else{[IO.File]::Move($temporary,$target)}
            } finally { if(Test-Path -LiteralPath $temporary -PathType Leaf){Remove-Item -LiteralPath $temporary -Force} }
            if((Get-FileHash -LiteralPath $target).Hash -ne $entry.Value){throw 'Installed checksum mismatch'}
        }
    }
    $completed=$true; Write-Log ('SUCCESS updated to '+$p.version)
} catch {
    $reason=$_.Exception.Message;Write-Log ('FAIL '+$reason)
    if($changed.Count -gt 0) {
        $rollbackOk=$true
        for($i=$changed.Count-1;$i -ge 0;$i--){
            $name=$changed[$i]
            try {
                $target=Safe-File $install $name
                if($backed.ContainsKey($name)){
                    if((Test-Path -LiteralPath $target -PathType Leaf) -and (Get-FileHash -LiteralPath $target).Hash -eq (Get-FileHash -LiteralPath $backed[$name]).Hash){continue}
                    Copy-Item -LiteralPath $backed[$name] -Destination $target -Force
                }
                elseif(Test-Path -LiteralPath $target -PathType Leaf){Remove-Item -LiteralPath $target -Force}
            }catch{$rollbackOk=$false;Write-Log ('ROLLBACK FAILED '+$name+' '+$_.Exception.Message)}
        }
        if($rollbackOk){Write-Log 'ROLLBACK completed'}else{Write-Log ('Restore backup manually from '+$backup)}
    }
    if($p.restart){try{Add-Type -AssemblyName System.Windows.Forms;[System.Windows.Forms.MessageBox]::Show(('Update failed. Existing data was retained. See: '+$log+[Environment]::NewLine+$reason),'Liangtu update')|Out-Null}catch{}}
}
if($completed -and $p.restart){try{Start-Process -FilePath (Join-Path $install 'Liangtu.exe') -ArgumentList '--show' -WorkingDirectory $install -WindowStyle Hidden | Out-Null}catch{Write-Log ('Restart failed: '+$_.Exception.Message)}}
if(-not $completed){exit 1}


