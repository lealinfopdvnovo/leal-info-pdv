$ErrorActionPreference='Stop'
foreach($entry in @(@('001','Loja.PET.1.2','054E30F5ED5D40A0B539511F50C4415F346B8EC20CBEFB325ACF364D0F2ACD8F'),@('002','Gelamaq.PET.payload','887AD549A9764770FC2E8924B13FF9D56E96E5511BF90A2647670C6830987034'))){
 $id=$entry[0];$a=[Reflection.Assembly]::LoadFile((Resolve-Path "legacy-$id/LealInfoPDV.dll").Path)
 $stream=$a.GetManifestResourceStream($entry[1]);if(!$stream){throw 'Original module missing'}
 try{$m=[IO.MemoryStream]::new();$stream.CopyTo($m);$bytes=$m.ToArray()}finally{$stream.Dispose()}
 if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)) -ne $entry[2]){throw 'Original module changed'}
 [IO.File]::WriteAllBytes((Join-Path (Resolve-Path "candidate/extensions/profiles/$id").Path 'PreservedPresentation.bin'),$bytes)
}
Write-Output 'PASS original Loja and Gelamaq customization binaries preserved without recompiling their implementation'
