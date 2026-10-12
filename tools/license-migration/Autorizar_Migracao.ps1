param(
    [Parameter(Mandatory=$true)][string]$Solicitacao,
    [Parameter(Mandatory=$true)][string]$ChavePrivada,
    [Parameter(Mandatory=$true)][string]$Saida,
    [Parameter(Mandatory=$true)][switch]$ConfirmoMesmoComputador,
    [switch]$ConfirmoRevisaoAtual,
    [string]$CheckpointAutorizado,
    [switch]$ConfirmoCheckpointAtual
)
# Executar SOMENTE no computador do vendedor, PowerShell 7. Sem internet.
$ErrorActionPreference='Stop'
if(-not $ConfirmoMesmoComputador -or -not $ConfirmoRevisaoAtual){throw 'O vendedor precisa confirmar o hardware e a revisao atual antes de autorizar.'}
if($PSVersionTable.PSVersion.Major -lt 7){throw 'PowerShell 7 necessario no computador do vendedor.'}
$r=Get-Content -Raw -LiteralPath $Solicitacao | ConvertFrom-Json
if($r.Version -ne 1 -or $r.Kind -ne 'LEAL-LICENSE-MIGRATION-REQUEST' -or $r.ToHardwareSerial -notmatch '^LH-[A-F0-9]{4}(-[A-F0-9]{4}){3}$'){throw 'Solicitacao invalida.'}
if(-not ('LealMigrationSigner' -as [type])) {
Add-Type -TypeDefinition @'
using System.Security.Cryptography;
public static class LealMigrationSigner {
    public static RSA Load(string pem) { var rsa=RSA.Create();rsa.ImportFromPem(pem);return rsa; }
}
'@
}
$rsa=[LealMigrationSigner]::Load([IO.File]::ReadAllText((Resolve-Path -LiteralPath $ChavePrivada)))
try {
    $envelope=$r.SignedLicense | ConvertFrom-Json
    $payload=[Convert]::FromBase64String($envelope.Payload)
    if(-not $rsa.VerifyData($payload,[Convert]::FromBase64String($envelope.Signature),[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)){throw 'Licenca original nao foi assinada por esta chave.'}
    $terms=[Text.Encoding]::UTF8.GetString($payload) | ConvertFrom-Json
    $digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($envelope.Payload+'.'+$envelope.Signature)))
    if($digest -cne $r.LicenseDigest -or $terms.ServerSerial -cne $r.FromSerial -or $terms.ClientCode -cne $r.ClientCode -or $terms.LicenseId -cne $r.LicenseId -or $terms.Revision -ne $r.Revision -or -not $terms.Active){throw 'Licenca/revisao divergente ou inativa.'}
    if($terms.ExpiresUtc -and [DateTimeOffset]::Parse($terms.ExpiresUtc) -le [DateTimeOffset]::UtcNow){throw 'Licenca vencida.'}
    if($CheckpointAutorizado){
        if(-not $ConfirmoCheckpointAtual){throw 'Checkpoint externo exige confirmacao do cadastro atual pelo vendedor.'}
        $backup=Get-Content -Raw -LiteralPath $CheckpointAutorizado | ConvertFrom-Json
        $backupBytes=[Convert]::FromBase64String($backup.Payload)
        if(-not $rsa.VerifyData($backupBytes,[Convert]::FromBase64String($backup.Signature),[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)){throw 'Checkpoint externo nao foi assinado pelo emissor.'}
        $approvedBackup=[Text.Encoding]::UTF8.GetString($backupBytes) | ConvertFrom-Json
        if($approvedBackup.Kind -ne 'LEAL-LICENSE-MIGRATION-AUTHORIZATION' -or $approvedBackup.LicenseDigest -cne $r.LicenseDigest -or $approvedBackup.ToHardwareSerial -cne $r.ToHardwareSerial -or -not $approvedBackup.Snapshot){throw 'Checkpoint externo nao pertence a esta licenca/hardware.'}
        $r.Snapshot=$approvedBackup.Snapshot
        $r.StateAvailability='CHECKPOINT_ASSINADO_CONFIRMADO_PELO_VENDEDOR'
    }
    $total=if($null -ne $terms.AdditionalTerminals){2+[int]$terms.AdditionalTerminals}else{[Math]::Max(2,[int]$terms.ComputerLimit)}
    if($r.Snapshot){
        $checkpoint=$r.Snapshot.SignedLicense | ConvertFrom-Json
        $checkpointDigest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($checkpoint.Payload+'.'+$checkpoint.Signature)))
        if($checkpointDigest -cne $digest -or @($r.Snapshot.Devices).Count+1 -gt $total){throw 'Checkpoint incompatível.'}
    } elseif($terms.BillingMode -ne 'unico') {throw 'Sem checkpoint, mensalidade nao pode ser recuperada.'}
    $r.Kind='LEAL-LICENSE-MIGRATION-AUTHORIZATION'
    $bytes=[Text.Encoding]::UTF8.GetBytes(($r | ConvertTo-Json -Depth 20 -Compress))
    $signature=$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)
    $signed=@{Payload=[Convert]::ToBase64String($bytes);Signature=[Convert]::ToBase64String($signature)} | ConvertTo-Json -Compress
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($Saida),$signed,[Text.UTF8Encoding]::new($false))
    Write-Host 'Autorizacao criada. Nenhuma compra/ponto foi consumido. Chave privada nao foi incluida.'
    if(-not $r.Snapshot){Write-Host 'SEM CHECKPOINT: so uso local permanente; cadastro de terminais permanece bloqueado.'}
} finally {$rsa.Dispose()}
