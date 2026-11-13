using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LealInfo.Components;

// Preparation only: reuse PetBootstrap's capsule and the existing transactional
// applicator. This class never applies an update, edits data, loads client code,
// accepts a licence or promotes a feed. No fallback to the old 10.463 fixture.
public static class InitialMigrationBridge
{
    public const string OriginVersion="10.467", Identity="001", PetVersion="1.3";
    public static readonly IReadOnlyDictionary<string,string> OriginHashes=
        new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(new Dictionary<string,string>{
        ["LealInfoPDV.exe"]="45cada6874ed2450cf3ae5564dfdfc7faabe749213d45126de42ef69d9d7d040",
        ["LealInfoPDV.dll"]="61ec4bdbbcdb7be92cee0fcac421c360cae5e58cd6604b73ef2eaad0717ca0d7",
        ["LealInfoPDV.deps.json"]="73477964d61c59ccf92d0532da4b8c46a18ade13ed0f1f69253b79127424d9e1",
        ["LealInfoPDV.runtimeconfig.json"]="d0eac8fac04c001178ff9406cf1b9636d7db1a4364271e83a24652bbc5916865"});
    public const string LegacyWorkerHash="a760f78df6b2d3d11b037727ca642be8e3d5efef802d0ac6898ea54f9d819968";
    static readonly string[] CoreNames={"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"};
    static readonly byte[] Magic=Encoding.ASCII.GetBytes("LEAL-PET-BOOT-01");
    const int Limit=32*1024*1024;
    public sealed record HostContract(string CurrentVersion, IReadOnlySet<string> Types,
        IReadOnlyDictionary<string,string> Resources);
    public sealed record Plan(string OriginVersion,string TargetVersion,string Identity,string PetVersion,
        string PackageSha256,IReadOnlyDictionary<string,string> Previous,
        IReadOnlyDictionary<string,string> Target,string PetSha256,bool RequiresCoreAndPetRecovery,
        bool OperationallyApproved,bool CanApply);

