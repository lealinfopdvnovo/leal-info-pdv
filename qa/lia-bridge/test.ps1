$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Isolated runner only'}
$worker=(Resolve-Path candidate/TransactionalUpdate.ps1).Path
$oldAssembly=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Resolve-Path legacy/LealInfoPDV.dll).Path))
$resource=$oldAssembly.GetManifestResourceStream('TransactionalUpdateWorker')
if(!$resource){throw 'Installed baseline worker missing'}
$legacyWorker=Join-Path $env:RUNNER_TEMP 'lia-baseline-worker.ps1'
$out=[IO.File]::Create($legacyWorker);try{$resource.CopyTo($out)}finally{$out.Dispose();$resource.Dispose()}
$core=@('LealInfoPDV.deps.json','LealInfoPDV.dll','LealInfoPDV.exe')
function TaskName($app){$h=[Security.Cryptography.SHA256]::Create();try{'LealInfoPDV-Recovery-'+([BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($app.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$h.Dispose()}}
function InvokeWorker($file,$mode,$app,$package,$hash,$fault=''){
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $file -Mode $mode -App $app -Channel 001 -Version 10.459 -Package $package -Sha256 $hash -Fault $fault
 return $LASTEXITCODE
}
$first=(Resolve-Path bridge-first.zip).Path
$final=(Resolve-Path bridge-final.zip).Path
$firstHash=(Get-FileHash $first).Hash
$finalHash=(Get-FileHash $final).Hash
$lia=Get-Content lia-hashes.json -Raw|ConvertFrom-Json
$names=@($core)+@($lia.PSObject.Properties.Name)
$results=@()
foreach($scenario in @('success','bad-sha','bad-lia-pin','duplicate-core','interrupt-core','interrupt-lia','interrupt-new','interrupt-recovery')){
 $app=Join-Path $env:RUNNER_TEMP ('lia-bridge-'+[Guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory "$app/LIC-AI" -Force|Out-Null
 Set-Content "$app/QA-ISOLATED" 'Disposable'
 foreach($n in $core){Copy-Item "legacy/$n" "$app/$n"}
 Set-Content "$app/LIC-AI/LicAi.dll" 'PREVIOUS-LIA-DLL'
 Set-Content "$app/LIC-AI/LicAi.exe" 'PREVIOUS-LIA-EXE'
 $sentinels=@('lealinfo.db','license.dat','settings.json','stock.dat','cash.dat','LIC-AI/settings.dat','Extensions/PET/manifest.json','Extensions/PET/Local.dll')
 foreach($n in $sentinels){$p=Join-Path $app $n;New-Item -ItemType Directory (Split-Path $p) -Force|Out-Null;[IO.File]::WriteAllText($p,'KEEP-'+$n)}
 try{
  $code=InvokeWorker $legacyWorker Prepare $app $first $firstHash
  if($code -ne 0){throw 'Installed 10.453 worker rejected bridge'}
  $code=InvokeWorker $legacyWorker Apply $app $first $firstHash
  if($code -ne 0){throw 'First hop failed'}
  if([IO.File]::ReadAllText("$app/LIC-AI/LicAi.dll").Trim() -ne 'PREVIOUS-LIA-DLL'){throw 'First hop touched LIA'}
  $assembly=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $app 'LealInfoPDV.dll')))
  $resource=$assembly.GetManifestResourceStream('TransactionalUpdateWorker')
  if(!$resource){throw 'Bridge worker missing in delivered PDV'}
  $worker=Join-Path $app 'delivered-worker.ps1'
  $out=[IO.File]::Create($worker);try{$resource.CopyTo($out)}finally{$out.Dispose();$resource.Dispose()}
  if((Get-FileHash $worker).Hash -ne (Get-FileHash candidate/TransactionalUpdate.ps1).Hash){throw 'Delivered worker differs'}
  $before=@{}
  foreach($n in $names){$p=Join-Path $app $n;$before[$n]=if(Test-Path $p){(Get-FileHash $p).Hash}else{$null}}
  $pkg=if($scenario -eq 'bad-lia-pin'){(Resolve-Path bridge-tampered.zip).Path}elseif($scenario -eq 'duplicate-core'){(Resolve-Path bridge-duplicate.zip).Path}else{$final}
  $hash=if($scenario -eq 'bad-sha'){'0'*64}else{(Get-FileHash $pkg).Hash}
  $code=InvokeWorker $worker Prepare $app $pkg $hash
  if($scenario -in @('bad-sha','bad-lia-pin','duplicate-core')){
   if($code -eq 0){throw 'Invalid integrity accepted'}
  }else{
   if($code -ne 0){throw 'Bridge preparation failed'}
   foreach($n in $names){if($null -ne $before[$n] -and (Get-FileHash (Join-Path $app ('.pdv-update/previous/'+$n))).Hash -ne $before[$n]){throw 'Backup hash mismatch'}}
   $fault=switch($scenario){
    'interrupt-core' {'copy-LealInfoPDV.dll'}
    'interrupt-lia' {'copy-LIC-AI/LicAi.dll'}
    'interrupt-new' {'copy-LIC-AI/LicAi.deps.json'}
    'interrupt-recovery' {'copy-LIC-AI/LicAi.dll'}
    default {''}
   }
   $code=InvokeWorker $worker Apply $app $pkg $hash $fault
   if($scenario -eq 'success'){
    if($code -ne 0){throw 'Apply failed'}
    dotnet controller/qa/lia-bridge/probe/bin/Release/net8.0-windows/Probe.dll "$app/LIC-AI/LicAi.dll"
    if($LASTEXITCODE -ne 0){throw 'Delivered LIA dialog probe failed'}
   }else{
    if($code -ne 73 -or (Test-Path "$app/LealInfoPDV.exe")){throw 'Interrupted set exposed'}
    if($scenario -eq 'interrupt-recovery'){
     $code=InvokeWorker $worker Recover $app $pkg $hash 'recover-LIC-AI/LicAi.dll'
     if($code -ne 73){throw 'Recovery interruption not injected'}
    }
    Start-ScheduledTask -TaskName (TaskName $app)
    $limit=(Get-Date).AddSeconds(40)
    do{Start-Sleep -Milliseconds 250;$state=Get-Content "$app/.pdv-update/journal.json" -Raw|ConvertFrom-Json}while($state.Phase -ne 'Recovered' -and (Get-Date) -lt $limit)
    if($state.Phase -ne 'Recovered'){throw 'Automatic SYSTEM recovery failed'}
   }
  }
  foreach($n in $names){
   $p=Join-Path $app $n
   $expected=if($scenario -eq 'success'){(Get-FileHash (Join-Path final-payload $n)).Hash}else{$before[$n]}
   if($null -eq $expected){if(Test-Path $p){throw 'New file not rolled back'}}
   elseif(!(Test-Path $p) -or (Get-FileHash $p).Hash -ne $expected){throw ('Final hash mismatch '+$n)}
  }
  foreach($n in $sentinels){if([IO.File]::ReadAllText((Join-Path $app $n)) -ne ('KEEP-'+$n)){throw 'Customer data changed'}}
  Write-Output "PASS BRIDGE: $scenario; old three-file hop + LIA hash/backup/rollback + customer/PET preservation"
  $results+=@{Scenario=$scenario;Pass=$true;Files=$names.Count}
 }finally{
  Stop-ScheduledTask -TaskName (TaskName $app) -ErrorAction SilentlyContinue
  Unregister-ScheduledTask -TaskName (TaskName $app) -Confirm:$false -ErrorAction SilentlyContinue
  Remove-Item $app -Recurse -Force
 }
}
$results|ConvertTo-Json|Set-Content bridge-results.json
$global:LASTEXITCODE=0
