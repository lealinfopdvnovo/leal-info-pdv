$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Disposable Windows only'}
$names=@('LealInfoPDV.deps.json','LealInfoPDV.dll','LealInfoPDV.exe')
$entry=Get-Content validated/release-output/candidate.json -Raw|ConvertFrom-Json
if($entry.Channel -ne '003' -or $entry.Version -ne '10.423' -or $entry.SourceCommit -ne $env:LAYOUT_SOURCE){throw 'Wrong candidate identity'}
$ui=Get-Content validated/layout-evidence/results.json -Raw|ConvertFrom-Json
if($ui.success -ne $true){throw 'UI not approved'}
$package=(Resolve-Path ('validated/release-output/'+$entry.Package)).Path
if((Get-FileHash $package).Hash.ToLowerInvariant() -ne $entry.PackageSha256){throw 'Candidate changed'}
$metadata=@(Get-ChildItem original -Filter candidate.json -Recurse)
if($metadata.Count -ne 1){throw 'Ambiguous 10.422 evidence'}
$old=Get-Content $metadata[0].FullName -Raw|ConvertFrom-Json
$baseline=(Resolve-Path (Join-Path $metadata[0].Directory.FullName $old.Package)).Path
if((Get-FileHash $baseline).Hash.ToLowerInvariant() -ne '546c776f7b9f48a3462f30ba257c8120c1f7bc144d957698565efe98f246e646'){throw '10.422 immutable baseline changed'}
if($old.Channel -ne '003' -or $old.Version -ne '10.422'){throw 'Wrong protected baseline'}
$worker=(Resolve-Path TransactionalUpdate.ps1).Path
# The worker and C# button apply path remain the ones validated previously.
function Normalize([string]$s){$s.Trim().Replace("`r`n","`n").TrimStart([char]0xfeff)}
$workerOriginal=git show be3d36780eec0bf90aef7666ac5df158115a8e99:TransactionalUpdate.ps1
if((Normalize ($workerOriginal -join "`n")) -ne (Normalize (Get-Content $worker -Raw))){throw 'Worker changed; wider validation required'}
$oldManager=(git show be3d36780eec0bf90aef7666ac5df158115a8e99:UpdateManager.cs) -join "`n"
$newManager=Get-Content UpdateManager.cs -Raw
if((Normalize $oldManager).Replace('10.422','VERSION') -ne (Normalize $newManager).Replace('10.423','VERSION')){throw 'Button updater changed; wider validation required'}
foreach($sourceName in @('TransactionalUpdate.cs','Licensing/UpdatePayloadPolicy.cs','Program.cs')){
 $previousSource=(git show ("be3d36780eec0bf90aef7666ac5df158115a8e99:"+$sourceName)) -join "`n"
 if((Normalize $previousSource) -ne (Normalize (Get-Content $sourceName -Raw))){throw ('Protected startup or payload policy changed: '+$sourceName)}
}
$results=[Collections.Generic.List[object]]::new()
function TaskName($app){$sha=[Security.Cryptography.SHA256]::Create();try{'LealInfoPDV-Recovery-'+([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($app.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$sha.Dispose()}}
foreach($scenario in @('success','interruption','bad-sha')){
 $app=Join-Path $env:RUNNER_TEMP ('layout-003-'+[Guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory $app|Out-Null
 Expand-Archive $baseline $app
 Set-Content (Join-Path $app 'QA-ISOLATED') 'Isolated 10.422 rollback fixture'
 $sentinels=@('lealinfo.db','network.license','network.config','stock.dat','settings.json')
 foreach($f in $sentinels){[IO.File]::WriteAllText((Join-Path $app $f),'UNCHANGED-'+$f)}
 try{
  $sha=if($scenario -eq 'bad-sha'){'0'*64}else{$entry.PackageSha256}
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Prepare -App $app -Channel 003 -Version 10.423 -Package $package -Sha256 $sha
  if($scenario -eq 'bad-sha'){
   if($LASTEXITCODE -eq 0){throw 'Corrupt package accepted'}
  }else{
   if($LASTEXITCODE -ne 0){throw 'Backup preparation failed'}
   foreach($n in $names){if((Get-FileHash (Join-Path $app ('.pdv-update/previous/'+$n))).Hash.ToLowerInvariant() -ne $old.TargetFiles.$n){throw 'Backup differs from 10.422'}}
   $fault=if($scenario -eq 'interruption'){'copy-LealInfoPDV.dll'}else{''}
   & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Apply -App $app -Channel 003 -Version 10.423 -Package $package -Sha256 $sha -Fault $fault
   if($scenario -eq 'success'){
    if($LASTEXITCODE -ne 0){throw 'Apply failed'}
    $state=Get-Content (Join-Path $app '.pdv-update/journal.json') -Raw|ConvertFrom-Json
    if($state.Phase -ne 'Committed'){throw 'No durable commit'}
   }else{
    if($LASTEXITCODE -ne 73){throw 'Process interruption did not occur'}
    if(Test-Path (Join-Path $app 'LealInfoPDV.exe')){throw 'Mixed executable set exposed'}
    Start-ScheduledTask -TaskName (TaskName $app)
    $limit=(Get-Date).AddSeconds(35)
    do{Start-Sleep -Milliseconds 250;$state=Get-Content (Join-Path $app '.pdv-update/journal.json') -Raw|ConvertFrom-Json}while($state.Phase -ne 'Recovered' -and (Get-Date) -lt $limit)
    if($state.Phase -ne 'Recovered'){throw 'SYSTEM recovery did not complete'}
   }
  }
  foreach($n in $names){$expected=if($scenario -eq 'success'){$entry.TargetFiles.$n}else{$old.TargetFiles.$n};if((Get-FileHash (Join-Path $app $n)).Hash.ToLowerInvariant() -ne $expected){throw ('Wrong final file '+$n)}}
  foreach($f in $sentinels){if([IO.File]::ReadAllText((Join-Path $app $f)) -ne ('UNCHANGED-'+$f)){throw 'Business data changed'}}
  $results.Add(@{scenario=$scenario;pass=$true;baseline='10.422';target='10.423';candidateSha256=$entry.PackageSha256})
  Write-Output ('PASS actual package 10.422 to 10.423: '+$scenario)
 }finally{
  Stop-ScheduledTask -TaskName (TaskName $app) -ErrorAction SilentlyContinue
  Unregister-ScheduledTask -TaskName (TaskName $app) -Confirm:$false -ErrorAction SilentlyContinue
  Remove-Item $app -Recurse -Force
 }
}
$results|ConvertTo-Json -Depth 5|Set-Content validated/release-output/layout-rollback-results.json -Encoding UTF8
Write-Output ('VALIDATED_PACKAGE_SHA256='+$entry.PackageSha256)

$global:LASTEXITCODE=0
