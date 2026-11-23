using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LealInfo.Components;
using LealInfoPDV;
if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||args.Length!=2)return 91;
string build=Path.GetFullPath(args[0]),module=Path.GetFullPath(args[1]);
var evidence=new List<string>();void Check(bool value,string message){if(!value)throw new Exception(message);evidence.Add(message);Console.WriteLine("PASS "+message);}
byte[] core=File.ReadAllBytes(Path.Combine(build,"LealInfoPDV.dll"));var contract=InitialMigrationBridge.InspectHost(core);InitialMigrationBridge.ValidateHostContract(contract);
Check(contract.CurrentVersion=="10.469","actual compiled updater constant is 10.469, newer than identified 10.467");
Check(contract.Types.Contains("LealInfoPDV.InitialMigrationCoordinator")&&contract.Resources.ContainsKey("JointInitialRecoveryWorker"),"real destination contains initial handoff and joint recovery worker");
var app=typeof(IPetExtension).Assembly;
List<string> Calls(string type,string method){
 var m=app.GetType(type,true)!.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static)!;
 var il=m.GetMethodBody()!.GetILAsByteArray()!;var ops=typeof(OpCodes).GetFields(BindingFlags.Static|BindingFlags.Public).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(o=>unchecked((ushort)o.Value));var result=new List<string>();
 for(int i=0;i<il.Length;){ushort code=il[i++];if(code==0xfe)code=(ushort)(0xfe00|il[i++]);var op=ops[code];int n=op.OperandType switch{OperandType.InlineNone=>0,OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget=>1,OperandType.InlineVar=>2,OperandType.InlineI8 or OperandType.InlineR=>8,OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(il,i),_=>4};
  if(op.OperandType==OperandType.InlineMethod){var called=m.Module.ResolveMethod(BitConverter.ToInt32(il,i))!;result.Add(called.DeclaringType!.Name+"."+called.Name);}i+=n;
 }return result;
}
var calls=Calls("LealInfoPDV.Program","Main");
Check(calls.Contains("InitialMigrationCoordinator.GuardStartup")&&calls.IndexOf("InstallationLicense.EnsureActivated")<calls.IndexOf("InitialMigrationCoordinator.BeforePetLoad")&&calls.IndexOf("InitialMigrationCoordinator.BeforePetLoad")<calls.IndexOf("Database.Initialize")&&calls.IndexOf("Database.Initialize")<calls.IndexOf("PetRuntime.Initialize"),"compiled entry arms joint recovery after legitimate activation and before data initialization/PET creation");
var readyCalls=Calls("LealInfoPDV.TransactionalUpdate","ReportReady");
Check(readyCalls.Contains("InitialMigrationCoordinator.get_Active")&&readyCalls.Contains("InitialMigrationCoordinator.get_ReadyForCoreHealth"),"compiled core health is deferred during the first migration until authenticated main window");

