using System.Security.Cryptography;
using System.Text.Json;
if(!OperatingSystem.IsWindows()||Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")return 91;
var approved=Path.GetFullPath(args[0]);var report=Path.GetFullPath(args[1]);
var files=new[]{"qa/organization/bridge/InitialMigrationBridge.cs","qa/organization/bridge/InitialMigrationCoordinator.cs.source","qa/organization/bridge/JointInitialRecovery.ps1"};
var source=files.ToDictionary(n=>n,n=>File.ReadAllText(Path.Combine(approved,n)));
if(!source[files[0]].Contains("FileMode.Open,FileAccess.Read,FileShare.Read")||
 !source[files[1]].Contains("InitialMigrationBridge.ReadPlainFile(Path.Combine(AppContext.BaseDirectory,\".pdv-update\",\"journal.json\")")||
 !source[files[2]].Contains("[IO.File]::Replace($tmp,$journal"))
 throw new Exception("Approved journal protocol changed; do not infer a finding from another implementation.");
var root=Path.Combine(Path.GetTempPath(),"LEAL-STAGE3-SHARE-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try{
 object Check(string label,FileShare sharing,out bool replaced){
  var folder=Path.Combine(root,label);Directory.CreateDirectory(folder);
  var journal=Path.Combine(folder,"journal.json");var temp=Path.Combine(folder,"journal.json.tmp");
  File.WriteAllText(journal,"{\"synthetic\":true,\"phase\":\"before\"}");File.WriteAllText(temp,"{\"synthetic\":true,\"phase\":\"after\"}");
  int? code=null;string? error=null;replaced=false;
  using(var reader=new FileStream(journal,FileMode.Open,FileAccess.Read,sharing)){
   try{File.Replace(temp,journal,null);replaced=true;}catch(IOException ex){code=ex.HResult;error=ex.GetType().Name;}
  }
  var actual=File.ReadAllText(journal);
  if(actual!=(replaced?"{\"synthetic\":true,\"phase\":\"after\"}":"{\"synthetic\":true,\"phase\":\"before\"}"))throw new Exception("Unexpected journal bytes.");
  Console.WriteLine(label+": atomic replacement "+(replaced?"succeeded":"blocked"));
  return new{Label=label,ReaderSharing=sharing.ToString(),AtomicReplacementSucceeded=replaced,HResult=code,ErrorType=error,SyntheticFilesOnly=true};
 }
 var current=Check("approved-reader-contract",FileShare.Read,out var currentReplaced);
 var control=Check("delete-sharing-control",FileShare.ReadWrite|FileShare.Delete,out var controlReplaced);
 if(!controlReplaced)throw new Exception("Positive control failed; filesystem protocol cannot be qualified.");
 var result=new{Kind="NEW Windows journal sharing contract probe; not an application/migration test",ApprovedCode="a0f49da19885c87415fa01f7848dbd85b0577f0b",Environment=Environment.OSVersion.ToString(),
 SourceHashes=files.ToDictionary(n=>n,n=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(approved,n)))).ToLowerInvariant()),
 Current=current,Control=control,FindingReproduced=!currentReplaced,OperationalApproval=false,LicensedStartupExecuted=false,MigrationExecuted=false,ProductionTouched=false};
 Directory.CreateDirectory(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
 Console.WriteLine("NEW CONTRACT PROBE COMPLETE; operational approval remains blocked.");return 0;
}finally{Directory.Delete(root,true);}
