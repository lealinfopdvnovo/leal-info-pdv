using System.Security.Cryptography;
using System.Text.Json;
using LealInfo.Components;

var checks=new List<string>();
void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);Console.WriteLine("PASS "+message);}
void Reject(Action call,string message){bool refused=false;try{call();}catch(Exception e) when(e is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException or BadImageFormatException){refused=true;}Check(refused,message);}
var root=Path.Combine(Path.GetTempPath(),"LEAL-BRIDGE-QA-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try{
 foreach(var version in new[]{"10.463","10.464","10.465","10.466","10.467","10.0467","1.4","10.468-beta","10.468.1"})
  Reject(()=>InitialMigrationBridge.ValidateTargetVersion(version),"old/replay/unsupported destination refused: "+version);
 InitialMigrationBridge.ValidateTargetVersion("10.468");Check(true,"new candidate version allowed by ordering only; not an approval");
 var types=new HashSet<string>(new[]{"LealInfoPDV.PetRuntime","LealInfoPDV.PetBootstrap","LealInfoPDV.IPetExtension","LealInfoPDV.IPetStartup","LealInfoPDV.IPetReady","LealInfoPDV.IFinancialMovementsPet"});
 var resources=new Dictionary<string,string>{{"PetTransactionalUpdateWorker",new string('a',64)}};
 var contract=new InitialMigrationBridge.HostContract("10.468",types,resources);
 InitialMigrationBridge.ValidateHostContract(contract);Check(true,"synthetic modular contract accepted for preflight only");
 foreach(var type in types){var missing=types.Where(x=>x!=type).ToHashSet();Reject(()=>InitialMigrationBridge.ValidateHostContract(contract with{Types=missing}),"missing contract refused: "+type);}
 Reject(()=>InitialMigrationBridge.ValidateHostContract(contract with{Resources=new Dictionary<string,string>()}),"missing PET recovery refused");
 Reject(()=>InitialMigrationBridge.InspectHost(new byte[]{1,2,3}),"invalid managed binary refused");
 Reject(()=>InitialMigrationBridge.VerifyOrigin(root),"missing original files refused without staging");
 foreach(var key in InitialMigrationBridge.OriginHashes.Keys)File.WriteAllText(Path.Combine(root,key),"SYNTHETIC-NOT-A-LICENSE");
 var before=Directory.GetFiles(root).ToDictionary(p=>Path.GetFileName(p)!,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
 Reject(()=>InitialMigrationBridge.VerifyOrigin(root),"unidentified origin refused");
 Reject(()=>InitialMigrationBridge.Prepare(root,Path.Combine(root,"not-a-package.zip"),new string('0',64)),"unknown origin rejected before any package staging");
 Check(before.All(x=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,x.Key!))))==x.Value)&&Directory.GetFiles(root).Length==before.Count,"rejected preparation preserves every original fixture byte and creates nothing");
 var plan=new InitialMigrationBridge.Plan("10.467","10.468","001","1.3",new string('a',64),new Dictionary<string,string>(),new Dictionary<string,string>(),new string('b',64),true,false,false);
 Reject(()=>InitialMigrationBridge.RequireTransactionalRecovery(plan),"legacy core-only recovery cannot activate a two-component migration");
 Reject(()=>InitialMigrationBridge.RequireTransactionalRecovery(plan with{CanApply=true,OperationallyApproved=true}),"forged approval flags cannot bypass recovery gate");
 Check(!ClientPolicy.Default.IsActive("004")&&new[]{"001","002","003"}.All(ClientPolicy.Default.IsActive),"existing client activity policy reused unchanged");
 var evidence=new{Pass=true,Checks=checks,Kind="new bridge policy and read-only fixture tests",RealLicensedStartup=false,RealMigration=false,Published=false};
 string output=args.Length==0?Path.Combine(root,"bridge-tests.json"):Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.WriteAllText(output,JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
 Console.WriteLine("PASS "+checks.Count+" targeted bridge checks; activation remains blocked");
}finally{Directory.Delete(root,true);}
