using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using LealInfo.Components;
static class UiProbe
{
 [STAThread] static int Main()
 {
  Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
  Application.EnableVisualStyles();
  var installed=new Installed("10.466","1.1","003");
  var g=new Release(ComponentKind.GLOBAL,"10.500",null,new string('a',64),"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v10.500/package.zip");
  var p=new Release(ComponentKind.PET,"1.2","003",new string('a',64),"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-003-pet-v1.2/package.zip");
  var plan=IndependentUpdater.Plan(installed,g,p);
  using var form=new ComponentUpdateForm(installed,plan,_=>Task.CompletedTask);
  form.Text="TESTE ISOLADO — Interface candidata, não instalada no PDV";
  form.Show();Application.DoEvents();
  Control? Find(Control c,string name){if(c.Name==name)return c;foreach(Control child in c.Controls){var f=Find(child,name);if(f!=null)return f;}return null;}
  if(Find(form,"updateGLOBAL") is not Button global || Find(form,"updatePET") is not Button pet || !global.Enabled||!pet.Enabled)throw new Exception("Missing independent update controls");
  Directory.CreateDirectory("pair-evidence");
  using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save("pair-evidence/updater-candidate-Windows.png",ImageFormat.Png);}
  form.Close();
  using var partial=new ComponentUpdateForm(installed,IndependentUpdater.Plan(installed,null,p),_=>Task.CompletedTask);
  if(Find(partial,"updateGLOBAL")!.Enabled || !Find(partial,"updatePET")!.Enabled)throw new Exception("Missing GLOBAL blocks PET");
  File.WriteAllText("pair-evidence/ui-test.txt","PASS independent GLOBAL/PET buttons, absent GLOBAL preserves PET, real WinForms capture. Fixture versions only. Activation/integration/migration not tested.");
  Console.WriteLine("PASS Windows independent updater UI candidate");
  return 0;
 }
}
