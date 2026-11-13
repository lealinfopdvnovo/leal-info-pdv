$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Isolated runner only'}
$worker=(Resolve-Path controller/qa/organization/bridge/JointInitialRecovery.ps1).Path
$target=(Resolve-Path destination-10468).Path
$previous=(Resolve-Path legacy-001).Path
$parent=Join-Path $env:ProgramFiles ('LEAL-INITIAL-QA-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $parent|Out-Null
$oldTemp=$env:RUNNER_TEMP;$env:RUNNER_TEMP=$parent
$results=@();$names=@('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')
function Hash($p){(Get-FileHash -LiteralPath $p).Hash.ToLowerInvariant()}
function Task($app){$h=[Security.Cryptography.SHA256]::Create();try{'LealInfoPDV-Initial-'+[BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($app.ToUpperInvariant()))).Replace('-','').Substring(0,16)}finally{$h.Dispose()}}
try{
 foreach($scenario in @('before-first-pet','after-first-pet','interrupted-joint-recovery-SYSTEM','corrupt-original-backup','global-lock-held','unexpected-pet-file','synthetic-joint-commit')){
  $app=Join-Path $parent $scenario;New-Item -ItemType Directory $app,(Join-Path $app '.pdv-update/previous') -Force|Out-Null
  Set-Content (Join-Path $app 'QA-ISOLATED') 'Disposable CI fixture; no licence or operational startup'
  $source=@{};$next=@{}
  foreach($n in $names){Copy-Item (Join-Path $target $n) (Join-Path $app $n);Copy-Item (Join-Path $previous $n) (Join-Path $app ('.pdv-update/previous/'+$n));$source[$n]=Hash (Join-Path $previous $n);$next[$n]=Hash (Join-Path $target $n)}
  Set-Content (Join-Path $app 'LealInfoPDV.runtimeconfig.json') '{"QA":"synthetic runtime marker; original exact runtime proof reused, not reconstructed"}'
  Set-Content (Join-Path $app 'protected-data.bin') 'SYNTHETIC DATA, NOT A LICENCE';Set-Content (Join-Path $app 'protected.config') 'SYNTHETIC CONFIG'
  $protected=@{};foreach($n in @('LealInfoPDV.runtimeconfig.json','protected-data.bin','protected.config')){$protected[$n]=Hash (Join-Path $app $n)}
  $nonce=[Guid]::NewGuid().ToString('N')
  $legacy=@{App=$app;Channel='001';Version='10.468';Phase='AwaitingHealth';Nonce=$nonce;Previous=$source;Target=$next}
  $legacy|ConvertTo-Json -Depth 8|Set-Content (Join-Path $app '.pdv-update/journal.json') -Encoding UTF8
  $args=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$worker,'-Mode','Prepare','-App',$app,'-Nonce',$nonce,'-PetSha256',(Hash (Join-Path $target 'Pet.dll')),'-ManifestSha256',(Hash (Join-Path $target 'manifest.json')),'-Version','10.468','-Fixture')
  try{
   & powershell.exe @args;if($LASTEXITCODE -ne 0){throw "Arming failed $scenario"}
   $state=Get-Content (Join-Path $app '.pdv-initial/journal.json') -Raw|ConvertFrom-Json
   if($state.Phase -ne 'Armed' -or (Test-Path (Join-Path $app 'Extensions/PET'))){throw 'Arming touched external PET or was not durable'}
   foreach($n in $names){if((Hash (Join-Path $app ('.pdv-initial/previous/'+$n))) -ne $source[$n]){throw 'Original initial backup not exact'}}
   if($scenario -ne 'before-first-pet'){
    New-Item -ItemType Directory (Join-Path $app 'Extensions/PET') -Force|Out-Null
    foreach($n in @('Pet.dll','manifest.json')){Copy-Item (Join-Path $target $n) (Join-Path $app ('Extensions/PET/'+$n))}
   }
   if($scenario -eq 'unexpected-pet-file'){Set-Content (Join-Path $app 'Extensions/PET/extra.keep') 'PRESERVE IN QUARANTINE'}
   if($scenario -eq 'synthetic-joint-commit'){
    $legacy.Phase='Committed';$legacy|ConvertTo-Json -Depth 8|Set-Content (Join-Path $app '.pdv-update/journal.json') -Encoding UTF8
    $me=[Diagnostics.Process]::GetCurrentProcess()
    @{Nonce=$nonce;Version='10.468';PetSha256=(Hash (Join-Path $target 'Pet.dll'));Pid=$me.Id;StartedTicks=$me.StartTime.ToUniversalTime().Ticks;SyntheticHealth=$true}|ConvertTo-Json|Set-Content (Join-Path $app '.pdv-initial/ready.json') -Encoding UTF8
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Commit -App $app -Fixture
    if($LASTEXITCODE -ne 0){throw 'Synthetic joint commit failed'}
   }else{
    if($scenario -eq 'corrupt-original-backup'){
     $path=Join-Path $app '.pdv-initial/previous/LealInfoPDV.dll';[IO.File]::WriteAllText($path,'BAD')
     & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Recover -App $app -Fixture
     if($LASTEXITCODE -eq 0 -or (Hash (Join-Path $app 'LealInfoPDV.dll')) -ne $next['LealInfoPDV.dll']){throw 'Corrupt backup was accepted or destination changed'}
     Copy-Item (Join-Path $previous 'LealInfoPDV.dll') $path -Force
    }
    if($scenario -eq 'global-lock-held'){
     $lock=[IO.File]::Open((Join-Path $app '.pdv-update/transaction.lock'),'OpenOrCreate','ReadWrite','None')
     try{& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Recover -App $app -Fixture;if($LASTEXITCODE -ne 0 -or (Hash (Join-Path $app 'LealInfoPDV.dll')) -ne $next['LealInfoPDV.dll']){throw 'Recovery raced original worker'}}finally{$lock.Dispose()}
    }
    if($scenario -eq 'interrupted-joint-recovery-SYSTEM'){
     & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Recover -App $app -Fixture -Fault 'restore-LealInfoPDV.dll'
     if($LASTEXITCODE -ne 73){throw 'Joint recovery interruption did not occur'}
     Start-ScheduledTask -TaskName (Task $app)
     $deadline=(Get-Date).AddSeconds(45)
     do{Start-Sleep -Milliseconds 250;try{$state=Get-Content (Join-Path $app '.pdv-initial/journal.json') -Raw|ConvertFrom-Json}catch{$state=$null}}while($state.Phase -ne 'Recovered' -and (Get-Date) -lt $deadline)
     if($state.Phase -ne 'Recovered'){throw 'Joint SYSTEM retry failed'}
    }else{
     & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Recover -App $app -Fixture
     if($LASTEXITCODE -ne 0){throw "Joint recovery failed $scenario"}
    }
   }
   $state=Get-Content (Join-Path $app '.pdv-initial/journal.json') -Raw|ConvertFrom-Json
   $expected=if($scenario -eq 'synthetic-joint-commit'){$next}else{$source}
   foreach($n in $names){if((Hash (Join-Path $app $n)) -ne $expected[$n]){throw 'Core bytes incorrect after joint transaction'}}
   if($scenario -eq 'synthetic-joint-commit'){if($state.Phase -ne 'Committed' -or !(Test-Path (Join-Path $app 'Extensions/PET/Pet.dll'))){throw 'Joint commit not durable'}}
   else{
    if($state.Phase -ne 'Recovered' -or (Test-Path (Join-Path $app 'Extensions/PET'))){throw 'First PET not reverted with core'}
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Recover -App $app -Fixture
    if($LASTEXITCODE -ne 0){throw 'Idempotent recovery failed'}
   }
   if($scenario -eq 'unexpected-pet-file'){
    $saved=Get-ChildItem (Join-Path $app '.pdv-initial') -Filter extra.keep -Recurse
    if(!$saved -or (Get-Content $saved.FullName -Raw).Trim() -ne 'PRESERVE IN QUARANTINE'){throw 'Unexpected content was lost instead of quarantined'}
   }
   foreach($n in $protected.Keys){if((Hash (Join-Path $app $n)) -ne $protected[$n]){throw "Protected fixture changed: $n"}}
   $results+=@{Pass=$true;Scenario=$scenario;Original10467Core=$true;Actual10468Candidate=$true;ActualLojaPet13=$true;SyntheticData=$true;SyntheticRuntimeMarker=$true;SyntheticHealth=($scenario -eq 'synthetic-joint-commit');LicensedStartup=$false;Published=$false}
   Write-Output "PASS new initial joint recovery $scenario"
  }finally{Stop-ScheduledTask -TaskName (Task $app) -ErrorAction SilentlyContinue;Unregister-ScheduledTask -TaskName (Task $app) -Confirm:$false -ErrorAction SilentlyContinue}
 }
 New-Item -ItemType Directory completion-evidence -Force|Out-Null
 $results|ConvertTo-Json -Depth 8|Set-Content completion-evidence/joint-recovery.json -Encoding UTF8
 $global:LASTEXITCODE=0
}finally{$env:RUNNER_TEMP=$oldTemp;Remove-Item $parent -Recurse -Force -ErrorAction SilentlyContinue}
