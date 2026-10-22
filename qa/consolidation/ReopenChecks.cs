using LealInfoPDV;
using System.Diagnostics;
using System.Reflection;
static partial class Tests
{
 static int ReopenChecks(string id,string version,string folder,string projectId){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||File.ReadAllText(Path.Combine(Database.AppFolder,"ISOLATED-CONSOLIDATION"))!=Environment.GetEnvironmentVariable("GITHUB_RUN_ID"))return 92;
  try{
   var state=DatabaseState();PetRuntime.Load(folder,id);
   var saved=GlassProjectStore.Load(projectId);
   Check(saved.Data.WidthMm==1200&&saved.Data.HeightMm==2000&&saved.Data.Quote.CustomerId==901,"new process reopens original project dimensions and customer");
   Check(GlassOperationsStore.Read(saved).Received==100m,"new process retains exact received amount");
   Check(GlassOperationsStore.Appointments().Count(x=>x.ProjectId==projectId)==1,"new process retains agenda");
   Check(GlassDocumentStore.Receipts(saved).Count==1&&GlassDocumentStore.Contracts(projectId).Count==1,"new process retains receipt and contract with valid hash");
   Check(DatabaseState()==state,"reopening retained project does not rewrite populated database");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
 static void ReopenInNewProcess(string id,string version,string folder){
  string project=Convert.ToString(Sql("SELECT id FROM glass_projects_v1 LIMIT 1"))!;
  var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
  foreach(var arg in new[]{Assembly.GetExecutingAssembly().Location,id,version,folder,"--reopen",project})start.ArgumentList.Add(arg);
  using var child=Process.Start(start)!;var output=child.StandardOutput.ReadToEndAsync();var error=child.StandardError.ReadToEndAsync();
  Check(child.WaitForExit(30000),"separate persistence process completes");
  var text=output.GetAwaiter().GetResult()+error.GetAwaiter().GetResult();Console.WriteLine(text);File.WriteAllText("evidence/"+id+"/reopen.txt",text);
  Check(child.ExitCode==0,"persisted domain records validated in separate process");
 }
}
