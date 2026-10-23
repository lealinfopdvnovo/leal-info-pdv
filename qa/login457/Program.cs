using LealInfoPDV;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
class Tests
{
 static IEnumerable<Control> Walk(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Walk(x)));
 static byte[] State(){using var f=new FileStream(Database.DbPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);return SHA256.HashData(f);}
 [STAThread]static int Main(){
  try{
   if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||File.Exists(Database.DbPath))throw new Exception("Isolated fresh profile required");
   ApplicationConfiguration.Initialize();Database.Initialize();Auth.CreateUser("QA","qa-login","qa-login-123!","ADMINISTRADOR","qa@example.invalid","");
   var initial=State();Directory.CreateDirectory("evidence");var checks=new List<string>();
   void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS "+text);}
   Check(typeof(MainForm).Assembly.GetName().Version!.Minor==457,"GLOBAL version is 10.457, not 10.458");
   foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)})
   using(var f=new LoginForm()){
    f.WindowState=FormWindowState.Normal;f.ClientSize=size;
    Exception? error=null;
    using var timer=new System.Windows.Forms.Timer{Interval=2500};
    timer.Tick+=(_,_)=>{timer.Stop();try{
     var badge=Walk(f).Single(x=>x.Name=="loginVersionBadge");
     Console.WriteLine($"BADGE visible={badge.Visible} text={badge.Text} bounds={badge.Bounds} parent={badge.Parent!.Bounds} client={f.ClientRectangle} screen={f.RectangleToClient(badge.RectangleToScreen(badge.ClientRectangle))}");
     using(var shot=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(shot,f.ClientRectangle);shot.Save("evidence/login-diagnostic.png");}
     Check(badge.Visible&&badge.Text.Contains("V10.457")&&f.ClientRectangle.Contains(f.RectangleToClient(badge.RectangleToScreen(badge.ClientRectangle))),"gold version badge fully visible "+size);
     Check(Walk(f).OfType<Button>().Any(b=>b.Name=="btnSairLogin"&&b.Enabled)&&f.AcceptButton is Button enter&&enter.Text=="ENTRAR","existing login actions retained "+size);
     using var pixels=new Bitmap(badge.Width,badge.Height);badge.DrawToBitmap(pixels,badge.ClientRectangle);int gold=0;
     for(int y=0;y<pixels.Height;y++)for(int x=0;x<pixels.Width;x++){var c=pixels.GetPixel(x,y);if(c.R>200&&c.G>140&&c.B<120)gold++;}
     Check(gold>100,"gold version lettering and border rendered "+size);
     using var image=new Bitmap(f.Width,f.Height);f.DrawToBitmap(image,f.ClientRectangle);image.Save("evidence/login-"+size.Width+"x"+size.Height+".png");
     var exit=Walk(f).OfType<Button>().Single(b=>b.Name=="btnSairLogin");exit.PerformClick();
    }catch(Exception e){error=e;f.Close();}};
    timer.Start();f.ShowDialog();if(error!=null)throw error;
   }
   Check(Auth.Current==null&&State().SequenceEqual(initial),"login rendering and closing preserve database byte for byte");
   File.WriteAllText("evidence/login457-results.json",JsonSerializer.Serialize(new{Success=true,Checks=checks,Channel=Environment.GetEnvironmentVariable("CHANNEL"),Version="10.457",Published=false,InstalledOnCustomer=false}));return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
