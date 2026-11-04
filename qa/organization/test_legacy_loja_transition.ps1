$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Isolated runner only'}
$worker=(Resolve-Path pair-evidence/legacy-001-worker.ps1).Path
if((Get-FileHash $worker).Hash -ne 'A760F78DF6B2D3D11B037727CA642BE8E3D5EFEF802D0AC6898EA54F9D819968'){throw 'Original Loja worker hash mismatch'}
$package=(Resolve-Path pair-fixtures/001/transition.zip).Path
$sha=(Get-FileHash $package).Hash
$names=@('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')
$build=Split-Path (Get-ChildItem candidate/bin/Release -Filter LealInfoPDV.exe -Recurse|Select-Object -First 1).FullName
$previousTemp=$env:RUNNER_TEMP
$root=Join-Path $env:ProgramFiles ('LealInfoPDV-QA-LEGACY-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $root|Out-Null
$env:RUNNER_TEMP=$root
function Hash($p){(Get-FileHash -LiteralPath $p).Hash}
function TaskName($p){$h=[Security.Cryptography.SHA256]::Create();try{'LealInfoPDV-Recovery-'+([BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($p.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$h.Dispose()}}
$results=@()
try{
 foreach($scenario in @('success','bad-sha','interruption-system','unqualified-startup-rollback')){
  $app=Join-Path $root $scenario;New-Item -ItemType Directory $app|Out-Null
  Copy-Item (Join-Path $build '*') $app -Recurse
  foreach($n in $names){Copy-Item (Join-Path legacy-001 $n) (Join-Path $app $n) -Force}
  Set-Content (Join-Path $app 'QA-ISOLATED') 'disposable legacy fixture'
  New-Item -ItemType Directory (Join-Path $app 'LIC-AI'),(Join-Path $app 'Extensions/PET') -Force|Out-Null
  foreach($n in @('LIC-AI/preserve.bin','Extensions/PET/retain.bin','license.protected','customer.db')){[IO.File]::WriteAllText((Join-Path $app $n),'UNCHANGED-'+$n)}
  $old=@{};foreach($n in $names){$old[$n]=Hash (Join-Path $app $n)}
  $protected=@{};foreach($n in @('LIC-AI/preserve.bin','Extensions/PET/retain.bin','license.protected','customer.db')){$protected[$n]=Hash (Join-Path $app $n)}
  try{
   if($scenario -eq 'unqualified-startup-rollback'){
    $process=Start-Process powershell.exe -ArgumentList '-NoProfile -Command Start-Sleep -Seconds 2' -PassThru
    $priorPid=$process.Id;$ticks=$process.StartTime.ToUniversalTime().Ticks;$process.Dispose()
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode PrepareApply -App $app -Package $package -Sha256 $sha -Version 10.463 -Channel 001 -PreviousPid $priorPid -PreviousStarted $ticks -Nonce fixture -HealthTimeoutSeconds 5
    if($LASTEXITCODE -eq 0){throw 'Unqualified licensed startup accepted'}
    $state=Get-Content (Join-Path $app '.pdv-update/journal.json') -Raw|ConvertFrom-Json
    if($state.Phase -ne 'Recovered'){throw 'Unqualified startup did not recover'}
   }else{
    $supplied=if($scenario -eq 'bad-sha'){'0'*64}else{$sha}
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Prepare -App $app -Package $package -Sha256 $supplied -Version 10.463 -Channel 001
    if($scenario -eq 'bad-sha'){if($LASTEXITCODE -eq 0){throw 'Bad transition hash accepted'}}
    else{
     if($LASTEXITCODE -ne 0){throw 'Original legacy prepare failed'}
     foreach($n in $names){if((Hash (Join-Path $app ('.pdv-update/previous/'+$n))) -ne $old[$n]){throw 'Original published binary backup differs'}}
     $fault=if($scenario -eq 'interruption-system'){'copy-LealInfoPDV.dll'}else{''}
     & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Apply -App $app -Channel 001 -Fault $fault
     if($scenario -eq 'success'){if($LASTEXITCODE -ne 0){throw 'Original legacy apply failed'}}
     else{
      if($LASTEXITCODE -ne 73){throw 'Original legacy fault checkpoint failed'}
      Start-ScheduledTask -TaskName (TaskName $app)
      $deadline=(Get-Date).AddSeconds(40)
      do{Start-Sleep -Milliseconds 250;$state=Get-Content (Join-Path $app '.pdv-update/journal.json') -Raw|ConvertFrom-Json}while($state.Phase -ne 'Recovered' -and (Get-Date) -lt $deadline)
      if($state.Phase -ne 'Recovered'){throw 'Original legacy SYSTEM recovery failed'}
     }
    }
   }
   foreach($n in $names){$expected=if($scenario -eq 'success'){Hash (Join-Path pair-fixtures/001 $n)}else{$old[$n]};if((Hash (Join-Path $app $n)) -ne $expected){throw "Final binary mismatch $n"}}
   foreach($n in $protected.Keys){if((Hash (Join-Path $app $n)) -ne $protected[$n]){throw "Protected local file changed $n"}}
   $results+=@{Pass=$true;Scenario=$scenario;OriginalPublishedWorker=$true;OriginalPublishedBinaries=$true;PositiveActivatedStartup=$false;OperationalCustomer=$false;Published=$false}
   Write-Output "PASS actual Loja legacy transition $scenario"
  }finally{Stop-ScheduledTask -TaskName (TaskName $app) -ErrorAction SilentlyContinue;Unregister-ScheduledTask -TaskName (TaskName $app) -Confirm:$false -ErrorAction SilentlyContinue}
 }
 $results|ConvertTo-Json -Depth 8|Set-Content pair-evidence/legacy-loja-transition.json -Encoding UTF8
 $global:LASTEXITCODE=0
}finally{$env:RUNNER_TEMP=$previousTemp;Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue}
