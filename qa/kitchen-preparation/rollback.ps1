$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Only disposable Windows CI'}
$entry=Get-Content build-entry.json -Raw|ConvertFrom-Json
$root=Join-Path $env:RUNNER_TEMP ('kitchen-update-'+[Guid]::NewGuid().ToString('N'))
$app=Join-Path $root 'app'
$stage=Join-Path $root 'stage'
New-Item -ItemType Directory $app,$stage -Force|Out-Null
Copy-Item previous/* $app -Recurse
Expand-Archive ('release-output/'+$entry.Package) $stage
$names=@('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')
$before=@{}
foreach($name in $names){$before[$name]=(Get-FileHash (Join-Path $app $name)).Hash}
# Use the exact installed updater block, without adding rollback around it.
$source=Get-Content installed-UpdateManager.cs -Raw
$start=$source.IndexOf('# Copia somente arquivos aprovados pelo PDV.')
$end=$source.IndexOf('Start-Process -FilePath $exe',$start)
if($start -lt 0 -or $end -le $start){throw 'Installed copy block not found'}
$block=$source.Substring($start,$end-$start)
$script="`$ErrorActionPreference='Stop'`n`$stage='"+$stage.Replace("'","''")+"'`n`$app='"+$app.Replace("'","''")+"'`n"+$block
$ps=Join-Path $root 'apply-copy.ps1'
Set-Content $ps $script -Encoding UTF8
# Lock the second file in the same filesystem enumeration used by the updater.
$files=@(Get-ChildItem $stage -Recurse -File)
if($files.Count -ne 3){throw 'Unexpected payload'}
$locked=Join-Path $app $files[1].Name
$lock=[IO.File]::Open($locked,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try{
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $ps *> release-output/interrupted-copy.log
  $copyExit=$LASTEXITCODE
}finally{$lock.Dispose()}
if($copyExit -eq 0){throw 'Failure injection did not block the real copy'}
$changed=@($names|Where-Object{(Get-FileHash (Join-Path $app $_)).Hash -ne $before[$_]})
$unchanged=@($names|Where-Object{(Get-FileHash (Join-Path $app $_)).Hash -eq $before[$_]})
if($changed.Count -eq 0 -or $unchanged.Count -eq 0){throw 'Failure did not exercise a partially applied update'}
$oldOnly=$true
$result=[ordered]@{
  Channel=$entry.Channel; Version=$entry.Version; InstalledVersion=$entry.Baseline.Version
  InstalledSource=$entry.BaselineSource; CandidateSource=$entry.SourceCommit
  RealInstalledCopyBlock=$true; FailedCopyExit=$copyExit
  ChangedAfterFailure=$changed; StillPreviousAfterFailure=$unchanged
  AutomaticExecutableRollbackPassed=$false
  ExistingExternalProtectionRestrictedTo001Version10404=$oldOnly
  SafeForPublication=$false; ProductionAccess=$false
  Reason='Installed updater leaves mixed executable versions after a real copy failure; existing bridge does not cover this release/channels.'
}
$result|ConvertTo-Json -Depth 6|Set-Content release-output/rollback-validation.json -Encoding UTF8
$candidate=Get-Content release-output/candidate.json -Raw|ConvertFrom-Json
$candidate|Add-Member -NotePropertyName PublicationAuthorized -NotePropertyValue $false -Force
$candidate|Add-Member -NotePropertyName RollbackPassed -NotePropertyValue $false -Force
$candidate|Add-Member -NotePropertyName PreparedOnly -NotePropertyValue $true -Force
$candidate|ConvertTo-Json -Depth 20|Set-Content release-output/candidate.json -Encoding UTF8
Write-Output 'BLOCKED: exact installed updater left mixed versions; no automatic rollback for this transition. No production files touched.'
exit 1
