using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Forms;

internal static class Program
{
    private sealed record Profile(string Channel,string File,string Sha256);
    private sealed record Mapping(Dictionary<string,Profile> Installed);
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            var root=AppContext.BaseDirectory;
            var original=Path.Combine(root,"LealInfoPDV.dll");
            if(!File.Exists(original))throw new FileNotFoundException("O programa instalado não foi localizado.",original);
            using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("DistributionMap")!;
            var mapping=JsonSerializer.Deserialize<Mapping>(stream)!;
            static string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();}
            var originalHash=Hash(original);
            var selected=original;var decision="ORIGINAL";
            if(mapping.Installed.TryGetValue(originalHash,out var profile))
            {
                var candidate=Path.Combine(root,profile.File);
                if(File.Exists(candidate)&&Hash(candidate)==profile.Sha256){selected=candidate;decision=profile.Channel;}
            }
            // Existing business DLL, deps, databases, licensing and settings are never replaced here.
            // The normal CLR loader runs the selected, byte-for-byte tested application.
            var resolver=new AssemblyDependencyResolver(original);
            AssemblyLoadContext.Default.Resolving+=(_,name)=>{
                var file=resolver.ResolveAssemblyToPath(name);
                return file==null?null:AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
            };
            AssemblyLoadContext.Default.ResolvingUnmanagedDll+=(_,name)=>{
                var file=resolver.ResolveUnmanagedDllToPath(name);
                return file==null?IntPtr.Zero:System.Runtime.InteropServices.NativeLibrary.Load(file);
            };
            var app=AssemblyLoadContext.Default.LoadFromAssemblyPath(selected);
            if(args.Length>0&&args[0]=="--distribution-probe")
            {
                if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")return 2;
                var updater=app.GetType("LealInfoPDV.UpdateManager",true)!;
                var flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static;
                if(args.Length==2)app.GetType("LealInfoPDV.Licensing.UpdatePayloadPolicy",true)!.GetMethod("Validate",flags)!.Invoke(null,new object[]{args[1]});
                var types=app.GetTypes();
                Console.WriteLine(JsonSerializer.Serialize(new{Decision=decision,Channel=updater.GetField("DistributionChannel",flags)?.GetRawConstantValue()??"LEGACY",Version=updater.GetField("CurrentVersion",flags)!.GetRawConstantValue(),OriginalSha256=originalHash,SelectedSha256=Hash(selected),HasMain=app.GetType("LealInfoPDV.MainForm")!=null,HasServiceNote=types.Any(t=>t.Name=="ServiceNoteForm"),HasParts=types.Any(t=>t.Name=="PartsWithdrawalForm"),EntryPoint=app.EntryPoint?.Name,STA=Thread.CurrentThread.GetApartmentState().ToString()}));
                return 0;
            }
            var entry=app.EntryPoint??throw new InvalidDataException("Programa sem ponto de entrada.");
            var result=entry.Invoke(null,entry.GetParameters().Length==0?null:new object[]{args});
            return result is int code?code:Environment.ExitCode;
        }
        catch(Exception e)
        {
            var message=(e is TargetInvocationException&&e.InnerException!=null?e.InnerException:e).Message;
            if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true")Console.Error.WriteLine(message);
            else MessageBox.Show(message,"LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Error);
            return 1;
        }
    }
}
