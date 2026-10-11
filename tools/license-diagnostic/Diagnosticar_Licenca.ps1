param(
    [string]$LicenseFolder = (Join-Path $env:LOCALAPPDATA 'LealInfoPDV'),
    [string]$OutputDirectory = $PSScriptRoot,
    [switch]$NoPause
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$production = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'LealInfoPDV')).TrimEnd('\')
$output = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if ($output.Equals($production, [StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($production + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Extraia o diagnostico fora da pasta de dados do PDV. Nenhum arquivo foi alterado.'
}
if (-not [IO.Directory]::Exists($output)) { throw 'A pasta do diagnostico precisa existir.' }
function Hash-Bytes([byte[]]$bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '') }
    finally { $sha.Dispose() }
}
$lines = New-Object 'System.Collections.Generic.List[string]'
$lines.Add('DIAGNOSTICO SOMENTE LEITURA - LICENCA LEAL INFO PDV')
$lines.Add('Data UTC: ' + [DateTime]::UtcNow.ToString('o'))
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$lines.Add('Identificador anonimizado do usuario Windows: ' + (Hash-Bytes ([Text.Encoding]::UTF8.GetBytes($identity.User.Value))).Substring(0,16))
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$lines.Add('Processo elevado/administrador: ' + $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
$machineSource = [Environment]::MachineName
try {
    $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('SOFTWARE\Microsoft\Cryptography')
    if ($null -ne $key) {
        try { $guid = $key.GetValue('MachineGuid'); if ($guid) { $machineSource = [string]$guid } }
        finally { $key.Dispose() }
    }
} catch { }
$hash = Hash-Bytes ([Text.Encoding]::UTF8.GetBytes('LEALINFO|' + $machineSource))
$serial = 'LI-' + $hash.Substring(0,4) + '-' + $hash.Substring(4,4) + '-' + $hash.Substring(8,4) + '-' + $hash.Substring(12,4)
foreach ($name in @('network.license', 'network.license.previous')) {
    $lines.Add(''); $lines.Add('Arquivo: ' + $name)
    $path = Join-Path $LicenseFolder $name
    if (-not [IO.File]::Exists($path)) { $lines.Add('Estado: AUSENTE'); continue }
    $bytes = [IO.File]::ReadAllBytes($path)
    $before = Hash-Bytes $bytes
    $lines.Add('Tamanho: ' + $bytes.Length)
    $lines.Add('SHA-256: ' + $before)
    $lines.Add('Ultima gravacao UTC: ' + [IO.File]::GetLastWriteTimeUtc($path).ToString('o'))
    $plain = $null
    try {
        $plain = [Security.Cryptography.ProtectedData]::Unprotect($bytes, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        $state = [Text.Encoding]::UTF8.GetString($plain) | ConvertFrom-Json
        if (-not $state.SignedLicense) { $lines.Add('Estado: SEM LICENCA ASSINADA'); continue }
        $envelope = $state.SignedLicense | ConvertFrom-Json
        $terms = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($envelope.Payload)) | ConvertFrom-Json
        $lines.Add('DPAPI e JSON: LEGIVEIS')
        $lines.Add('Serial corresponde ao computador: ' + ($terms.ServerSerial -ceq $serial))
        $lines.Add('Revisao: ' + [long]$terms.Revision)
        $lines.Add('Terminais registrados: ' + @($state.Devices).Count)
        $lines.Add('Importante: leitura NAO equivale a validacao da assinatura ou autorizacao de uso.')
    } catch {
        $errorType = $_.Exception.GetType().FullName
        $lines.Add('Estado: FALHA DE LEITURA')
        $lines.Add('Tipo tecnico: ' + $errorType)
        $lines.Add('HRESULT: ' + $_.Exception.HResult)
        # Não registrar mensagem, JSON, payload, assinatura ou dados pessoais.
    } finally {
        if ($null -ne $plain) { [Array]::Clear($plain, 0, $plain.Length) }
        $state = $null; $envelope = $null; $terms = $null
        $after = Hash-Bytes ([IO.File]::ReadAllBytes($path))
        $lines.Add('Hash permaneceu igual durante a leitura: ' + ($before -ceq $after))
    }
}
$lines.Add(''); $lines.Add('Nenhuma gravacao na instalacao, banco, licenca ou configuracao foi realizada.')
$report = Join-Path $output 'DIAGNOSTICO_LICENCA.txt'
[IO.File]::WriteAllLines($report, $lines, (New-Object Text.UTF8Encoding($false)))
$lines | ForEach-Object { Write-Host $_ }
Write-Host ''; Write-Host 'Envie apenas DIAGNOSTICO_LICENCA.txt. Nao envie a chave nem o arquivo de licenca.'
if (-not $NoPause) { [void](Read-Host 'Pressione ENTER para fechar') }
