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
Check(contract.CurrentVersion=="10.468","actual compiled updater constant is 10.468, newer than identified 10.467");
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
var dll=File.ReadAllBytes(module);var sha=Convert.ToHexString(SHA256.HashData(dll)).ToLowerInvariant();byte[] manifest=JsonSerializer.SerializeToUtf8Bytes(new{Identity="001",Version="1.3",ApiVersion=1,Assembly="Pet.dll",Sha256=sha,MinGlobalVersion="10.468",MaxGlobalVersionExclusive="10.469"});
byte[] capsule;using(var stream=new MemoryStream()){using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true)){using(var s=zip.CreateEntry("Pet.dll").Open())s.Write(dll);using(var s=zip.CreateEntry("manifest.json").Open())s.Write(manifest);}capsule=stream.ToArray();}
var tail=new byte[56];BinaryPrimitives.WriteInt64LittleEndian(tail,capsule.Length);SHA256.HashData(capsule).CopyTo(tail,8);Encoding.ASCII.GetBytes("LEAL-PET-BOOT-01").CopyTo(tail,40);
string target=Path.GetFullPath("destination-10468");Directory.CreateDirectory(target);
File.WriteAllBytes(Path.Combine(target,"LealInfoPDV.exe"),File.ReadAllBytes(Path.Combine(build,"LealInfoPDV.exe")).Concat(capsule).Concat(tail).ToArray());
foreach(var name in new[]{"LealInfoPDV.dll","LealInfoPDV.deps.json"})File.Copy(Path.Combine(build,name),Path.Combine(target,name),true);
var checkedCapsule=InitialMigrationBridge.InspectCapsule(File.ReadAllBytes(Path.Combine(target,"LealInfoPDV.exe")),"10.468");
Check(checkedCapsule.Version=="1.3"&&checkedCapsule.Sha256==sha,"existing capsule parser accepts compiled Loja PET 1.3 and preserved original module");
File.WriteAllBytes(Path.Combine(target,"Pet.dll"),dll);File.WriteAllBytes(Path.Combine(target,"manifest.json"),manifest);
using(var zip=ZipFile.Open(Path.Combine(target,"initial-transition.zip"),ZipArchiveMode.Create))foreach(var name in new[]{"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"})zip.CreateEntryFromFile(Path.Combine(target,name),name);
var original=InitialMigrationBridge.InspectHost(File.ReadAllBytes("legacy-001/LealInfoPDV.dll"));
foreach(var r in original.Resources.Where(r=>r.Key is not ("TransactionalUpdateWorker" or "Loja.PET.1.2" or "PET.CertificationSeal")))Check(contract.Resources.TryGetValue(r.Key,out var value)&&value==r.Value,"original 10.467 resource retained: "+r.Key);
Directory.CreateDirectory("completion-evidence");File.WriteAllText("completion-evidence/destination.json",JsonSerializer.Serialize(new{Pass=true,Checks=evidence,Origin="10.467",Target="10.468",Pet="1.3",TargetHashes=new[]{"LealInfoPDV.exe","LealInfoPDV.dll","LealInfoPDV.deps.json"}.ToDictionary(n=>n,n=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(target,n)))).ToLowerInvariant()),PackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(target,"initial-transition.zip")))).ToLowerInvariant(),PetSha256=sha,JointRecoveryWorkerSha256=contract.Resources["JointInitialRecoveryWorker"],LicensedOperationalStartup=false,Published=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("PASS new destination compiled and capsule packaged only in isolated workspace; no release/feed");return 0;
