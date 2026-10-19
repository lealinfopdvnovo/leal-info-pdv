$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Only isolated Windows'}
$app=Join-Path $env:RUNNER_TEMP ('activation-003-'+[Guid]::NewGuid().ToString('N'))
$control=Join-Path $env:ProgramData 'LealInfoPDVProtection003'
New-Item -ItemType Directory $app,evidence -Force|Out-Null
Invoke-WebRequest 'https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-003-v10.415/LEAL_INFO_PDV_003_V10_415_UPDATE.zip' -OutFile previous.zip
if((Get-FileHash previous.zip).Hash.ToLowerInvariant() -ne '3d8f48bb50642e1f21152b9c01295c035f1728191d6095609c464cdb7df5b734'){throw 'Baseline SHA mismatch'}
Expand-Archive previous.zip $app
Set-Content (Join-Path $app 'QA-ISOLATED') 'DISPOSABLE TEST'
$protected=@('lealinfo.db','network.license','network.config','cash.dat','stock.dat')
foreach($n in $protected){Set-Content (Join-Path $app $n) ('SYNTHETIC_'+$n)}
$names=@('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')
$before=@{};foreach($n in ($names+$protected)){$before[$n]=(Get-FileHash (Join-Path $app $n)).Hash}
function AssertUnchanged{foreach($n in ($names+$protected)){if((Get-FileHash (Join-Path $app $n)).Hash -ne $before[$n]){throw 'Changed protected file '+$n}}}
$ps=Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
try{
 & $ps -NoProfile -ExecutionPolicy Bypass -File tools/Protection003/Ativar_Protecao_003.ps1 -App $app 2>&1|Tee-Object evidence/activation.log
 if($LASTEXITCODE -ne 0){throw 'Activation failed'}
 AssertUnchanged
 Write-Output 'PASS activation preserved every installed executable and protected sentinel'
 & $ps -NoProfile -ExecutionPolicy Bypass -File tools/Protection003/Ativar_Protecao_003.ps1 -App $app -Mode Verify
 if($LASTEXITCODE -ne 0){throw 'Verification failed'}
 & $ps -NoProfile -ExecutionPolicy Bypass -File tools/Protection003/Ativar_Protecao_003.ps1 -App $app
 if($LASTEXITCODE -ne 0){throw 'Idempotent activation failed'}
 AssertUnchanged
 $task=Get-ScheduledTask LealInfoPDV-Protection003
 if($task.Principal.UserId -notin @('SYSTEM','S-1-5-18')){throw 'Not SYSTEM'}
 if(!@($task.Triggers|Where-Object{$_.CimClass.CimClassName -eq 'MSFT_TaskBootTrigger'}).Count){throw 'No boot trigger'}
 Start-ScheduledTask LealInfoPDV-Protection003;Start-Sleep -Seconds 3;AssertUnchanged
 Write-Output 'PASS real SYSTEM task is passive while 10.415 is intact'
 Add-Content (Join-Path $app 'LealInfoPDV.dll') 'QA_INTERRUPTED_COPY'
 Start-ScheduledTask LealInfoPDV-Protection003
 $deadline=(Get-Date).AddSeconds(45)
 do{Start-Sleep -Milliseconds 250;$done=Test-Path (Join-Path $control 'last-recovery.json')}while(!$done -and (Get-Date) -lt $deadline)
 if(!$done){throw 'External recovery timeout'}
 AssertUnchanged
 Write-Output 'PASS real SYSTEM task recovered full approved 10.415 set using unchanged validated worker'
 $activated=Get-Content (Join-Path $control 'protection.json') -Raw|ConvertFrom-Json
 @{Channel='003';ActivationPassed=$true;InstalledExeDllDepsUnchanged=$true;Idempotent=$true;ScheduledSystemRecoveryPassed=$true;BootTriggerPresent=$true;UpdateAuthorized=$activated.UpdateAuthorized;RealStorePcTested=$false;PdVLaunched=$false;WorkerSource=$activated.WorkerSource}|ConvertTo-Json|Set-Content evidence/result.json
 Copy-Item tools/Protection003/Ativar_Protecao_003.ps1 evidence/
}finally{
 Stop-ScheduledTask LealInfoPDV-Protection003 -ErrorAction SilentlyContinue
 Unregister-ScheduledTask LealInfoPDV-Protection003 -Confirm:$false -ErrorAction SilentlyContinue
}
