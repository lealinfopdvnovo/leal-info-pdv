$ErrorActionPreference='Stop'
$id=$env:INVENTORY_ID
$path=(Resolve-Path "inventory-packages/$id/LealInfoPDV.dll").Path
$a=[Reflection.Assembly]::LoadFile($path)
$resources=@()
foreach($name in $a.GetManifestResourceNames()){
 $s=$a.GetManifestResourceStream($name)
 try{
  $m=[IO.MemoryStream]::new();$s.CopyTo($m)
  $b=$m.ToArray()
  $resources+=@{Name=$name;Bytes=$b.Length;Sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($b))}
 }finally{$s.Dispose()}
}
@{Identity=$id;AssemblyVersion=$a.GetName().Version.ToString();Resources=$resources}|ConvertTo-Json -Depth 20 -Compress
