using System.Diagnostics;
using System.Text.Json;
using LealInfo.Components;
if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||!OperatingSystem.IsWindows()||args.Length!=4)return 91;
var app=Path.GetFullPath(args[0]);var ready=args[1];var stop=args[2];var report=args[3];
int count=0;var errors=new List<string>();var watch=Stopwatch.StartNew();
try{
 while(!File.Exists(stop)&&watch.Elapsed<TimeSpan.FromSeconds(50)){
  foreach(var journal in new[]{Path.Combine(app,".pdv-update","journal.json"),Path.Combine(app,".pdv-initial","journal.json")}){
   using var doc=JournalSnapshot.Read(journal,TimeSpan.FromSeconds(1));if(string.IsNullOrEmpty(doc.RootElement.GetProperty("Phase").GetString()))throw new Exception("Phase absent");count++;
  }
  if(!File.Exists(ready))File.WriteAllText(ready,"actual snapshot readers polling both transaction journals");
  Thread.Sleep(1);
 }
 if(!File.Exists(stop))throw new TimeoutException("Recovery reader was not stopped within bound");
}catch(Exception e){errors.Add(e.GetType().Name+": "+e.Message);}
var result=new{Snapshots=count,Errors=errors,ActualJournalSnapshotReader=true,LicensedStartup=false};
File.WriteAllText(report,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("POLL_EVIDENCE="+JsonSerializer.Serialize(result));return errors.Count==0?0:1;
