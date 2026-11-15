$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Disposable runner only'}
$app=Join-Path $env:ProgramFiles 'LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO'
# Compile against the same assembly already installed by the EXE, without rebuilding the host.
$source=Join-Path (Get-Location) 'controller/qa/etapa4-setup'
$project=Get-Content "$source/ContractProbe.csproj" -Raw
$hostDll=Join-Path $app 'LealInfoPDV.dll'
$project=$project.Replace('../../../qa-publish/LealInfoPDV.dll',$hostDll)
$probe=Join-Path $env:RUNNER_TEMP 'angra-contract-probe';New-Item -ItemType Directory $probe -Force|Out-Null
Set-Content "$probe/ContractProbe.csproj" $project -Encoding UTF8
Copy-Item "$source/ContractProbe.cs" $probe
[xml]$props=Get-Content candidate/Directory.Build.props
$sdk=$props.Project.PropertyGroup.WindowsSdkPackageVersion|Where-Object{$_}|Select-Object -Last 1
& dotnet build "$probe/ContractProbe.csproj" -c Release -o "$probe/out" "-p:WindowsSdkPackageVersion=$sdk"
if($LASTEXITCODE -ne 0){throw 'Contract probe failed to compile'}
$before=(Get-FileHash $hostDll).Hash
Copy-Item "$probe/out/ContractProbe.dll" $app
$env:QA_FEED=(Resolve-Path qa-channel/version.json).Path
$env:QA_PACKAGE=(Resolve-Path qa-channel/ANGRA_005_PET_1_4.zip).Path
$env:QA_EVIDENCE=(Resolve-Path qa-setup-evidence).Path
try{
 foreach($case in @('planner')){
  & dotnet exec --runtimeconfig "$app/LealInfoPDV.runtimeconfig.json" --depsfile "$app/LealInfoPDV.deps.json" "$app/ContractProbe.dll" $case
  if($LASTEXITCODE -ne 0){throw ('Contract case failed: '+$case)}
 }
 if((Get-FileHash $hostDll).Hash -ne $before){throw 'Installed core changed'}
 # The issuer public key and license verifier remain original in the shipped build.
 foreach($file in @('Network/NetworkLicense.cs','Network/SellerPublicKey.cs','Licensing/InstallationLicense.cs')){
  if((Get-FileHash "candidate/$file").Hash -ne (Get-FileHash "controller/$file").Hash){throw ('License source changed: '+$file)}
 }
 # Activate the actual two-file PET package through the existing transactional worker.
 $worker=(Resolve-Path candidate/PetTransactionalUpdate.ps1).Path
 $package=(Resolve-Path qa-channel/ANGRA_005_PET_1_4.zip).Path
 $sha=(Get-FileHash $package).Hash.ToLowerInvariant()
 $savedTemp=$env:RUNNER_TEMP;$env:RUNNER_TEMP=$env:ProgramFiles
 Set-Content (Join-Path $app 'QA-ISOLATED') 'Disposable runner fixture only'
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Prepare -App $app -Package $package -Sha256 $sha -Version 1.4 -Channel PET -Identity 005 -GlobalVersion 10.468
 if($LASTEXITCODE -ne 0){throw 'Actual QA PET prepare failed'}
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -Mode Apply -App $app -Channel PET -Identity 005 -GlobalVersion 10.468
 if($LASTEXITCODE -ne 0){throw 'Actual QA PET apply failed'}
 $env:RUNNER_TEMP=$savedTemp;Remove-Item (Join-Path $app 'QA-ISOLATED')
 foreach($case in @('old-license-identity','wrong-api','bad-hash','wrong-module-identity','success')){
  & dotnet exec --runtimeconfig "$app/LealInfoPDV.runtimeconfig.json" --depsfile "$app/LealInfoPDV.deps.json" "$app/ContractProbe.dll" $case
  if($LASTEXITCODE -ne 0){throw ('Patched contract case failed: '+$case)}
 }
 if((Get-FileHash $hostDll).Hash -ne $before){throw 'PET patch changed host'}
 $global:LASTEXITCODE=0
}finally{if($savedTemp){$env:RUNNER_TEMP=$savedTemp}}
