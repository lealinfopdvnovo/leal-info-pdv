using System.Reflection;
using System.Windows.Forms;
internal static class Probe {
[STAThread] static void Main() {
Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
var assembly=typeof(LealInfoPDV.DatabaseBackupService).Assembly;
if(assembly.GetName().Version!.ToString() != "10.469.0.0") throw new Exception("Wrong assembly version");
var updater=assembly.GetType("LealInfoPDV.UpdateManager",true)!;
if((string)updater.GetField("CurrentVersion", BindingFlags.Static|BindingFlags.Public)!.GetRawConstantValue()! != "10.469") throw new Exception("Wrong updater version");
using var owner=new Form { Width=1000, Height=700 };
owner.Show();
using var window=(Form)Activator.CreateInstance(assembly.GetType("LealInfoPDV.BackupProgressForm",true)!,true)!;
window.Show(owner); Application.DoEvents();
var report=window.GetType().GetMethod("Report")!;
IEnumerable<Control> Walk(Control c) { foreach(Control x in c.Controls) { yield return x; foreach(var y in Walk(x)) yield return y; } }
foreach(int value in new[]{0,10,40,70,100}) {
report.Invoke(window,new object[]{value}); Application.DoEvents();
if(Walk(window).OfType<ProgressBar>().Single().Value!=value) throw new Exception("Progress mismatch");
if(!Walk(window).OfType<Label>().Any(x=>x.Text.StartsWith(value+"%"))) throw new Exception("Percent missing");
}
var close=Walk(window).OfType<Button>().Single(); close.PerformClick(); Application.DoEvents();
if(window.Visible || window.IsDisposed) throw new Exception("Close must only hide display");
Console.WriteLine("PASS Windows: janela, cinco etapas, porcentagem, FECHAR sem cancelar; sem executar backup real.");
} }
