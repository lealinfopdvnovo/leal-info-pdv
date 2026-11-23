$ErrorActionPreference='Stop'
$a=[Reflection.Assembly]::LoadFile((Resolve-Path legacy-001/LealInfoPDV.dll).Path)
$stream=$a.GetManifestResourceStream('Loja.PET.1.2');if(!$stream){throw 'Original Loja customization absent'}
try{$m=[IO.MemoryStream]::new();$stream.CopyTo($m);$bytes=$m.ToArray()}finally{$stream.Dispose()}
if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)) -ne '054E30F5ED5D40A0B539511F50C4415F346B8EC20CBEFB325ACF364D0F2ACD8F'){throw 'Original Loja customization hash mismatch'}
[IO.File]::WriteAllBytes((Join-Path (Resolve-Path 'candidate/extensions/profiles/001').Path 'PreservedPresentation.bin'),$bytes)
