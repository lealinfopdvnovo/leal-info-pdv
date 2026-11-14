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
$env:QA_EVIDENCE=(Resolve-Path qa-setup-evidence).Path
try{
 foreach($case in @('old-license-identity','wrong-api','bad-hash','wrong-module-identity','success')){
  & dotnet exec --runtimeconfig "$app/LealInfoPDV.runtimeconfig.json" --depsfile "$app/LealInfoPDV.deps.json" "$app/ContractProbe.dll" $case
  if($LASTEXITCODE -ne 0){throw ('Contract case failed: '+$case)}
 }
 if((Get-FileHash $hostDll).Hash -ne $before){throw 'Installed core changed'}
 # The issuer public key and license verifier remain original in the shipped build.
 foreach($file in @('Network/NetworkLicense.cs','Network/SellerPublicKey.cs','Licensing/InstallationLicense.cs')){
  if((Get-FileHash "candidate/$file").Hash -ne (Get-FileHash "controller/$file").Hash){throw ('License source changed: '+$file)}
 }
 $global:LASTEXITCODE=0
}finally{Remove-Item "$app/ContractProbe.dll" -Force -ErrorAction SilentlyContinue}
