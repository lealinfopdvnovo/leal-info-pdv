$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Isolated runner only'}
$worker=(Resolve-Path controller/qa/organization/bridge/JointInitialRecovery.ps1).Path
$oldWorker=(Resolve-Path approved/qa/organization/bridge/JointInitialRecovery.ps1).Path
function Hash($p){(Get-FileHash -LiteralPath $p).Hash.ToLowerInvariant()}
if((Hash $worker) -ne (Hash $oldWorker)){throw 'Joint recovery engine changed outside reader correction scope'}
$origin=(Resolve-Path legacy-001).Path
$parent=Join-Path $env:ProgramFiles ('LEAL-JOURNAL-CONCURRENCY-'+[Guid]::NewGuid().ToString('N'))
$app=Join-Path $parent 'case';New-Item -ItemType Directory (Join-Path $app '.pdv-update/previous') -Force|Out-Null
$oldTemp=$env:RUNNER_TEMP;$env:RUNNER_TEMP=$parent
$names=@('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')
$source=@{};$target=@{};$protected=@{}
$nonce=[Guid]::NewGuid().ToString('N')
$poll=$null
$h=[Security.Cryptography.SHA256]::Create();try{$task='LealInfoPDV-Initial-'+[BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($app.ToUpperInvariant()))).Replace('-','').Substring(0,16)}finally{$h.Dispose()}
function State($p){
 $f=[IO.File]::Open($p,'Open','Read',([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
 try{$r=[IO.StreamReader]::new($f);try{$r.ReadToEnd()|ConvertFrom-Json}finally{$r.Dispose()}}finally{$f.Dispose()}
}
try{
 Set-Content (Join-Path $app 'QA-ISOLATED') 'CI-only journal concurrency fixture; no activation or application startup'
 foreach($n in $names){
  Copy-Item (Join-Path $origin $n) (Join-Path $app ('.pdv-update/previous/'+$n))
  $source[$n]=Hash (Join-Path $origin $n)
  [IO.File]::WriteAllText((Join-Path $app $n),'SYNTHETIC TARGET BYTES - NOT AN APPLICATION '+$n);$target[$n]=Hash (Join-Path $app $n)
 }
 foreach($n in @('LealInfoPDV.runtimeconfig.json','data.keep','configuration.keep')){
  Set-Content (Join-Path $app $n) ('SYNTHETIC PRESERVATION MARKER '+$n);$protected[$n]=Hash (Join-Path $app $n)
 }
 @{App=$app;Channel='001';Version='10.468';Phase='AwaitingHealth';Nonce=$nonce;Previous=$source;Target=$target}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $app '.pdv-update/journal.json') -Encoding UTF8
 $petBytes=[Text.Encoding]::UTF8.GetBytes('SYNTHETIC FIRST PET BYTES - NOT CUSTOMER MODULE')
 $petPath=Join-Path $parent 'Pet.fixture';[IO.File]::WriteAllBytes($petPath,$petBytes);$petHash=Hash $petPath
 $manifestPath=Join-Path $parent 'manifest.fixture';[IO.File]::WriteAllText($manifestPath,('{"Identity":"001","Version":"1.3","Assembly":"Pet.dll","Sha256":"'+$petHash+'"}'));$manifestHash=Hash $manifestPath
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Prepare -App $app -Nonce $nonce -PetSha256 $petHash -ManifestSha256 $manifestHash -Version 10.468 -Fixture
 if($LASTEXITCODE -ne 0){throw 'Actual joint worker did not arm the new fixture'}
 New-Item -ItemType Directory (Join-Path $app 'Extensions/PET') -Force|Out-Null
 Copy-Item $petPath (Join-Path $app 'Extensions/PET/Pet.dll');Copy-Item $manifestPath (Join-Path $app 'Extensions/PET/manifest.json')
 $reader=(Get-ChildItem controller/qa/organization/journal-fix/bin/Release -Filter RecoveryJournalPolling.dll -Recurse|Select-Object -First 1).FullName
 if(!$reader){throw 'Actual journal polling harness missing'}
 $ready=Join-Path $parent 'reader.ready';$stop=Join-Path $parent 'reader.stop';$stats=Join-Path $parent 'reader.json'
 $poll=Start-Process dotnet -ArgumentList ('"'+$reader+'" "'+$app+'" "'+$ready+'" "'+$stop+'" "'+$stats+'"') -PassThru
 $deadline=(Get-Date).AddSeconds(15)
 while(!(Test-Path $ready) -and !$poll.HasExited -and (Get-Date) -lt $deadline){Start-Sleep -Milliseconds 50}
 if(!(Test-Path $ready)){throw 'Snapshot reader did not start'}
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Recover -App $app -Fixture -Fault 'restore-LealInfoPDV.dll'
 if($LASTEXITCODE -ne 73){throw 'New concurrent interruption checkpoint failed'}
 # The REAL SYSTEM task resumes while the ACTUAL corrected reader keeps both
 # journals open/reopening. No synthetic health or customer startup is injected.
 Start-ScheduledTask -TaskName $task
 $deadline=(Get-Date).AddSeconds(35);$state=$null
 do{Start-Sleep -Milliseconds 100;try{$state=State (Join-Path $app '.pdv-initial/journal.json')}catch{$state=$null}}while($state.Phase -ne 'Recovered' -and !$poll.HasExited -and (Get-Date) -lt $deadline)
 if($state.Phase -ne 'Recovered'){throw 'SYSTEM joint recovery failed under concurrent snapshot readers'}
 Set-Content $stop 'stop'
 if(!$poll.WaitForExit(5000) -or $poll.ExitCode -ne 0){throw 'Snapshot reader observed a concurrency or parsing failure'}
 $reading=Get-Content $stats -Raw|ConvertFrom-Json
 if($reading.Errors.Count -ne 0 -or $reading.Snapshots -lt 2){throw 'Concurrent snapshots not verified'}
 foreach($n in $names){if((Hash (Join-Path $app $n)) -ne $source[$n]){throw 'Core rollback differs from trusted original'}}
 if(Test-Path (Join-Path $app 'Extensions/PET')){throw 'First external PET not recovered jointly'}
 foreach($n in $protected.Keys){if((Hash (Join-Path $app $n)) -ne $protected[$n]){throw 'Protected fixture bytes changed'}}
 New-Item -ItemType Directory journal-fix-evidence -Force|Out-Null
 $result=@{Pass=$true;NewScenario='concurrent-actual-journal-readers-during-interrupted-SYSTEM-joint-recovery';Snapshots=$reading.Snapshots;ReaderErrors=$reading.Errors;OriginalCoreHashes=$source;ProtectedFixtureHashes=$protected;RecoveryWorkerSha256=(Hash $worker);ActualUnchangedRecoveryEngine=$true;ActualCorrectedJournalReader=$true;SystemRecovery=$true;CoreRecovered=$true;FirstPetRemovedFromActivePath=$true;TargetAndPetSynthetic=$true;RuntimeAndDataSynthetic=$true;LicensedStartup=$false;OperationalApproval=$false;ProductionTouched=$false}
 $result|ConvertTo-Json -Depth 8|Set-Content journal-fix-evidence/concurrent-joint-recovery.json -Encoding UTF8
 Write-Output ('RECOVERY_EVIDENCE='+($result|ConvertTo-Json -Depth 8 -Compress))
 Write-Output 'PASS new concurrent-reader interrupted SYSTEM recovery; core and first PET recovered together'
 $global:LASTEXITCODE=0
}finally{
 if($poll){if(!$poll.HasExited){$poll.Kill();$poll.WaitForExit()};$poll.Dispose()}
 Stop-ScheduledTask -TaskName $task -ErrorAction SilentlyContinue
 Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
 $env:RUNNER_TEMP=$oldTemp;Remove-Item $parent -Recurse -Force -ErrorAction SilentlyContinue
}
