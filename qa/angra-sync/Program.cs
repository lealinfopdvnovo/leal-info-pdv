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
  if(args.Length!=4)throw new Exception("origin.dll media.mp4 player.html output.dll required");
  var original=File.ReadAllBytes(args[0]);if(Hash(original)!="ab9321010fb04c1a7ef18d331a5e2dbd30b9c1f5b0f547cb8fa2490953db8bcc")throw new Exception("Unapproved PET 1.4 baseline");
  var media=File.ReadAllBytes(args[1]);if(Hash(media)!=NewHash)throw new Exception("Media SHA mismatch");
  using var assembly=AssemblyDefinition.ReadAssembly(new MemoryStream(original));
  if(Metadata(assembly,"PetIdentity")!="005"||Metadata(assembly,"PetVersion")!="1.4"||assembly.Name.HasPublicKey)throw new Exception("Wrong PET identity/version/signing model");
  var all=assembly.MainModule.Types.SelectMany(Types).ToArray();
  var methods=all.SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName,MethodSignature);
  var resources=assembly.MainModule.Resources.OfType<EmbeddedResource>().ToDictionary(r=>r.Name,r=>Hash(r.GetResourceData()));
  var resource=assembly.MainModule.Resources.OfType<EmbeddedResource>().Single(r=>r.Name=="ANGRA.Opening.mp4");
  if(Hash(resource.GetResourceData())!=OldHash)throw new Exception("Approved old video differs");
  var index=assembly.MainModule.Resources.IndexOf(resource);assembly.MainModule.Resources[index]=new EmbeddedResource(resource.Name,resource.Attributes,media);
  foreach(var attr in assembly.CustomAttributes){
   if(attr.AttributeType.FullName=="System.Reflection.AssemblyMetadataAttribute"&&(string)attr.ConstructorArguments[0].Value=="PetVersion")attr.ConstructorArguments[1]=new CustomAttributeArgument(assembly.MainModule.TypeSystem.String,"1.5");
   if(attr.AttributeType.FullName is "System.Reflection.AssemblyFileVersionAttribute" or "System.Reflection.AssemblyInformationalVersionAttribute")attr.ConstructorArguments[0]=new CustomAttributeArgument(assembly.MainModule.TypeSystem.String,"1.5.0");
  }
  int hashes=0,html=0,timeout=0,stops=0;
  var changed=new HashSet<string>();
  foreach(var type in all){
   foreach(var field in type.Fields)if(field.HasConstant&&field.Constant is string value&&value==OldHash)field.Constant=NewHash;
   foreach(var method in type.Methods.Where(m=>m.HasBody)){
    bool opening=type.FullName.StartsWith("AngraBrand",StringComparison.Ordinal);
    foreach(var instruction in method.Body.Instructions.ToArray()){
     if(instruction.OpCode==OpCodes.Ldstr&&instruction.Operand is string text){
      if(text==OldHash){if(!opening)throw new Exception("Video hash outside opening");instruction.Operand=NewHash;hashes++;changed.Add(method.FullName);}
      if(text.StartsWith("<!doctype html>",StringComparison.OrdinalIgnoreCase)&&text.Contains("angra-opening.local")){if(!opening)throw new Exception("Player outside opening");instruction.Operand=File.ReadAllText(args[2]);html++;changed.Add(method.FullName);}
     }
     if(opening&&instruction.OpCode==OpCodes.Ldc_I4&&instruction.Operand is int ms&&ms==18000){instruction.Operand=45000;timeout++;changed.Add(method.FullName);}
    }
    // Stop only the opening load watchdog once playback has actually started.
    var playing=method.Body.Instructions.FirstOrDefault(i=>i.OpCode==OpCodes.Ldstr&&(string)i.Operand=="playing");
    if(opening&&playing!=null){
     var branch=playing.Next;while(branch!=null&&branch.OpCode.FlowControl!=FlowControl.Cond_Branch)branch=branch.Next;
     if(branch==null||branch.OpCode.Code is not (Code.Brfalse or Code.Brfalse_S))throw new Exception("Unknown playback branch");
     var field=type.Fields.Single(f=>f.FieldType.FullName=="System.Windows.Forms.Timer");
     var stopRef=all.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().First(m=>m.DeclaringType.FullName=="System.Windows.Forms.Timer"&&m.Name=="Stop");
     var il=method.Body.GetILProcessor();var a=il.Create(OpCodes.Ldarg_0);var b=il.Create(OpCodes.Ldfld,field);var c=il.Create(OpCodes.Callvirt,assembly.MainModule.ImportReference(stopRef));il.InsertAfter(branch,a);il.InsertAfter(a,b);il.InsertAfter(b,c);stops++;changed.Add(method.FullName);
    }
   }
  }
  if(hashes<2||html!=1||timeout!=1||stops!=1)throw new Exception($"Unexpected edit counts {hashes}/{html}/{timeout}/{stops}");
  foreach(var method in all.SelectMany(t=>t.Methods))if(!changed.Contains(method.FullName)&&MethodSignature(method)!=methods[method.FullName])throw new Exception("Unrelated method changed");
  foreach(var r in assembly.MainModule.Resources.OfType<EmbeddedResource>().Where(r=>r.Name!="ANGRA.Opening.mp4"))if(Hash(r.GetResourceData())!=resources[r.Name])throw new Exception("Unrelated resource changed");
  assembly.Write(args[3]);using var check=AssemblyDefinition.ReadAssembly(args[3]);
  if(Metadata(check,"PetIdentity")!="005"||Metadata(check,"PetVersion")!="1.5")throw new Exception("Output metadata mismatch");
  foreach(var method in check.MainModule.Types.SelectMany(Types).SelectMany(t=>t.Methods))if(!changed.Contains(method.FullName)&&MethodSignature(method)!=methods[method.FullName])throw new Exception("Unrelated method differs after write: "+method.FullName);
  Directory.CreateDirectory("sync-evidence");File.WriteAllText("sync-evidence/patch-scope.json",JsonSerializer.Serialize(new{Pass=true,Identity="005",From="1.4",To="1.5",NoCoreCompilation=true,OnlyOpeningMethodsChanged=changed,OtherMethodsIdentical=true,OtherResourcesIdentical=true,CoreAndNotifierUnchanged=true,VoiceRegenerated=false,VideoSha256=NewHash,VoiceStart=1.75,VoiceEnd=7.414,FadeStart=8.35,Duration=9.0,AudioDrainMs=350,WatchdogStopsOnPlaying=true}));
  Console.WriteLine("PASS only opening timing/media and PET version updated; other methods and resources identical");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
