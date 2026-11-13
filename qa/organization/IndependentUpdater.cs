using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LealInfo.Components;

public enum ComponentKind { GLOBAL, PET }
public sealed record Release(ComponentKind Component, string Version, string? Identity, string Sha256, string PackageUrl,
    string? MinGlobalVersion=null, string? MaxGlobalVersionExclusive=null, int PetApiVersion=1);
public sealed record Installed(string GlobalVersion, string PetVersion, string Identity,
    string? PetMinGlobalVersion=null, string? PetMaxGlobalVersionExclusive=null, int PetApiVersion=1);
public sealed record Selection(IReadOnlyList<Release> Available, IReadOnlyList<string> Errors);
public sealed record Staged(Release Release, string Folder, IReadOnlyDictionary<string,string> Hashes);

// Preparation only. Activation requires the external transactional worker.
// This module never writes the application installation or a distribution feed.
public sealed class ClientPolicy
{
    public static ClientPolicy Default { get; }=new(new Dictionary<string,bool>{{"001",true},{"002",true},{"003",true},{"004",false}});
    private readonly IReadOnlyDictionary<string,bool> clients;
    public ClientPolicy(IReadOnlyDictionary<string,bool> values)=>clients=new Dictionary<string,bool>(values);
    public bool IsActive(string identity)=>clients.TryGetValue(identity,out var active)&&active;
    public void AssertActive(string identity){if(!IsActive(identity))throw new InvalidDataException("Cliente inativo ou não cadastrado: atualização/migração bloqueada.");}
    public ClientPolicy WithActivity(string identity,bool active)
    {
        if(!clients.ContainsKey(identity))throw new InvalidDataException("Cliente não cadastrado.");
        var next=clients.ToDictionary(x=>x.Key,x=>x.Value);next[identity]=active;return new(next);
    }
}

public static class IndependentUpdater
{
    public static Selection Plan(Installed installed, Release? global, Release? pet)
    {
        if (!Regex.IsMatch(installed.Identity, @"\A[0-9]{3,9}\z")) throw new InvalidDataException("Invalid installation identity");
        if(!ClientPolicy.Default.IsActive(installed.Identity))return new Selection(Array.Empty<Release>(),new[]{"Cliente inativo ou não cadastrado: nenhuma oferta disponível."});
        var result=new List<Release>();var errors=new List<string>();
        void Check(Release? release,ComponentKind kind,string current)
        {
            if(release is null)return;
            try
            {
                ValidateManifest(release,installed.Identity);
                if(release.Component!=kind)throw new InvalidDataException("Wrong feed component");
                if(release.PetApiVersion!=installed.PetApiVersion)throw new InvalidDataException("Incompatible PET API");
                if(kind==ComponentKind.PET)ValidateCompatibility(installed.GlobalVersion,release.MinGlobalVersion,release.MaxGlobalVersionExclusive);
                else ValidateCompatibility(release.Version,installed.PetMinGlobalVersion,installed.PetMaxGlobalVersionExclusive);
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
        ClientPolicy.Default.AssertActive(identity);
        bool pet = release.Component == ComponentKind.PET;
        if(release.Component is not (ComponentKind.GLOBAL or ComponentKind.PET)) throw new InvalidDataException("Unknown component");
        if(pet ? release.Identity != identity : !string.IsNullOrEmpty(release.Identity))
            throw new InvalidDataException("Wrong component identity");
        if(!Regex.IsMatch(release.Version,pet ? @"\A1\.\d+(?:\.\d+)*\z" : @"\A10\.\d+(?:\.\d+)*\z"))
            throw new InvalidDataException("Wrong component version scheme");
        if(!Regex.IsMatch(release.Sha256,@"\A[0-9a-fA-F]{64}\z")) throw new InvalidDataException("Invalid package hash");
        if(release.PetApiVersion!=1)throw new InvalidDataException("Unsupported PET API");
        ValidateCompatibility("10.465",release.MinGlobalVersion,release.MaxGlobalVersionExclusive,false);
        if(!Uri.TryCreate(release.PackageUrl,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.Host!="github.com"
           || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidDataException("Invalid package URL");
        string prefix="/lealinfopdvnovo/leal-info-pdv-updates/releases/download/"+(pet ? "client-"+identity+"-pet-v" : "v");
        if(!uri.AbsolutePath.StartsWith(prefix,StringComparison.Ordinal) || !uri.AbsolutePath.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Wrong package channel");
    }

    public static void ValidateCompatibility(string global,string? minimum,string? maximum,bool checkVersion=true)
    {
        // Schema 1 remains readable for migration. Schema 2 feeds require both bounds.
        if(minimum is null && maximum is null)return;
        if(minimum is null || maximum is null || !Version.TryParse(minimum,out var min) ||
           !Version.TryParse(maximum,out var max) || min.Major!=10 || max<=min ||
           !Version.TryParse(global,out var current))throw new InvalidDataException("Invalid compatibility range");
        if(checkVersion && (current<min || current>=max))throw new InvalidDataException("GLOBAL outside PET compatibility range");
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
                // The transactional worker activates exactly these two files.
                // Resources/dependencies must be embedded in Pet.dll, never staged then silently ignored.
                if(name is not ("Extensions/PET/manifest.json" or "Extensions/PET/Pet.dll"))
                    throw new InvalidDataException("PET payload must contain only manifest.json and Pet.dll");
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
               root.GetProperty("ApiVersion").GetInt32()!=release.PetApiVersion||assembly!="Pet.dll"||files.Count!=2||
               !files.TryGetValue("Extensions/PET/"+assembly,out var dll)||
               !Convert.ToHexString(SHA256.HashData(dll)).Equals(root.GetProperty("Sha256").GetString(),StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("PET assembly identity or integrity mismatch");
            string? Bound(string name)=>root.TryGetProperty(name,out var p)?p.GetString():null;
            if(Bound("MinGlobalVersion")!=release.MinGlobalVersion || Bound("MaxGlobalVersionExclusive")!=release.MaxGlobalVersionExclusive)
                throw new InvalidDataException("PET package and feed compatibility differ");
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
