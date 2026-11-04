$ErrorActionPreference='Stop'
$a=[Reflection.Assembly]::LoadFile((Resolve-Path legacy-001/LealInfoPDV.dll).Path)
foreach($entry in @(@('TransactionalUpdateWorker','legacy-001-worker.ps1','A760F78DF6B2D3D11B037727CA642BE8E3D5EFEF802D0AC6898EA54F9D819968'),@('PET.CertificationSeal.001','approved-seal-001.png','8B2C8DAC49D85710DBD4D80137D3A0A66DF89F987353BC10451ADF8C18525542'))){
 $stream=$a.GetManifestResourceStream($entry[0]);if(!$stream){throw 'Required published resource missing'}
 try{$memory=[IO.MemoryStream]::new();$stream.CopyTo($memory);$bytes=$memory.ToArray()}finally{$stream.Dispose()}
 if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)) -ne $entry[2]){throw 'Published resource changed'}
 [IO.File]::WriteAllBytes((Join-Path (Resolve-Path pair-evidence).Path $entry[1]),$bytes)
}
Write-Output 'PASS exact published Loja worker and approved seal extracted for migration compatibility; no execution or production change'
