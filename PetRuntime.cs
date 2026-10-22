using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

namespace LealInfoPDV;

// Versioned extension surface for future client migration. GLOBAL owns this contract;
// actual client PETs and their migrations are intentionally outside this preparation.
public interface IPetExtension
{
    void Customize(Form form);
}

public interface IPetStartup { void Initialize(); }
public interface IPetReady { void ReportReady(); }

internal static class PetRuntime
{
    internal const int ApiVersion=1;
    private sealed record Manifest(string Identity,string Version,int ApiVersion,string Assembly,string Sha256);
    private static IPetExtension? extension;
    private static ConditionalWeakTable<Form,object> applied=new();
    internal static string? CurrentVersion { get; private set; }
    internal static void Initialize(string identity){
        PetBootstrap.EnsurePresent(AppContext.BaseDirectory,identity);
        Load(Path.Combine(AppContext.BaseDirectory,"Extensions","PET"),identity);
    }

    internal static void Load(string folder,string identity)
    {
        if(extension!=null)throw new InvalidOperationException("PET já carregado.");
        var path=Path.Combine(folder,"manifest.json");
        if(!File.Exists(path)){if(Directory.Exists(folder)&&Directory.EnumerateFileSystemEntries(folder).Any())throw new InvalidDataException("PET sem manifesto; revise a instalação local.");return;}
        if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0 || (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Pasta PET redirecionada.");
        var m=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path))??throw new InvalidDataException("Manifesto PET vazio.");
        if(string.IsNullOrWhiteSpace(identity)||m.Identity!=identity||m.ApiVersion!=ApiVersion||!System.Text.RegularExpressions.Regex.IsMatch(m.Version??"",@"\A1\.\d+(?:\.\d+)*\z"))throw new InvalidDataException("Identidade, versão ou contrato PET incompatível.");
        if(string.IsNullOrEmpty(m.Assembly)||m.Assembly!=Path.GetFileName(m.Assembly)||!m.Assembly.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)||m.Assembly.Contains(':')||m.Assembly.Contains('\\'))throw new InvalidDataException("Caminho PET inválido.");
        var assemblyPath=Path.Combine(folder,m.Assembly);
        if((File.GetAttributes(assemblyPath)&FileAttributes.ReparsePoint)!=0||m.Sha256?.Length!=64||!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))).Equals(m.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Integridade PET inválida.");
        var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
        var metadata=assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToArray();
        var declaredIdentity=metadata.SingleOrDefault(x=>x.Key=="PetIdentity")?.Value;
        var declaredVersion=metadata.SingleOrDefault(x=>x.Key=="PetVersion")?.Value;
        if((declaredIdentity!=null&&declaredIdentity!=identity)||(declaredVersion!=null&&declaredVersion!=m.Version))throw new InvalidDataException("Biblioteca PET não corresponde à identidade/versão do manifesto.");
        var types=assembly.GetExportedTypes().Where(t=>!t.IsAbstract&&typeof(IPetExtension).IsAssignableFrom(t)).ToArray();
        if(types.Length!=1)throw new InvalidDataException("PET deve fornecer uma única extensão compatível.");
        extension=(IPetExtension?)Activator.CreateInstance(types[0])??throw new InvalidDataException("PET não inicializado.");
        CurrentVersion=m.Version;
        if(extension is IPetStartup startup)startup.Initialize();
        Application.Idle+=Apply;
    }
    internal static void ReportReady(){if(extension is IPetReady ready)ready.ReportReady();}
    private static void Apply(object? sender,EventArgs args)
    {
        foreach(var form in Application.OpenForms.Cast<Form>().Where(f=>f.Visible&&!f.IsDisposed).ToArray())
        {
            if(applied.TryGetValue(form,out _))continue;
            // Apply once; a failing customization cannot repeatedly modify the form.
            applied.Add(form,new object());
            try{extension!.Customize(form);}
            catch(Exception e){Application.Idle-=Apply;MessageBox.Show(form,"Não foi possível aplicar a personalização local: "+e.Message,"PET",MessageBoxButtons.OK,MessageBoxIcon.Error);return;}
        }
    }
}
