using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using LealInfoPDV.Network;
using Microsoft.Win32;
[assembly:System.Runtime.CompilerServices.InternalsVisibleTo("Protection003.Tests")]
namespace Protection003;
internal sealed record Plan(string Channel,string Version,string SourceCommit,string PackageSha256,string WorkerSha256,Dictionary<string,string> Previous,Dictionary<string,string> Target);
internal sealed class Core
{
    internal static readonly string[] Names={"LealInfoPDV.deps.json","LealInfoPDV.dll","LealInfoPDV.exe"};
    internal readonly string App,Data,Root;
    readonly Plan plan;readonly byte[] package,worker;readonly string serial;readonly string? key;readonly bool fixture;
    readonly Action<string>? point;readonly string bundleId=Guid.NewGuid().ToString("N");
    internal Core(string app,string data,Plan plan,byte[] package,byte[] worker,string serial,string? key=null,bool fixture=false,Action<string>? point=null)
    {App=Path.GetFullPath(app).TrimEnd('\\');Data=Path.GetFullPath(data);Root=Path.Combine(App,".pdv-update");this.plan=plan;this.package=package;this.worker=worker;this.serial=serial;this.key=key;this.fixture=fixture;this.point=point;}
    internal static byte[] Resource(string name){using var s=typeof(Core).Assembly.GetManifestResourceStream(name)??throw new IOException("Recurso ausente: "+name);using var m=new MemoryStream();s.CopyTo(m);return m.ToArray();}
    internal static string Hash(byte[] data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    internal static string HashFile(string path){using var s=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();}
    internal static string Serial(){using var k=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");var h=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("LEALINFO|"+(k?.GetValue("MachineGuid")?.ToString()??Environment.MachineName))));return $"LI-{h[..4]}-{h[4..8]}-{h[8..12]}-{h[12..16]}";}
    internal static Core Installed()=>new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"LEAL INFO PDV"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LealInfoPDV"),JsonSerializer.Deserialize<Plan>(Resource("Plan"))!,Resource("Package"),Resource("Worker"),Serial());
    static void Need(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    internal static void NoReparse(string path)
    {
        string? p=Path.GetFullPath(path);
        while(p!=null){if(File.Exists(p)||Directory.Exists(p))Need((File.GetAttributes(p)&FileAttributes.ReparsePoint)==0,"Caminho redirecionado recusado.");p=Path.GetDirectoryName(p);}
    }
    internal void Validate()
    {
        Need(plan.Channel=="003"&&plan.Version=="10.422"&&plan.SourceCommit=="be3d36780eec0bf90aef7666ac5df158115a8e99","Plano não autorizado para o 003.");
        Need(Names.Order().SequenceEqual(plan.Previous.Keys.Order())&&Names.Order().SequenceEqual(plan.Target.Keys.Order()),"Plano de arquivos inválido.");
        Need(Hash(package)==plan.PackageSha256&&Hash(worker)==plan.WorkerSha256,"Integridade do ativador inválida.");
        if(fixture){Need(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true"&&Path.GetFileName(App).StartsWith("activation-003-QA-")&&File.Exists(Path.Combine(App,"QA-ISOLATED")),"Fixture recusada.");}
        else Need(string.Equals(App,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"LEAL INFO PDV"),StringComparison.OrdinalIgnoreCase),"Instalação esperada em Program Files não encontrada.");
        NoReparse(App);NoReparse(Data);NoReparse(Root);
        if(Directory.Exists(Root)){
            var pending=new Stack<string>();pending.Push(Root);
            while(pending.Count>0)foreach(var p in Directory.EnumerateFileSystemEntries(pending.Pop())){NoReparse(p);if(Directory.Exists(p))pending.Push(p);}
            if(Directory.EnumerateFileSystemEntries(Root).Any()){
                var acl=new DirectoryInfo(Root).GetAccessControl();
                Need(acl.AreAccessRulesProtected&&acl.GetOwner(typeof(SecurityIdentifier)).Value=="S-1-5-32-544","Pasta de proteção existente sem ACL confiável. Nenhum arquivo foi sobrescrito.");
                foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))
                    if(rule.AccessControlType==AccessControlType.Allow&&(rule.FileSystemRights&(FileSystemRights.Write|FileSystemRights.Delete|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership))!=0)
                        Need(rule.IdentityReference.Value is "S-1-5-18" or "S-1-5-32-544","Pasta de recuperação permite escrita não autorizada.");
            }
        }
        Need(File.Exists(Path.Combine(Data,"lealinfo.db")),"Dados existentes não encontrados. Use a mesma conta Windows do PDV.");
        string license=Path.Combine(Data,"network.license");NoReparse(license);NoReparse(Path.Combine(Data,"network.config"));Need(File.Exists(license),"Licença existente não encontrada. Use a mesma conta Windows do PDV.");
        var licence=new NetworkLicense(license,serial,key);licence.CheckAccessReadOnly();
        Need(licence.Terms.ClientCode=="003"&&licence.Terms.Revision>=1,"Licença assinada e ativa do cliente 003 obrigatória.");
        var config=Path.Combine(Data,"network.config");
        if(File.Exists(config))Need(ProtectedFile.Read<JsonElement>(config).GetProperty("Mode").GetString()!="terminal","Ative somente no servidor 003, com os terminais fechados.");
        foreach(var n in Names){NoReparse(Path.Combine(App,n));Need(HashFile(Path.Combine(App,n))==plan.Previous[n],"Arquivo instalado difere da 10.415 aprovada: "+n);}
        foreach(var p in Process.GetProcessesByName("LealInfoPDV"))using(p)Need(p.MainModule?.FileName is string f&&!string.Equals(Path.GetFullPath(f),Path.Combine(App,"LealInfoPDV.exe"),StringComparison.OrdinalIgnoreCase),"Feche o PDV e os terminais antes da ativação.");
        string journal=Path.Combine(Root,"journal.json");if(File.Exists(journal)){using var j=JsonDocument.Parse(File.ReadAllText(journal));Need(new[]{"Prepared","Aborted","Recovered","Committed"}.Contains(j.RootElement.GetProperty("Phase").GetString()),"Há uma recuperação pendente. Nenhuma cópia anterior será substituída.");}
    }
    static void Durable(string file,byte[] data){using var s=new FileStream(file,FileMode.Create,FileAccess.Write,FileShare.None);s.Write(data);s.Flush(true);}
    internal string Bundle=>Path.Combine(Root,"activation003-"+bundleId);
    internal string WorkerPath=>Path.Combine(Bundle,"worker.ps1");
    internal string PackagePath=>Path.Combine(Bundle,"package.zip");
    internal int LaunchedWorkerId { get; private set; }
    internal string TaskName=>"LealInfoPDV-Recovery-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(App.ToUpperInvariant())))[..16];
    static string PS=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
    static string Q(string s)=>"'"+s.Replace("'","''")+"'";
    internal static Process StartPs(string script,bool redirect=true)
    {script="$env:PSModulePath=(Join-Path $PSHOME 'Modules')+';'+(Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/Modules')\n"+script;var p=new ProcessStartInfo(PS,"-NoProfile -ExecutionPolicy Bypass -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=redirect,RedirectStandardError=redirect};return Process.Start(p)??throw new IOException("Proteção externa não iniciou.");}
    internal static async Task<string> RunPs(string script)
    {using var p=StartPs(script);var a=p.StandardOutput.ReadToEndAsync();var b=p.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));try{await p.WaitForExitAsync(timeout.Token);}catch{p.Kill(true);throw;}string output=await a,error=await b;if(p.ExitCode!=0)throw new IOException("Proteção recusada: "+error+output);return output;}
    string Args(string mode)=>"& "+Q(WorkerPath)+" -Mode "+mode+" -App "+Q(App)+" -Channel 003 -Version 10.422 -Package "+Q(PackagePath)+" -Sha256 "+Q(plan.PackageSha256);
    internal async Task Prepare()
    {
        Validate();point?.Invoke("validated");
        Directory.CreateDirectory(Root);
        var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);acl.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));
        foreach(var sid in new[]{WellKnownSidType.LocalSystemSid,WellKnownSidType.BuiltinAdministratorsSid})acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid,null),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null),FileSystemRights.ReadAndExecute,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        new DirectoryInfo(Root).SetAccessControl(acl);
        Directory.CreateDirectory(Bundle);Durable(WorkerPath,worker);point?.Invoke("worker");Durable(PackagePath,package);point?.Invoke("package");
        await RunPs(Args("Prepare")+"\nexit $LASTEXITCODE");point?.Invoke("prepared");
        await VerifyProtection();point?.Invoke("proof");
    }
    internal async Task VerifyProtection()
    {
        Need(HashFile(WorkerPath)==plan.WorkerSha256&&HashFile(PackagePath)==plan.PackageSha256,"Recursos protegidos foram adulterados.");
        Need(HashFile(Path.Combine(Root,"worker.ps1"))==plan.WorkerSha256,"Worker da recuperação difere do aprovado.");
        using var j=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"journal.json")));var s=j.RootElement;
        Need(s.GetProperty("App").GetString()==App&&s.GetProperty("Channel").GetString()=="003"&&s.GetProperty("Phase").GetString()=="Prepared","Preparação não está ativa. Clique em verificar novamente.");
        foreach(var n in Names){Need(s.GetProperty("Previous").GetProperty(n).GetString()==plan.Previous[n]&&HashFile(Path.Combine(Root,"previous",n))==plan.Previous[n],"Backup não confere: "+n);Need(s.GetProperty("Target").GetProperty(n).GetString()==plan.Target[n]&&HashFile(Path.Combine(Root,"stage",n))==plan.Target[n],"Staging não confere: "+n);}
        var taskProof=await RunPs("$ErrorActionPreference='Stop';$t=Get-ScheduledTask -TaskName "+Q(TaskName)+";$p=[string]$t.Principal.UserId;if($p -notmatch '^S-1-'){$p=(New-Object Security.Principal.NTAccount $p).Translate([Security.Principal.SecurityIdentifier]).Value};if($p -ne 'S-1-5-18' -or $t.State -eq 'Disabled' -or $t.Triggers.Count -lt 2 -or $t.Actions[0].Arguments -notlike "+Q("*"+Path.Combine(Root,"worker.ps1")+"*")+"){throw 'Tarefa de recuperacao nao confirmada'};'SYSTEM-RECOVERY-VERIFIED'");
        Need(taskProof.Contains("SYSTEM-RECOVERY-VERIFIED")&&new DirectoryInfo(Root).GetAccessControl().AreAccessRulesProtected,"Proteção externa/ACL não confirmada.");
        Durable(Path.Combine(Bundle,"protection-proof.json"),JsonSerializer.SerializeToUtf8Bytes(new{Channel="003",BaselineVersion="10.415",TargetVersion="10.422",plan.SourceCommit,plan.PackageSha256,plan.WorkerSha256,plan.Previous,plan.Target,App,TaskName,RecoveryPrincipal="S-1-5-18",BackupVerified=true,StagingVerified=true,ExecutableFilesUnchanged=Names.All(n=>HashFile(Path.Combine(App,n))==plan.Previous[n]),IsolatedFixture=fixture,CreatedUtc=DateTimeOffset.UtcNow}));
    }
    // No legacy apply-update.ps1 is launched. The already tested protected worker owns the first copy.
    internal async Task<string> Handoff(int pid,long started,string fault="")
    {
        Validate();await VerifyProtection();var nonce=Guid.NewGuid().ToString("N");
        Need(fault==""||fixture,"Falha de teste recusada.");
        var script=Args("PrepareApply")+" -PreviousPid "+pid+" -PreviousStarted "+started+" -Nonce "+Q(nonce)+(fault==""?"":" -Fault "+Q(fault))+"\nexit $LASTEXITCODE";
        using var p=StartPs(script,false);LaunchedWorkerId=p.Id;var until=DateTime.UtcNow.AddSeconds(65);
        while(DateTime.UtcNow<until){if(p.HasExited)throw new IOException("Aplicador encerrou antes da confirmação. O PDV não foi autorizado a trocar arquivos.");try{if(File.ReadAllText(Path.Combine(Root,"prepared.ready"))==nonce){await VerifyProtection();return nonce;}}catch(FileNotFoundException){}await Task.Delay(150);}
        throw new TimeoutException("A proteção não confirmou a preparação. Nenhuma troca foi autorizada.");
    }
}
