$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Isolated runner required'}
$app=Join-Path $env:RUNNER_TEMP ('testes003-update-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $app|Out-Null
$names=@('LealInfoPDV.exe','LealInfoPDV.dll','LealInfoPDV.deps.json')
Copy-Item ../installed003/* $app
Set-Content (Join-Path $app 'customer.db') 'PRESERVED'
$stage=Join-Path $env:RUNNER_TEMP ('payload003-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $stage|Out-Null
foreach($n in $names){Copy-Item (Join-Path publish $n) (Join-Path $stage $n)}
$zip=$stage+'.zip';Compress-Archive ($stage+'/*') $zip
$worker=(Resolve-Path TransactionalUpdate.ps1).Path
foreach($phase in @('update','restore')){
 $package=if($phase -eq 'update'){$zip}else{(Resolve-Path ../installed003.zip).Path}
 $version=if($phase -eq 'update'){'10.459'}else{'10.458'}
 $hash=(Get-FileHash $package).Hash
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Prepare -App $app -Channel GLOBAL -Version $version -Package $package -Sha256 $hash
 if($LASTEXITCODE -ne 0){throw 'Preparation failed'}
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Apply -App $app -Channel GLOBAL
 if($LASTEXITCODE -ne 0){throw 'Apply failed'}
 $expected=if($phase -eq 'update'){'publish'}else{'../installed003'}
 foreach($n in $names){if((Get-FileHash (Join-Path $app $n)).Hash -ne (Get-FileHash (Join-Path $expected $n)).Hash){throw 'Integrity mismatch'}}
 if((Get-Content (Join-Path $app 'customer.db') -Raw).Trim() -ne 'PRESERVED'){throw 'Customer file changed'}
}
New-Item -ItemType Directory evidence -Force|Out-Null
@{Success=$true;NormalUpdate=$true;NormalRollback=$true;PreviousPreserved=$true;FaultInjected=$false;RealComputer=$false}|ConvertTo-Json|Set-Content evidence/testes003-rollback.json
$global:LASTEXITCODE=0
