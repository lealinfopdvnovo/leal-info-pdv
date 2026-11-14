using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfo.Components;
if(!OperatingSystem.IsWindows()||Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")return 91;
var evidence=Path.GetFullPath(args[0]);var root=Path.Combine(Path.GetTempPath(),"LEAL-JOURNAL-FIX-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var checks=new List<object>();
void Check(bool value,string name,object? detail=null){if(!value)throw new Exception(name);checks.Add(new{Name=name,Pass=true,Detail=detail});Console.WriteLine("PASS "+name);}
void Fresh(string p,int n){File.WriteAllText(p,JsonSerializer.Serialize(new{Sequence=n,Mirror=n,Padding=new string((char)('a'+n%26),4096)}));}
void Replace(string p,int n){var temp=p+".tmp";Fresh(temp,n);File.Replace(temp,p,null);}
void Exclusive(string p){using var file=new FileStream(p,FileMode.Open,FileAccess.ReadWrite,FileShare.None);}
var path=Path.Combine(root,"journal.json");
try{
 Fresh(path,0);
 // Actual factory used by JournalSnapshot, held across replacement deterministically.
 using(var held=JournalSnapshot.OpenReader(path)){Replace(path,1);using var state=JournalSnapshot.Read(path);Check(state.RootElement.GetProperty("Sequence").GetInt32()==1,"atomic replacement succeeds while the actual journal reader remains open");}
 Exclusive(path);Check(true,"successful snapshots release every file handle before returning");
 using(var snap=JournalSnapshot.Read(path)){Replace(path,2);Check(snap.RootElement.GetProperty("Sequence").GetInt32()==1,"returned JSON owns its immutable snapshot and does not pin the file");}
 using(var binary=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){bool blocked=false;try{Replace(path,3);}catch(IOException){blocked=true;}Check(blocked,"restrictive binary sharing remains restrictive; fix is limited to journals");}
 if(File.Exists(path+".tmp"))File.Delete(path+".tmp");
 int writes=0,reads=0;var faults=new List<Exception>();
 var writer=Task.Run(()=>{try{for(int n=4;n<304;n++){Replace(path,n);Interlocked.Increment(ref writes);Thread.Sleep(1);}}catch(Exception e){lock(faults)faults.Add(e);}});
 var reader=Task.Run(()=>{try{while(!writer.IsCompleted||reads<300){using var d=JournalSnapshot.Read(path);var r=d.RootElement;int n=r.GetProperty("Sequence").GetInt32();if(n!=r.GetProperty("Mirror").GetInt32()||r.GetProperty("Padding").GetString()!=new string((char)('a'+n%26),4096))throw new Exception("Torn snapshot");Interlocked.Increment(ref reads);}}catch(Exception e){lock(faults)faults.Add(e);}});
 if(!Task.WaitAll(new[]{writer,reader},TimeSpan.FromSeconds(20)))throw new TimeoutException("Concurrent probe hung");
 Check(faults.Count==0&&writes==300&&reads>=300,"concurrent atomic writes and actual snapshot reads complete without sharing errors or mixed JSON",new{Writes=writes,Reads=reads});
 Exclusive(path);
 using(var locked=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
  var reading=Task.Run(()=>{using var d=JournalSnapshot.Read(path,TimeSpan.FromSeconds(2));return d.RootElement.GetProperty("Sequence").GetInt32();});
  Thread.Sleep(40);locked.Dispose();Check(reading.GetAwaiter().GetResult()==303,"transient exclusive lock is retried and read succeeds after release");
 }
 using(var locked=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
  var watch=Stopwatch.StartNew();bool rejected=false;try{using var d=JournalSnapshot.Read(path,TimeSpan.FromMilliseconds(60));}catch(TimeoutException){rejected=true;}
  Check(rejected&&watch.Elapsed<TimeSpan.FromSeconds(2),"persistent contention fails closed within a bounded deadline",new{ElapsedMs=watch.ElapsedMilliseconds});
 }
 Exclusive(path);
 File.WriteAllText(path,"{broken");
 bool bad=false;try{using var d=JournalSnapshot.Read(path,TimeSpan.FromMilliseconds(40));}catch(TimeoutException e){bad=e.InnerException is JsonException;}
 Exclusive(path);Check(bad,"malformed persistent journal is rejected and no handle remains locked");
 File.WriteAllText(path,new string('x',65537));bool oversized=false;try{using var d=JournalSnapshot.Read(path);}catch(InvalidDataException){oversized=true;}
 Exclusive(path);Check(oversized,"journal size bound remains enforced without a leaked handle");
 Fresh(path,400);
 using(var locked=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
  using var cancel=new CancellationTokenSource(40);bool cancelled=false;try{using var d=JournalSnapshot.Read(path,TimeSpan.FromSeconds(2),cancel.Token);}catch(OperationCanceledException){cancelled=true;}
  Check(cancelled,"cancellation during a transient retry releases the reader");
 }
 Exclusive(path);
 var approved=Path.GetFullPath(args[1]);var fixedRoot=Path.GetFullPath(args[2]);
 string S(string folder,string name)=>File.ReadAllText(Path.Combine(folder,name)).Replace("\r\n","\n");
 string Body(string text){var a=text.IndexOf("    public static byte[] ReadPlainFile");var b=text.IndexOf("    public static IReadOnlyDictionary<string,string> VerifyOrigin",a);return text[a..b];}
 Check(Body(S(approved,"qa/organization/bridge/InitialMigrationBridge.cs"))==Body(S(fixedRoot,"qa/organization/bridge/InitialMigrationBridge.cs")),"complete original binary/hash reader implementation remains byte-equivalent apart from checkout line endings");
 Check(S(approved,"qa/organization/bridge/JointInitialRecovery.ps1")==S(fixedRoot,"qa/organization/bridge/JointInitialRecovery.ps1"),"joint recovery engine, core/PET backup and interruption checkpoints are unchanged");
 var coordinator=S(fixedRoot,"qa/organization/bridge/InitialMigrationCoordinator.cs.source");
 Check(!coordinator.Contains("ReadPlainFile(journal,65536)")&&coordinator.Contains("Core()=>JournalSnapshot.Read")&&coordinator.Contains("using var doc=JournalSnapshot.Read(journal)")&&coordinator.Contains("using var old=JournalSnapshot.Read(journal)"),"all affected coordinator journal reads use the corrected snapshot reader");
 Directory.CreateDirectory(Path.GetDirectoryName(evidence)!);
 var sources=new[]{"qa/organization/bridge/JournalSnapshot.cs","qa/organization/bridge/InitialMigrationBridge.cs","qa/organization/bridge/InitialMigrationCoordinator.cs.source","qa/organization/bridge/JointInitialRecovery.ps1"};
 File.WriteAllText(evidence,JsonSerializer.Serialize(new{Pass=true,Checks=checks,SourceHashes=sources.ToDictionary(n=>n,n=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(fixedRoot,n)))).ToLowerInvariant()),LicensedStartup=false,OperationalApproval=false,ProductionTouched=false},new JsonSerializerOptions{WriteIndented=true}));
 Console.WriteLine("NEW JOURNAL FIX CHECKS COMPLETE; no operational approval.");return 0;
}finally{Directory.Delete(root,true);}
