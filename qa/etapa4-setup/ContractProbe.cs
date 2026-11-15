using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LealInfoPDV;
using LealInfoPDV.Network;

// Disposable runner-only probe. No key, test license or probe is included in the installer.
static class ContractProbe
{
 const BindingFlags F=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static Assembly Host=>typeof(IPetExtension).Assembly;
 static object? Call(string type,string method,params object?[] values)=>Host.GetType("LealInfoPDV."+type,true)!.GetMethod(method,F)!.Invoke(null,values);
 static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static Exception Unwrap(Exception e)=>e is TargetInvocationException t&&t.InnerException!=null?Unwrap(t.InnerException):e;
 static void Reject(Action action,string message){try{action();}catch(Exception e){Console.WriteLine("PASS rejected "+message+": "+Unwrap(e).Message);return;}throw new Exception("Accepted "+message);}
 static IEnumerable<Control> Walk(Control root){yield return root;foreach(Control c in root.Controls)foreach(var n in Walk(c))yield return n;}
 static void Planner(string folder){
  var feed=JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("QA_FEED")!)).RootElement;
  var release=new LealInfo.Components.Release(LealInfo.Components.ComponentKind.PET,feed.GetProperty("Version").GetString()!,"005",feed.GetProperty("Sha256").GetString()!,feed.GetProperty("PackageUrl").GetString()!,"10.468","10.469",1);
  Check(LealInfo.Components.IndependentUpdater.Plan(new("10.468","1.3","005","10.468","10.469",1),null,release).Available.Count==1,"same update planner used by ATUALIZAR detects 005 PET 1.4");
  foreach(var id in new[]{"001","002","003","004"})Check(LealInfo.Components.IndependentUpdater.Plan(new("10.468","1.3",id),null,release).Available.Count==0,"QA patch unavailable to "+id);
  Reject(()=>LealInfo.Components.IndependentUpdater.ValidateManifest(release with {PackageUrl="https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-005-pet-v1.4/update.zip"},"005"),"production package URL");
  var staged=LealInfo.Components.IndependentUpdater.StageAsync(release,"005",Path.Combine(Path.GetTempPath(),"angra-stage-"+Guid.NewGuid()),(_,_)=>Task.FromResult(File.ReadAllBytes(Environment.GetEnvironmentVariable("QA_PACKAGE")!))).GetAwaiter().GetResult();
  Check(staged.Hashes.Count==2,"actual PET package passes integrity/contract staging");
 }
 [STAThread] static int Main(string[] args)
 {
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||args.Length!=1)return 91;
  try{
   Application.EnableVisualStyles();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
   Call("QaIsolation","Initialize");
   var folder=Path.Combine(AppContext.BaseDirectory,"Extensions","PET");
   var serial=Database.DeviceSerial();using var rsa=RSA.Create(2048);var pub=rsa.ExportSubjectPublicKeyInfoPem();
   var terms=new LicenseTerms(serial,2,"unico",null,"",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),Guid.NewGuid().ToString(),"standard","005",true,0);
   var bytes=JsonSerializer.SerializeToUtf8Bytes(terms);
   var signed=JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(bytes),Convert.ToBase64String(rsa.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))));
   // Exercise production signature, serial, activation and DPAPI persistence routines using an ephemeral issuer fixture.
   var state=Path.Combine(Path.GetTempPath(),"qa-license-probe-"+Guid.NewGuid()+".license");
   try{
    var store=new NetworkLicense(state,serial,pub);
    typeof(NetworkLicense).GetMethod("Activate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(store,new object[]{signed});
    store.CheckAccess();Check(store.Terms.ClientCode=="005"&&store.Terms.Active,"signed Standard 005 fixture activated and persisted");
    Reject(()=>Call("Licensing.InstallationLicense","AcceptServer",signed,"WRONG-SERIAL",pub),"wrong hardware serial");
    using(var wrong=RSA.Create(2048))Reject(()=>Call("Licensing.InstallationLicense","AcceptServer",signed,serial,wrong.ExportSubjectPublicKeyInfoPem()),"wrong signing key");
    Call("Licensing.InstallationLicense","AcceptServer",signed,serial,pub);
   }finally{File.Delete(state);}
   var mode=args[0];
   if(mode=="planner"){Planner(folder);return 0;}
   if(mode!="success"){
    var path=Path.Combine(folder,"manifest.json");var original=File.ReadAllBytes(path);
    try{
     var manifest=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(original)!;
     if(mode=="wrong-api")manifest["ApiVersion"]=JsonSerializer.SerializeToElement(2);
     else if(mode=="bad-hash")manifest["Sha256"]=JsonSerializer.SerializeToElement(new string('0',64));
     else if(mode=="wrong-module-identity")manifest["Identity"]=JsonSerializer.SerializeToElement("001");
     else if(mode!="old-license-identity")throw new Exception("Unknown test");
     File.WriteAllText(path,JsonSerializer.Serialize(manifest));
     var id=mode is "old-license-identity" or "wrong-module-identity"?"001":"005";
     Reject(()=>Call("PetRuntime","Load",folder,id),mode);
    }finally{File.WriteAllBytes(path,original);}
    return 0;
   }
   Database.Initialize();
   Call("PetRuntime","Initialize","005");
   Check((string?)Host.GetType("LealInfoPDV.PetRuntime")!.GetProperty("CurrentVersion",F)!.GetValue(null)=="1.4","installed PET 005 / 1.3 loads with all identity/API/hash/range guards enabled");
   var module=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="LealInfoPDV.Pet.Profile");
   Check(module.GetCustomAttributes<AssemblyMetadataAttribute>().Single(x=>x.Key=="PetIdentity").Value=="005","compiled PET metadata declares 005");
   if(Auth.UserCount()==0)Auth.CreateUser("QA","qa","qa-test-123!","ADMINISTRADOR","qa@example.invalid","");
   Check(Auth.Login("qa","qa-test-123!")!=null,"application authentication succeeds after licensed PET load");
   var opening=JsonDocument.Parse(File.ReadAllText(Path.Combine(QaIsolation.LocalRoot,"opening-result.json"))).RootElement;
   Check(opening.GetProperty("Played").GetBoolean()&&opening.GetProperty("Ended").GetBoolean()&&opening.GetProperty("LastSeconds").GetDouble()>7,"offline video played with audible flag and ended before login");
   File.Copy(Path.Combine(QaIsolation.LocalRoot,"opening-result.json"),Path.Combine(Environment.GetEnvironmentVariable("QA_EVIDENCE")!,"opening-result.json"),true);
   using(var login=new LoginForm()){
    login.Show();var deadline=DateTime.UtcNow.AddSeconds(3);while(DateTime.UtcNow<deadline){Application.DoEvents();Thread.Sleep(10);}
    Application.RaiseIdle(EventArgs.Empty);Application.DoEvents();Check(login.Visible,"native login rendered after licensed PET initialization");
    var labels=Walk(login).Select(c=>c.Text).ToArray();Check(labels.Contains("ANGRA")&&labels.Contains("VIDROS")&&!labels.Contains("CONECTADO"),"login displays ANGRA VIDROS");
    using var image=new System.Drawing.Bitmap(login.Width,login.Height);login.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));
    image.Save(Path.Combine(Environment.GetEnvironmentVariable("QA_EVIDENCE")!,"login-pet005.png"));login.Close();
   }
   using(var main=new MainForm()){
    // The fixture license remains loaded through the supported network-license verifier.
    main.Show();Application.RaiseIdle(EventArgs.Empty);Application.DoEvents();
    var statuses=Walk(main).OfType<StatusStrip>().SelectMany(c=>c.Items.Cast<ToolStripItem>()).Select(c=>c.Text).ToArray();
    Check(main.Text.Contains("ANGRA VIDROS")&&statuses.Any(c=>c.Contains("ANGRA VIDROS • PET V1.4")),"main header and footer display ANGRA VIDROS and technical PET 1.4");
    using var image=new System.Drawing.Bitmap(main.Width,main.Height);main.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(Environment.GetEnvironmentVariable("QA_EVIDENCE")!,"main-pet005.png"));
    // Dispose avoids invoking user-confirmation or backup dialogs in this fixture.
   }
   File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("QA_EVIDENCE")!,"contract-005.json"),JsonSerializer.Serialize(new{Pass=true,Identity="005",Version="1.4",SignedFixture=true,ActualCustomerLicenseUploaded=false,CustomerMachineTested=false,LicenseGuardsEnabled=true,NativeLoginRendered=true}));
   return 0;
  }catch(Exception e){Console.Error.WriteLine(Unwrap(e));return 1;}
 }
}
