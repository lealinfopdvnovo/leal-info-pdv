using LealInfoPDV;
using System.Text.Json;
class Probe {
 static IEnumerable<Control> Walk(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Walk(x)));
 [STAThread]static int Main(){try{
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated CI only");
  ApplicationConfiguration.Initialize();Database.Initialize();
  var checks=new List<string>();
  void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);Console.WriteLine("PASS "+message);}
  using var f=new MainForm();
  var b=Walk(f).Single(x=>x.Name=="btnAssistenteAI");
  Check(b.Size==new Size(210,210),"Original LIA control restored exactly once");
  Check(b.Enabled && b.Cursor==Cursors.Hand,"LIA interaction retained");
  Check(!b.Visible,"Unlicensed isolated profile does not expose paid feature");
  foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)}){
   f.WindowState=FormWindowState.Normal;f.ClientSize=size;f.PerformLayout();
   Check(f.ClientRectangle.Contains(b.Bounds),"LIA bounds contained "+size);
  }
  Directory.CreateDirectory("evidence");File.WriteAllText("evidence/lia-results.json",JsonSerializer.Serialize(new{Success=true,Checks=checks,RealVoiceTested=false,Published=false}));return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
