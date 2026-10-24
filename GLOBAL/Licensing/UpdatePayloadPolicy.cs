namespace LealInfoPDV.Licensing;
internal static class UpdatePayloadPolicy
{
    internal static bool IsProgramFile(string name)
    {
        var path=name.Replace('\\','/');
        if(path.StartsWith('/') || path.Split('/').Any(p=>p is ".." or "." || p.Contains(':')))return false;
        var file=Path.GetFileName(path);
        var extension=Path.GetExtension(file).ToLowerInvariant();
        if(extension is ".pem" or ".db" or ".sqlite" or ".license" or ".config" or ".identity" or ".certificate" or ".protected" or ".leallicenca" or ".lealrede" or ".dat")return false;
        if(path.StartsWith("Assets/",StringComparison.OrdinalIgnoreCase))return extension is ".png" or ".jpg" or ".jpeg" or ".mp4" or ".ico";
        if(extension==".xml" && file.StartsWith("Microsoft.Web.WebView2.",StringComparison.OrdinalIgnoreCase))return true; // Documentação do SDK, sem configurações do cliente.
        return extension is ".exe" or ".dll" or ".pdb" || file.EndsWith(".deps.json",StringComparison.OrdinalIgnoreCase) || file.EndsWith(".runtimeconfig.json",StringComparison.OrdinalIgnoreCase);
    }
    internal static void Validate(string zip)
    {
        using var archive=System.IO.Compression.ZipFile.OpenRead(zip);
        var common=new HashSet<string>(StringComparer.Ordinal){"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"};
        if(archive.Entries.Count!=3 || archive.Entries.Select(e=>e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=3 || archive.Entries.Any(e=>!common.Contains(e.FullName)))
            throw new InvalidDataException("O pacote GLOBAL deve conter somente os três arquivos comuns do programa. PET, dados, licença e configurações devem permanecer intactos.");
    }
}
