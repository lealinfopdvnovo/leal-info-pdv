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
        return extension is ".exe" or ".dll" or ".pdb" || file.EndsWith(".deps.json",StringComparison.OrdinalIgnoreCase) || file.EndsWith(".runtimeconfig.json",StringComparison.OrdinalIgnoreCase);
    }
    internal static void Validate(string zip)
    {
        using var archive=System.IO.Compression.ZipFile.OpenRead(zip);
        if(!archive.Entries.Any(e=>e.FullName.Equals("LealInfoPDV.exe",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Pacote sem o executável do PDV.");
        foreach(var entry in archive.Entries.Where(e=>e.Name.Length>0))if(!IsProgramFile(entry.FullName))throw new InvalidDataException("O pacote contém arquivos que não pertencem ao programa. Atualização cancelada para preservar os dados locais.");
    }
}
