using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LealInfoPDV;
using LealInfoPDV.Network;
static class Probe {
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 static Assembly Host=>typeof(LoginForm).Assembly;
 static object? Call(string type,string method,params object?[] args)=>Host.GetType("LealInfoPDV."+type,true)!.GetMethod(method,F)!.Invoke(null,args);
 static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control child in root.Controls)foreach(var c in Walk(child))yield return c;}
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
 [STAThread]static int Main(string[] args){try{
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||args.Length!=1)return 91;
  Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Call("QaIsolation","Initialize");
  if(args[0]=="offer"){
   var d=JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("TRANSITION_FEED")!)).RootElement;
   var release=new LealInfo.Components.Release(LealInfo.Components.ComponentKind.PET,"1.9","005",d.GetProperty("Sha256").GetString()!,d.GetProperty("PackageUrl").GetString()!,"10.468","10.469",1);
   Check(LealInfo.Components.IndependentUpdater.Plan(new("10.468","1.8","005","10.468","10.469",1),null,release).Available.Count==1,"unchanged updater offers 1.9 to QA005 on 1.8");
   var stage=LealInfo.Components.IndependentUpdater.StageAsync(release,"005",Path.Combine(Path.GetTempPath(),"spacing-stage-"+Guid.NewGuid()),(_,_)=>Task.FromResult(File.ReadAllBytes(Environment.GetEnvironmentVariable("TRANSITION_PACKAGE")!))).GetAwaiter().GetResult();
   Check(stage.Hashes.Count==2,"existing updater validates integrity and PET contract");return 0;
  }
  var plan=args[0];var serial=Database.DeviceSerial();using var rsa=RSA.Create(2048);
  var terms=new LicenseTerms(serial,2,"unico",null,"",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),Guid.NewGuid().ToString(),plan,"005",true,0);
  var raw=JsonSerializer.SerializeToUtf8Bytes(terms);var signed=JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(raw),Convert.ToBase64String(rsa.SignData(raw,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))));Call("Licensing.InstallationLicense","AcceptServer",signed,serial,rsa.ExportSubjectPublicKeyInfoPem());
  var currentBefore=Host.GetType("LealInfoPDV.Licensing.InstallationLicense",true)!.GetProperty("Current",F)!.GetValue(null);
  Database.Initialize();if(Auth.UserCount()==0)Auth.CreateUser("QA","qa","qa-test-123!","ADMINISTRADOR","qa@example.invalid","");
  foreach(var n in new[]{"opening-result.json","transition-result.json"})File.Delete(Path.Combine(QaIsolation.LocalRoot,n));
  Call("PetRuntime","Initialize","005");
  using var login=new LoginForm();using var observer=new System.Windows.Forms.Timer{Interval=100};
  var timeout=Stopwatch.StartNew();Exception? failure=null;bool done=false;var cases=new List<object>();
  observer.Tick+=(_,_)=>{
   try{
    if(timeout.Elapsed.TotalSeconds>60)throw new Exception("Login spacing deadline exceeded");
    var result=Path.Combine(QaIsolation.LocalRoot,"transition-result.json");if(!File.Exists(result))return;
    using var tr=JsonDocument.Parse(File.ReadAllText(result));if(!tr.RootElement.GetProperty("Pass").GetBoolean())return;
    observer.Stop();
    var controls=Walk(login).ToArray();
    var product=controls.OfType<Label>().Single(l=>l.Text=="PDV "+plan.ToUpperInvariant());
    var tagline=controls.OfType<Label>().Single(l=>l.Text=="TECNOLOGIA QUE CONECTA");
    var productFont=product.Font;var taglineFont=tagline.Font;var foreground=product.ForeColor;var background=product.BackColor;
    Check(controls.Any(c=>c.Text=="ANGRA")&&controls.Any(c=>c.Text=="VIDROS"),"approved ANGRA title retained");
    Check(controls.Any(c=>c.Text.StartsWith("LEAL INFO CONECTADO — ACESSO SEGURO")),"approved manufacturer footer retained");
    var output=Environment.GetEnvironmentVariable("TRANSITION_EVIDENCE")!;
    foreach(int width in new[]{1366,1920})foreach(float factor in new[]{1f,1.25f,1.5f}){
     // Controlled text scaling reproduces font growth without changing OS settings or shipped fonts.
     product.Font=new Font(productFont.FontFamily,productFont.Size*factor,productFont.Style,productFont.Unit);
     tagline.Font=new Font(taglineFont.FontFamily,taglineFont.Size*factor,taglineFont.Style,taglineFont.Unit);
     login.WindowState=FormWindowState.Normal;login.Bounds=new Rectangle(0,0,width,width==1366?768:1080);login.Width=width+1;login.Width=width;
     Pump(200);
     var other=controls.Where(c=>c!=product&&c!=tagline).Select(c=>(c,c.Bounds,c.Font,c.ForeColor,c.BackColor)).ToArray();
     var originalPlanTop=product.Top;var left=product.Left;var w=product.Width;
     // Exercise a late layout write like the cinematic/resize path in the owner recording.
     tagline.Top=product.Top;Pump(50);
     Check(other.All(x=>x.c.Bounds==x.Bounds&&x.c.Font==x.Font&&x.c.ForeColor==x.ForeColor&&x.c.BackColor==x.BackColor),"spacing changes no unrelated control geometry, font or color");
     int pHeight=TextRenderer.MeasureText(product.Text,product.Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding).Height;
     int tHeight=TextRenderer.MeasureText(tagline.Text,tagline.Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding).Height;
     Check(product.Height>=pHeight+8&&tagline.Height>=tHeight+8&&tagline.Top-product.Bottom>=4,"complete separate plan/tagline lines "+plan+" "+width+" "+factor);
     Check(product.Top==originalPlanTop&&product.Left==left&&product.Width==w&&product.ForeColor==foreground&&product.BackColor==background,"plan position, horizontal dimensions and colors retained");
     Check(product.TextAlign==ContentAlignment.MiddleCenter&&tagline.TextAlign==ContentAlignment.MiddleCenter&&Math.Abs((product.Left+product.Width/2.0)-(tagline.Left+tagline.Width/2.0))<1,"both lines remain centered");
     int percent=(int)Math.Round(factor*100);
     using var image=new Bitmap(login.Width,login.Height);login.DrawToBitmap(image,login.ClientRectangle);image.Save(Path.Combine(output,$"login-{plan}-{width}-{percent}.png"));
     cases.Add(new{Plan=plan,Width=width,Height=login.Height,SimulatedTextScale=percent,Product=new{product.Top,product.Height,MeasuredTextHeight=pHeight,product.Left,product.Width},Tagline=new{tagline.Top,tagline.Height,MeasuredTextHeight=tHeight},Gap=tagline.Top-product.Bottom});
    }
    Check(ReferenceEquals(currentBefore,Host.GetType("LealInfoPDV.Licensing.InstallationLicense",true)!.GetProperty("Current",F)!.GetValue(null)),"license object retained");
    File.WriteAllText(Path.Combine(output,"spacing-"+plan+".json"),JsonSerializer.Serialize(new{Pass=true,Identity="005",Version="1.9",Plan=plan,Cases=cases,RealWindowsForms=true,OperatingSystemDpiChanged=false,CustomerMachineTested=false}));done=true;login.Close();
   }catch(Exception ex){failure=ex;observer.Stop();login.Close();}
  };
  observer.Start();login.ShowDialog();if(failure!=null)throw failure;Check(done,"real runtime spacing validation completed for "+plan);return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
