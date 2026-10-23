using System.Reflection;
using System.Drawing;
using System.Windows.Forms;
using LealInfoPDV;
static class Tests
{
 static int passed;
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
 static IEnumerable<Control> Walk(Control c){foreach(Control child in c.Controls){yield return child;foreach(var n in Walk(child))yield return n;}}
 [STAThread] static void Main(){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated Windows only");
  Application.EnableVisualStyles();
  var type=typeof(MainForm).Assembly.GetType("LealInfoPDV.UpdateNotificationForm",true)!;
  Directory.CreateDirectory("evidence");
  foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)}){
   using var main=new Form{ClientSize=size,StartPosition=FormStartPosition.Manual,Location=new Point(0,0)};main.Show();Application.DoEvents();
   int applied=0;
   using var toast=(Form)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{main,"10.446",(Func<Task>)(()=>{applied++;return Task.CompletedTask;})},null)!;
   toast.Show(main);Application.DoEvents();
   Check(toast.Owner==main&&!toast.ShowInTaskbar,"owned notification without taskbar entry "+size.Width);
   Check(toast.Controls.Count>0&&Walk(toast).OfType<Label>().Any(x=>x.Text=="Nova atualização"),"notification title "+size.Width);
   var buttons=Walk(toast).OfType<Button>().ToArray();var now=buttons.Single(x=>x.Text=="Atualizar agora");var later=buttons.Single(x=>x.Text=="Depois");
   Check(buttons.Length==2&&applied==0,"exact two actions and no automatic download "+size.Width);
   var screen=Screen.FromControl(main).WorkingArea;
   Check(screen.Contains(toast.Bounds),"notification inside working area "+size.Width);
   Check(now.Bounds.Width>=100&&later.Bounds.Width>=70,"actions remain readable "+size.Width);
   using(var bmp=new Bitmap(toast.Width,toast.Height)){toast.DrawToBitmap(bmp,toast.ClientRectangle);bmp.Save($"evidence/notification-{size.Width}.png");}
   later.PerformClick();Application.DoEvents();Check(applied==0&&!main.IsDisposed,"Later preserves main window and downloads nothing "+size.Width);
   using var update=(Form)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{main,"10.446",(Func<Task>)(()=>{applied++;return Task.CompletedTask;})},null)!;
   update.Show(main);Application.DoEvents();Walk(update).OfType<Button>().Single(x=>x.Text=="Atualizar agora").PerformClick();Application.DoEvents();
   Check(applied==1,"Update now invokes existing update action exactly once "+size.Width);
   main.Close();
  }
  using(var main=new Form()){
   main.Show();
   using var toast=(Form)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{main,"10.446",(Func<Task>)(()=>Task.CompletedTask)},null)!;
   toast.Show(main);main.Close();Application.DoEvents();Check(toast.IsDisposed,"owner close disposes notification");
  }
  File.WriteAllText("evidence/results.json",System.Text.Json.JsonSerializer.Serialize(new{Success=true,Checks=passed,Published=false,InstalledOnCustomer=false}));
 }
}
