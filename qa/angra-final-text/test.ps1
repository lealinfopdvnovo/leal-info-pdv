$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Disposable Windows runner only'}
$app=Join-Path $env:ProgramFiles 'LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO'
$setup=(Resolve-Path bundle/LEAL_INFO_ANGRA_QA_ATUALIZADOR.exe).Path
if((Get-FileHash $setup).Hash.ToLowerInvariant() -ne '315b1b5a05de36a80c29d63a4e1354baf5552a1968f572fd55c5242994825d70'){throw 'Approved QA installer hash mismatch'}
if(!(Test-Path (Join-Path $app 'LealInfoPDV.dll'))){throw 'Approved baseline must already be installed'}
# Stop only the auto-started QA notifier in this disposable runner to isolate screen capture.
$qaRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'LEAL-INFO-QA/ANGRA-ETAPA4-20261114'))
Get-Process -ErrorAction SilentlyContinue|Where-Object {$_.Path -and $_.Path.StartsWith($qaRoot,[StringComparison]::OrdinalIgnoreCase)}|Stop-Process -Force
Copy-Item baseline/Extensions/PET/* (Join-Path $app 'Extensions/PET/') -Force
$protected=@{}
foreach($name in @('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')){$protected[$name]=(Get-FileHash (Join-Path $app $name)).Hash}
if($protected['LealInfoPDV.dll'].ToLowerInvariant() -ne 'd225a8c67d6b980626e7e74592bb81b84476c8354887a199c74903eb203767d0'){throw 'Unapproved QA host'}
$hostDll=Join-Path $app 'LealInfoPDV.dll'
$assembly=[Reflection.Assembly]::LoadFile($hostDll)
$stream=$assembly.GetManifestResourceStream('PetTransactionalUpdateWorker');if(!$stream){throw 'Existing worker absent'}
try{$target=[IO.File]::Create((Join-Path (Get-Location) 'transition-worker.ps1'));try{$stream.CopyTo($target)}finally{$target.Dispose()}}finally{$stream.Dispose()}
$worker=(Resolve-Path transition-worker.ps1).Path
# Build only the disposable test probe. Host, notifier, license and business code are reused unchanged.
$dir=Join-Path $env:RUNNER_TEMP 'transition-probe';New-Item -ItemType Directory $dir -Force|Out-Null
$project=(Get-Content controller/qa/angra-final-text/TransitionProbe.csproj -Raw).Replace('HOST_DLL',$hostDll).Replace('HOST_DIR',$app)
Set-Content "$dir/TransitionProbe.csproj" $project -Encoding UTF8
Copy-Item controller/qa/angra-final-text/TransitionProbe.cs $dir
$deps=Get-Content "$app/LealInfoPDV.deps.json" -Raw|ConvertFrom-Json
$sdk=($deps.libraries.PSObject.Properties.Name|Where-Object{$_ -like '*Microsoft.Windows.SDK.NET.Ref/*'}|Select-Object -First 1).Split('/')[-1]
dotnet build "$dir/TransitionProbe.csproj" -c Release -o "$dir/out" "-p:WindowsSdkPackageVersion=$sdk"
if($LASTEXITCODE -ne 0){throw 'Sync probe compile failed'}
Copy-Item "$dir/out/TransitionProbe.dll" $app
$env:TRANSITION_EVIDENCE=(Resolve-Path text-evidence).Path
$env:TRANSITION_FEED=(Resolve-Path transition-channel/version.json).Path
$env:TRANSITION_PACKAGE=(Resolve-Path transition-channel/ANGRA_005_PET_1_7.zip).Path
& dotnet exec --runtimeconfig "$app/LealInfoPDV.runtimeconfig.json" --depsfile "$app/LealInfoPDV.deps.json" "$app/TransitionProbe.dll" offer
if($LASTEXITCODE -ne 0){throw 'Offer/staging verification failed'}
$oldManifest=(Get-FileHash "$app/Extensions/PET/manifest.json").Hash
$oldPet=(Get-FileHash "$app/Extensions/PET/Pet.dll").Hash
$canary=Join-Path $app 'PRESERVED-QA-DATA';Set-Content $canary 'SYNTHETIC LICENSE/DATA CANARY';$canaryHash=(Get-FileHash $canary).Hash
$oldTemp=$env:RUNNER_TEMP;$env:RUNNER_TEMP=$env:ProgramFiles;Set-Content "$app/QA-ISOLATED" 'Disposable runner only'
$package=$env:TRANSITION_PACKAGE;$hash=(Get-FileHash $package).Hash.ToLowerInvariant()
try{
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Prepare -App $app -Package $package -Sha256 $hash -Version 1.7 -Channel PET -Identity 005 -GlobalVersion 10.468
 if($LASTEXITCODE -ne 0){throw 'Prepare 1.5 failed'}
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Apply -App $app -Channel PET -Identity 005 -GlobalVersion 10.468 -Fault copy-Pet.dll
 if($LASTEXITCODE -ne 73){throw 'Interruption checkpoint not reached'}
 $sha=[Security.Cryptography.SHA256]::Create()
 try{$task='LealInfoPDV-PetRecovery-'+([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($app.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$sha.Dispose()}
 Start-ScheduledTask -TaskName $task
 $deadline=(Get-Date).AddSeconds(40);$state=$null
 do{
  Start-Sleep -Milliseconds 200
  try{$f=[IO.File]::Open("$app/.pdv-pet-update/journal.json",'Open','Read',([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete));try{$reader=[IO.StreamReader]::new($f);try{$state=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}}finally{$f.Dispose()}}catch{}
 }while($state.Phase -ne 'Recovered' -and (Get-Date) -lt $deadline)
 if($state.Phase -ne 'Recovered' -or (Get-FileHash "$app/Extensions/PET/Pet.dll").Hash -ne $oldPet -or (Get-FileHash "$app/Extensions/PET/manifest.json").Hash -ne $oldManifest){throw 'PET 1.6 not restored by SYSTEM after interruption'}
 Write-Output 'PASS interrupted actual PET 1.7 package restored exact approved PET 1.6'
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Prepare -App $app -Package $package -Sha256 $hash -Version 1.7 -Channel PET -Identity 005 -GlobalVersion 10.468
 if($LASTEXITCODE -ne 0){throw 'Second prepare failed'}
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Apply -App $app -Channel PET -Identity 005 -GlobalVersion 10.468
 if($LASTEXITCODE -ne 0){throw 'PET 1.7 apply failed'}
}finally{$env:RUNNER_TEMP=$oldTemp;Remove-Item "$app/QA-ISOLATED" -ErrorAction SilentlyContinue}
# Exercise all actual licensed editions against the real login and main controls.
$project=(Get-Content controller/qa/angra-final-text/TextProbe.csproj -Raw).Replace('HOST_DLL',$hostDll).Replace('HOST_DIR',$app)
Set-Content "$dir/TextProbe.csproj" $project -Encoding UTF8
Copy-Item controller/qa/angra-final-text/TextProbe.cs $dir
dotnet build "$dir/TextProbe.csproj" -c Release -o "$dir/text-out" "-p:WindowsSdkPackageVersion=$sdk"
if($LASTEXITCODE -ne 0){throw 'Text probe compile failed'}
Copy-Item "$dir/text-out/TextProbe.dll" $app
foreach($edition in @('standard','plus','pro')){
 & dotnet exec --runtimeconfig "$app/LealInfoPDV.runtimeconfig.json" --depsfile "$app/LealInfoPDV.deps.json" "$app/TextProbe.dll" $edition
 if($LASTEXITCODE -ne 0){throw ('Real text/plan UI verification failed: '+$edition)}
}
& dotnet exec --runtimeconfig "$app/LealInfoPDV.runtimeconfig.json" --depsfile "$app/LealInfoPDV.deps.json" "$app/TransitionProbe.dll" transition
if($LASTEXITCODE -ne 0){throw 'Continuous visual transition failed'}
foreach($n in $protected.Keys){if((Get-FileHash (Join-Path $app $n)).Hash -ne $protected[$n]){throw ('Core/notifier changed: '+$n)}}
if((Get-FileHash $canary).Hash -ne $canaryHash){throw 'Data canary changed'}
@{Pass=$true;PreviousPet15RestoredAfterSystemRecovery=$true;NewPetApplied=$true;CoreHashes=$protected;DataCanaryUnchanged=$true;WorkerSHA256=(Get-FileHash $worker).Hash.ToLowerInvariant();CoreRecompiled=$false;NotifierModified=$false;LicenseModified=$false}|ConvertTo-Json -Depth 5|Set-Content text-evidence/apply-recovery.json -Encoding UTF8
$global:LASTEXITCODE=0
