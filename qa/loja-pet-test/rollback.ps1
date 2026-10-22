$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Isolated Windows only'}
$worker=(Resolve-Path TransactionalUpdate.ps1).Path
$names=@('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')
$payload=Join-Path $env:RUNNER_TEMP ('global-payload-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $payload|Out-Null
foreach($name in $names){Copy-Item (Join-Path common $name) (Join-Path $payload $name)}
$package=$payload+'.zip';Compress-Archive ($payload+'/*') $package
$hash=(Get-FileHash $package).Hash.ToLowerInvariant()
function TaskName($app){$h=[Security.Cryptography.SHA256]::Create();try{'LealInfoPDV-Recovery-'+([BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($app.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$h.Dispose()}}
$results=@()
foreach($scenario in @('success','interruption','bad-sha')){
 $app=Join-Path $env:RUNNER_TEMP ('global-preserve-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $app|Out-Null
 Set-Content (Join-Path $app 'QA-ISOLATED') 'Disposable fixture'
 $before=@{}
 foreach($n in $names){Copy-Item (Join-Path baseline $n) (Join-Path $app $n);$before[$n]=(Get-FileHash (Join-Path $app $n)).Hash}
 $sentinels=@('lealinfo.db','license.dat','network.config','stock.dat','cash.dat','settings.json','Extensions/PET/manifest.json','Extensions/PET/Local.dll')
 foreach($n in $sentinels){$p=Join-Path $app $n;New-Item -ItemType Directory (Split-Path $p) -Force|Out-Null;[IO.File]::WriteAllText($p,'PRESERVED-'+$n)}
 $beforeLocal=@{};foreach($n in $sentinels){$beforeLocal[$n]=(Get-FileHash (Join-Path $app $n)).Hash}
 try{
  $supplied=if($scenario -eq 'bad-sha'){'0'*64}else{$hash}
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Prepare -App $app -Channel GLOBAL -Version 10.443 -Package $package -Sha256 $supplied
  if($scenario -eq 'bad-sha'){if($LASTEXITCODE -eq 0){throw 'Wrong SHA accepted'}}else{
   if($LASTEXITCODE -ne 0){throw 'GLOBAL preparation failed'}
   foreach($n in $names){if((Get-FileHash (Join-Path $app ('.pdv-update/previous/'+$n))).Hash -ne $before[$n]){throw 'Backup integrity failure'}}
   $fault=if($scenario -eq 'interruption'){'copy-LealInfoPDV.dll'}else{''}
   & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Apply -App $app -Channel GLOBAL -Version 10.443 -Package $package -Sha256 $hash -Fault $fault
   if($scenario -eq 'success'){if($LASTEXITCODE -ne 0){throw 'Apply failed'}}else{
    if($LASTEXITCODE -ne 73 -or (Test-Path (Join-Path $app 'LealInfoPDV.exe'))){throw 'Interrupted executable set exposed'}
    Start-ScheduledTask -TaskName (TaskName $app);$limit=(Get-Date).AddSeconds(35)
    do{Start-Sleep -Milliseconds 250;$state=Get-Content (Join-Path $app '.pdv-update/journal.json') -Raw|ConvertFrom-Json}while($state.Phase -ne 'Recovered' -and (Get-Date) -lt $limit)
    if($state.Phase -ne 'Recovered'){throw 'SYSTEM recovery failed'}
   }
  }
  foreach($n in $names){$expected=if($scenario -eq 'success'){(Get-FileHash (Join-Path $payload $n)).Hash}else{$before[$n]};if((Get-FileHash (Join-Path $app $n)).Hash -ne $expected){throw 'Final executable set mismatch'}}
  foreach($n in $sentinels){if((Get-FileHash (Join-Path $app $n)).Hash -ne $beforeLocal[$n]){throw 'Changed customer/PET file'}}
  Write-Output "PASS isolated LOJA executable fixture: $scenario; actual published binaries backed up; local file sentinels preserved"
  $results+=@{Scenario=$scenario;Pass=$true;LocalFilesPreserved=$true;CommonOnly=$true;LegacyBaseline='10.436';PetUpdateTested=$false;MigrationApproved=$false}
 }finally{
  Stop-ScheduledTask -TaskName (TaskName $app) -ErrorAction SilentlyContinue
  Unregister-ScheduledTask -TaskName (TaskName $app) -Confirm:$false -ErrorAction SilentlyContinue
  Remove-Item $app -Recurse -Force
 }
}
$results|ConvertTo-Json -Depth 6|Set-Content evidence/common-rollback.json -Encoding UTF8
$global:LASTEXITCODE=0
