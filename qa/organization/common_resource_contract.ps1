param([ValidateSet('Extract','Verify')][string]$Mode='Extract')
$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true'){throw 'Isolated runner only'}
$path=if($Mode -eq 'Extract'){(Resolve-Path published-current/LealInfoPDV.dll).Path}else{(Get-ChildItem candidate/bin/Release -Filter LealInfoPDV.dll -Recurse|Select-Object -First 1).FullName}
$a=[Reflection.Assembly]::LoadFile($path)
$resources=@{}
foreach($name in $a.GetManifestResourceNames()){
 $stream=$a.GetManifestResourceStream($name)
 try{$memory=[IO.MemoryStream]::new();$stream.CopyTo($memory);$bytes=$memory.ToArray()}finally{$stream.Dispose()}
 $resources[$name]=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
 if($Mode -eq 'Extract' -and $name -eq 'LealInfoPDV.UpdateNotifier.Native'){
  New-Item -ItemType Directory candidate/current-embedded -Force|Out-Null
  [IO.File]::WriteAllBytes((Join-Path (Resolve-Path candidate/current-embedded).Path 'Notifier.exe'),$bytes)
 }
}
if($Mode -eq 'Extract'){
 if(!$resources.ContainsKey('LealInfoPDV.UpdateNotifier.Native')){throw 'Published common notifier missing'}
 $resources|ConvertTo-Json -Depth 8|Set-Content pair-evidence/common-resource-original.json -Encoding UTF8
}else{
 $original=Get-Content pair-evidence/common-resource-original.json -Raw|ConvertFrom-Json
 foreach($entry in $original.PSObject.Properties){
  if(!$resources.ContainsKey($entry.Name) -or $resources[$entry.Name] -ne $entry.Value){throw ('Current common embedded resource changed/lost: '+$entry.Name)}
 }
 if(!$resources.ContainsKey('PetTransactionalUpdateWorker')){throw 'Independent PET recovery resource missing'}
 @{Pass=$true;ExistingResourcesPreserved=$original.PSObject.Properties.Name;AddedResource='PetTransactionalUpdateWorker';Published=$false}|ConvertTo-Json -Depth 8|Set-Content pair-evidence/common-resource-contract.json -Encoding UTF8
 Write-Output 'PASS every published common embedded resource preserved byte for byte; PET worker added independently'
}
