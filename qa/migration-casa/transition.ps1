$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Isolated Windows only'}
$worker=(Resolve-Path qa/migration-casa/legacy-worker.ps1).Path
$package=(Resolve-Path migration-update.zip).Path
$hash=(Get-FileHash $package).Hash.ToLowerInvariant()
$names=@('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')
$results=@();$success=''
function TaskName($app){$h=[Security.Cryptography.SHA256]::Create();try{'LealInfoPDV-Recovery-'+([BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($app.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$h.Dispose()}}
foreach($scenario in @('success','interruption','bad-sha','health-failure')){
 $app=Join-Path $env:RUNNER_TEMP ('casa-transition-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $app|Out-Null
 Copy-Item publish/* $app -Recurse
 foreach($n in $names){Copy-Item "legacy/$n" (Join-Path $app $n) -Force}
 Set-Content (Join-Path $app 'QA-ISOLATED') 'Migration fixture'
 $previous=@{};foreach($n in $names){$previous[$n]=(Get-FileHash (Join-Path $app $n)).Hash}
 $license=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'LealInfoPDV/network.license'
 if(Test-Path $license){throw 'Fixture must have no operational license'}
 [IO.File]::WriteAllText($license,'INVALID-QA-LICENSE-NOT-A-CUSTOMER');$licenseHash=(Get-FileHash $license).Hash
 try{
  $supplied=if($scenario -eq 'bad-sha'){'0'*64}else{$hash}
  if($scenario -eq 'health-failure'){
   $prior=Start-Process powershell.exe -ArgumentList '-NoProfile -Command Start-Sleep -Seconds 2' -PassThru
   $started=$prior.StartTime.ToUniversalTime().Ticks;$previousPid=$prior.Id;$prior.Dispose()
   & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode PrepareApply -App $app -Channel 003 -Version 10.444 -Package $package -Sha256 $hash -PreviousPid $previousPid -PreviousStarted $started -Nonce 'fixture' -HealthTimeoutSeconds 5
   if($LASTEXITCODE -eq 0){throw 'Invalid activation incorrectly accepted as healthy startup'}
   $state=Get-Content (Join-Path $app '.pdv-update/journal.json') -Raw|ConvertFrom-Json
   if($state.Phase -ne 'Recovered'){throw 'Failed real startup was not rolled back'}
  }else{
   & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Prepare -App $app -Channel 003 -Version 10.444 -Package $package -Sha256 $supplied
   if($scenario -eq 'bad-sha'){if($LASTEXITCODE -eq 0){throw 'Wrong package hash accepted'}}else{
    if($LASTEXITCODE -ne 0){throw 'Legacy preparation failed'}
    foreach($n in $names){if((Get-FileHash (Join-Path $app ('.pdv-update/previous/'+$n))).Hash -ne $previous[$n]){throw 'Original real executable backup differs'}}
    $fault=if($scenario -eq 'interruption'){'copy-LealInfoPDV.dll'}else{''}
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Apply -App $app -Channel 003 -Fault $fault
    if($scenario -eq 'success'){
     if($LASTEXITCODE -ne 0){throw 'Legacy apply failed'}
     dotnet qa/migration-casa/bin/Release/net8.0-windows10.0.19041.0/LealInfoPDV.Pet.CompatibilityTests.dll $app
     if($LASTEXITCODE -ne 0){throw 'Migrated business compatibility failed'}
     $success=$app
    }else{
     if($LASTEXITCODE -ne 73 -or (Test-Path (Join-Path $app 'LealInfoPDV.exe'))){throw 'Interrupted mixed application was exposed'}
     # A restored legacy build must ignore a separately installed new profile.
     Copy-Item (Join-Path $success 'Extensions') (Join-Path $app 'Extensions') -Recurse
     Start-ScheduledTask -TaskName (TaskName $app);$deadline=(Get-Date).AddSeconds(35)
     do{Start-Sleep -Milliseconds 250;$state=Get-Content (Join-Path $app '.pdv-update/journal.json') -Raw|ConvertFrom-Json}while($state.Phase -ne 'Recovered' -and (Get-Date) -lt $deadline)
     if($state.Phase -ne 'Recovered'){throw 'Actual installed worker SYSTEM recovery failed'}
     $profileHash=(Get-FileHash (Join-Path $app 'Extensions/PET/Pet.dll')).Hash
     dotnet qa/migration-casa/seeder/bin/Release/net8.0-windows10.0.19041.0/Seeder.dll (Join-Path $app 'LealInfoPDV.dll') --verify-rollback
     if($LASTEXITCODE -ne 0 -or (Get-FileHash (Join-Path $app 'Extensions/PET/Pet.dll')).Hash -ne $profileHash){throw 'Legacy bootstrap rollback compatibility failed'}
    }
   }
  }
  foreach($n in $names){$expected=if($scenario -eq 'success'){(Get-FileHash "payload/$n").Hash}else{$previous[$n]};if((Get-FileHash (Join-Path $app $n)).Hash -ne $expected){throw 'Final real binary hashes differ'}}
  if((Get-FileHash $license).Hash -ne $licenseHash){throw 'License fixture changed'}
  $results+=@{Scenario=$scenario;Pass=$true;ActualLegacyFiles=$true;OriginalWorker=$true;CustomerInstallation=$false}
  Write-Output "PASS CASA transition $scenario; verified original files and license preserved"
 }finally{
  Stop-ScheduledTask -TaskName (TaskName $app) -ErrorAction SilentlyContinue
  Unregister-ScheduledTask -TaskName (TaskName $app) -Confirm:$false -ErrorAction SilentlyContinue
  Remove-Item $license -ErrorAction SilentlyContinue
 }
}
$results|ConvertTo-Json -Depth 5|Set-Content evidence/transition.json
$global:LASTEXITCODE=0
