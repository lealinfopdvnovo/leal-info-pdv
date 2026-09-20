$ErrorActionPreference = 'Stop'
$path = 'UpdateManager.cs'
$source = Get-Content $path -Raw -Encoding UTF8
$source = $source.Replace('10.302','10.303')
Set-Content $path $source -Encoding UTF8
if ((Get-Content $path -Raw) -notmatch 'CurrentVersion="10\.303"') { throw 'UpdateManager nao foi sincronizado para V10.303' }
Write-Host 'UpdateManager sincronizado para V10.303.'
