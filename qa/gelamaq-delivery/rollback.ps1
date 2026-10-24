$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Isolated Windows only'}
$worker=(Resolve-Path TransactionalUpdate.ps1).Path
$names=@('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')
$payload=Join-Path $env:RUNNER_TEMP ('gelamaq-payload-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $payload|Out-Null
foreach($n in $names){Copy-Item (Join-Path publish $n) (Join-Path $payload $n)}
$package=$payload+'.zip';Compress-Archive ($payload+'/*') $package
$hash=(Get-FileHash $package).Hash.ToLowerInvariant()
function TaskName($app){$h=[Security.Cryptography.SHA256]::Create();try{'LealInfoPDV-Recovery-'+([BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($app.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$h.Dispose()}}
$results=@()
foreach($scenario in @('success','interruption-hidden','interruption-deps','interruption-dll','interruption-exe','bad-sha','health-failure')){
 $app=Join-Path $env:RUNNER_TEMP ('gelamaq-preserve-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $app|Out-Null
 Copy-Item publish/* $app -Recurse -Force
 $before=@{}
 foreach($n in $names){Copy-Item (Join-Path ../published002 $n) (Join-Path $app $n) -Force;$before[$n]=(Get-FileHash (Join-Path $app $n)).Hash}
 Set-Content (Join-Path $app 'QA-ISOLATED') 'Disposable fixture'
 $sentinels=@('lealinfo.db','license.dat','network.config','stock.dat','cash.dat','settings.json','Extensions/PET/manifest.json','Extensions/PET/Local.dll')
 foreach($n in $sentinels){$p=Join-Path $app $n;New-Item -ItemType Directory (Split-Path $p) -Force|Out-Null;[IO.File]::WriteAllText($p,'PRESERVED-'+$n)}
 try{
  $supplied=if($scenario -eq 'bad-sha'){'0'*64}else{$hash}
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Prepare -App $app -Channel 002 -Version 10.458 -Package $package -Sha256 $supplied
  if($scenario -eq 'bad-sha'){if($LASTEXITCODE -eq 0){throw 'Wrong SHA accepted'}}else{
   if($LASTEXITCODE -ne 0){throw 'Gelamaq preparation failed'}
   foreach($n in $names){if((Get-FileHash (Join-Path $app ('.pdv-update/previous/'+$n))).Hash -ne $before[$n]){throw 'Backup hash differs from actual published 002 package'}}
   $fault=switch($scenario){'interruption-hidden'{'hidden'}'interruption-deps'{'copy-LealInfoPDV.deps.json'}'interruption-dll'{'copy-LealInfoPDV.dll'}'interruption-exe'{'copy-LealInfoPDV.exe'}default{''}}
   if($scenario -eq 'health-failure'){
    # Launch the actual updated PDV in the disposable unlicensed runner. It cannot
    # confirm readiness; the unchanged worker must detect this and restore 10.457.
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode PrepareApply -App $app -Channel 002 -Version 10.458 -Package $package -Sha256 $hash -PreviousPid 2147483000 -PreviousStarted 1 -Nonce 'isolated-health-failure' -HealthTimeoutSeconds 2
    if($LASTEXITCODE -eq 0){throw 'Unexpected startup readiness in unlicensed fixture'}
    $state=Get-Content (Join-Path $app '.pdv-update/journal.json') -Raw|ConvertFrom-Json
    if($state.Phase -ne 'Recovered'){throw 'Automatic unhealthy startup rollback failed'}
   }else{
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Apply -App $app -Channel 002 -Fault $fault
    if($scenario -eq 'success'){if($LASTEXITCODE -ne 0){throw 'Apply failed'}}elseif($LASTEXITCODE -ne 73){throw 'Fault did not interrupt worker'}
   }
   if($scenario -ne 'success'){
    Enable-ScheduledTask -TaskName (TaskName $app)|Out-Null
    Start-ScheduledTask -TaskName (TaskName $app);$limit=(Get-Date).AddSeconds(35)
    do{Start-Sleep -Milliseconds 250;$state=Get-Content (Join-Path $app '.pdv-update/journal.json') -Raw|ConvertFrom-Json}while($state.Phase -ne 'Recovered' -and (Get-Date) -lt $limit)
    if($state.Phase -ne 'Recovered'){throw 'SYSTEM rollback failed'}
   }
  }
  foreach($n in $names){$expected=if($scenario -eq 'success'){(Get-FileHash (Join-Path $payload $n)).Hash}else{$before[$n]};if((Get-FileHash (Join-Path $app $n)).Hash -ne $expected){throw 'Final executable set mismatch'}}
  foreach($n in $sentinels){if([IO.File]::ReadAllText((Join-Path $app $n)) -ne ('PRESERVED-'+$n)){throw 'Changed data license stock settings or unrelated PET'}}
  if($scenario -eq 'success'){
   $testBin='qa/gelamaq-delivery/bin/Release/net8.0-windows10.0.19041.0'
   foreach($n in @('GelamaqDeliveryTests.dll','GelamaqDeliveryTests.deps.json','GelamaqDeliveryTests.runtimeconfig.json')){Copy-Item (Join-Path $testBin $n) (Join-Path $app $n)}
   & dotnet (Join-Path $app 'GelamaqDeliveryTests.dll') --payload-check
   if($LASTEXITCODE -ne 0){throw 'Updated installation did not load corrected PET'}
  }
  $results+=@{Scenario=$scenario;Pass=$true;Baseline='real published 002 10.457';GlobalVersion='10.457';PetVersion='1.1';CustomerFilesUnchanged=$true}
  Write-Output "PASS GELAMAQ $scenario"
 }finally{
  Stop-ScheduledTask -TaskName (TaskName $app) -ErrorAction SilentlyContinue
  Unregister-ScheduledTask -TaskName (TaskName $app) -Confirm:$false -ErrorAction SilentlyContinue
  Remove-Item $app -Recurse -Force
 }
}
$results|ConvertTo-Json -Depth 6|Set-Content evidence/gelamaq-rollback.json -Encoding UTF8
$global:LASTEXITCODE=0
