$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Isolated runner only'}
$worker=(Resolve-Path (Join-Path $PSScriptRoot 'PetTransactionalUpdate.ps1')).Path
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Hash($p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant()}
function TaskName($p){
 $h=[Security.Cryptography.SHA256]::Create()
 try{'LealInfoPDV-PetRecovery-'+([BitConverter]::ToString($h.ComputeHash([Text.Encoding]::UTF8.GetBytes($p.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$h.Dispose()}
}
$results=@()
$runnerOriginal=$env:RUNNER_TEMP
$fixtureRoot=Join-Path $env:ProgramFiles ('LealInfoPDV-QA-PET-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $fixtureRoot|Out-Null
$env:RUNNER_TEMP=$fixtureRoot
try{
foreach($owner in @('001','003')){
foreach($case in @('compatible-range','incompatible-range','wrong-api','success','bad-hash','wrong-identity','wrong-manifest','downgrade','traversal','global-pending','tampered-stage','interruption-system-recovery','health-failure')){
 $app=Join-Path $env:RUNNER_TEMP ('pet-transaction-'+[Guid]::NewGuid().ToString('N'))
 $pet=Join-Path $app 'Extensions/PET';New-Item -ItemType Directory $pet -Force|Out-Null
 Set-Content (Join-Path $app 'QA-ISOLATED') 'disposable fixture'
 foreach($n in @('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json','customer.db','license.protected')){[IO.File]::WriteAllText((Join-Path $app $n),'UNCHANGED-'+$n)}
 [IO.File]::WriteAllText((Join-Path $pet 'Pet.dll'),'OLD-PET-OPAQUE-FIXTURE')
 $old=@{Identity=$owner;Version='1.1';ApiVersion=1;Assembly='Pet.dll';Sha256=(Hash (Join-Path $pet 'Pet.dll'))}
 [IO.File]::WriteAllText((Join-Path $pet 'manifest.json'),($old|ConvertTo-Json))
 $original=@{};foreach($n in @('manifest.json','Pet.dll')){$original[$n]=Hash (Join-Path $pet $n)}
 $protected=@{};foreach($n in @('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json','customer.db','license.protected')){$protected[$n]=Hash (Join-Path $app $n)}
 $temp=Join-Path $env:RUNNER_TEMP ('pet-package-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $temp|Out-Null
 $package=Join-Path $temp 'update.zip'
 $z=[IO.Compression.ZipFile]::Open($package,'Create')
 $version=if($case -eq 'downgrade'){'1.1'}else{'1.2'}
 $bytes=[Text.Encoding]::UTF8.GetBytes('NEW-PET-OPAQUE-FIXTURE')
 $h=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($h.ComputeHash($bytes)).Replace('-','').ToLowerInvariant()}finally{$h.Dispose()}
 $manifest=@{Identity=$(if($case -eq 'wrong-manifest'){'004'}else{$owner});Version=$version;ApiVersion=1;Assembly='Pet.dll';Sha256=$hash}
 if($case -in @('compatible-range','incompatible-range')){$manifest.MinGlobalVersion='10.465';$manifest.MaxGlobalVersionExclusive=if($case -eq 'incompatible-range'){'10.470'}else{'10.480'}}
 if($case -eq 'wrong-api'){$manifest.ApiVersion=2}
 try{
  foreach($entry in @(@('Extensions/PET/Pet.dll',$bytes),@('Extensions/PET/manifest.json',[Text.Encoding]::UTF8.GetBytes(($manifest|ConvertTo-Json))))){
   $e=$z.CreateEntry($entry[0]);$s=$e.Open();try{$s.Write($entry[1],0,$entry[1].Length)}finally{$s.Dispose()}
  }
  if($case -eq 'traversal'){$e=$z.CreateEntry('../LealInfoPDV.dll');$s=$e.Open();try{$s.WriteByte(1)}finally{$s.Dispose()}}
 }finally{$z.Dispose()}
 $sha=Hash $package
 $identity=if($case -eq 'wrong-identity'){'004'}else{$owner}
 if($case -eq 'bad-hash'){$sha='0'*64}
 try{
  if($case -eq 'global-pending'){
   New-Item -ItemType Directory (Join-Path $app '.pdv-update') -Force|Out-Null
   [IO.File]::WriteAllText((Join-Path $app '.pdv-update/journal.json'),'{"Phase":"Applying"}')
  }
  if($case -eq 'health-failure'){
   $process=Start-Process powershell.exe -ArgumentList '-NoProfile -Command Start-Sleep -Seconds 2' -PassThru
   $pidBefore=$process.Id;$ticks=$process.StartTime.ToUniversalTime().Ticks;$process.Dispose()
   & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode PrepareApply -App $app -Package $package -Sha256 $sha -Version $version -Channel PET -Identity $identity -GlobalVersion 10.470 -PreviousPid $pidBefore -PreviousStarted $ticks -Nonce fixture -HealthTimeoutSeconds 5
   if($LASTEXITCODE -eq 0){throw 'Invalid executable was accepted as healthy'}
  }else{
   & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Prepare -App $app -Package $package -Sha256 $sha -Version $version -Channel PET -Identity $identity -GlobalVersion 10.470
   $reject=$case -in @('bad-hash','wrong-identity','wrong-manifest','downgrade','traversal','global-pending','incompatible-range','wrong-api')
   if($reject){if($LASTEXITCODE -eq 0){throw "Unsafe prepare accepted: $case"}}
   else{
    if($LASTEXITCODE -ne 0){throw "Prepare failed: $case"}
    foreach($n in @('manifest.json','Pet.dll')){if((Hash (Join-Path $app ('.pdv-pet-update/previous/'+$n))) -ne $original[$n]){throw 'Backup bytes differ'}}
    if($case -eq 'tampered-stage'){[IO.File]::WriteAllText((Join-Path $app '.pdv-pet-update/stage/Pet.dll'),'TAMPERED')}
    $fault=if($case -eq 'interruption-system-recovery'){'copy-Pet.dll'}else{''}
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Apply -App $app -Channel PET -Identity $identity -GlobalVersion 10.470 -Fault $fault
    if($case -in @('success','compatible-range')){
     if($LASTEXITCODE -ne 0){throw 'PET apply failed'}
    }elseif($case -eq 'tampered-stage'){
     if($LASTEXITCODE -eq 0){throw 'Tampered stage accepted'}
    }else{
     if($LASTEXITCODE -ne 73){throw 'Interruption checkpoint failed'}
     Start-ScheduledTask -TaskName (TaskName $app)
     $deadline=(Get-Date).AddSeconds(40)
     do{Start-Sleep -Milliseconds 250;$state=Get-Content (Join-Path $app '.pdv-pet-update/journal.json') -Raw|ConvertFrom-Json}while($state.Phase -ne 'Recovered' -and (Get-Date) -lt $deadline)
     if($state.Phase -ne 'Recovered'){
      Write-Output ($state|ConvertTo-Json -Depth 12)
      Get-ScheduledTaskInfo -TaskName (TaskName $app)|Format-List|Out-String|Write-Output
      $failureLog=Join-Path $app '.pdv-pet-update/recovery-error.log'
      if(Test-Path $failureLog){Get-Content $failureLog|Write-Output}
      throw 'Independent SYSTEM recovery failed'
     }
    }
   }
  }
  foreach($n in $protected.Keys){if((Hash (Join-Path $app $n)) -ne $protected[$n]){throw "GLOBAL or protected data changed: $n"}}
  if($case -in @('success','compatible-range')){
   $m=Get-Content (Join-Path $pet 'manifest.json') -Raw|ConvertFrom-Json
   if($m.Version -ne '1.2' -or (Hash (Join-Path $pet 'Pet.dll')) -ne $hash){throw 'Target PET mismatch'}
  }else{foreach($n in $original.Keys){if((Hash (Join-Path $pet $n)) -ne $original[$n]){throw "Original PET not preserved/restored: $case / $n"}}}
  $results+=@{Identity=$owner;Scenario=$case;ProtectedHashes=$protected;Pass=$true;BaseAndDataUnchanged=$true;OperationalCustomer=$false}
  Write-Output "PASS PET transaction $case"
 }finally{
  Stop-ScheduledTask -TaskName (TaskName $app) -ErrorAction SilentlyContinue
  Unregister-ScheduledTask -TaskName (TaskName $app) -Confirm:$false -ErrorAction SilentlyContinue
  Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
 }
}
}
New-Item -ItemType Directory pair-evidence -Force|Out-Null
$results|ConvertTo-Json -Depth 8|Set-Content pair-evidence/pet-transaction-results.json -Encoding UTF8
$global:LASTEXITCODE=0

}finally{
 $env:RUNNER_TEMP=$runnerOriginal
 Remove-Item $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
}
