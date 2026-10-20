using Protection003;
using LealInfoPDV.Network;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.IO.Compression;
static class Tests
{
    static readonly List<object> results=new();
    static Plan Production=>JsonSerializer.Deserialize<Plan>(Core.Resource("Plan"))!;
    static byte[] Package=>Core.Resource("Package");static byte[] Worker=>Core.Resource("Worker");
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    static string Quote(string s)=>"'"+s.Replace("'","''")+"'";
    static void License(string data,RSA rsa,string channel="003",string mode="unico",bool active=true,string serial="QA-SERVER"){
        var terms=new LicenseTerms(serial,2,mode,mode=="mensal"?DateTimeOffset.UtcNow.AddDays(-1):null,"",1,"QA-ONLY","pro",channel,active);
        byte[] payload=JsonSerializer.SerializeToUtf8Bytes(terms);var envelope=new SignedLicense(Convert.ToBase64String(payload),Convert.ToBase64String(rsa.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss)));
        ProtectedFile.Write(Path.Combine(data,"network.license"),new LicenseState{SignedLicense=JsonSerializer.Serialize(envelope)});
    }
    static Dictionary<string,string> DataHashes(string app)=>Directory.EnumerateFiles(Path.Combine(app,"data")).ToDictionary(p=>Path.GetFileName(p),Core.HashFile);
    static void Preserve(string app,Dictionary<string,string> before){Check(before.OrderBy(x=>x.Key).SequenceEqual(DataHashes(app).OrderBy(x=>x.Key)),"license data stock and configuration bytes unchanged");}
    static void Original(string app){foreach(var n in Core.Names)Check(Core.HashFile(Path.Combine(app,n))==Production.Previous[n],"original 10.415 hash "+n);}
    static string NewFixture(RSA rsa,bool programFiles=false){
        var app=Path.Combine(programFiles?Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles):Environment.GetEnvironmentVariable("RUNNER_TEMP")!,"activation-003-QA-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(app);
        foreach(var n in Core.Names)File.Copy(Path.Combine("previous",n),Path.Combine(app,n));File.WriteAllText(Path.Combine(app,"QA-ISOLATED"),"Synthetic CI fixture");
        string data=Path.Combine(app,"data");Directory.CreateDirectory(data);foreach(var n in new[]{"lealinfo.db","stock.dat","settings.json"})File.WriteAllText(Path.Combine(data,n),"UNCHANGED-SYNTHETIC-"+n);
        License(data,rsa);ProtectedFile.Write(Path.Combine(data,"network.config"),new {Mode="local"});File.WriteAllText(Path.Combine(app,"qa-public.pem"),rsa.ExportSubjectPublicKeyInfoPem());return app;
    }
    static Core Engine(string app,Plan? plan=null,byte[]? package=null,byte[]? worker=null,Action<string>? point=null)=>new(app,Path.Combine(app,"data"),plan??Production,package??Package,worker??Worker,"QA-SERVER",File.ReadAllText(Path.Combine(app,"qa-public.pem")),true,point);
    static async Task Reject(Func<Task> action,string name){bool rejected=false;try{await action();}catch{rejected=true;}Check(rejected,name);results.Add(new{scenario=name,pass=true});}
    static void Cleanup(string app){var e=Engine(app);Core.RunPs("Stop-ScheduledTask -TaskName "+Quote(e.TaskName)+" -ErrorAction SilentlyContinue;Unregister-ScheduledTask -TaskName "+Quote(e.TaskName)+" -Confirm:$false -ErrorAction SilentlyContinue;exit 0").GetAwaiter().GetResult();foreach(var p in Process.GetProcessesByName("LealInfoPDV"))using(p){if(p.MainModule?.FileName==Path.Combine(app,"LealInfoPDV.exe")){p.Kill();p.WaitForExit();}}Directory.Delete(app,true);}
    static async Task<string> Phase(string app,string[] expected){var until=DateTime.UtcNow.AddSeconds(45);string phase="";while(DateTime.UtcNow<until){try{using var j=JsonDocument.Parse(File.ReadAllText(Path.Combine(app,".pdv-update/journal.json")));phase=j.RootElement.GetProperty("Phase").GetString()!;if(expected.Contains(phase))return phase;}catch(IOException){}await Task.Delay(200);}Console.Error.WriteLine(await Core.RunPs("Get-ScheduledTask -TaskName "+Quote(Engine(app).TaskName)+"|Format-List *;Get-ScheduledTaskInfo -TaskName "+Quote(Engine(app).TaskName)+"|Format-List *;Get-CimInstance Win32_Process -Filter \"Name='powershell.exe'\"|Select-Object ProcessId,CommandLine|Format-List"));foreach(var f in Directory.GetFiles(Path.Combine(app,".pdv-update"),"*.log"))Console.Error.WriteLine(f+"\n"+File.ReadAllText(f));throw new Exception("State timeout "+phase);}
    static Process Delay(){return Core.StartPs("Start-Sleep -Seconds 45",false);}
    [STAThread] static async Task<int> Main(string[] args){
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||!OperatingSystem.IsWindows())throw new Exception("Windows disposable CI only");
        if(args.Length==3&&args[0]=="--fault"){await Engine(args[1],point:p=>{if(p==args[2])Environment.Exit(73);}).Prepare();return 0;}
        try{
            using var rsa=RSA.Create(2048);
            if(args.Contains("--transition-only"))goto transitions;
            foreach(var channel in new[]{"001","002","004","GLOBAL"}){var app=NewFixture(rsa);try{License(Path.Combine(app,"data"),rsa,channel);var before=DataHashes(app);await Reject(()=>Engine(app).Prepare(),"reject-client-"+channel);Original(app);Preserve(app,before);Check(!Directory.Exists(Path.Combine(app,".pdv-update")),"rejected client creates no recovery files");}finally{Cleanup(app);}}
            foreach(var reason in new[]{"inactive","expired","wrong-serial","terminal","bad-signature","bad-package","bad-worker","bad-installed-file"}){var app=NewFixture(rsa);try{
                var data=Path.Combine(app,"data");if(reason=="inactive")License(data,rsa,active:false);if(reason=="expired")License(data,rsa,mode:"mensal");if(reason=="wrong-serial")License(data,rsa,serial:"OTHER");if(reason=="terminal")ProtectedFile.Write(Path.Combine(data,"network.config"),new{Mode="terminal"});if(reason=="bad-signature"){using var other=RSA.Create(2048);License(data,other);}if(reason=="bad-installed-file")File.AppendAllText(Path.Combine(app,"LealInfoPDV.dll"),"TAMPER");
                var before=DataHashes(app);await Reject(()=>Engine(app,package:reason=="bad-package"?new byte[]{1}:null,worker:reason=="bad-worker"?new byte[]{1}:null).Prepare(),reason);Preserve(app,before);Check(!Directory.Exists(Path.Combine(app,".pdv-update")),"invalid identity or integrity rejected before activation");
            }finally{Cleanup(app);}}
            foreach(var point in new[]{"validated","worker","package","prepared","proof"}){var app=NewFixture(rsa);try{var before=DataHashes(app);var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};start.ArgumentList.Add("--fault");start.ArgumentList.Add(app);start.ArgumentList.Add(point);using var child=Process.Start(start)!;using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(100));await child.WaitForExitAsync(timeout.Token);Check(child.ExitCode==73,"real activator process terminated at "+point);Original(app);Preserve(app,before);
                if(point is "prepared" or "proof"){await Core.RunPs("Start-ScheduledTask -TaskName "+Quote(Engine(app).TaskName));Check(await Phase(app,new[]{"Aborted"})=="Aborted","SYSTEM recovery safely aborts interrupted preparation");Original(app);}results.Add(new{scenario="activator-exit-"+point,pass=true});
            }finally{Cleanup(app);}}
            foreach(var failure in new[]{"disabled-task","corrupt-backup","corrupt-staging"}){var app=NewFixture(rsa);try{var e=Engine(app);await e.Prepare();if(failure=="disabled-task")await Core.RunPs("Disable-ScheduledTask -TaskName "+Quote(e.TaskName));else File.AppendAllText(Path.Combine(e.Root,failure=="corrupt-backup"?"previous/LealInfoPDV.dll":"stage/LealInfoPDV.dll"),"TAMPER");await Reject(()=>e.VerifyProtection(),failure);Original(app);}finally{Cleanup(app);}}
            transitions:
            foreach(var point in new[]{"hidden","copy-LealInfoPDV.dll"}){var app=NewFixture(rsa);try{var before=DataHashes(app);var e=Engine(app);await e.Prepare();using var predecessor=Delay();await e.Handoff(predecessor.Id,predecessor.StartTime.ToUniversalTime().Ticks,point);Original(app);predecessor.Kill();predecessor.WaitForExit();await Phase(app,new[]{"Applying"});try{using var worker=Process.GetProcessById(e.LaunchedWorkerId);Check(worker.WaitForExit(15000)&&worker.ExitCode==73,"faulted external worker really exited before recovery trigger");}catch(ArgumentException){}await Core.RunPs("Start-ScheduledTask -TaskName "+Quote(e.TaskName));Check(await Phase(app,new[]{"Recovered"})=="Recovered","first external activation recovers after real worker exit "+point);Original(app);Preserve(app,before);results.Add(new{scenario="first-transition-"+point,pass=true});}finally{Cleanup(app);}}
            foreach(bool badHost in new[]{false,true}){var app=NewFixture(rsa,true);try{
                foreach(var f in Directory.GetFiles("startup-fixture-publish")){string n=Path.GetFileName(f);if(!Core.Names.Contains(n))File.Copy(f,Path.Combine(app,n),true);}
                string zip=Path.Combine(app,"synthetic.zip");using(var z=ZipFile.Open(zip,ZipArchiveMode.Create))foreach(var n in Core.Names){var entry=z.CreateEntry(n);using var w=entry.Open();byte[] bytes=badHost&&n=="LealInfoPDV.exe"?new byte[]{1,2,3}:File.ReadAllBytes(Path.Combine("startup-fixture-publish",n));w.Write(bytes);}
                byte[] pkg=File.ReadAllBytes(zip);Dictionary<string,string> target;using(var z=ZipFile.OpenRead(zip))target=z.Entries.ToDictionary(x=>x.FullName,x=>{using var s=x.Open();return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();});
                var plan=Production with{PackageSha256=Core.Hash(pkg),Target=target};var before=DataHashes(app);var e=Engine(app,plan,pkg);await e.Prepare();Original(app);Preserve(app,before);using var predecessor=Delay();await e.Handoff(predecessor.Id,predecessor.StartTime.ToUniversalTime().Ticks);Original(app);predecessor.Kill();predecessor.WaitForExit();string expected=badHost?"Recovered":"Committed";Check(await Phase(app,new[]{expected})==expected,"protected Program Files first activation "+expected);if(badHost)Original(app);else foreach(var n in Core.Names)Check(Core.HashFile(Path.Combine(app,n))==target[n],"complete healthy synthetic target "+n);Preserve(app,before);results.Add(new{scenario=badHost?"first-transition-startup-failure-automatic-rollback":"first-transition-protected-program-files-synthetic-health",pass=true,fullPdvHomologation=false});
            }finally{Cleanup(app);}}
            Directory.CreateDirectory("activation-evidence");File.WriteAllText("activation-evidence/results.json",JsonSerializer.Serialize(new{success=true,SourceCommit=Environment.GetEnvironmentVariable("GITHUB_SHA"),PreservedCustomerProfiles=true,Scenarios=results,RealCustomerActivation=false,PhysicalPowerCutTested=false},new JsonSerializerOptions{WriteIndented=true}));return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);Directory.CreateDirectory("activation-evidence");File.WriteAllText("activation-evidence/failure.json",JsonSerializer.Serialize(new{error=ex.ToString(),Scenarios=results}));return 1;}
    }
}
