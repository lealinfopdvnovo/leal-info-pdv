using LealInfoPDV;
static partial class Tests
{
 static void LoginChecks(string id){
  using var login=new LoginForm{EmbeddedMode=true};
  login.Show();var until=DateTime.UtcNow.AddSeconds(3);
  while(DateTime.UtcNow<until){Application.DoEvents();System.Threading.Thread.Sleep(10);}
  login.WindowState=FormWindowState.Normal;
  foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)}){
   login.ClientSize=size;Application.DoEvents();
   var titles=Walk(login).OfType<Label>().Where(x=>x.Text is "LEAL INFO" or "CONECTADO").ToArray();
   var card=Walk(login).OfType<TableLayoutPanel>().Single(x=>x.GetType().Name=="GlassTablePanel");
   Check(titles.Length==2&&titles.All(x=>x.GetType().Name=="CrystalTitleLabel"),"approved crystal branding retained "+size.Width);
   Check(titles.All(x=>x.Parent!.PointToScreen(x.Location).Y+x.Height<=card.PointToScreen(Point.Empty).Y),"login brand stays above access card "+size.Width);
   Check(Walk(login).OfType<TextBox>().Count(x=>x.Visible)>=2&&login.AcceptButton!=null,"existing credential fields and login action retained "+size.Width);
  }
  login.ClientSize=new Size(1366,768);Application.DoEvents();
  using(var image=new Bitmap(login.Width,login.Height)){login.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save("evidence/"+id+"/login.png");}
  login.Close();Check(Auth.Login("qaglobal","QA-global-123!")!=null,"existing credentials still authenticate after visual integration");
 }
}
