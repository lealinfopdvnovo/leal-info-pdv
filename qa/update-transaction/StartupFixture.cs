namespace LealInfoPDV;
// Explicit synthetic startup fixture, never a claim of full PDV/license homologation.
internal sealed class MainForm:System.Windows.Forms.Form{}
internal static class UpdateManager { internal const string CurrentVersion="10.416"; }
internal static class StartupFixture
{
 [STAThread] static void Main()
 {
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true" || !File.Exists(Path.Combine(AppContext.BaseDirectory,"QA-ISOLATED")))throw new Exception("Only isolated fixture");
  TransactionalUpdate.BeforeStartup();
  TransactionalUpdate.ReportReady();
  Thread.Sleep(60000);
 }
}
