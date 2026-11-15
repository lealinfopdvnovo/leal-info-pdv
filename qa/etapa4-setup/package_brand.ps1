$ErrorActionPreference='Stop'
$module=(Get-ChildItem candidate/extensions/profiles/001/bin/Release -Filter LealInfoPDV.Pet.Profile.dll -Recurse|Select-Object -First 1).FullName
New-Item -ItemType Directory qa-patch/Extensions/PET -Force|Out-Null
Copy-Item $module qa-patch/Extensions/PET/Pet.dll
$hash=(Get-FileHash $module).Hash.ToLowerInvariant()
@{Identity='005';Version='1.4';ApiVersion=1;Assembly='Pet.dll';Sha256=$hash;MinGlobalVersion='10.468';MaxGlobalVersionExclusive='10.469'}|ConvertTo-Json|Set-Content qa-patch/Extensions/PET/manifest.json -Encoding UTF8
New-Item -ItemType Directory qa-channel -Force|Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory((Resolve-Path qa-patch).Path,(Join-Path (Resolve-Path qa-channel).Path 'ANGRA_005_PET_1_4.zip'))
$package=(Get-FileHash qa-channel/ANGRA_005_PET_1_4.zip).Hash.ToLowerInvariant()
@{SchemaVersion=2;Component='PET';Version='1.4';TargetClientCode='005';PetApiVersion=1;Sha256=$package;PackageUrl='https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv/qa/angra-etapa4-setup-20261114/qa/etapa4-setup/channel/ANGRA_005_PET_1_4.zip';MinGlobalVersion='10.468';MaxGlobalVersionExclusive='10.469'}|ConvertTo-Json|Set-Content qa-channel/version.json -Encoding UTF8
Write-Output "PASS QA PET 005 / 1.4 package SHA256 $package"
