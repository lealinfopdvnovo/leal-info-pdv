using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace LealInfoPDV;

// Executable recovery never loads the business DLL or opens an operational database.
internal static class TransactionalUpdate
{
    private static FileStream? applicationLease;
    private static string? StartupNonce()
    {
        var args=Environment.GetCommandLineArgs();
        return args.Length==3 && args[1]=="--update-transaction" ? args[2] : null;
    }
    internal static void BeforeStartup()
    {
        var guard=Path.Combine(AppContext.BaseDirectory,".pdv-update","startup.guard");
        if(File.Exists(guard))applicationLease=new FileStream(guard,FileMode.Open,FileAccess.Read,FileShare.Read);
        var journal=Path.Combine(AppContext.BaseDirectory,".pdv-update","journal.json");
        if(!File.Exists(journal))return;
        using var state=JsonDocument.Parse(File.ReadAllText(journal));
        var phase=state.RootElement.GetProperty("Phase").GetString();
        bool controlled=phase=="AwaitingHealth" && StartupNonce()==state.RootElement.GetProperty("Nonce").GetString();
        if(!controlled && phase is not ("Committed" or "Recovered" or "Prepared" or "Aborted"))
            throw new InvalidOperationException("A atualização está em recuperação automática. Aguarde a proteção externa concluir e abra o PDV novamente.");
    }
    internal static void ReportReady()
    {
        var nonce=StartupNonce();
        if(string.IsNullOrEmpty(nonce))return;
        var root=Path.Combine(AppContext.BaseDirectory,".pdv-update");
        using var state=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"journal.json")));
        if(state.RootElement.GetProperty("Phase").GetString()!="AwaitingHealth" || state.RootElement.GetProperty("Nonce").GetString()!=nonce)return;
        using var process=Process.GetCurrentProcess();
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new{Nonce=nonce,Version=UpdateManager.CurrentVersion,Pid=Environment.ProcessId,StartedTicks=process.StartTime.ToUniversalTime().Ticks});
        var temp=Path.Combine(root,"health.json.tmp");
        using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes);file.Flush(true);}
        File.Move(temp,Path.Combine(root,"health.json"),true);
    }
    internal static async Task BeginAsync(string package,string sha256,string version,string channel)
    {
        if(channel is not ("001" or "003"))throw new InvalidOperationException("Atualização não autorizada para este canal.");
        if(Application.OpenForms.Cast<Form>().Any(f=>f.Visible && f is not MainForm))
            throw new InvalidOperationException("Feche as telas de vendas e demais janelas antes de atualizar.");
        var windows=Application.OpenForms.OfType<MainForm>().Where(f=>f.Visible && f.Enabled).ToArray();
        foreach(var window in windows)window.Enabled=false;
        try
        {
        var app=AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var nonce=Guid.NewGuid().ToString("N");
        var temp=Path.Combine(Path.GetTempPath(),"LealInfoPDV_Transaction_"+nonce);Directory.CreateDirectory(temp);
        var source=Path.Combine(temp,"worker.ps1");
        using(var resource=typeof(TransactionalUpdate).Assembly.GetManifestResourceStream("TransactionalUpdateWorker") ?? throw new InvalidDataException("Proteção de atualização ausente."))
        using(var output=new FileStream(source,FileMode.CreateNew,FileAccess.Write,FileShare.None)){resource.CopyTo(output);output.Flush(true);}
        var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
        static string Q(string s)=>"'"+s.Replace("'","''")+"'";
        using var current=Process.GetCurrentProcess();
        // The elevated bootstrap checks immutable bytes before placing the worker in Program Files.
        var bootstrap=$$"""
$ErrorActionPreference='Stop'
trap { $_ | Out-String | Set-Content -LiteralPath {{Q(Path.Combine(temp,"bootstrap-error.log"))}}; exit 1 }
$src={{Q(source)}};$app={{Q(app)}}
$bytes=[IO.File]::ReadAllBytes($src);$sha=[Security.Cryptography.SHA256]::Create()
try{$h=[BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','')}finally{$sha.Dispose()}
if($h -ne '{{hash}}'){throw 'Worker SHA-256 mismatch'}
$allowed=[IO.Path]::GetFullPath($env:ProgramFiles).TrimEnd('\')+'\'
if(!$app.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Installation must be under protected Program Files'}
if(([IO.File]::GetAttributes($app) -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Reparse installation refused'}
$root=Join-Path $app '.pdv-update';New-Item -ItemType Directory $root -Force|Out-Null
if(([IO.File]::GetAttributes($root) -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Reparse recovery folder refused'}
if(@(Get-ChildItem -LiteralPath $root -Force -Recurse|Where-Object{($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}).Count -ne 0){throw 'Reparse transaction member refused'}
$acl=New-Object Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true,$false)
foreach($sid in @('S-1-5-18','S-1-5-32-544')){
 $identity=New-Object Security.Principal.SecurityIdentifier $sid
 $rule=New-Object Security.AccessControl.FileSystemAccessRule($identity,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
 $acl.AddAccessRule($rule)
}
$users=New-Object Security.Principal.SecurityIdentifier 'S-1-5-32-545'
$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($users,'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow')))
$acl.SetOwner((New-Object Security.Principal.SecurityIdentifier 'S-1-5-32-544'))
(Get-Item -LiteralPath $root).SetAccessControl($acl)
$dest=Join-Path $root ('entry-{{nonce}}.ps1')
$f=[IO.File]::Open($dest,'CreateNew','Write','None');try{$f.Write($bytes,0,$bytes.Length);$f.Flush($true)}finally{$f.Dispose()}
& $dest -Mode PrepareApply -App $app -Package {{Q(package)}} -Sha256 {{Q(sha256)}} -Version {{Q(version)}} -Channel {{Q(channel)}} -PreviousPid {{Environment.ProcessId}} -PreviousStarted {{current.StartTime.ToUniversalTime().Ticks}} -Nonce '{{nonce}}'
exit $LASTEXITCODE
""";
        var encoded=Convert.ToBase64String(Encoding.Unicode.GetBytes(bootstrap));
        using var worker=Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -EncodedCommand "+encoded){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden}) ?? throw new IOException("Proteção externa não iniciou.");
        var ready=Path.Combine(app,".pdv-update","prepared.ready");
        var deadline=DateTime.UtcNow.AddSeconds(60);
        while(DateTime.UtcNow<deadline)
        {
            if(worker.HasExited)
            {
                var error=Path.Combine(temp,"bootstrap-error.log");
                if(!File.Exists(error))error=Path.Combine(app,".pdv-update","recovery-error.log");
                var detail=File.Exists(error)?File.ReadAllText(error):"Aplicador encerrou sem confirmar a preparação.";
                throw new IOException("Atualização recusada antes da substituição: "+detail);
            }
            try{if(File.Exists(ready)&&File.ReadAllText(ready)==nonce){Application.Exit();return;}}catch(IOException){}
            await Task.Delay(200);
        }
        throw new TimeoutException("A proteção não confirmou backup e recuperação externa. O PDV permanece aberto e nenhum arquivo deve ser substituído enquanto estiver em execução.");
        }
        finally { foreach(var window in windows)if(!window.IsDisposed)window.Enabled=true; }
    }
}
