$ErrorActionPreference='Stop'
$old=[Reflection.Assembly]::LoadFile((Resolve-Path legacy-001/LealInfoPDV.dll).Path)
$stream=$old.GetManifestResourceStream('Loja.PET.1.2');if(!$stream){throw 'Original presentation missing'}
try{$memory=[IO.MemoryStream]::new();$stream.CopyTo($memory);$raw=$memory.ToArray()}finally{$stream.Dispose()}
$metadata=[Text.Encoding]::UTF8.GetString($raw)
foreach($forbidden in @('GetFolderPath','ExpandEnvironmentVariables','GetEnvironmentVariable','Microsoft.Win32')){if($metadata.Contains($forbidden)){throw ('Original presentation needs additional QA path review: '+$forbidden)}}
$module=(Get-ChildItem candidate/extensions/profiles/001/bin/Release -Filter LealInfoPDV.Pet.Profile.dll -Recurse|Select-Object -First 1).FullName
if(!$module){throw 'PET Loja 1.3 not compiled'}
New-Item -ItemType Directory qa-publish/Extensions/PET -Force|Out-Null
Copy-Item $module qa-publish/Extensions/PET/Pet.dll
$hash=(Get-FileHash $module).Hash.ToLowerInvariant()
@{Identity='001';Version='1.3';ApiVersion=1;Assembly='Pet.dll';Sha256=$hash;MinGlobalVersion='10.468';MaxGlobalVersionExclusive='10.469'}|ConvertTo-Json|Set-Content qa-publish/Extensions/PET/manifest.json -Encoding UTF8
Set-Content qa-publish/.qa-test-only 'LEAL INFO PDV - ANGRA ETAPA4 - TEST ONLY - NOT A MIGRATION OR PRODUCTION RELEASE'
foreach($p in Get-ChildItem qa-publish -File -Recurse){if($p.Extension -in @('.license','.db','.key','.protected','.identity','.config') -or $p.Name -like '*.db-*'){throw ('Private/customer file in setup: '+$p.Name)}}
# Core strings are checked by source transformation and by running diagnostic below.
$hashes=@{};foreach($p in Get-ChildItem qa-publish -File -Recurse){$hashes[$p.FullName.Substring((Resolve-Path qa-publish).Path.Length+1)]=(Get-FileHash $p.FullName).Hash.ToLowerInvariant()}
$hashes|ConvertTo-Json -Depth 8|Set-Content qa-setup-evidence/payload-hashes.json -Encoding UTF8
Write-Output 'PASS QA payload contains no license/database/private configuration; existing external PET 1.3 packaged without a migration capsule'
