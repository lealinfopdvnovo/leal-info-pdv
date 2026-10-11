$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
$root=Join-Path $env:TEMP ('LicencaDiagnosticoQA-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $root | Out-Null
try {
    $inputDir=Join-Path $root 'input';$outDir=Join-Path $root 'output'
    New-Item -ItemType Directory $inputDir,$outDir | Out-Null
    $path=Join-Path $inputDir 'network.license'
    $script=Join-Path $PSScriptRoot 'Diagnosticar_Licenca.ps1'
    function Run-Diagnostic {
        & $script -LicenseFolder $inputDir -OutputDirectory $outDir -NoPause | Out-Null
        return [IO.File]::ReadAllText((Join-Path $outDir 'DIAGNOSTICO_LICENCA.txt'))
    }
    $r=Run-Diagnostic
    if($r -notmatch 'AUSENTE'){throw 'Ausencia nao detectada'}
    [IO.File]::WriteAllBytes($path,[byte[]](1,2,3,4))
    $hash=(Get-FileHash $path).Hash
    $r=Run-Diagnostic
    if($r -notmatch 'FALHA DE LEITURA' -or (Get-FileHash $path).Hash -ne $hash){throw 'Corrompido nao preservado'}
    $payload=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('{"ServerSerial":"FICTICIO","Revision":42}'))
    $envelope=@{Payload=$payload;Signature='SEGREDO-NAO-EXIBIR'} | ConvertTo-Json -Compress
    $state=@{SignedLicense=$envelope;Devices=@(@{Name='PESSOA-NAO-EXIBIR';PublicKey='CHAVE-NAO-EXIBIR'})} | ConvertTo-Json -Compress -Depth 8
    $bytes=[Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($state),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
    [IO.File]::WriteAllBytes($path,$bytes)
    [IO.File]::WriteAllBytes($path+'.previous',$bytes)
    $hash=(Get-FileHash $path).Hash
    $r=Run-Diagnostic
    if($r -notmatch 'LEGIVEIS' -or $r -notmatch 'Revisao: 42' -or $r -notmatch 'Terminais registrados: 1' -or $r -notmatch 'computador: False'){throw 'Estado legivel nao detectado'}
    foreach($secret in @('SEGREDO-NAO-EXIBIR','PESSOA-NAO-EXIBIR','CHAVE-NAO-EXIBIR','FICTICIO',$payload)) {
        if($r.Contains($secret)){throw 'Conteudo sensivel vazou no relatorio'}
    }
    if((Get-FileHash $path).Hash -ne $hash -or (Get-FileHash ($path+'.previous')).Hash -ne $hash){throw 'Diagnostico alterou estado'}
    $blocked=$false
    try { & $script -LicenseFolder $inputDir -OutputDirectory (Join-Path $env:LOCALAPPDATA 'LealInfoPDV') -NoPause }
    catch { $blocked=$true }
    if(-not $blocked){throw 'Gravacao no AppData do PDV nao bloqueada'}
    Write-Host 'PASS: ausente, ilegivel, DPAPI legivel, copia anterior, serial divergente, hashes preservados, dados sensiveis omitidos e escrita no AppData bloqueada.'
} finally { Remove-Item $root -Recurse -Force }