Check(app.GetType("LealInfoPDV.PetRuntime")!.GetMethod("Initialize",BindingFlags.Static|BindingFlags.NonPublic)!=null,"existing modular loader reused");
var dll=File.ReadAllBytes(module);var sha=Convert.ToHexString(SHA256.HashData(dll)).ToLowerInvariant();byte[] manifest=JsonSerializer.SerializeToUtf8Bytes(new{Identity="001",Version="1.3",ApiVersion=1,Assembly="Pet.dll",Sha256=sha,MinGlobalVersion="10.469",MaxGlobalVersionExclusive="10.470"});
byte[] capsule;using(var stream=new MemoryStream()){using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true)){using(var s=zip.CreateEntry("Pet.dll").Open())s.Write(dll);using(var s=zip.CreateEntry("manifest.json").Open())s.Write(manifest);}capsule=stream.ToArray();}
var tail=new byte[56];BinaryPrimitives.WriteInt64LittleEndian(tail,capsule.Length);SHA256.HashData(capsule).CopyTo(tail,8);Encoding.ASCII.GetBytes("LEAL-PET-BOOT-01").CopyTo(tail,40);
string target=Path.GetFullPath("destination-10469");Directory.CreateDirectory(target);
File.WriteAllBytes(Path.Combine(target,"LealInfoPDV.exe"),File.ReadAllBytes(Path.Combine(build,"LealInfoPDV.exe")).Concat(capsule).Concat(tail).ToArray());
foreach(var name in new[]{"LealInfoPDV.dll","LealInfoPDV.deps.json"})File.Copy(Path.Combine(build,name),Path.Combine(target,name),true);
var checkedCapsule=InitialMigrationBridge.InspectCapsule(File.ReadAllBytes(Path.Combine(target,"LealInfoPDV.exe")),"10.469");
Check(checkedCapsule.Version=="1.3"&&checkedCapsule.Sha256==sha,"existing capsule parser accepts compiled Loja PET 1.3 and preserved original module");
File.WriteAllBytes(Path.Combine(target,"Pet.dll"),dll);File.WriteAllBytes(Path.Combine(target,"manifest.json"),manifest);
using(var zip=ZipFile.Open(Path.Combine(target,"initial-transition.zip"),ZipArchiveMode.Create))foreach(var name in new[]{"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"})zip.CreateEntryFromFile(Path.Combine(target,name),name);
var original=InitialMigrationBridge.InspectHost(File.ReadAllBytes("legacy-001/LealInfoPDV.dll"));
foreach(var r in original.Resources.Where(r=>r.Key is not ("TransactionalUpdateWorker" or "Loja.PET.1.2" or "PET.CertificationSeal")))Check(contract.Resources.TryGetValue(r.Key,out var value)&&value==r.Value,"original 10.467 resource retained: "+r.Key);
var targetZip=Path.Combine(target,"initial-transition.zip");
foreach(var name in new[]{"legacy-001","legacy-001-backup"}){
 using var dependencies=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(name,"LealInfoPDV.deps.json")));
 using var publishedDependencies=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(target,"LealInfoPDV.deps.json")));
 var originLibraries=dependencies.RootElement.GetProperty("libraries").EnumerateObject().Where(p=>!p.Name.StartsWith("LealInfoPDV/")).Select(p=>p.Name).ToHashSet();
 var destinationLibraries=publishedDependencies.RootElement.GetProperty("libraries").EnumerateObject().Where(p=>!p.Name.StartsWith("LealInfoPDV/")).Select(p=>p.Name).ToHashSet();
 Check(originLibraries.SetEquals(destinationLibraries),"three-file package uses dependencies already installed by "+name);
 var sourceContract=InitialMigrationBridge.InspectHost(File.ReadAllBytes(Path.Combine(name,"LealInfoPDV.dll")));
 Check(sourceContract.Resources.TryGetValue("TransactionalUpdateWorker",out var workerHash)&&workerHash==InitialMigrationBridge.LegacyWorkerHash,
   "same protected transaction worker retained in origin "+name);
 var originAssembly=new System.Runtime.Loader.AssemblyLoadContext(name,true).LoadFromAssemblyPath(Path.GetFullPath(Path.Combine(name,"LealInfoPDV.dll")));
 var update=originAssembly.GetType("LealInfoPDV.UpdateManager",true)!;
 var manifestType=update.GetNestedType("UpdateManifest",BindingFlags.NonPublic)!;
 var candidate=Activator.CreateInstance(manifestType,true)!;
 void Set(string key,string value)=>manifestType.GetProperty(key)!.SetValue(candidate,value);
 Set("Version","10.469");Set("TargetClientCode","001");
 Set("PackageUrl","https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-001-v10.469-transbordo/LEAL_INFO_PDV_001_V10_469_UPDATE.zip");
 Set("Sha256",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(targetZip))).ToLowerInvariant());
 Set("SourceCommit",Environment.GetEnvironmentVariable("GITHUB_SHA")!);
 var select=update.GetMethod("SelectManifest",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public)!;
 var selectorArgs=new object?[]{"001",name=="legacy-001"?"10.467":"10.468",null,candidate};
 Check(ReferenceEquals(select.Invoke(null,selectorArgs),candidate),"original updater selects Loja offer: "+name);
 selectorArgs[0]="002";
 Check(select.Invoke(null,selectorArgs)==null,"other client rejects Loja offer: "+name);
}
var runtimeName="LealInfoPDV.runtimeconfig.json";
foreach(var (folder,version) in new[]{("legacy-001","10.467"),("legacy-001-backup","10.468")}){
 var previous=InitialMigrationBridge.OriginHashes.Keys.ToDictionary(n=>n,n=>n==runtimeName
    ? InitialMigrationBridge.OriginHashes[n]
    : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder,n)))).ToLowerInvariant());
 Check(InitialMigrationBridge.IdentifyOrigin(previous)==version,"approved origin tuple: "+version);
 previous["LealInfoPDV.dll"]=new string('0',64);
 try{InitialMigrationBridge.IdentifyOrigin(previous);throw new Exception("Unknown source accepted");}
 catch(InvalidDataException){Check(true,"unknown tuple rejected: "+version);}
}
Directory.CreateDirectory("completion-evidence");File.WriteAllText("completion-evidence/destination.json",JsonSerializer.Serialize(new{Pass=true,Checks=evidence,Origins=new[]{"10.467","10.468"},Target="10.469",Pet="1.3",TargetHashes=new[]{"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"}.ToDictionary(n=>n,n=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(target,n)))).ToLowerInvariant()),PackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(target,"initial-transition.zip")))).ToLowerInvariant(),PetSha256=sha,JointRecoveryWorkerSha256=contract.Resources["JointInitialRecoveryWorker"],LicensedOperationalStartup=false,Published=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("PASS new destination compiled and capsule packaged only in isolated workspace; no release/feed");return 0;
