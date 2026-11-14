$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Isolated Windows runner only'}
$setup=(Resolve-Path setup-output/LEAL_INFO_TESTE_ANGRA_ETAPA4_CORRIGIDO.exe).Path
$local=[Environment]::GetFolderPath('LocalApplicationData')
$app=Join-Path $env:ProgramFiles 'LEAL INFO PDV - TESTE ANGRA ETAPA4 CORRIGIDO'
$qa=Join-Path $local 'LEAL-INFO-QA/ANGRA-ETAPA4-20261114'
if(Test-Path $app){throw 'Runner test install folder already exists'}
$oldQa=Join-Path $env:ProgramFiles 'LEAL INFO PDV - TESTE ANGRA ETAPA4'
New-Item -ItemType Directory $oldQa -Force|Out-Null
$oldCanary=Join-Path $oldQa 'LealInfoPDV.exe';Set-Content $oldCanary 'OLD QA BINARIES MUST REMAIN UNCHANGED'
$oldHash=(Get-FileHash $oldCanary).Hash
New-Item -ItemType Directory $qa -Force|Out-Null
$existingData=Join-Path $qa 'existing-activation-canary';Set-Content $existingData 'EXISTING QA DATA'
$existingHash=(Get-FileHash $existingData).Hash
$canaries=@();$process=$null;$before=@{}
try{
 foreach($folder in @('LealInfoPDV','LEAL INFO PDV')){
  $dir=Join-Path $local $folder;New-Item -ItemType Directory $dir -Force|Out-Null
  $file=Join-Path $dir ('QA-CANARY-'+[Guid]::NewGuid().ToString('N'));Set-Content $file 'SYNTHETIC PRODUCTION-PATH CANARY';$canaries+=@{Path=$file;Hash=(Get-FileHash $file).Hash}
 }
 foreach($folder in @('LealInfoPDV','LEAL INFO PDV')){foreach($f in Get-ChildItem (Join-Path $local $folder) -Recurse -File){$before[$f.FullName]=(Get-FileHash $f.FullName).Hash}}
 $blocked=Join-Path $env:ProgramFiles ('LEAL-QA-DO-NOT-OVERWRITE-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $blocked|Out-Null
 $sentinel=Join-Path $blocked 'LealInfoPDV.exe';Set-Content $sentinel 'SYNTHETIC EXISTING INSTALLATION';$sentinelHash=(Get-FileHash $sentinel).Hash
 $denied=Start-Process $setup -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR="'+$blocked+'"') -Wait -PassThru
 if($denied.ExitCode -eq 0 -or (Get-FileHash $sentinel).Hash -ne $sentinelHash -or (Test-Path $app)){throw 'Setup accepted unsafe directory override'}
 Remove-Item $blocked -Recurse -Force
 $installer=Start-Process $setup -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-' -Wait -PassThru
 if($installer.ExitCode -ne 0){throw ('Setup installation failed '+$installer.ExitCode)}
 $exe=Join-Path $app 'LealInfoPDV.exe'
 $process=Start-Process $exe -ArgumentList '--qa-check-isolation' -PassThru
 if(!$process.WaitForExit(15000) -or $process.ExitCode -ne 0){throw 'Installed QA diagnostic failed'};$process.Dispose();$process=$null
 $data=Get-Content (Join-Path $qa 'isolamento.json') -Raw|ConvertFrom-Json
 foreach($field in @('DatabaseFolder','DatabasePath','BackupFolder','LocalRoot','RoamingRoot','CommonRoot')){
  if(!$data.$field.StartsWith($qa,[StringComparison]::OrdinalIgnoreCase)){throw ('Data isolation failed: '+$field)}
 }
 if($data.LicenseBypassed -ne $false -or $data.ProductionUpdates -ne $false){throw 'QA guard metadata wrong'}
 # Observe the genuine activation wall. Never type a key, copy a license or bypass it.
 Add-Type -AssemblyName UIAutomationClient;Add-Type -AssemblyName UIAutomationTypes
 $process=Start-Process $exe -PassThru
 $deadline=(Get-Date).AddSeconds(25);$activation=$false
 do{
  Start-Sleep -Milliseconds 250
  $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)
  $windows=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,$condition)
  foreach($window in $windows){
   $buttons=$window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button))
   $names=@($buttons|ForEach-Object{$_.Current.Name})
   if(($names -contains 'ATIVAR CHAVE') -and ($names -contains 'IMPORTAR LICENÇA')){$activation=$true}
  }
 }while(!$activation -and !$process.HasExited -and (Get-Date) -lt $deadline)
 if(!$activation){throw 'Genuine activation window was not observed'}
 $process.Kill();$process.WaitForExit();$process.Dispose();$process=$null
 foreach($c in $canaries){if((Get-FileHash $c.Path).Hash -ne $c.Hash){throw 'Production-path canary changed'}}
 $after=@{};foreach($folder in @('LealInfoPDV','LEAL INFO PDV')){foreach($f in Get-ChildItem (Join-Path $local $folder) -Recurse -File){$after[$f.FullName]=(Get-FileHash $f.FullName).Hash}}
 if($before.Count -ne $after.Count -or @($before.Keys|Where-Object{$after[$_] -ne $before[$_]}).Count -ne 0){throw 'A production data path was accessed for writes'}
 $manifest=Get-Content (Join-Path $app 'Extensions/PET/manifest.json') -Raw|ConvertFrom-Json
 if($manifest.Identity -ne '005' -or $manifest.Version -ne '1.3' -or (Get-FileHash (Join-Path $app 'Extensions/PET/Pet.dll')).Hash.ToLowerInvariant() -ne $manifest.Sha256){throw 'Installed PET integrity mismatch'}
 $shortcut=Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'LEAL INFO PDV - TESTE ANGRA (Etapa 4).lnk'
 if(!(Test-Path $shortcut)){throw 'Separate test shortcut missing'}
 $installedHash=(Get-FileHash $exe).Hash
 $again=Start-Process $setup -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-' -Wait -PassThru
 if($again.ExitCode -eq 0 -or (Get-FileHash $exe).Hash -ne $installedHash){throw 'Existing QA installation was overwritten'}
 if((Get-FileHash $oldCanary).Hash -ne $oldHash -or (Get-FileHash $existingData).Hash -ne $existingHash){throw 'Old QA/data changed'}
 $result=@{OldQAUnchanged=$true;ExistingDataUnchanged=$true;Pass=$true;DirectoryOverrideRejected=$true;ExistingDestinationPreserved=$true;SetupSha256=(Get-FileHash $setup).Hash.ToLowerInvariant();InstalledInSeparateFolder=$true;SeparateWritableRoots=$true;ProductionCanariesUnchanged=$true;GenuineActivationObserved=$true;LicenseBypassed=$false;LicensedLoginTested=$false;ExternalPetIdentity='005';ExternalPetVersion='1.3';MigrationValidated=$false;ProductionUpdated=$false;FreshFunctionalQABuild=$true}
 $result|ConvertTo-Json -Depth 8|Set-Content qa-setup-evidence/setup-validation.json -Encoding UTF8
 Write-Output ('SETUP_VALIDATION='+($result|ConvertTo-Json -Depth 8 -Compress))
 $global:LASTEXITCODE=0
}finally{if($process -and !$process.HasExited){$process.Kill();$process.WaitForExit()};foreach($c in $canaries){Remove-Item $c.Path -Force -ErrorAction SilentlyContinue}}
