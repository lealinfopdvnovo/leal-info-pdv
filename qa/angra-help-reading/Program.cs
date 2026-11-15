using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;
using System.Text.Json;
static class Patcher
{
 const string OldHash="3841b67fe8285afca01610a72f9b89f6ca69f41be85d92d05246f160458c85f9";
 const string NewHash="a2b5144645a2412fc21dc18f2685cc78ba50ae5cadbaaea22e24c88074fc9d89";
 static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
 static IEnumerable<TypeDefinition> Types(TypeDefinition t){yield return t;foreach(var n in t.NestedTypes)foreach(var x in Types(n))yield return x;}
 static string MethodSignature(MethodDefinition m){
  if(!m.HasBody)return m.FullName;
  var ins=m.Body.Instructions;
  string Operand(object? o)=>o switch {null=>"",Instruction i=>"IL#"+ins.IndexOf(i),Instruction[] a=>string.Join(',',a.Select(i=>ins.IndexOf(i))),MemberReference x=>x.FullName,VariableDefinition x=>"V"+x.Index,ParameterDefinition x=>"P"+x.Index,_=>o.ToString()??""};
  return string.Join('|',ins.Select(i=>i.OpCode.Code+":"+Operand(i.Operand)));
 }
 static string Metadata(AssemblyDefinition a,string key)=>a.CustomAttributes.Single(x=>x.AttributeType.FullName=="System.Reflection.AssemblyMetadataAttribute"&&(string)x.ConstructorArguments[0].Value==key).ConstructorArguments[1].Value.ToString()!;
 static int Main(string[] args){try{
  if(args.Length!=3)throw new Exception("baseline helper output required");
  var bytes=File.ReadAllBytes(args[0]);if(Hash(bytes)!="c589d8f7843af964b880b12c27e8d76f553ffc06c3ff3ebe466323d283a49e46")throw new Exception("Unapproved PET 1.11 baseline");
  using var assembly=AssemblyDefinition.ReadAssembly(new MemoryStream(bytes));
  if(Metadata(assembly,"PetIdentity")!="005"||Metadata(assembly,"PetVersion")!="1.11"||assembly.Name.HasPublicKey)throw new Exception("Wrong baseline identity/version");
  var methods=assembly.MainModule.Types.SelectMany(Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName,MethodSignature);
  var resources=assembly.MainModule.Resources.OfType<EmbeddedResource>().ToDictionary(r=>r.Name,r=>Hash(r.GetResourceData()));
  const string resource="ANGRA.HelpPremium.Helper";
  var old=assembly.MainModule.Resources.Single(r=>r.Name==resource);int index=assembly.MainModule.Resources.IndexOf(old);
  assembly.MainModule.Resources[index]=new EmbeddedResource(resource,old.Attributes,File.ReadAllBytes(args[1]));
  foreach(var attr in assembly.CustomAttributes){
   if(attr.AttributeType.FullName=="System.Reflection.AssemblyMetadataAttribute"&&(string)attr.ConstructorArguments[0].Value=="PetVersion")attr.ConstructorArguments[1]=new CustomAttributeArgument(assembly.MainModule.TypeSystem.String,"1.12");
   if(attr.AttributeType.FullName is "System.Reflection.AssemblyFileVersionAttribute" or "System.Reflection.AssemblyInformationalVersionAttribute")attr.ConstructorArguments[0]=new CustomAttributeArgument(assembly.MainModule.TypeSystem.String,"1.12.0");
  }
  assembly.Write(args[2]);using var check=AssemblyDefinition.ReadAssembly(args[2]);
  if(Metadata(check,"PetIdentity")!="005"||Metadata(check,"PetVersion")!="1.12")throw new Exception("Metadata mismatch");
  foreach(var method in check.MainModule.Types.SelectMany(Types).SelectMany(t=>t.Methods))if(!methods.TryGetValue(method.FullName,out var before)||MethodSignature(method)!=before)throw new Exception("Original PET method changed: "+method.FullName);
  foreach(var r in check.MainModule.Resources.OfType<EmbeddedResource>())if(r.Name!=resource&&(!resources.TryGetValue(r.Name,out var before)||Hash(r.GetResourceData())!=before))throw new Exception("Unrelated resource changed: "+r.Name);
  Directory.CreateDirectory("timing-evidence");File.WriteAllText("timing-evidence/patch-scope.json",JsonSerializer.Serialize(new{Pass=true,Identity="005",From="1.11",To="1.12",AllOriginalPetMethodsIdentical=true,OnlyResourceChanged=resource,AllOtherOriginalResourcesIdentical=true,MediaSHA256=resources["ANGRA.Opening.mp4"],CoreAndNotifierRecompiled=false}));
  Console.WriteLine("PASS all PET methods and unrelated resources byte-preserved; only existing help resource updated");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