    static void Require(bool value,string reason){if(!value)throw new InvalidDataException(reason);}
    static string Hash(byte[] value)=>Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    static byte[] Read(Stream source,int limit){
        using var result=new MemoryStream();var buffer=new byte[81920];int count;
        while((count=source.Read(buffer))>0){Require(result.Length+count<=limit,"File exceeds migration bound");result.Write(buffer,0,count);}
        return result.ToArray();
    }
    public static byte[] ReadPlainFile(string path,int limit=Limit){
        path=Path.GetFullPath(path);
        for(var parent=new FileInfo(path).Directory;parent!=null;parent=parent.Parent)
            Require((parent.Attributes&FileAttributes.ReparsePoint)==0,"Redirected migration ancestor");
        var before=new FileInfo(path);
        Require(before.Exists&&(before.Attributes&FileAttributes.ReparsePoint)==0,"Missing or redirected migration file");
        Require(before.Length>0&&before.Length<=limit,"Invalid migration file size");
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var bytes=Read(file,limit);var after=new FileInfo(path);
        Require(bytes.LongLength==before.Length&&after.Length==before.Length&&after.LastWriteTimeUtc==before.LastWriteTimeUtc,"Migration file changed while reading");
        return bytes;
    }
    public static IReadOnlyDictionary<string,string> VerifyOrigin(string folder){
        var actual=new Dictionary<string,string>();
        foreach(var item in OriginHashes){var hash=Hash(ReadPlainFile(Path.Combine(folder,item.Key)));Require(hash==item.Value,"Unidentified origin: "+item.Key);actual.Add(item.Key,hash);}
        return actual;
    }
    // Metadata only. Never Assembly.Load an unqualified destination or PET.
    public static HostContract InspectHost(byte[] bytes){
        using var stream=new MemoryStream(bytes);using var pe=new PEReader(stream);
        Require(pe.HasMetadata&&pe.PEHeaders.CorHeader!=null,"Destination is not a managed host");
        var md=pe.GetMetadataReader();var names=new HashSet<string>();string? version=null;
        foreach(var handle in md.TypeDefinitions){var t=md.GetTypeDefinition(handle);var ns=md.GetString(t.Namespace);var name=md.GetString(t.Name);names.Add(ns+"."+name);
            if(ns=="LealInfoPDV"&&name=="UpdateManager")foreach(var field in t.GetFields()){
                var f=md.GetFieldDefinition(field);if(md.GetString(f.Name)!="CurrentVersion"||f.GetDefaultValue().IsNil)continue;
                var c=md.GetConstant(f.GetDefaultValue());Require(c.TypeCode==ConstantTypeCode.String,"Invalid core version constant");version=Encoding.Unicode.GetString(md.GetBlobBytes(c.Value));
            }
        }
        Require(version!=null,"Missing actual updater version");var resources=new Dictionary<string,string>();
        foreach(var handle in md.ManifestResources){var r=md.GetManifestResource(handle);Require(r.Implementation.IsNil,"External host resource refused");
            var section=pe.GetSectionData(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress);var offset=checked((int)r.Offset);var block=section.GetReader(offset,section.Length-offset);
            int length=block.ReadInt32();Require(length>0&&length<=Limit&&length<=block.RemainingBytes,"Invalid host resource");resources.Add(md.GetString(r.Name),Hash(block.ReadBytes(length)));
        }
        return new(version!,names,resources);
    }
    public static void ValidateTargetVersion(string version){
        Require(System.Text.RegularExpressions.Regex.IsMatch(version??"",@"\A10\.\d+\z")&&Version.TryParse(version,out var parsed)&&parsed>Version.Parse(OriginVersion),"First transition requires a real newer core, never 10.463 or a replay");
    }
    public static void ValidateHostContract(HostContract target){
        ValidateTargetVersion(target.CurrentVersion);
        foreach(var type in new[]{"LealInfoPDV.PetRuntime","LealInfoPDV.PetBootstrap","LealInfoPDV.IPetExtension","LealInfoPDV.IPetStartup","LealInfoPDV.IPetReady","LealInfoPDV.IFinancialMovementsPet"})
            Require(target.Types.Contains(type),"Missing modular contract: "+type);
        Require(target.Resources.ContainsKey("PetTransactionalUpdateWorker"),"Independent PET recovery missing");
    }
    static (string Version,string Sha256) Capsule(byte[] host,string targetVersion){
        Require(host.Length>=120&&host.AsSpan(host.Length-16).SequenceEqual(Magic),"Missing existing bootstrap capsule");
        var trailer=host.AsSpan(host.Length-56);long length=BinaryPrimitives.ReadInt64LittleEndian(trailer);
        Require(length>0&&length<=Limit&&host.Length-56-length>=64,"Invalid bootstrap size");
        var payload=host.AsSpan(checked(host.Length-56-(int)length),(int)length).ToArray();
        Require(SHA256.HashData(payload).AsSpan().SequenceEqual(trailer.Slice(8,32)),"Bootstrap SHA mismatch");
        using var zip=new ZipArchive(new MemoryStream(payload),ZipArchiveMode.Read);
        Require(zip.Entries.Count==2&&zip.Entries.Select(x=>x.FullName).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"manifest.json","Pet.dll"}),"Bootstrap must use existing two-file format");
        byte[] Get(string name,int limit){using var stream=zip.GetEntry(name)!.Open();return Read(stream,limit);}
        var module=Get("Pet.dll",Limit);using var doc=JsonDocument.Parse(Get("manifest.json",16384));var m=doc.RootElement;
        Require(m.GetProperty("Identity").GetString()==Identity&&m.GetProperty("Version").GetString()==PetVersion&&m.GetProperty("Assembly").GetString()=="Pet.dll"&&m.GetProperty("ApiVersion").GetInt32()==1,"Loja 1.3 bootstrap identity/version/API mismatch");
        string hash=Hash(module);Require(hash.Equals(m.GetProperty("Sha256").GetString(),StringComparison.OrdinalIgnoreCase),"Bootstrap module hash mismatch");
        IndependentUpdater.ValidateCompatibility(targetVersion,m.GetProperty("MinGlobalVersion").GetString(),m.GetProperty("MaxGlobalVersionExclusive").GetString());
        // Keep the legacy Loja binary byte-for-byte inside the approved profile wrapper.
        var pet=InspectModuleResources(module);
        Require(pet.Values.Contains("054e30f5ed5d40a0b539511f50c4415f346b8ec20cbefb325acf364d0f2acd8f"),"Original Loja customization not preserved in profile");
        return (PetVersion,hash);
    }
    static IReadOnlyDictionary<string,string> InspectModuleResources(byte[] bytes){
        using var stream=new MemoryStream(bytes);using var pe=new PEReader(stream);Require(pe.HasMetadata,"Invalid profile binary");var md=pe.GetMetadataReader();
        var hashes=new Dictionary<string,string>();foreach(var h in md.ManifestResources){var r=md.GetManifestResource(h);Require(r.Implementation.IsNil,"External profile resources refused");
            var section=pe.GetSectionData(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress);var offset=checked((int)r.Offset);var reader=section.GetReader(offset,section.Length-offset);int n=reader.ReadInt32();Require(n>0&&n<=Limit&&n<=reader.RemainingBytes,"Invalid profile resource");hashes.Add(md.GetString(r.Name),Hash(reader.ReadBytes(n)));
        }return hashes;
    }
    public static Plan Prepare(string origin,string package,string packageSha256){
        // Validate source before staging/creating anything. No runtimeconfig replacement.
        var previous=VerifyOrigin(origin);var bytes=ReadPlainFile(package,128*1024*1024);
        Require(Hash(bytes).Equals(packageSha256,StringComparison.OrdinalIgnoreCase),"Transition package SHA mismatch");
        Require(!Directory.Exists(Path.Combine(origin,"Extensions","PET")),"Existing external PET requires a different transition; never overwrite it");
        using var zip=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);
        Require(zip.Entries.Count==3&&zip.Entries.Select(x=>x.FullName).ToHashSet(StringComparer.Ordinal).SetEquals(CoreNames),"First transition permits only existing three-file worker payload");
        var files=new Dictionary<string,byte[]>();foreach(var entry in zip.Entries){Require(((entry.ExternalAttributes>>16)&0xf000)!=0xa000,"Package link refused");using var stream=entry.Open();files.Add(entry.FullName,Read(stream,Limit));}
        var contract=InspectHost(files["LealInfoPDV.dll"]);ValidateHostContract(contract);
        var capsule=Capsule(files["LealInfoPDV.exe"],contract.CurrentVersion);
        using var deps=JsonDocument.Parse(files["LealInfoPDV.deps.json"]);Require(deps.RootElement.TryGetProperty("targets",out _),"Invalid destination dependency manifest");
        return new(OriginVersion,contract.CurrentVersion,Identity,capsule.Version,Hash(bytes),previous,
            files.ToDictionary(x=>x.Key,x=>Hash(x.Value)),capsule.Sha256,true,false,false);
    }
    public static void RequireTransactionalRecovery(Plan plan){
        // The identified legacy worker backs up/restores only the three core files.
        // A boolean/operator override cannot turn that into a two-component rollback.
        // Stage 3 must supply and test the handoff before this gate can be replaced.
        throw new InvalidOperationException("Migration activation blocked: qualify the existing worker handoff for both core and first external PET, then licensed Windows startup. No update was applied.");
    }

    public static void VerifyPreparedBackup(string app,Plan plan){
        Require(plan.OriginVersion==OriginVersion&&plan.Identity==Identity&&plan.PetVersion==PetVersion,"Wrong initial transition identity");
        Require(plan.Previous.Count==OriginHashes.Count&&OriginHashes.All(x=>plan.Previous.TryGetValue(x.Key,out var value)&&value==x.Value),"Untrusted previous hashes");
        foreach(var name in CoreNames)Require(Hash(ReadPlainFile(Path.Combine(app,".pdv-update","previous",name)))==OriginHashes[name],"Original worker backup mismatch: "+name);
        Require(Hash(ReadPlainFile(Path.Combine(app,"LealInfoPDV.runtimeconfig.json")))==OriginHashes["LealInfoPDV.runtimeconfig.json"],"Runtimeconfig must remain identical");
        using var journal=JsonDocument.Parse(ReadPlainFile(Path.Combine(app,".pdv-update","journal.json"),65536));var j=journal.RootElement;
        Require(j.GetProperty("Phase").GetString()=="Prepared"&&j.GetProperty("Channel").GetString()==Identity&&j.GetProperty("Version").GetString()==plan.TargetVersion,"Wrong initial transaction journal");
        Require(j.GetProperty("PackageSha256").GetString()!.Equals(plan.PackageSha256,StringComparison.OrdinalIgnoreCase),"Wrong prepared package");
        Require(Path.GetFullPath(j.GetProperty("App").GetString()!).TrimEnd(Path.DirectorySeparatorChar)==Path.GetFullPath(app).TrimEnd(Path.DirectorySeparatorChar),"Prepared backup belongs to another installation");
        foreach(var name in CoreNames){
            Require(j.GetProperty("Previous").GetProperty(name).GetString()!.Equals(OriginHashes[name],StringComparison.OrdinalIgnoreCase),"Journal previous hash mismatch");
            Require(j.GetProperty("Target").GetProperty(name).GetString()!.Equals(plan.Target[name],StringComparison.OrdinalIgnoreCase),"Journal target hash mismatch");
            Require(Hash(ReadPlainFile(Path.Combine(app,".pdv-update","stage",name)))==plan.Target[name],"Prepared target differs from plan");
        }
    }
    public static void VerifyCoreRollback(string app){
        VerifyOrigin(app);
        Require(!Directory.Exists(Path.Combine(app,"Extensions","PET")),"Core restored but first external PET remains: rollback incomplete");
    }
}
