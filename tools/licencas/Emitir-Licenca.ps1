# Execute apenas no computador do vendedor. Requer PowerShell 7 (pwsh).
param(
    [Parameter(Mandatory=$true)][string]$Servidor,
    [ValidateRange(2,1000)][int]$Computadores = 2,
    [Parameter(Mandatory=$true)][ValidateSet('unico','mensal')][string]$Modalidade,
    [string]$Validade,
    [string]$Contato = '',
    [string]$Chave = "$PSScriptRoot/chave-vendedor.pem",
    [string]$Saida = "$PSScriptRoot/Licenca_LEAL_INFO_PDV.leallicenca"
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Use PowerShell 7 (pwsh).' }
$expires = $null
if ($Modalidade -eq 'mensal') {
    if ([string]::IsNullOrWhiteSpace($Validade)) { throw 'Informe -Validade em ISO 8601 com fuso, exemplo 2026-11-01T23:59:59-03:00.' }
    $expires = [DateTimeOffset]::Parse($Validade).ToUniversalTime().ToString('o')
    if ([DateTimeOffset]::Parse($expires) -le [DateTimeOffset]::UtcNow) { throw 'Validade deve estar no futuro.' }
} elseif (-not [string]::IsNullOrWhiteSpace($Validade)) { throw 'Pagamento unico nao utiliza validade.' }
$terms = [ordered]@{ ServerSerial=$Servidor;ComputerLimit=$Computadores;BillingMode=$Modalidade;ExpiresUtc=$expires;
    SellerContact=$Contato;Revision=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds();LicenseId=[Guid]::NewGuid().ToString() }
$payload = [Text.Encoding]::UTF8.GetBytes(($terms | ConvertTo-Json -Compress))
# A ponte em C# evita limitações de binding de ReadOnlySpan<char> em versões do PowerShell.
if (-not ('LealLicenseSigner' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Security.Cryptography;
public static class LealLicenseSigner {
    public static byte[] Sign(string pem, byte[] payload) {
        using (RSA rsa = RSA.Create()) {
            rsa.ImportFromPem(pem);
            return rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
    }
}
'@
}
$signature = [LealLicenseSigner]::Sign((Get-Content -LiteralPath $Chave -Raw),$payload)
$signed = @{ Payload=[Convert]::ToBase64String($payload);Signature=[Convert]::ToBase64String($signature) } | ConvertTo-Json
Set-Content -LiteralPath $Saida -Value $signed -Encoding utf8NoBOM
Write-Host "Licenca criada: $Saida"
Write-Host "Computadores totais (incluindo servidor): $Computadores | Modalidade: $Modalidade"
