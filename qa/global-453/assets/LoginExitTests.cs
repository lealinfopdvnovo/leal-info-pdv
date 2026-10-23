using LealInfoPDV;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Forms;
static class LoginExitTests
{
 const BindingFlags I=BindingFlags.Instance|BindingFlags.NonPublic;
 static IEnumerable<Control> Walk(Control root)=>root.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(Walk(c)));
 static readonly List<string> checks=new();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS "+text);}
 static void Escape(Form f){object?[] args={Message.Create(f.Handle,0x0100,(IntPtr)27,IntPtr.Zero),Keys.Escape};Check((bool)f.GetType().GetMethod("ProcessCmdKey",I)!.Invoke(f,args)!,"Esc handled by "+f.GetType().Name);}
 static void Exercise(Form f,Action action)
 {
  Exception? failure=null;using var timer=new System.Windows.Forms.Timer{Interval=150};
  timer.Tick+=(_,_)=>{timer.Stop();try{action();}catch(Exception e){failure=e;f.Close();}};
  timer.Start();var result=f.ShowDialog();timer.Stop();if(failure!=null)throw failure;
  Check(result==DialogResult.Cancel&&!f.Visible,"login cancellation closes "+f.GetType().Name+" without opening main window");
 }
 static byte[] DatabaseHash(){using var stream=new FileStream(Database.DbPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);return SHA256.HashData(stream);}
 internal static void Run()
 {
  Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","login tests restricted to isolated Windows runner");
  byte[] before=DatabaseHash();Check(Auth.Current==null,"login tests begin without authenticated session");
  foreach(bool escape in new[]{false,true})using(var f=new LoginForm())
  {
   f.WindowState=FormWindowState.Normal;f.ClientSize=new System.Drawing.Size(1024,600);
   Exercise(f,()=>{
    var exit=Walk(f).OfType<Button>().Single(b=>b.Name=="btnSairLogin");
    Check(exit.Visible&&exit.Enabled&&f.ClientRectangle.Contains(f.RectangleToClient(exit.RectangleToScreen(exit.ClientRectangle))),"SAIR button fully visible and enabled at 1024x600");
    Check(ReferenceEquals(f.CancelButton,exit)&&f.AcceptButton is Button enter&&enter.Text=="ENTRAR","Esc and Enter retain separate exit and authentication actions");
    if(escape){Walk(f).OfType<TextBox>().First(x=>x.UseSystemPasswordChar).Focus();Escape(f);}else exit.PerformClick();
   });
  }
  foreach(bool escape in new[]{false,true})using(var f=new SplashForm())
  {
   // Exercise the actual embedded login and its existing close propagation, without video/network.
   typeof(SplashForm).GetField("introStarted",I)!.SetValue(f,true);
   typeof(SplashForm).GetMethod("LoadRealLogin",I)!.Invoke(f,null);
   typeof(SplashForm).GetMethod("FinishIntro",I)!.Invoke(f,null);
   Exercise(f,()=>{
    var login=(LoginForm)typeof(SplashForm).GetField("login",I)!.GetValue(f)!;
    Check(login.Visible,"embedded login is visible after intro");
    if(escape)Escape(f);else Walk(login).OfType<Button>().Single(b=>b.Name=="btnSairLogin").PerformClick();
   });
  }
  Check(Auth.Current==null,"closing login never authenticates a session");
  Check(before.SequenceEqual(DatabaseHash()),"login exit preserves database byte for byte");
  File.WriteAllText("evidence/login-exit-results.json",JsonSerializer.Serialize(new{Success=true,Checks=checks,RealCustomerWindowsTested=false}));
 }
}
