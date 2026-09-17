$ErrorActionPreference = 'Stop'
$path = 'LIC-AI\Security\LocalSecretStore.cs'
if (!(Test-Path $path)) { throw 'LocalSecretStore legado nao encontrado.' }
$c = Get-Content $path -Raw

# A chave atual da LIA ja e fornecida pelo PDV/ambiente. A protecao DPAPI abaixo e apenas
# compatibilidade para settings.dat legado. Em alguns hosts single-file, ProtectedData pode
# lancar PlatformNotSupportedException/NotSupportedException. Nao exibir erro nem interromper a LIA.
$oldGet = @'
            var encrypted = File.ReadAllBytes(_path);
            var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
'@
$newGet = @'
            var encrypted = File.ReadAllBytes(_path);
            try
            {
                var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (PlatformNotSupportedException) { return null; }
            catch (NotSupportedException) { return null; }
'@
if (-not $c.Contains($oldGet)) { throw 'Bloco legado de leitura DPAPI nao encontrado.' }
$c = $c.Replace($oldGet, $newGet)

$oldSave = @'
        var plain = Encoding.UTF8.GetBytes(apiKey);
        var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, encrypted);
'@
$newSave = @'
        var plain = Encoding.UTF8.GetBytes(apiKey);
        try
        {
            var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_path, encrypted);
        }
        catch (PlatformNotSupportedException) { return; }
        catch (NotSupportedException) { return; }
'@
if (-not $c.Contains($oldSave)) { throw 'Bloco legado de gravacao DPAPI nao encontrado.' }
$c = $c.Replace($oldSave, $newSave)
Set-Content $path $c -Encoding UTF8
Write-Host 'Protecao legada: falha de DPAPI nao interrompe nem gera janelas repetidas na LIA.'
