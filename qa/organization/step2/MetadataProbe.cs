using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
var path=Path.GetFullPath(args[0]);var bytes=File.ReadAllBytes(path);
using var pe=new PEReader(new MemoryStream(bytes));var md=pe.GetMetadataReader();
var types=md.TypeDefinitions.Select(h=>md.GetTypeDefinition(h)).ToArray();
bool Type(string name)=>types.Any(t=>md.GetString(t.Name)==name);
string? current=null;
foreach(var t in types.Where(t=>md.GetString(t.Name)=="UpdateManager"))foreach(var f in t.GetFields().Select(h=>md.GetFieldDefinition(h)))if(md.GetString(f.Name)=="CurrentVersion"&&!f.GetDefaultValue().IsNil){var c=md.GetConstant(f.GetDefaultValue());current=Encoding.Unicode.GetString(md.GetBlobBytes(c.Value));}
var resources=new Dictionary<string,string>();var section=pe.GetSectionData(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress).GetContent().ToArray();
foreach(var h in md.ManifestResources){var r=md.GetManifestResource(h);if(!r.Implementation.IsNil)continue;var off=checked((int)r.Offset);var len=BitConverter.ToInt32(section,off);resources[md.GetString(r.Name)]=Convert.ToHexString(SHA256.HashData(section.AsSpan(off+4,len))).ToLowerInvariant();}
var published=resources.TryGetValue("Loja.PET.1.2",out var pet)&&pet=="054e30f5ed5d40a0b539511f50c4415f346b8ec20cbefb325acf364d0f2acd8f";
var display=bytes.AsSpan().IndexOf(Encoding.Unicode.GetBytes("10.465 • PET Loja V1.3"))>=0;
if(current!="10.467"||!published||!display)throw new Exception("Published version/resource/display metadata mismatch");
Directory.CreateDirectory("step2-evidence");
File.WriteAllText("step2-evidence/original-loader-contract.json",JsonSerializer.Serialize(new{Pass=true,UpdaterVersion=current,DisplayLiteral="10.465 • PET Loja V1.3",EmbeddedLojaPet12Hash=pet,ExternalPetRuntime=Type("PetRuntime"),ExternalIPetExtension=Type("IPetExtension"),LegacyLojaBootstrap=Type("LojaPetBootstrap"),Resources=resources,OperationalStartupQualified=false},new JsonSerializerOptions{WriteIndented=true}));
var worker="TransactionalUpdateWorker";if(resources[worker]!="a760f78df6b2d3d11b037727ca642be8e3d5efef802d0ac6898ea54f9d819968")throw new Exception("Original worker hash differs");
foreach(var h in md.ManifestResources){var r=md.GetManifestResource(h);if(md.GetString(r.Name)!=worker)continue;int off=checked((int)r.Offset),len=BitConverter.ToInt32(section,off);File.WriteAllBytes("step2-work/original-worker.ps1",section.AsSpan(off+4,len).ToArray());}
Console.WriteLine("PASS original 10.467 metadata, display PET 1.3, embedded legacy PET and exact recovery worker");
