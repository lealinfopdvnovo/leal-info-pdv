$ErrorActionPreference='Stop'
# The new destination must retain the notifier shipped with the IDENTIFIED origin,
# not the older common fixture. No notifier is executed here.
$a=[Reflection.Assembly]::LoadFile((Resolve-Path legacy-001/LealInfoPDV.dll).Path)
$stream=$a.GetManifestResourceStream('LealInfoPDV.UpdateNotifier.Native')
if(!$stream){throw 'Identified origin notifier absent'}
try{$memory=[IO.MemoryStream]::new();$stream.CopyTo($memory);$bytes=$memory.ToArray()}finally{$stream.Dispose()}
if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant() -ne '248db1ac05dac2ae3b09df626935a700336c2ce67901cd069739afa200d2a457'){throw 'Identified notifier hash differs'}
[IO.File]::WriteAllBytes((Join-Path (Resolve-Path candidate/current-embedded).Path 'Notifier.exe'),$bytes)
Write-Output 'PASS identified 10.467 notifier retained byte for byte in the new candidate'
