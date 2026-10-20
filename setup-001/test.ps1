$ErrorActionPreference='Stop'
$names=@('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')
$meta=Get-Content bundle/candidate.json -Raw|ConvertFrom-Json
$original=Get-Content bundle/candidate.json -Raw
Copy-Item bundle/payload.zip payload-original.zip
function Check($ok,$message){if(!$ok){throw $message};"PASS $message"}
function TaskName($app){$sha=[Security.Cryptography.SHA256]::Create();try{'LealInfoPDV-Recovery-'+([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($app.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$sha.Dispose()}}
function Fixture{
 $app=Join-Path $env:ProgramFiles ('setup001-qa-'+[Guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory $app|Out-Null
 Copy-Item startup-fixture-publish/* $app -Recurse
 foreach($n in $names){Copy-Item (Join-Path previous $n) (Join-Path $app $n) -Force}
 Set-Content "$app/QA-ISOLATED" 'QA'
 foreach($n in @('license.dat','cash.dat','stock.dat','lealinfo.db')){Set-Content (Join-Path $app $n) 'UNCHANGED'}
 New-Item -ItemType Directory (Join-Path $app '.pdv-update')|Out-Null
 @{App=$app;Channel='001';Version='10.415';Phase='Armed';Activation='Protection001-NoUpdate';Previous=$meta.PreviousFiles}|ConvertTo-Json -Depth 20|Set-Content (Join-Path $app '.pdv-update/journal.json') -Encoding UTF8
 return $app
}
function Original($app){foreach($n in $names){Check ((Get-FileHash (Join-Path $app $n)).Hash -eq (Get-FileHash (Join-Path previous $n)).Hash) "restored $n"}}
function Cleanup($app){
 Get-Process LealInfoPDV -ErrorAction SilentlyContinue|Where-Object{$_.Path -eq "$app/LealInfoPDV.exe" -or $_.Path -eq "$app\LealInfoPDV.exe"}|ForEach-Object{$_.Kill();$_.WaitForExit()}
 Stop-ScheduledTask -TaskName (TaskName $app) -ErrorAction SilentlyContinue
 Unregister-ScheduledTask -TaskName (TaskName $app) -Confirm:$false -ErrorAction SilentlyContinue
 Remove-Item $app -Recurse -Force
}
try{
 $systemSid=New-Object Security.Principal.SecurityIdentifier 'S-1-5-18'
 $qualified=$systemSid.Translate([Security.Principal.NTAccount]).Value
 $resolved=(New-Object Security.Principal.NTAccount $qualified).Translate([Security.Principal.SecurityIdentifier])
 Check ($resolved.Value -eq 'S-1-5-18') ('qualified/localized task identity resolves to SYSTEM: '+$qualified)
 Check ((Get-Content bundle/worker.ps1 -Raw) -notmatch "UserId -notin") 'no locale-dependent principal string comparison'
 New-Item -ItemType Directory fixture-payload|Out-Null
 foreach($n in $names){Copy-Item (Join-Path startup-fixture-publish $n) fixture-payload}
 Remove-Item bundle/payload.zip
 Compress-Archive fixture-payload/* bundle/payload.zip
 $meta.PackageSha256=(Get-FileHash bundle/payload.zip).Hash.ToLowerInvariant()
 $meta|ConvertTo-Json -Depth 20|Set-Content bundle/candidate.json -Encoding UTF8
 $app=Fixture
 try{
  Add-Content "$app/LealInfoPDV.dll" 'TAMPER'
  & powershell.exe -NoProfile -ExecutionPolicy Bypass -File bundle/activate.ps1 -App $app
  Check ($LASTEXITCODE -ne 0) 'wrong baseline refused before replacement'
 }finally{Cleanup $app}
 $app=Fixture
 try{
  $locked=[IO.File]::Open((Join-Path $app 'LealInfoPDV.dll'),'Open','Read','Read')
  try {
   & powershell.exe -NoProfile -ExecutionPolicy Bypass -File bundle/activate.ps1 -App $app
   Check ($LASTEXITCODE -ne 0) 'actual locked DLL setup failure refuses success'
  }finally{$locked.Dispose()}
  Start-ScheduledTask -TaskName (TaskName $app)
  $limit=(Get-Date).AddSeconds(40)
  do{Start-Sleep -Milliseconds 300;$state=Get-Content "$app/.pdv-update/journal.json" -Raw|ConvertFrom-Json}while($state.Phase -ne 'Recovered' -and (Get-Date) -lt $limit)
  Check ($state.Phase -eq 'Recovered') 'SYSTEM task recovers first transition without PDV'
  Original $app
 }finally{Cleanup $app}
 # Build and execute the actual Inno UI wrapper using a synthetic healthy host.
 Copy-Item bundle source/setup-001/bundle -Recurse -Force
 & $env:ISCC source/setup-001/setup.iss
 if($LASTEXITCODE -ne 0){throw 'QA setup compilation failed'}
 $app=Fixture
 try{
  $p=Start-Process source/setup-001/output/Setup_Correcao_PDV_001_10_416.exe -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$app+'"'),('/LOG="'+(Join-Path (Get-Location) 'setup-ui.log')+'"')) -Wait -PassThru
  if($p.ExitCode -ne 0){Get-Content (Join-Path $env:TEMP 'LEAL_SETUP_001_ERRO.txt') -ErrorAction SilentlyContinue;Get-Content setup-ui.log}
  Check ($p.ExitCode -eq 0) 'real setup executable completes protected first transition'
  $state=Get-Content "$app/.pdv-update/journal.json" -Raw|ConvertFrom-Json
  Check ($state.Phase -eq 'Committed') 'real setup waits for healthy startup confirmation'
  foreach($n in @('license.dat','cash.dat','stock.dat','lealinfo.db')){Check ((Get-Content (Join-Path $app $n)).Trim() -eq 'UNCHANGED') "preserved $n"}
 }finally{Cleanup $app}
}finally{
 Copy-Item payload-original.zip bundle/payload.zip -Force
 [IO.File]::WriteAllText((Join-Path (Get-Location) 'bundle/candidate.json'),$original)
}
'PASS setup wrapper and first-transition recovery; synthetic Windows host, not store homologation'
