$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Only isolated Windows CI'}
$worker=(Resolve-Path candidate/TransactionalUpdate.ps1).Path
$entry=Get-Content build-entry.json -Raw|ConvertFrom-Json
$package=(Resolve-Path ('release-output/'+$entry.Package)).Path
$hash=(Get-FileHash $package).Hash
$results=[Collections.Generic.List[object]]::new()
$names=@('LealInfoPDV.deps.json','LealInfoPDV.dll','LealInfoPDV.exe')
function TaskName($app){$sha=[Security.Cryptography.SHA256]::Create();try{'LealInfoPDV-Recovery-'+([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($app.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$sha.Dispose()}}
function Fixture{
 $app=Join-Path $env:RUNNER_TEMP ('transaction-qa-'+[Guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory $app|Out-Null;Copy-Item previous/* $app -Recurse
 Set-Content (Join-Path $app 'QA-ISOLATED') 'Synthetic executable-only fixture'
 foreach($f in @('lealinfo.db','network.license','network.config','cash.dat','stock.dat','settings.json')){[IO.File]::WriteAllText((Join-Path $app $f),'UNCHANGED-SYNTHETIC-'+$f)}
 return $app
}
function InvokeWorker($app,$mode,$fault='',$sha=$hash){
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode $mode -App $app -Channel $entry.Channel -Version $entry.Version -Package $package -Sha256 $sha -Fault $fault
 return $LASTEXITCODE
}
function Check($ok,$why){if(!$ok){throw $why};Write-Output ('PASS '+$why)}
function State($app){Get-Content (Join-Path $app '.pdv-update/journal.json') -Raw|ConvertFrom-Json}
function AssertOriginal($app){foreach($n in $names){if((Get-FileHash (Join-Path $app $n)).Hash -ne (Get-FileHash (Join-Path (Resolve-Path previous).Path $n)).Hash){throw 'Mixed or incomplete rollback: '+$n}}}
function CheckSentinels($app){foreach($f in @('lealinfo.db','network.license','network.config','cash.dat','stock.dat','settings.json')){if([IO.File]::ReadAllText((Join-Path $app $f)) -ne ('UNCHANGED-SYNTHETIC-'+$f)){throw 'Protected fixture changed '+$f}}}
function Cleanup($app){Unregister-ScheduledTask -TaskName (TaskName $app) -Confirm:$false -ErrorAction SilentlyContinue;Remove-Item $app -Recurse -Force}
try{
 $app=Fixture
 try{
  Check ((InvokeWorker $app Prepare) -eq 0) 'prepare, staging, full hash-verified executable backup and independent SYSTEM recovery task'
  Check ((Get-ScheduledTask -TaskName (TaskName $app)).State -ne 'Disabled') 'external recovery is enabled before mutation'
  foreach($n in $names){Check ((Get-FileHash (Join-Path $app ('.pdv-update/previous/'+$n))).Hash -eq (Get-FileHash (Join-Path $app $n)).Hash) ('verified backup '+$n)}
  Check ((InvokeWorker $app Apply) -eq 0) 'transactional apply success'
  $s=State $app;Check ($s.Phase -eq 'Committed') 'durable commit'
  foreach($n in $names){Check ((Get-FileHash (Join-Path $app $n)).Hash.ToLowerInvariant() -eq $s.Target.$n) ('complete target '+$n)}
  CheckSentinels $app
  $results.Add(@{scenario='success-and-preservation';pass=$true})
 }finally{Cleanup $app}
 $app=Fixture
 try{Check ((InvokeWorker $app Prepare '' ('0'*64)) -ne 0) 'wrong package SHA rejected';AssertOriginal $app;$results.Add(@{scenario='bad-package-hash';pass=$true})}finally{Cleanup $app}
 foreach($point in @('prepared','hidden','copy-LealInfoPDV.deps.json','copy-LealInfoPDV.dll','copy-LealInfoPDV.exe','published')){
  $app=Fixture
  try{
   if($point -eq 'prepared'){Check ((InvokeWorker $app Prepare $point) -eq 73) 'actual worker exit after durable preparation'}else{
    Check ((InvokeWorker $app Prepare) -eq 0) 'prepare for actual process interruption'
    Check ((InvokeWorker $app Apply $point) -eq 73) ('actual worker abrupt exit '+$point)
   }
   $s=State $app
   if(Test-Path (Join-Path $app 'LealInfoPDV.exe')){
    $allOld=$true;$allNew=$true
    foreach($n in $names){$h=(Get-FileHash (Join-Path $app $n)).Hash.ToLowerInvariant();$allOld=$allOld -and ($h -eq $s.Previous.$n);$allNew=$allNew -and ($h -eq $s.Target.$n)}
    Check ($allOld -or $allNew) 'visible apphost never exposes a mixed executable set'
   }
   # Run the real scheduled action under SYSTEM, independently of any PDV executable/DLL.
   Start-ScheduledTask -TaskName (TaskName $app)
   $limit=(Get-Date).AddSeconds(35)
   do{Start-Sleep -Milliseconds 250;$phase=(State $app).Phase}while($phase -notin @('Recovered','Aborted') -and (Get-Date) -lt $limit)
   Check ($phase -in @('Recovered','Aborted')) ('scheduled recovery without PDV '+$point)
   AssertOriginal $app;CheckSentinels $app
   $results.Add(@{scenario='interruption-'+$point;pass=$true;recovery='real SYSTEM scheduled task'})
  }finally{Cleanup $app}
 }
 $app=Fixture
 try{
  Check ((InvokeWorker $app Prepare) -eq 0) 'prepare real locked-file failure'
  $lock=[IO.File]::Open((Join-Path $app 'LealInfoPDV.dll'),'Open','Read','Read')
  try{Check ((InvokeWorker $app Apply) -ne 0) 'real copy failure fails closed'}finally{$lock.Dispose()}
  Check (!(Test-Path (Join-Path $app 'LealInfoPDV.exe'))) 'apphost unavailable while rollback is blocked by real lock'
  Check ((InvokeWorker $app Recover) -eq 0) 'rollback resumes after lock released'
  AssertOriginal $app;CheckSentinels $app;$results.Add(@{scenario='locked-dll-and-retry';pass=$true})
 }finally{Cleanup $app}
 foreach($point in @('recover-hidden','recover-LealInfoPDV.deps.json','recover-LealInfoPDV.dll','recover-LealInfoPDV.exe')){
  $app=Fixture
  try{
   Check ((InvokeWorker $app Prepare) -eq 0) 'prepare interrupted recovery'
   Check ((InvokeWorker $app Apply 'copy-LealInfoPDV.dll') -eq 73) 'interrupt initial application'
   Check ((InvokeWorker $app Recover $point) -eq 73) ('actual recovery process exit '+$point)
   Check ((InvokeWorker $app Recover) -eq 0) 'fresh process resumes durable rollback'
   AssertOriginal $app;CheckSentinels $app;$results.Add(@{scenario='interrupted-'+$point;pass=$true})
  }finally{Cleanup $app}
 }
 $app=Fixture
 try{
  Check ((InvokeWorker $app Prepare) -eq 0) 'prepare staging-tamper test'
  Add-Content (Join-Path $app '.pdv-update/stage/LealInfoPDV.dll') 'CORRUPTED'
  Check ((InvokeWorker $app Apply) -ne 0) 'changed staging is rejected before mutation'
  AssertOriginal $app;$results.Add(@{scenario='staging-tamper';pass=$true})
 }finally{Cleanup $app}
 $app=Fixture
 try{
  Check ((InvokeWorker $app Prepare) -eq 0) 'prepare backup-tamper test'
  Check ((InvokeWorker $app Apply 'hidden') -eq 73) 'host hidden before interruption'
  Add-Content (Join-Path $app '.pdv-update/previous/LealInfoPDV.dll') 'CORRUPTED'
  Check ((InvokeWorker $app Recover) -ne 0) 'corrupt backup refuses unsafe recovery'
  Check (!(Test-Path (Join-Path $app 'LealInfoPDV.exe'))) 'corrupt backup cannot expose an inconsistent runnable installation'
  $results.Add(@{scenario='corrupt-backup-fail-closed';pass=$true})
 }finally{Cleanup $app}

 $app=Fixture
 $originalPackage=$package;$originalHash=$hash
 try{
  # True OS startup failure: an invalid native apphost cannot open the PDV.
  $badStage=Join-Path $env:RUNNER_TEMP ('bad-apphost-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $badStage|Out-Null
  Copy-Item previous/* $badStage
  [IO.File]::WriteAllText((Join-Path $badStage 'LealInfoPDV.exe'),'INVALID NATIVE APPHOST')
  $package=$badStage+'.zip';Compress-Archive ($badStage+'/*') $package;$hash=(Get-FileHash $package).Hash
  $previous=Start-Process powershell.exe -ArgumentList '-NoProfile -Command Start-Sleep -Seconds 3' -PassThru
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode PrepareApply -App $app -Channel $entry.Channel -Version $entry.Version -Package $package -Sha256 $hash -PreviousPid $previous.Id -PreviousStarted ($previous.StartTime.ToUniversalTime().Ticks) -Nonce 'QA-HANDOFF'
  Check ($LASTEXITCODE -ne 0) 'real native startup failure triggers recovery without PDV opening'
  Check ((State $app).Phase -eq 'Recovered') 'startup failure has complete automatic rollback'
  AssertOriginal $app;CheckSentinels $app;$results.Add(@{scenario='native-startup-failure-and-rollback';pass=$true})
 }finally{$package=$originalPackage;$hash=$originalHash;Cleanup $app}
 $app=Fixture
 try{
  # Exercise the production C# startup guard/receipt and real process handoff in a synthetic host.
  Copy-Item startup-fixture-publish/* $app -Recurse -Force
  foreach($n in $names){Copy-Item (Join-Path (Resolve-Path previous).Path $n) (Join-Path $app $n) -Force}
  $fixtureStage=Join-Path $env:RUNNER_TEMP ('healthy-apphost-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $fixtureStage|Out-Null
  foreach($n in $names){Copy-Item (Join-Path (Resolve-Path startup-fixture-publish).Path $n) $fixtureStage}
  $package=$fixtureStage+'.zip';Compress-Archive ($fixtureStage+'/*') $package;$hash=(Get-FileHash $package).Hash
  $previous=Start-Process powershell.exe -ArgumentList '-NoProfile -Command Start-Sleep -Seconds 3' -PassThru
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode PrepareApply -App $app -Channel $entry.Channel -Version $entry.Version -Package $package -Sha256 $hash -PreviousPid $previous.Id -PreviousStarted ($previous.StartTime.ToUniversalTime().Ticks) -Nonce 'QA-HANDOFF'
  Check ($LASTEXITCODE -eq 0) 'actual handoff launches a synthetic Windows host with production startup guard/receipt'
  Check ((State $app).Phase -eq 'Committed') 'health confirmation commits only the complete target'
  $receipt=Get-Content (Join-Path $app '.pdv-update/health.json') -Raw|ConvertFrom-Json
  $healthy=Get-Process -Id $receipt.Pid
  Check ($healthy.Path -eq (Join-Path $app 'LealInfoPDV.exe')) 'health belongs to the launched process at the expected installation'
  $healthy.Kill();$healthy.WaitForExit()
  CheckSentinels $app;$results.Add(@{scenario='controlled-startup-production-receipt-synthetic-host';pass=$true;fullPdvHomologation=$false})
 }finally{$package=$originalPackage;$hash=$originalHash;Cleanup $app}
 $results|ConvertTo-Json -Depth 8|Set-Content release-output/transaction-results.json -Encoding UTF8
 # Successful tests of the new worker do not imply that the installed 10.415 can safely bootstrap it.
 @{Channel=$entry.Channel;CandidateVersion=$entry.Version;NewTransactionalWorkerPassed=$true;Legacy10415BootstrapProtected=$false;PublicationAuthorized=$false;SafeForPublication=$false;Requires='Validated pre-installation protection for the old updater transition';PhysicalPowerCutTested=$false;ActualWindowsRebootTested=$false;ScheduledRecoveryTested=$true}|ConvertTo-Json|Set-Content release-output/release-gate.json
 Write-Output 'PASS new transactional worker; BLOCKED first legacy transition. No release/install capability in this workflow.'
}catch{
 @{error=$_.Exception.ToString();results=$results}|ConvertTo-Json -Depth 10|Set-Content release-output/transaction-failure.json
 throw
}
