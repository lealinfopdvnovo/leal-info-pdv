param(
 [ValidateSet('Prepare','Commit','Recover')][string]$Mode='Recover',
 [Parameter(Mandatory=$true)][string]$App,
 [string]$Nonce='', [string]$PetSha256='', [string]$ManifestSha256='',
 [string]$Version='', [string]$Fault='', [switch]$Fixture
)
$ErrorActionPreference='Stop'
$env:PSModulePath=(Join-Path $PSHOME 'Modules')+';'+(Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/Modules')
Import-Module (Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/Modules/ScheduledTasks/ScheduledTasks.psd1') -ErrorAction Stop
$App=[IO.Path]::GetFullPath($App).TrimEnd('\')
$root=Join-Path $App '.pdv-initial';$journal=Join-Path $root 'journal.json'
$names=@('LealInfoPDV.deps.json','LealInfoPDV.dll','LealInfoPDV.exe')
$origin=@{
 'LealInfoPDV.exe'='45cada6874ed2450cf3ae5564dfdfc7faabe749213d45126de42ef69d9d7d040'
 'LealInfoPDV.dll'='61ec4bdbbcdb7be92cee0fcac421c360cae5e58cd6604b73ef2eaad0717ca0d7'
 'LealInfoPDV.deps.json'='73477964d61c59ccf92d0532da4b8c46a18ade13ed0f1f69253b79127424d9e1'
}
$runtimeHash='d0eac8fac04c001178ff9406cf1b9636d7db1a4364271e83a24652bbc5916865'
$fixtureOK=$Fixture -and $env:GITHUB_ACTIONS -eq 'true' -and $env:RUNNER_TEMP -and $App.StartsWith(([IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd('\')+'\'),[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath (Join-Path $App 'QA-ISOLATED'))
if($Fixture -and !$fixtureOK){throw 'Fixture refused'}
if(!$fixtureOK -and !$App.StartsWith(([IO.Path]::GetFullPath($env:ProgramFiles).TrimEnd('\')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Protected installation required'}
function NoLink([string]$p){
 $item=Get-Item -LiteralPath $p -Force -ErrorAction SilentlyContinue
 if($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Redirected initial migration path'}
}
for($p=$App;$p;$p=[IO.Path]::GetDirectoryName($p)){NoLink $p}
NoLink $root;NoLink (Join-Path $App '.pdv-update');NoLink (Join-Path $App 'Extensions');NoLink (Join-Path $App 'Extensions/PET')
# These byte-copy/hash/journal primitives are the same operations as the existing
# applicator. This journal adds ownership of the FIRST PET; ordinary feeds stay unchanged.
function HashFile([string]$p){NoLink $p;$f=[IO.File]::Open($p,'Open','Read','Read');$h=[Security.Cryptography.SHA256]::Create();try{[BitConverter]::ToString($h.ComputeHash($f)).Replace('-','').ToLowerInvariant()}finally{$h.Dispose();$f.Dispose()}}
function FlushFile([string]$p){$f=[IO.File]::Open($p,'Open','ReadWrite','None');try{$f.Flush($true)}finally{$f.Dispose()}}
function CopyDurable([string]$from,[string]$to){NoLink $to;[IO.File]::Copy($from,$to,$true);FlushFile $to}
function SaveState($s){
 $bytes=[Text.Encoding]::UTF8.GetBytes(($s|ConvertTo-Json -Depth 12));$tmp=$journal+'.tmp';NoLink $tmp
 $f=[IO.File]::Open($tmp,'Create','Write','None');try{$f.Write($bytes,0,$bytes.Length);$f.Flush($true)}finally{$f.Dispose()}
 if(Test-Path -LiteralPath $journal){[IO.File]::Replace($tmp,$journal,[NullString]::Value)}else{[IO.File]::Move($tmp,$journal)}
}
function ReplaceFile([string]$from,[string]$to,[string]$hash){
 $temp=$to+'.pdv-new';CopyDurable $from $temp;if((HashFile $temp) -ne $hash){throw 'Replacement hash mismatch'}
 if(Test-Path -LiteralPath $to){[IO.File]::Replace($temp,$to,[NullString]::Value)}else{[IO.File]::Move($temp,$to)}
}
function Point([string]$name){if($Fault){if(!$fixtureOK){throw 'Fault refused'};if($Fault -eq $name){[Environment]::Exit(73)}}}
function Matches($hashes){foreach($n in $names){if(!(Test-Path -LiteralPath (Join-Path $App $n)) -or (HashFile (Join-Path $App $n)) -ne $hashes.$n){return $false}};return $true}
function RuntimeOK($s){return ((HashFile (Join-Path $App 'LealInfoPDV.runtimeconfig.json')) -eq $s.RuntimeSha256)}
function CoreState{Get-Content -LiteralPath (Join-Path $App '.pdv-update/journal.json') -Raw|ConvertFrom-Json}
function Ready($s){
 try{
  if(!(Matches $s.Target) -or !(RuntimeOK $s)){return $false}
  if((HashFile (Join-Path $App 'Extensions/PET/Pet.dll')) -ne $s.PetSha256 -or (HashFile (Join-Path $App 'Extensions/PET/manifest.json')) -ne $s.ManifestSha256){return $false}
  $r=Get-Content -LiteralPath (Join-Path $root 'ready.json') -Raw|ConvertFrom-Json
  if($r.Nonce -ne $s.Nonce -or $r.Version -ne $s.Version -or $r.PetSha256 -ne $s.PetSha256){return $false}
  $p=[Diagnostics.Process]::GetProcessById([int]$r.Pid)
  try{
   if($p.HasExited -or $p.StartTime.ToUniversalTime().Ticks -ne $r.StartedTicks){return $false}
   if($fixtureOK -and $r.SyntheticHealth -eq $true){return $true}
   return [IO.Path]::GetFullPath($p.MainModule.FileName) -eq (Join-Path $App 'LealInfoPDV.exe')
  }finally{$p.Dispose()}
 }catch{return $false}
}
function RegisterRecovery{
 $action=New-ScheduledTaskAction -Execute (Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe') -Argument ('-NoProfile -ExecutionPolicy Bypass -File "'+(Join-Path $root 'worker.ps1')+'" -Mode Recover -App "'+$App+'"')
 $boot=New-ScheduledTaskTrigger -AtStartup
 $retry=New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 1)
 $settings=New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 5)
 Register-ScheduledTask -TaskName $task -Action $action -Trigger @($boot,$retry) -Settings $settings -User 'S-1-5-18' -RunLevel Highest -Force|Out-Null
 $registered=Get-ScheduledTask -TaskName $task
 $principal=[string]$registered.Principal.UserId
 $sid=if($principal -match '^S-1-'){New-Object Security.Principal.SecurityIdentifier $principal}else{(New-Object Security.Principal.NTAccount $principal).Translate([Security.Principal.SecurityIdentifier])}
 if($registered.State -eq 'Disabled' -or $sid.Value -ne 'S-1-5-18'){throw 'Initial recovery SYSTEM task not active'}
}
$h=[Security.Cryptography.SHA256]::Create();try{$task='LealInfoPDV-Initial-'+[BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($App.ToUpperInvariant()))).Replace('-','').Substring(0,16)}finally{$h.Dispose()}
New-Item -ItemType Directory $root -Force|Out-Null
# Program Files plus explicit RX for Users; only administrators/SYSTEM may alter receipts.
$acl=New-Object Security.AccessControl.DirectorySecurity;$acl.SetAccessRuleProtection($true,$false)
foreach($id in @('S-1-5-18','S-1-5-32-544')){$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((New-Object Security.Principal.SecurityIdentifier $id),'FullControl','ContainerInherit,ObjectInherit','None','Allow')))}
$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule((New-Object Security.Principal.SecurityIdentifier 'S-1-5-32-545'),'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow')))
(Get-Item -LiteralPath $root).SetAccessControl($acl)
$gate=$null;$coreGate=$null;$launchGate=$null
try{
 try{$gate=[IO.File]::Open((Join-Path $root 'transaction.lock'),'OpenOrCreate','ReadWrite','None')}catch{if($Mode -eq 'Recover'){exit 0};throw}
 if($Mode -eq 'Prepare'){
  if(Test-Path -LiteralPath $journal){throw 'Initial migration already journaled'}
  $c=CoreState
  if($c.Phase -ne 'AwaitingHealth' -or $c.Nonce -ne $Nonce -or $c.Channel -ne '001' -or $c.Version -ne $Version -or [version]$Version -le [version]'10.467'){throw 'Not the controlled 10.467 first transition'}
  if([IO.Path]::GetFullPath($c.App).TrimEnd('\') -ne $App){throw 'Wrong legacy app identity'}
  if(!(Matches $c.Target)){throw 'Current destination differs from protected legacy journal'}
  if(Test-Path -LiteralPath (Join-Path $App 'Extensions/PET')){throw 'Existing PET must never be overwritten'}
  if($PetSha256 -notmatch '^[0-9a-f]{64}$' -or $ManifestSha256 -notmatch '^[0-9a-f]{64}$'){throw 'Invalid PET hashes'}
  $rh=HashFile (Join-Path $App 'LealInfoPDV.runtimeconfig.json')
  if(!$fixtureOK -and $rh -ne $runtimeHash){throw 'Original runtimeconfig changed'}
  $backup=Join-Path $root 'previous';NoLink $backup;New-Item -ItemType Directory $backup -Force|Out-Null
  foreach($n in $names){
   if($c.Previous.$n -ne $origin[$n] -or (HashFile (Join-Path $App ('.pdv-update/previous/'+$n))) -ne $origin[$n]){throw 'Unidentified original backup'}
   CopyDurable (Join-Path $App ('.pdv-update/previous/'+$n)) (Join-Path $backup $n)
   if((HashFile (Join-Path $backup $n)) -ne $origin[$n]){throw 'Durable initial backup mismatch'}
  }
  $s=[pscustomobject]@{Schema=1;App=$App;Identity='001';Phase='Armed';Version=$Version;Nonce=$Nonce;Previous=[pscustomobject]$origin;Target=$c.Target;RuntimeSha256=$rh;PetSha256=$PetSha256;ManifestSha256=$ManifestSha256;ExtensionsExisted=(Test-Path -LiteralPath (Join-Path $App 'Extensions'));SyntheticFixture=[bool]$fixtureOK}
  if([IO.Path]::GetFullPath($PSCommandPath) -ne (Join-Path $root 'worker.ps1')){CopyDurable $PSCommandPath (Join-Path $root 'worker.ps1')}
  RegisterRecovery
  SaveState $s
  Point 'armed'
 }else{
  if(!(Test-Path -LiteralPath $journal)){exit 0}
  $s=Get-Content -LiteralPath $journal -Raw|ConvertFrom-Json
  if($s.App -ne $App -or $s.Identity -ne '001' -or $s.Schema -ne 1){throw 'Wrong initial journal'}
  if($s.Phase -in @('Committed','Recovered')){exit 0}
  # Serialize with the ORIGINAL applicator and its recovery task. Never race it.
  try{$coreGate=[IO.File]::Open((Join-Path $App '.pdv-update/transaction.lock'),'OpenOrCreate','ReadWrite','None')}catch{if($Mode -eq 'Recover'){exit 0};exit 75}
  $c=CoreState
  if($c.Phase -eq 'Committed' -and $c.Nonce -eq $s.Nonce -and (Ready $s)){
   $s.Phase='Committed';SaveState $s
  }elseif($Mode -eq 'Commit'){throw 'Joint core/PET health not qualified'}
  else{
   foreach($n in $names){if($s.Previous.$n -ne $origin[$n] -or (HashFile (Join-Path $root ('previous/'+$n))) -ne $origin[$n]){throw 'Initial backup corrupt; nothing restored'}}
   if(!(RuntimeOK $s)){throw 'Runtimeconfig changed outside migration; recovery stopped'}
   foreach($p in @(Get-Process -Name LealInfoPDV -ErrorAction SilentlyContinue)){
    if(!$p.Path){throw 'Cannot establish process identity'}
    if([IO.Path]::GetFullPath($p.Path) -eq (Join-Path $App 'LealInfoPDV.exe')){
     $row=Get-CimInstance Win32_Process -Filter ('ProcessId='+$p.Id)
     if($row.CommandLine -notlike ('*--update-transaction '+$s.Nonce+'*')){throw 'Operational PDV running; recovery deferred'}
     $p.Kill();if(!$p.WaitForExit(15000)){throw 'Uncommitted startup did not stop'}
    }
   }
   $launchGate=[IO.File]::Open((Join-Path $App '.pdv-update/startup.guard'),'OpenOrCreate','ReadWrite','None')
   $s.Phase='Recovering';SaveState $s
   $exe=Join-Path $App 'LealInfoPDV.exe';NoLink $exe
   if(Test-Path -LiteralPath $exe){[IO.File]::Move($exe,(Join-Path $root ('held-'+[Guid]::NewGuid().ToString('N')+'.exe')))}
   foreach($n in @('LealInfoPDV.deps.json','LealInfoPDV.dll')){ReplaceFile (Join-Path $root ('previous/'+$n)) (Join-Path $App $n) $origin[$n];Point ('restore-'+$n)}
   # Source had NO external PET. Quarantine the whole new directory, including
   # unexpected files; never delete customer material or follow a redirected path.
   $pet=Join-Path $App 'Extensions/PET';NoLink (Join-Path $App 'Extensions');NoLink $pet
   if(Test-Path -LiteralPath $pet){[IO.Directory]::Move($pet,(Join-Path $root ('quarantined-pet-'+[Guid]::NewGuid().ToString('N'))))}
   Point 'pet-recovered'
   if(!$s.ExtensionsExisted -and (Test-Path -LiteralPath (Join-Path $App 'Extensions')) -and @(Get-ChildItem -LiteralPath (Join-Path $App 'Extensions') -Force).Count -eq 0){[IO.Directory]::Delete((Join-Path $App 'Extensions'))}
   ReplaceFile (Join-Path $root 'previous/LealInfoPDV.exe') (Join-Path $App 'LealInfoPDV.exe') $origin['LealInfoPDV.exe']
   if(!(Matches ([pscustomobject]$origin)) -or !(RuntimeOK $s) -or (Test-Path -LiteralPath $pet)){throw 'Joint rollback incomplete'}
   $s.Phase='Recovered';SaveState $s
  }
 }
}catch{[Console]::Error.WriteLine($_.Exception.Message);exit 1}
finally{
 if($launchGate){$launchGate.Dispose()};if($coreGate){$coreGate.Dispose()};if($gate){$gate.Dispose()}
 if(Test-Path -LiteralPath $journal){try{$end=Get-Content -LiteralPath $journal -Raw|ConvertFrom-Json;if($end.Phase -in @('Committed','Recovered')){Disable-ScheduledTask -TaskName $task -ErrorAction SilentlyContinue|Out-Null}}catch{}}
}
