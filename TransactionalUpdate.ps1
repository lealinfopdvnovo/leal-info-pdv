param(
 [ValidateSet('PrepareApply','Prepare','Apply','Recover')][string]$Mode='Recover',
 [Parameter(Mandatory=$true)][string]$App,
 [string]$Package='', [string]$Sha256='', [string]$Version='',
 [ValidateSet('001','003')][string]$Channel='001',
 [int]$PreviousPid=0, [long]$PreviousStarted=0,
 [string]$Nonce='', [string]$Fault='', [int]$HealthTimeoutSeconds=120
)
$ErrorActionPreference='Stop'
$App=[IO.Path]::GetFullPath($App).TrimEnd('\')
$root=Join-Path $App '.pdv-update'
$names=@('LealInfoPDV.deps.json','LealInfoPDV.dll','LealInfoPDV.exe')
$journal=Join-Path $root 'journal.json'
$sha=[Security.Cryptography.SHA256]::Create()
try{$task='LealInfoPDV-Recovery-'+([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($App.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$sha.Dispose()}
function HashFile([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant()}
function FlushFile([string]$p){$f=[IO.File]::Open($p,'Open','ReadWrite','None');try{$f.Flush($true)}finally{$f.Dispose()}}
function SaveState($s){
 $bytes=[Text.Encoding]::UTF8.GetBytes(($s|ConvertTo-Json -Depth 12))
 $tmp=$journal+'.tmp';$f=[IO.File]::Open($tmp,'Create','Write','None')
 try{$f.Write($bytes,0,$bytes.Length);$f.Flush($true)}finally{$f.Dispose()}
 if(Test-Path $journal){[IO.File]::Replace($tmp,$journal,$null)}else{[IO.File]::Move($tmp,$journal)}
}
function Point([string]$name){
 if($Fault -eq ''){return}
 if($env:GITHUB_ACTIONS -ne 'true' -or !(Test-Path (Join-Path $App 'QA-ISOLATED')) -or !$App.StartsWith([IO.Path]::GetFullPath($env:RUNNER_TEMP),[StringComparison]::OrdinalIgnoreCase)){throw 'Fault injection refused'}
 if($Fault -eq $name){[Environment]::Exit(73)}
}
function Matches([string]$folder,$hashes){
 foreach($n in $names){if(!(Test-Path (Join-Path $folder $n)) -or (HashFile (Join-Path $folder $n)) -ne $hashes.$n){return $false}}
 return $true
}
function AssertNoProcess{
 foreach($p in @(Get-Process -Name LealInfoPDV -ErrorAction SilentlyContinue)){
  # Refuse unknown executable identity rather than terminating an operational PDV.
  if(!$p.Path){throw 'Cannot establish process identity'}
  if([IO.Path]::GetFullPath($p.Path) -eq (Join-Path $App 'LealInfoPDV.exe')){throw 'PDV is still running; recovery deferred'}
 }
}
function CopyDurable([string]$from,[string]$to){[IO.File]::Copy($from,$to,$true);FlushFile $to}
function ReplaceFile([string]$from,[string]$to,[string]$hash){
 $temp=$to+'.pdv-new';CopyDurable $from $temp
 if((HashFile $temp) -ne $hash){throw 'Replacement hash mismatch'}
 if(Test-Path $to){[IO.File]::Replace($temp,$to,$null)}else{[IO.File]::Move($temp,$to)}
}
function HideHost{
 $hostPath=Join-Path $App 'LealInfoPDV.exe'
 if(Test-Path $hostPath){[IO.File]::Move($hostPath,(Join-Path $root ('held-'+[Guid]::NewGuid().ToString('N')+'.exe')))}
}
function Recover($s){
 if($s.Phase -in @('Committed','Recovered','Aborted')){return}
 if($s.Phase -eq 'AwaitingHealth' -and (Healthy $s)){$s.Phase='Committed';SaveState $s;return}
 if($s.Phase -eq 'AwaitingHealth'){StopPendingStartup $s}
 if($s.Phase -eq 'Prepared'){$s.Phase='Aborted';SaveState $s;return}
 if($s.App -ne $App -or $s.Channel -notin @('001','003')){throw 'Wrong transaction identity'}
 if(!(Matches (Join-Path $root 'previous') $s.Previous)){throw 'Backup hash mismatch; no recovery file overwritten'}
 AssertNoProcess
 $s.Phase='Recovering';SaveState $s
 HideHost;Point 'recover-hidden'
 foreach($n in $names){ReplaceFile (Join-Path $root ('previous/'+$n)) (Join-Path $App $n) $s.Previous.$n;Point ('recover-'+$n)}
 if(!(Matches $App $s.Previous)){throw 'Incomplete recovery'}
 $s.Phase='Recovered';SaveState $s
}
function Healthy($s){
 try{
  $r=Get-Content (Join-Path $root 'health.json') -Raw|ConvertFrom-Json
  if($r.Nonce -ne $s.Nonce -or $r.Version -ne $s.Version -or !(Matches $App $s.Target)){return $false}
  $p=[Diagnostics.Process]::GetProcessById([int]$r.Pid)
  try{return !$p.HasExited -and $p.StartTime.ToUniversalTime().Ticks -eq $r.StartedTicks -and [IO.Path]::GetFullPath($p.MainModule.FileName) -eq (Join-Path $App 'LealInfoPDV.exe')}finally{$p.Dispose()}
 }catch{return $false}
}
function StopPendingStartup($s){
 # Only the startup created for this exact transaction may be stopped, before it reports readiness.
 $rows=Get-CimInstance Win32_Process -Filter "Name='LealInfoPDV.exe'"
 foreach($row in $rows){
  if($row.ExecutablePath -eq (Join-Path $App 'LealInfoPDV.exe') -and $row.CommandLine -like ('*--update-transaction '+$s.Nonce+'*')){
   $p=[Diagnostics.Process]::GetProcessById([int]$row.ProcessId)
   try{$p.Kill();if(!$p.WaitForExit(15000)){throw 'Unready startup did not stop'}}finally{$p.Dispose()}
  }
 }
}
function Prepare{
 if($Channel -notin @('001','003') -or !($Version -match '^10\.\d+$')){throw 'Unsupported channel/version'}
 if(Test-Path $journal){$old=Get-Content $journal -Raw|ConvertFrom-Json;if($old.Phase -eq 'Prepared'){$old.Phase='Aborted';SaveState $old};if($old.Phase -notin @('Committed','Recovered','Aborted')){throw 'Pending transaction must recover before another update'}
  # Preserve all earlier verified backups/journals for diagnosis and rollback.
  $archive=$root+'-history-'+[Guid]::NewGuid().ToString('N');
  foreach($n in @('journal.json','previous','stage')){if(Test-Path (Join-Path $root $n)){New-Item -ItemType Directory $archive -Force|Out-Null;Move-Item (Join-Path $root $n) $archive}}
 }
 if(!(Test-Path $Package) -or (HashFile $Package) -ne $Sha256.ToLowerInvariant()){throw 'Package SHA-256 mismatch'}
 $size=(Get-Item $Package).Length
 foreach($n in $names){$size+=(Get-Item (Join-Path $App $n)).Length}
 $drive=New-Object IO.DriveInfo ([IO.Path]::GetPathRoot($App))
 if($drive.AvailableFreeSpace -lt ($size*4+16MB)){throw 'Insufficient staging/backup disk space'}
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $z=[IO.Compression.ZipFile]::OpenRead($Package)
 try{
  if($z.Entries.Count -ne 3 -or @($z.Entries|Where-Object{$_.FullName -cnotin $names}).Count -ne 0){throw 'Only exact three-file incremental payload is accepted'}
  if(@($z.Entries|Select-Object -ExpandProperty FullName -Unique).Count -ne 3){throw 'Duplicate payload member'}
  foreach($e in $z.Entries){if($e.Length -gt 512MB){throw 'Payload member too large'}}
  $expanded=($z.Entries|Measure-Object Length -Sum).Sum
  if($drive.AvailableFreeSpace -lt ($expanded*3+$size+16MB)){throw 'Insufficient expanded disk space'}
  New-Item -ItemType Directory (Join-Path $root 'stage'),(Join-Path $root 'previous') -Force|Out-Null
  $targets=@{};$previous=@{}
  foreach($e in $z.Entries){$dest=Join-Path $root ('stage/'+$e.FullName);[IO.Compression.ZipFileExtensions]::ExtractToFile($e,$dest,$true);FlushFile $dest;$targets[$e.FullName]=HashFile $dest}
  foreach($n in $names){$src=Join-Path $App $n;$original=HashFile $src;CopyDurable $src (Join-Path $root ('previous/'+$n));if((HashFile (Join-Path $root ('previous/'+$n))) -ne $original){throw 'Backup verification failed'};$previous[$n]=$original}
 }finally{$z.Dispose()}
 $s=[pscustomobject]@{App=$App;Channel=$Channel;Version=$Version;Phase='Prepared';Previous=[pscustomobject]$previous;Target=[pscustomobject]$targets;PackageSha256=$Sha256;Nonce=[Guid]::NewGuid().ToString('N');CreatedUtc=[DateTime]::UtcNow.ToString('o')}
 if(!(Matches $App $s.Previous)){throw 'Installed files changed during backup'}
 SaveState $s
 # Persist a dependency-free worker under the protected installation, before any replacement.
 CopyDurable $PSCommandPath (Join-Path $root 'worker.ps1')
 $action=New-ScheduledTaskAction -Execute (Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe') -Argument ('-NoProfile -ExecutionPolicy Bypass -File "'+(Join-Path $root 'worker.ps1')+'" -Mode Recover -App "'+$App+'" -Channel '+$Channel)
 $boot=New-ScheduledTaskTrigger -AtStartup
 $retry=New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 1)
 $settings=New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 5)
 Register-ScheduledTask -TaskName $task -Action $action -Trigger @($boot,$retry) -Settings $settings -User SYSTEM -RunLevel Highest -Force|Out-Null
 $registered=Get-ScheduledTask -TaskName $task
 if($registered.State -eq 'Disabled' -or $registered.Principal.UserId -notin @('SYSTEM','S-1-5-18')){throw 'External recovery not active'}
 Point 'prepared'
 return $s
}
function Apply($s,[bool]$health=$false){
 if($s.Phase -ne 'Prepared' -or $s.App -ne $App -or $s.Channel -ne $Channel){throw 'Transaction not prepared for this installation'}
 if(!(Matches $App $s.Previous) -or !(Matches (Join-Path $root 'previous') $s.Previous) -or !(Matches (Join-Path $root 'stage') $s.Target)){throw 'Pre-apply verification failed'}
 AssertNoProcess
 $s.Phase='Applying';SaveState $s
 try{
  HideHost;Point 'hidden'
  foreach($n in $names){ReplaceFile (Join-Path $root ('stage/'+$n)) (Join-Path $App $n) $s.Target.$n;Point ('copy-'+$n)}
  if(!(Matches $App $s.Target)){throw 'Target verification failed'}
  Point 'published'
  $s.Phase=$(if($health){'AwaitingHealth'}else{'Committed'});SaveState $s
 }catch{Recover $s;throw}
}
New-Item -ItemType Directory $root -Force|Out-Null
# The worker is elevated by the ordinary update UI; the task runs independently as SYSTEM.
$gate=$null
try{
 try{$gate=[IO.File]::Open((Join-Path $root 'transaction.lock'),'OpenOrCreate','ReadWrite','None')}catch{if($Mode -eq 'Recover'){exit 0};throw 'Another update/recovery owns this installation'}
 if($Mode -eq 'Recover'){
  if(Test-Path $journal){Recover (Get-Content $journal -Raw|ConvertFrom-Json)}
 }elseif($Mode -eq 'Apply'){
  Apply (Get-Content $journal -Raw|ConvertFrom-Json)
 }else{
  $s=Prepare
  if($Mode -eq 'PrepareApply'){
   [IO.File]::WriteAllText((Join-Path $root 'prepared.ready'),$Nonce)
   if($PreviousPid -le 0 -or $PreviousStarted -le 0){throw 'Invalid handoff process'}
   try{$p=[Diagnostics.Process]::GetProcessById($PreviousPid)}catch [ArgumentException]{$p=$null}
   if($p){try{if($p.StartTime.ToUniversalTime().Ticks -ne $PreviousStarted){throw 'Previous process identity changed'};if(!$p.WaitForExit(60000)){throw 'PDV did not close; nothing replaced'}}finally{$p.Dispose()}}
   if($HealthTimeoutSeconds -ne 120 -and ($env:GITHUB_ACTIONS -ne 'true' -or !(Test-Path (Join-Path $App 'QA-ISOLATED')))){throw 'Test timeout refused'}
   Apply $s $true
   $launch=New-Object Diagnostics.ProcessStartInfo
   $launch.FileName=Join-Path $App 'LealInfoPDV.exe';$launch.WorkingDirectory=$App
   $launch.UseShellExecute=$false;$launch.Arguments='--update-transaction '+$s.Nonce
   $launch.EnvironmentVariables['LEAL_UPDATE_NONCE']=$s.Nonce
   $started=[Diagnostics.Process]::Start($launch)
   $deadline=(Get-Date).AddSeconds($HealthTimeoutSeconds)
   try{
    do{if(Healthy $s){$s.Phase='Committed';SaveState $s;break};if($started.HasExited){throw 'Updated PDV failed to start'};Start-Sleep -Milliseconds 250}while((Get-Date) -lt $deadline)
    if($s.Phase -ne 'Committed'){throw 'Updated PDV did not confirm real startup'}
   }finally{$started.Dispose()}
  }
 }
}catch{
 $failure=$_.Exception.Message
 Add-Content (Join-Path $root 'recovery-error.log') $failure
 try{if(Test-Path $journal){$s=Get-Content $journal -Raw|ConvertFrom-Json;if($s.Phase -in @('Applying','Recovering','AwaitingHealth')){Recover $s}}}catch{Add-Content (Join-Path $root 'recovery-error.log') $_.Exception.Message}
 [Console]::Error.WriteLine($failure);exit 1
}finally{if($gate){$gate.Dispose()};if(Test-Path $journal){try{$end=Get-Content $journal -Raw|ConvertFrom-Json;if($end.Phase -in @('Committed','Recovered','Aborted')){Disable-ScheduledTask -TaskName $task -ErrorAction SilentlyContinue|Out-Null}}catch{}}}
