param([Parameter(Mandatory=$true)][string]$App,[string]$Fault='')
$ErrorActionPreference='Stop'
try {
 $env:PSModulePath=(Join-Path $PSHOME 'Modules')+';'+(Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/Modules')
 Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Management/Microsoft.PowerShell.Management.psd1') -ErrorAction Stop
 Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop
 $App=[IO.Path]::GetFullPath($App).TrimEnd('\')
 $programFiles64=if($env:ProgramW6432){$env:ProgramW6432}else{$env:ProgramFiles}
 $allowed=[IO.Path]::GetFullPath($programFiles64).TrimEnd('\')+'\'
 if(!$App.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw ("Pasta recusada: App=["+$App+"]; Permitida=["+$allowed+"]; 64bit="+[Environment]::Is64BitProcess)}
 if($Fault -and ($env:GITHUB_ACTIONS -ne 'true' -or !(Test-Path (Join-Path $App 'QA-ISOLATED')))){throw 'Teste recusado'}
 $meta=Get-Content "$PSScriptRoot/candidate.json" -Raw|ConvertFrom-Json
 if($meta.Channel -ne '003' -or $meta.Version -ne '10.416'){throw 'Pacote de canal incorreto'}
 foreach($n in @('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')){
  if((Get-FileHash (Join-Path $App $n) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $meta.PreviousFiles.$n){throw 'Esta instalacao nao corresponde a base 10.415 do 003. Nenhum executavel substituido.'}
 }
 if((Get-FileHash "$PSScriptRoot/payload.zip" -Algorithm SHA256).Hash.ToLowerInvariant() -ne $meta.PackageSha256){throw 'Integridade do pacote invalida'}
 if(@(Get-Process LealInfoPDV -ErrorAction SilentlyContinue|Where-Object{!$_.Path -or $_.Path -eq (Join-Path $App 'LealInfoPDV.exe')}).Count){throw 'Feche o PDV antes de continuar.'}
 $root=Join-Path $App '.pdv-update'
 foreach($p in @($App,$root)){if((Test-Path $p) -and ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Pasta redirecionada recusada'}}
 New-Item -ItemType Directory $root -Force|Out-Null
 if(@(Get-ChildItem $root -Force -Recurse|Where-Object{$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Arquivo redirecionado recusado'}
 $acl=New-Object Security.AccessControl.DirectorySecurity
 $acl.SetAccessRuleProtection($true,$false)
 foreach($sid in @('S-1-5-18','S-1-5-32-544')){
  $id=New-Object Security.Principal.SecurityIdentifier $sid
  $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($id,'FullControl','ContainerInherit,ObjectInherit','None','Allow')))
 }
 $users=New-Object Security.Principal.SecurityIdentifier 'S-1-5-32-545'
 $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($users,'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow')))
 $acl.SetOwner((New-Object Security.Principal.SecurityIdentifier 'S-1-5-32-544'))
 (Get-Item $root).SetAccessControl($acl)
 # Transfer the one-time 10.415 protection without deleting its approved backup.
 $journal=Join-Path $root 'journal.json'
 if(Test-Path $journal){
  $old=Get-Content $journal -Raw|ConvertFrom-Json
  if($old.Phase -eq 'Armed'){
   if($old.Activation -ne 'Protection003-NoUpdate' -or $old.App -ne $App -or $old.Channel -ne '003' -or $old.Version -ne '10.415'){throw 'Protecao anterior desconhecida'}
   foreach($n in @('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')){if($old.Previous.$n -ne $meta.PreviousFiles.$n){throw 'Backup anterior desconhecido'}}
   Import-Module (Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/Modules/ScheduledTasks/ScheduledTasks.psd1') -ErrorAction Stop
   $lease=[IO.File]::Open((Join-Path $root 'transaction.lock'),'OpenOrCreate','ReadWrite','None')
   try {
    $legacy=Get-ScheduledTask -TaskName 'LealInfoPDV-Protection003' -ErrorAction SilentlyContinue
    if($legacy){
     $control=Join-Path $env:ProgramData 'LealInfoPDVProtection003'
     $cfg=Get-Content (Join-Path $control 'protection.json') -Raw|ConvertFrom-Json
     if($cfg.App -ne $App -or $cfg.Channel -ne '003' -or $legacy.Actions.Arguments -ne $cfg.TaskArguments){throw 'Tarefa anterior desconhecida'}
     Disable-ScheduledTask -TaskName 'LealInfoPDV-Protection003'|Out-Null
     Stop-ScheduledTask -TaskName 'LealInfoPDV-Protection003'
    }
    $old.Phase='Aborted'
    $bytes=[Text.Encoding]::UTF8.GetBytes(($old|ConvertTo-Json -Depth 20))
    $f=[IO.File]::Open(($journal+'.setup-tmp'),'Create','Write','None')
    try{$f.Write($bytes,0,$bytes.Length);$f.Flush($true)}finally{$f.Dispose()}
    [IO.File]::Replace(($journal+'.setup-tmp'),$journal,[NullString]::Value)
   }finally{$lease.Dispose()}
  }
 }

 # Reuse the validated worker unchanged, including health receipt and independent recovery.
 $handoff=Start-Process "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" -ArgumentList '-NoProfile -Command Start-Sleep -Seconds 2' -WindowStyle Hidden -PassThru
 $ticks=$handoff.StartTime.ToUniversalTime().Ticks
 & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot/worker.ps1" -Mode PrepareApply -App $App -Package "$PSScriptRoot/payload.zip" -Sha256 $meta.PackageSha256 -Version 10.416 -Channel 003 -PreviousPid $handoff.Id -PreviousStarted $ticks -Nonce SETUP003
 if($LASTEXITCODE -ne 0){throw 'Aplicacao nao confirmada. Consulte .pdv-update/recovery-error.log; a recuperacao externa permanece responsavel por uma transacao pendente.'}
 $s=Get-Content "$root/journal.json" -Raw|ConvertFrom-Json
 if($s.Phase -ne 'Committed'){throw 'Atualizacao nao confirmada'}
 exit 0
}catch{
 [IO.File]::WriteAllText((Join-Path $env:TEMP 'LEAL_SETUP_003_ERRO.txt'),$_.Exception.ToString())
 [Console]::Error.WriteLine($_.Exception.Message)
 exit 1
}
