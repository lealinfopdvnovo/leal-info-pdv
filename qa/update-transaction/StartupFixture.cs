namespace LealInfoPDV;
// Explicit synthetic startup fixture, never a claim of full PDV/license homologation.
internal sealed class MainForm:System.Windows.Forms.Form{}
internal static class UpdateManager { internal const string CurrentVersion="10.422"; }
internal static class StartupFixture
{
 [STAThread] static void Main(string[] args)
 {
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true" || !File.Exists(Path.Combine(AppContext.BaseDirectory,"QA-ISOLATED")))throw new Exception("Only isolated fixture");
  TransactionalUpdate.BeforeStartup();
  if(args.Length==4 && args[0]=="--begin-qa")
  {
   using var timer=new System.Windows.Forms.Timer{Interval=50};
   timer.Tick+=async (_,_)=>{timer.Stop();try{await TransactionalUpdate.BeginAsync(args[1],args[2],"10.422",args[3]);}catch(Exception ex){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"QA-STARTUP-ERROR.txt"),ex.ToString());Environment.ExitCode=1;System.Windows.Forms.Application.Exit();}};
   timer.Start();System.Windows.Forms.Application.Run();return;
  }
  TransactionalUpdate.ReportReady();
  Thread.Sleep(60000);
 }
}
