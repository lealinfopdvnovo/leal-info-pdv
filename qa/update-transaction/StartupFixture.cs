namespace LealInfoPDV;
// Explicit synthetic startup fixture, never a claim of full PDV/license homologation.
internal sealed class MainForm:System.Windows.Forms.Form{}
internal static class UpdateManager { internal const string CurrentVersion="10.416"; }
internal static class StartupFixture
{
 [STAThread] static void Main(string[] args)
 {
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true" || !File.Exists(Path.Combine(AppContext.BaseDirectory,"QA-ISOLATED")))throw new Exception("Only isolated fixture");
  TransactionalUpdate.BeforeStartup();
  if(args.Length==4 && args[0]=="--begin-qa") { TransactionalUpdate.BeginAsync(args[1],args[2],"10.416",args[3]).GetAwaiter().GetResult(); return; }
  TransactionalUpdate.ReportReady();
  Thread.Sleep(60000);
 }
}
