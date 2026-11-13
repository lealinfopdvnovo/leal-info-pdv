using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LealInfo.Components;

public enum ComponentKind { GLOBAL, PET }
public sealed record Release(ComponentKind Component, string Version, string? Identity, string Sha256, string PackageUrl);
public sealed record Installed(string GlobalVersion, string PetVersion, string Identity);
public sealed record Selection(IReadOnlyList<Release> Available, IReadOnlyList<string> Errors);
public sealed record Staged(Release Release, string Folder, IReadOnlyDictionary<string,string> Hashes);

// Preparation only. Activation requires the external transactional worker.
// This module never writes the application installation or a distribution feed.
public static class IndependentUpdater
{
    public static Selection Plan(Installed installed, Release? global, Release? pet)
    {
        if (!Regex.IsMatch(installed.Identity, @"\A[0-9]{3,9}\z")) throw new InvalidDataException("Invalid installation identity");
        var result=new List<Release>();var errors=new List<string>();
        void Check(Release? release,ComponentKind kind,string current)
        {
            if(release is null)return;
            try
            {
                ValidateManifest(release,installed.Identity);
                if(release.Component!=kind)throw new InvalidDataException("Wrong feed component");
                if(Version.Parse(release.Version)>Version.Parse(current))result.Add(release);
            }
            catch(Exception error) when(error is InvalidDataException or FormatException or ArgumentException or OverflowException)
            {errors.Add(kind+": "+error.Message);}
        }
        Check(global,ComponentKind.GLOBAL,installed.GlobalVersion);
        Check(pet,ComponentKind.PET,installed.PetVersion);
        return new Selection(result,errors);
    }

    public static void ValidateManifest(Release release, string identity)
    {
        bool pet = release.Component == ComponentKind.PET;
        if(release.Component is not (ComponentKind.GLOBAL or ComponentKind.PET)) throw new InvalidDataException("Unknown component");
        if(pet ? release.Identity != identity : !string.IsNullOrEmpty(release.Identity))
            throw new InvalidDataException("Wrong component identity");
        if(!Regex.IsMatch(release.Version,pet ? @"\A1\.\d+(?:\.\d+)*\z" : @"\A10\.\d+(?:\.\d+)*\z"))
            throw new InvalidDataException("Wrong component version scheme");
        if(!Regex.IsMatch(release.Sha256,@"\A[0-9a-fA-F]{64}\z")) throw new InvalidDataException("Invalid package hash");
        if(!Uri.TryCreate(release.PackageUrl,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.Host!="github.com"
           || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidDataException("Invalid package URL");
        string prefix="/lealinfopdvnovo/leal-info-pdv-updates/releases/download/"+(pet ? "client-"+identity+"-pet-v" : "v");
        if(!uri.AbsolutePath.StartsWith(prefix,StringComparison.Ordinal) || !uri.AbsolutePath.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Wrong package channel");
    }

    public static async Task<Staged> StageAsync(Release release,string identity,string stagingRoot,
        Func<Uri,CancellationToken,Task<byte[]>> download,CancellationToken cancellation=default)
    {
        ValidateManifest(release,identity);
        byte[] bytes=await download(new Uri(release.PackageUrl),cancellation);
        if(bytes.Length>128*1024*1024 || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(release.Sha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Package integrity mismatch");
        using var zip=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
        long size=0;
        foreach(var entry in zip.Entries)
        {
            string name=entry.FullName;
            if(string.IsNullOrEmpty(name)||name.Split('/').Any(p=>p is "" or "." or "..")||
               name.Contains('\\')||name.Contains(':')||name.StartsWith('/')||
               name.Any(c=>c<32)||name.Any(c=>"<>\"|?*".Contains(c))||
               name.Split('/').Any(p=>p.EndsWith(' ')||p.EndsWith('.'))||
               !names.Add(name)) throw new InvalidDataException("Unsafe or duplicate package path");
            if((entry.ExternalAttributes>>16 & 0xF000)==0xA000) throw new InvalidDataException("Symbolic link rejected");
            if(entry.Length<=0 || entry.Length>64*1024*1024 || (size+=entry.Length)>128*1024*1024)
                throw new InvalidDataException("Package size exceeded");
            if(release.Component==ComponentKind.GLOBAL)
            {
                if(name is not ("LealInfoPDV.exe" or "LealInfoPDV.dll" or "LealInfoPDV.deps.json"))
                    throw new InvalidDataException("GLOBAL cannot write PET or data");
            }
            else
            {
                if(!name.StartsWith("Extensions/PET/",StringComparison.Ordinal)) throw new InvalidDataException("PET cannot write base files");
                string leaf=Path.GetFileName(name);
                if(!Regex.IsMatch(leaf,@"\A[A-Za-z0-9_.-]+\z") ||
                   !(name=="Extensions/PET/manifest.json" || new[]{".dll",".png",".jpg",".jpeg"}.Contains(Path.GetExtension(name).ToLowerInvariant())))
                    throw new InvalidDataException("Forbidden PET payload");
            }
            using var stream=entry.Open();using var output=new MemoryStream();
            byte[] buffer=new byte[81920];int count;
            while((count=await stream.ReadAsync(buffer,cancellation))>0)
            {
                if(output.Length+count>entry.Length) throw new InvalidDataException("Decompressed size mismatch");
                output.Write(buffer,0,count);
            }
            if(output.Length!=entry.Length)throw new InvalidDataException("Incomplete entry");
            files.Add(name,output.ToArray());
        }
        if(release.Component==ComponentKind.GLOBAL && !names.SetEquals(new[]{"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"}))
            throw new InvalidDataException("Incomplete GLOBAL");
        if(release.Component==ComponentKind.PET)
        {
            if(!files.TryGetValue("Extensions/PET/manifest.json",out var manifest))throw new InvalidDataException("Missing PET manifest");
            using var json=JsonDocument.Parse(manifest);var root=json.RootElement;
            string assembly=root.GetProperty("Assembly").GetString()??"";
            if(root.GetProperty("Identity").GetString()!=identity||root.GetProperty("Version").GetString()!=release.Version||
               root.GetProperty("ApiVersion").GetInt32()!=1||assembly!=Path.GetFileName(assembly)||
               !files.TryGetValue("Extensions/PET/"+assembly,out var dll)||
               !Convert.ToHexString(SHA256.HashData(dll)).Equals(root.GetProperty("Sha256").GetString(),StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("PET assembly identity or integrity mismatch");
        }
        string full=Path.GetFullPath(stagingRoot);
        for(var parent=new DirectoryInfo(full);parent is not null;parent=parent.Parent)
            if(parent.Exists && (parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Staging path redirected");
        Directory.CreateDirectory(full);
        string target=Path.Combine(full,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(target);
        try
        {
            var hashes=new Dictionary<string,string>();
            foreach(var pair in files)
            {
                cancellation.ThrowIfCancellationRequested();
                string path=Path.Combine(target,pair.Key.Replace('/',Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {await file.WriteAsync(pair.Value,cancellation);file.Flush(true);}
                hashes[pair.Key]=Convert.ToHexString(SHA256.HashData(pair.Value));
            }
            return new Staged(release,target,hashes);
        }
        catch { Directory.Delete(target,true);throw; }
    }
}
