$ErrorActionPreference = 'Stop'
$path = 'UpdateManager.cs'
$source = Get-Content $path -Raw -Encoding UTF8
$source = $source.Replace('10.303','10.304')
Set-Content $path $source -Encoding UTF8
if ((Get-Content $path -Raw) -notmatch 'CurrentVersion="10\\.304"') { throw 'UpdateManager nao foi sincronizado para V10.304' }
Write-Host 'UpdateManager sincronizado para V10.304.'
