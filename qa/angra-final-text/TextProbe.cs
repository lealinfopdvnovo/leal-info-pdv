using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
using LealInfoPDV.Network;
static class Probe {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 static Assembly Host=>typeof(MainForm).Assembly;
 static object? Call(string type,string method,params object?[] args)=>Host.GetType("LealInfoPDV."+type,true)!.GetMethod(method,F)!.Invoke(null,args);
 static void CheckPixel(bool same){if(!same)throw new Exception("Approved artwork changed outside establishment footer text");}
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static IEnumerable<Control> Walk(Control c){yield return c;foreach(Control child in c.Controls)foreach(var x in Walk(child))yield return x;}
 [STAThread]static int Main(string[] args){try{
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||args.Length!=1)return 91;
  Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Call("QaIsolation","Initialize");
  string plan=args[0];var serial=Database.DeviceSerial();using var rsa=RSA.Create(2048);
  var terms=new LicenseTerms(serial,2,"unico",null,"",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),Guid.NewGuid().ToString(),plan,"005",true,0);
  var raw=JsonSerializer.SerializeToUtf8Bytes(terms);var signed=JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(raw),Convert.ToBase64String(rsa.SignData(raw,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))));Call("Licensing.InstallationLicense","AcceptServer",signed,serial,rsa.ExportSubjectPublicKeyInfoPem());
  Database.Initialize();if(Auth.UserCount()==0)Auth.CreateUser("QA","qa","qa-test-123!","ADMINISTRADOR","qa@example.invalid","");Check(Auth.Login("qa","qa-test-123!")!=null,"existing QA credentials usable");
  var pet=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory,"Extensions/PET/Pet.dll"));
  var extension=(IPetExtension)Activator.CreateInstance(pet.GetExportedTypes().Single(t=>!t.IsAbstract&&typeof(IPetExtension).IsAssignableFrom(t)))!;
  var licenseType=Host.GetType("LealInfoPDV.Licensing.InstallationLicense",true)!;
  var licenseBefore=licenseType.GetProperty("Current",F)!.GetValue(null);
  using var login=new LoginForm();extension.Customize(login);login.Opacity=1;login.Show();Application.DoEvents();
  Check(Walk(login).Any(c=>c is Label&&c.Text=="PDV "+plan.ToUpperInvariant()),"actual login follows verified "+plan+" license");
  Check(Walk(login).Any(c=>c.Text=="ANGRA")&&Walk(login).Any(c=>c.Text=="VIDROS"),"approved login branding retained");
  var output=Environment.GetEnvironmentVariable("TRANSITION_EVIDENCE")!;
  using(var image=new Bitmap(login.Width,login.Height)){login.DrawToBitmap(image,login.ClientRectangle);image.Save(Path.Combine(output,"login-"+plan+".png"));}login.Close();
  using var main=new MainForm();((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!).Stop();extension.Customize(main);
  // All edits must be text-only: bounds, font and control identities survive repeated application.
  var layout=Walk(main).Select(c=>(c,c.Bounds,c.Font,c.Dock)).ToArray();extension.Customize(main);
  Check(layout.All(x=>x.c.Bounds==x.Bounds&&x.c.Font==x.Font&&x.c.Dock==x.Dock),"main layout and fonts preserved");
  // Reuse the existing ProfileProbe isolation: show the genuine UI without onboarding/update dialogs.
  const BindingFlags instance=BindingFlags.Instance|BindingFlags.NonPublic;
  typeof(MainForm).GetField("automaticBackupCompleted",instance)!.SetValue(main,true);
  var events=(System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component).GetProperty("Events",instance)!.GetValue(main)!;
  var shownKey=typeof(Form).GetFields(BindingFlags.Static|BindingFlags.NonPublic).Single(x=>x.Name.Contains("Shown",StringComparison.OrdinalIgnoreCase)).GetValue(null)!;
  events.RemoveHandler(shownKey,events[shownKey]);
  main.Show();Application.DoEvents();
  var home=(PictureBox)typeof(MainForm).GetField("mainScreenPicture",instance)!.GetValue(main)!;
  using(var original=new Bitmap(Path.Combine(AppContext.BaseDirectory,"Assets","tela_principal.png")))using(var actual=new Bitmap(home.Image!)){
   bool changed=false;for(int y=0;y<original.Height;y+=3)for(int x=0;x<original.Width;x+=3){
    bool same=original.GetPixel(x,y)==actual.GetPixel(x,y);
    if(x>=70&&x<420&&y>=903&&y<935)changed|=!same;else CheckPixel(same);
   }
   Check(changed,"visible baked footer identifies establishment while all sampled pixels outside text area remain identical");
   actual.Save(Path.Combine(output,"home-art-"+plan+".png"));
  }
  var strips=Walk(main).OfType<StatusStrip>().ToArray();
  Check(main.Text.StartsWith("ANGRA VIDROS")&&main.Text.Contains("LEAL INFO CONECTADO"),"upper title identifies establishment and retains manufacturer");
  Check(strips.Any(s=>s.Items.Cast<ToolStripItem>().Any(i=>i.Text=="ANGRA VIDROS")),"main lower bar identifies ANGRA VIDROS");
  Check(strips.Any(s=>s.Items.Cast<ToolStripItem>().Any(i=>i.Name=="loja_pet_version"&&i.Text=="ANGRA VIDROS • PET V1.7")),"technical PET version accurately retained in lower bar");
  Check(!strips.SelectMany(s=>s.Items.Cast<ToolStripItem>()).Any(i=>i.Text.Contains("LOJA LEAL INFO")),"no former establishment identity in main lower bar");
  using(var image=new Bitmap(main.Width,main.Height)){main.DrawToBitmap(image,main.ClientRectangle);image.Save(Path.Combine(output,"main-"+plan+".png"));}
  Check(ReferenceEquals(licenseBefore,licenseType.GetProperty("Current",F)!.GetValue(null)),"license object unchanged by presentation");
  File.WriteAllText(Path.Combine(output,"texts-"+plan+".json"),JsonSerializer.Serialize(new{Pass=true,Identity="005",Version="1.7",Plan=plan,Title=main.Text,Status=strips.SelectMany(s=>s.Items.Cast<ToolStripItem>()).Select(i=>new{i.Name,i.Text}),ActualLicenseObjectUnchanged=ReferenceEquals(licenseBefore,licenseType.GetProperty("Current",F)!.GetValue(null)),CustomerMachineTested=false}));
  // Close test form without invoking unrelated operational shutdown/backup workflows.
  main.Dispose();return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
