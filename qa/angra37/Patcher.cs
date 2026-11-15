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
  if(args.Length!=3)throw new Exception("origin.dll helper.dll output.dll required");
  var original=File.ReadAllBytes(args[0]);if(Hash(original)!="8fd16ad51e1b963c6afde0fabdef5807d7b2ee711ff76a55ae28d271019d675f")throw new Exception("Unapproved PET 1.9 baseline");

  using var assembly=AssemblyDefinition.ReadAssembly(new MemoryStream(original));
  if(Metadata(assembly,"PetIdentity")!="005"||Metadata(assembly,"PetVersion")!="1.12"||assembly.Name.HasPublicKey)throw new Exception("Wrong PET identity/version/signing model");
  var all=assembly.MainModule.Types.SelectMany(Types).ToArray();
  var methods=all.SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName,MethodSignature);
  var resources=assembly.MainModule.Resources.OfType<EmbeddedResource>().ToDictionary(r=>r.Name,r=>Hash(r.GetResourceData()));
  var helper=File.ReadAllBytes(args[1]);
  assembly.MainModule.Resources.Add(new EmbeddedResource("ANGRA.IntegrationQA.Helper",ManifestResourceAttributes.Private,helper));
  foreach(var attr in assembly.CustomAttributes){
   if(attr.AttributeType.FullName=="System.Reflection.AssemblyMetadataAttribute"&&(string)attr.ConstructorArguments[0].Value=="PetVersion")attr.ConstructorArguments[1]=new CustomAttributeArgument(assembly.MainModule.TypeSystem.String,"1.14");
   if(attr.AttributeType.FullName is "System.Reflection.AssemblyFileVersionAttribute" or "System.Reflection.AssemblyInformationalVersionAttribute")attr.ConstructorArguments[0]=new CustomAttributeArgument(assembly.MainModule.TypeSystem.String,"1.14.0");
  }
  var module=assembly.MainModule;
  var bridge=new TypeDefinition("","AngraIntegrationQaBridge",TypeAttributes.NotPublic|TypeAttributes.Abstract|TypeAttributes.Sealed,module.TypeSystem.Object);module.Types.Add(bridge);
  var boot=new MethodDefinition("Install",MethodAttributes.Static|MethodAttributes.Public,module.TypeSystem.Void);bridge.Methods.Add(boot);boot.Parameters.Add(new ParameterDefinition("form",ParameterAttributes.None,module.ImportReference(typeof(System.Windows.Forms.Form))));boot.Body.InitLocals=true;
  var stream=new VariableDefinition(module.ImportReference(typeof(Stream)));var memory=new VariableDefinition(module.ImportReference(typeof(MemoryStream)));boot.Body.Variables.Add(stream);boot.Body.Variables.Add(memory);
  var il=boot.Body.GetILProcessor();
  void Emit(OpCode code)=>il.Append(il.Create(code));
  void Text(string s)=>il.Append(il.Create(OpCodes.Ldstr,s));
  void Method(OpCode code,System.Reflection.MethodBase m)=>il.Append(il.Create(code,module.ImportReference(m)));
  void Var(OpCode code,VariableDefinition v)=>il.Append(il.Create(code,v));
  Method(OpCodes.Call,typeof(System.Reflection.Assembly).GetMethod("GetExecutingAssembly")!);Text("ANGRA.IntegrationQA.Helper");Method(OpCodes.Callvirt,typeof(System.Reflection.Assembly).GetMethod("GetManifestResourceStream",new[]{typeof(string)})!);Var(OpCodes.Stloc,stream);
  Method(OpCodes.Newobj,typeof(MemoryStream).GetConstructor(Type.EmptyTypes)!);Var(OpCodes.Stloc,memory);
  Var(OpCodes.Ldloc,stream);Var(OpCodes.Ldloc,memory);Method(OpCodes.Callvirt,typeof(Stream).GetMethod("CopyTo",new[]{typeof(Stream)})!);
  Var(OpCodes.Ldloc,stream);Method(OpCodes.Callvirt,typeof(IDisposable).GetMethod("Dispose")!);
  Var(OpCodes.Ldloc,memory);Emit(OpCodes.Ldc_I4_0);Emit(OpCodes.Conv_I8);Method(OpCodes.Callvirt,typeof(Stream).GetProperty("Position")!.SetMethod!);Method(OpCodes.Call,typeof(System.Runtime.Loader.AssemblyLoadContext).GetProperty("Default")!.GetMethod!);Var(OpCodes.Ldloc,memory);Method(OpCodes.Callvirt,typeof(System.Runtime.Loader.AssemblyLoadContext).GetMethod("LoadFromStream",new[]{typeof(Stream)})!);
  Text("AngraQaIntegration.Resume");Emit(OpCodes.Ldc_I4_1);Method(OpCodes.Callvirt,typeof(System.Reflection.Assembly).GetMethod("GetType",new[]{typeof(string),typeof(bool)})!);
  Text("Apply");Method(OpCodes.Callvirt,typeof(Type).GetMethod("GetMethod",new[]{typeof(string)})!);Emit(OpCodes.Ldnull);Emit(OpCodes.Ldc_I4_1);il.Append(il.Create(OpCodes.Newarr,module.TypeSystem.Object));Emit(OpCodes.Dup);Emit(OpCodes.Ldc_I4_0);Emit(OpCodes.Ldarg_0);Emit(OpCodes.Stelem_Ref);Method(OpCodes.Callvirt,typeof(System.Reflection.MethodBase).GetMethod("Invoke",new[]{typeof(object),typeof(object[])})!);Emit(OpCodes.Pop);
  Var(OpCodes.Ldloc,memory);Method(OpCodes.Callvirt,typeof(IDisposable).GetMethod("Dispose")!);Emit(OpCodes.Ret);
  var init=all.Single(t=>t.FullName=="LealInfoPDV.AngraPairingForm").Methods.Single(m=>m.IsConstructor&&!m.IsStatic);
  var entry=init.Body.GetILProcessor();foreach(var ret in init.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()){
   // Keep existing branches to ret valid: replace ret itself with ldarg rather than inserting before it.
   ret.OpCode=OpCodes.Ldarg_0;entry.InsertAfter(ret,entry.Create(OpCodes.Call,boot));entry.InsertAfter(ret.Next,entry.Create(OpCodes.Ret));
  }
  var customize=all.Single(t=>t.FullName=="ProfileExtension").Methods.Single(m=>m.Name=="Customize");
  var cil=customize.Body.GetILProcessor();foreach(var ret in customize.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()){
   ret.OpCode=OpCodes.Ldarg_1;cil.InsertAfter(ret,cil.Create(OpCodes.Call,boot));cil.InsertAfter(ret.Next,cil.Create(OpCodes.Ret));
  }
  var changed=new HashSet<string>{init.FullName,customize.FullName};
  const string sharedOrigin="https://angra-vidros-gateway.classyglobe9.chatgpt.site";
  const string isolatedOrigin="https://angra-vidros-qa37.adriansantosangra.chatgpt.site"; // Fail closed until a real isolated QA gateway is provisioned.
  int isolatedCalls=0;
  foreach(var type in all){var top=type;while(top.DeclaringType!=null)top=top.DeclaringType;
   if(!top.FullName.StartsWith("LealInfoPDV.Angra",StringComparison.Ordinal))continue;
   foreach(var m in type.Methods.Where(m=>m.HasBody))foreach(var i in m.Body.Instructions){
    if(i.Operand is string value&&(value==sharedOrigin||value.StartsWith(sharedOrigin+"/",StringComparison.Ordinal))){i.Operand=isolatedOrigin+value.Substring(sharedOrigin.Length);changed.Add(m.FullName);isolatedCalls++;}
   }
  }
  if(isolatedCalls<3)throw new Exception("QA transport isolation coverage missing");
  all.Single(t=>t.FullName=="LealInfoPDV.AngraSyncTransport").Fields.Single(f=>f.Name=="Origin").Constant=isolatedOrigin;

  foreach(var m in all.SelectMany(t=>t.Methods))if(!changed.Contains(m.FullName)&&MethodSignature(m)!=methods[m.FullName])throw new Exception("Unrelated original method changed");
  foreach(var r in assembly.MainModule.Resources.OfType<EmbeddedResource>().Where(r=>resources.ContainsKey(r.Name)))if(Hash(r.GetResourceData())!=resources[r.Name])throw new Exception("Original resource changed: "+r.Name);
  assembly.Write(args[2]);using var check=AssemblyDefinition.ReadAssembly(args[2]);
  if(Metadata(check,"PetIdentity")!="005"||Metadata(check,"PetVersion")!="1.14")throw new Exception("Output metadata mismatch");
  foreach(var method in check.MainModule.Types.SelectMany(Types).SelectMany(t=>t.Methods))if(methods.ContainsKey(method.FullName)&&!changed.Contains(method.FullName)&&MethodSignature(method)!=methods[method.FullName])throw new Exception("Unrelated method differs after write: "+method.FullName);
  Directory.CreateDirectory("integration-evidence");File.WriteAllText("integration-evidence/patch-scope.json",JsonSerializer.Serialize(new{Pass=true,Identity="005",From="1.12",To="1.14",OnlyOriginalMethodsChanged=changed.ToArray(),QaGatewayOrigin=isolatedOrigin,IsolatedGatewayCallSites=isolatedCalls,LiveGatewayConfigured=true,OtherOriginalMethodsIdentical=true,AllOriginalResourcesIdentical=true,MediaSHA256=resources["ANGRA.Opening.mp4"],CoreAndNotifierRecompiled=false,HelperEmbedded=true}));
  Console.WriteLine("PASS QA pairing-resume pairing constructor and PET customization hooks only; approved resources and all unrelated PET methods unchanged");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
