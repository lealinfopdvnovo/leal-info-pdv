using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
static class LegacyModuleContracts
{
 static string TypeName(MetadataReader reader,EntityHandle handle)=>handle.Kind switch{
  HandleKind.TypeReference=>ReferenceName(reader,(TypeReferenceHandle)handle),
  HandleKind.TypeDefinition=>DefinitionName(reader,(TypeDefinitionHandle)handle),
  _=>"<"+handle.Kind+">"
 };
 static string ReferenceName(MetadataReader reader,TypeReferenceHandle handle){
  var t=reader.GetTypeReference(handle);var name=reader.GetString(t.Name);var ns=reader.GetString(t.Namespace);
  return t.ResolutionScope.Kind==HandleKind.TypeReference?ReferenceName(reader,(TypeReferenceHandle)t.ResolutionScope)+"+"+name:(ns.Length==0?name:ns+"."+name);
 }
 static string DefinitionName(MetadataReader reader,TypeDefinitionHandle handle){
  var t=reader.GetTypeDefinition(handle);var parent=t.GetDeclaringType();var name=reader.GetString(t.Name);var ns=reader.GetString(t.Namespace);
  return parent.IsNil?(ns.Length==0?name:ns+"."+name):DefinitionName(reader,parent)+"+"+name;
 }
 static string Scope(MetadataReader reader,EntityHandle handle)=>handle.Kind switch{
  HandleKind.AssemblyReference=>reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)handle).Name),
  HandleKind.TypeReference=>Scope(reader,reader.GetTypeReference((TypeReferenceHandle)handle).ResolutionScope),
  _=>""
 };
 static byte[] Resource(string file,string name){
  using var stream=File.OpenRead(file);using var pe=new PEReader(stream);var reader=pe.GetMetadataReader();
  foreach(var handle in reader.ManifestResources){
   var resource=reader.GetManifestResource(handle);if(reader.GetString(resource.Name)!=name)continue;
   if(!resource.Implementation.IsNil)throw new InvalidDataException("External resource refused");
   var data=pe.GetSectionData(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress).GetReader(checked((int)resource.Offset),pe.PEHeaders.CorHeader.ResourcesDirectory.Size-checked((int)resource.Offset));
   int length=data.ReadInt32();if(length<=0||length>32*1024*1024||length>data.RemainingBytes)throw new InvalidDataException("Invalid module size");
   return data.ReadBytes(length);
  }throw new InvalidDataException("Missing expected legacy module "+name);
 }
 static int Main(string[] args){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||args.Length!=1)return 91;
  using var file=File.OpenRead(args[0]);using var commonPe=new PEReader(file);var common=commonPe.GetMetadataReader();
  var commonNames=common.TypeDefinitions.Select(h=>DefinitionName(common,h)).ToHashSet(StringComparer.Ordinal);
  var results=new List<object>();bool compatible=true;
  foreach(var item in new[]{
   (Id:"001",Resource:"Loja.PET.1.2",Sha:"054E30F5ED5D40A0B539511F50C4415F346B8EC20CBEFB325ACF364D0F2ACD8F"),
   (Id:"002",Resource:"Gelamaq.PET.payload",Sha:"887AD549A9764770FC2E8924B13FF9D56E96E5511BF90A2647670C6830987034")
  }){
   var bytes=Resource("legacy-"+item.Id+"/LealInfoPDV.dll",item.Resource);
   var digest=Convert.ToHexString(SHA256.HashData(bytes));if(digest!=item.Sha)throw new InvalidDataException("Published module hash changed");
   using var pe=new PEReader(new MemoryStream(bytes));var reader=pe.GetMetadataReader();
   var coreRefs=reader.TypeReferences.Where(h=>Scope(reader,reader.GetTypeReference(h).ResolutionScope)=="LealInfoPDV").Select(h=>ReferenceName(reader,h)).Distinct().Order().ToArray();
   var missing=coreRefs.Where(name=>!commonNames.Contains(name)).ToArray();compatible&=missing.Length==0;
   var types=reader.TypeDefinitions.Where(h=>(reader.GetTypeDefinition(h).Attributes&TypeAttributes.VisibilityMask)==TypeAttributes.Public).Select(h=>{
    var type=reader.GetTypeDefinition(h);
    return new{Name=DefinitionName(reader,h),Interfaces=type.GetInterfaceImplementations().Select(i=>TypeName(reader,reader.GetInterfaceImplementation(i).Interface)).ToArray(),Methods=type.GetMethods().Where(m=>(reader.GetMethodDefinition(m).Attributes&MethodAttributes.MemberAccessMask)==MethodAttributes.Public).Select(m=>reader.GetString(reader.GetMethodDefinition(m).Name)).ToArray()};
   }).ToArray();
   var dir=Path.Combine("legacy-components",item.Id);Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"Pet-original.dll"),bytes);
   results.Add(new{Identity=item.Id,Resource=item.Resource,Sha256=digest,CoreTypeContractCompatible=missing.Length==0,CoreReferences=coreRefs,MissingCoreTypes=missing,Types=types,Executed=false,Published=false});
   Console.WriteLine("PASS exact legacy PET resource preserved for "+item.Id+"; missing common API types: "+missing.Length);
  }
  Directory.CreateDirectory("pair-evidence");File.WriteAllText("pair-evidence/legacy-pet-contracts.json",JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
  return compatible?0:1;
 }
}
