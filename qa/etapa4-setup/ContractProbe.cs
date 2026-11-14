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
   Check((string?)Host.GetType("LealInfoPDV.PetRuntime")!.GetProperty("CurrentVersion",F)!.GetValue(null)=="1.3","installed PET 005 / 1.3 loads with all identity/API/hash/range guards enabled");
   var module=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="LealInfoPDV.Pet.Profile");
   Check(module.GetCustomAttributes<AssemblyMetadataAttribute>().Single(x=>x.Key=="PetIdentity").Value=="005","compiled PET metadata declares 005");
   if(Auth.UserCount()==0)Auth.CreateUser("QA","qa","qa-test-123!","ADMINISTRADOR","qa@example.invalid","");
   Check(Auth.Login("qa","qa-test-123!")!=null,"application authentication succeeds after licensed PET load");
   using(var login=new LoginForm()){
    login.Show();var deadline=DateTime.UtcNow.AddSeconds(3);while(DateTime.UtcNow<deadline){Application.DoEvents();Thread.Sleep(10);}
    Application.RaiseIdle(EventArgs.Empty);Application.DoEvents();Check(login.Visible,"native login rendered after licensed PET initialization");
    using var image=new System.Drawing.Bitmap(login.Width,login.Height);login.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,image.Width,image.Height));
    image.Save(Path.Combine(Environment.GetEnvironmentVariable("QA_EVIDENCE")!,"login-pet005.png"));login.Close();
   }
   File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("QA_EVIDENCE")!,"contract-005.json"),JsonSerializer.Serialize(new{Pass=true,Identity="005",Version="1.3",SignedFixture=true,ActualCustomerLicenseUploaded=false,CustomerMachineTested=false,LicenseGuardsEnabled=true,NativeLoginRendered=true}));
   return 0;
  }catch(Exception e){Console.Error.WriteLine(Unwrap(e));return 1;}
 }
}
