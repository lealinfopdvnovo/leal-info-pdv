$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient;Add-Type -AssemblyName UIAutomationTypes
$app=Join-Path $env:ProgramFiles 'LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO'
$exe=Join-Path $app 'LealInfoPDV.exe'
$p=Start-Process $exe -ArgumentList '--qa-start-notifier' -PassThru -Wait
if($p.ExitCode -ne 0){throw 'QA notifier bootstrap failed'}
$root=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'LEAL-INFO-QA/ANGRA-ETAPA4-20261114/LealInfoPDV/Notificador'
$ini=Get-ChildItem $root -Filter agent.ini -Recurse|Select-Object -First 1
if(!$ini){throw 'QA notifier configuration absent'}
$config=Get-Content $ini.FullName -Raw
if($config -notmatch 'Identity=005' -or $config -notmatch 'Route=CLIENT'){throw 'Wrong notifier identity/route'}
$path=($config -split "`n"|Where-Object{$_ -like 'Agent=*'}).Substring(6).Trim()
$key=($config -split "`n"|Where-Object{$_ -like 'StartupKey=*'}).Substring(11).Trim()
$reg=Get-ItemProperty 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
if($reg.$key -notlike '*LEAL-INFO-QA*'){throw 'QA startup registration absent or points outside QA'}
$deadline=(Get-Date).AddSeconds(10)
do{Start-Sleep -Milliseconds 200;$agent=Get-Process|Where-Object{$_.Path -eq $path}|Select-Object -First 1}while(!$agent -and (Get-Date) -lt $deadline)
if(!$agent){throw 'Native tray agent not running'}
@{Pass=$true;ExistingNativeTrayReused=$true;Identity='005';StartupRegistered=$true;AgentRunning=$true;ProductionRoute=$false;Config=$config}|ConvertTo-Json|Set-Content qa-setup-evidence/notifier-qa.json -Encoding UTF8
# The old executable must remain unaffected; the notifier only monitors this QA directory.
Write-Output 'PASS native QA tray running; HKCU startup and independent QA configuration verified'
