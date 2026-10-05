using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.IO.Compression;
using System.Diagnostics;
using System.Security.Cryptography;
using LealInfoPDV;

internal static class Program
{
    const BindingFlags S=BindingFlags.Static|BindingFlags.NonPublic;
    static readonly Assembly Pdv=typeof(Database).Assembly;
    static readonly Type Updater=Pdv.GetType("LealInfoPDV.UpdateManager",true)!;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static string Manifest(string version,string id="",string? url=null)=>JsonSerializer.Serialize(new {Version=version,TargetClientCode=id,Sha256=new string('a',64),PackageUrl=url??(id==""?$"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v{version}/UPDATE.zip":$"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-{id}-v{version}/UPDATE.zip")});
    sealed class Feed(string? global,string? targeted):HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken token)
        {
            Requests++;var body=req.RequestUri!.AbsolutePath.EndsWith("/main/version.json")?global:targeted;
            return Task.FromResult(new HttpResponseMessage(body==null?HttpStatusCode.NotFound:HttpStatusCode.OK){Content=new StringContent(body??"not found")});
        }
    }
    static async Task Test(string name,string client,string installed,string? global,string? targeted,string? expectedVersion,string expectedId="",int? requests=null)
    {
        using var feed=new Feed(global,targeted);using var http=new HttpClient(feed);
        var method=Updater.GetMethod("LoadManifestAsync",S,null,new[]{typeof(HttpClient),typeof(string),typeof(string)},null)!;
        var task=(Task)method.Invoke(null,new object[]{http,client,installed})!;await task;
        var result=task.GetType().GetProperty("Result")!.GetValue(task);
        if(expectedVersion==null)Check(result==null,"Manifesto não aplicável aceito: "+name);
        else
        {
            Check(result!=null,"Sem atualização: "+name);
            Check((string)result!.GetType().GetProperty("Version")!.GetValue(result)! ==expectedVersion,"Versão incorreta: "+name);
            Check((string)result.GetType().GetProperty("TargetClientCode")!.GetValue(result)! ==expectedId,"ID incorreto: "+name);
        }
        if(requests.HasValue)Check(feed.Requests==requests.Value,"Consulta global foi pulada: "+name);
        Console.WriteLine("PASS: "+name);
    }
    static void Policy(string zip)=>Pdv.GetType("LealInfoPDV.Licensing.UpdatePayloadPolicy",true)!.GetMethod("Validate",S)!.Invoke(null,new object[]{zip});
    static void ApplyTest(string zip,string source)
    {
        Policy(zip);
        var root=Path.Combine(Path.GetTempPath(),"pdv-global-preserve-"+Guid.NewGuid().ToString("N"));var app=Path.Combine(root,"app");var stage=Path.Combine(root,"stage");Directory.CreateDirectory(app);
        try
        {
            var names=new[]{"lealinfo.db","network.license","network.config","users.protected","client-code.protected","company-logo.png","speedfood-session.dat","Dados/empresa.json","Assets/logo-customizado-cliente.png"};
            var before=new Dictionary<string,string>();
            foreach(var name in names){var file=Path.Combine(app,name);Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllText(file,"QA_LOCAL_PRESERVAR_"+name);before[name]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));}
            var outside=Path.Combine(root,"AppData","company-logo.png");Directory.CreateDirectory(Path.GetDirectoryName(outside)!);File.WriteAllText(outside,"QA_LOGO_APPDATA");
            File.WriteAllText(Path.Combine(app,"LealInfoPDV.exe"),"QA_PROGRAMA_ANTERIOR");
            ZipFile.ExtractToDirectory(zip,stage);
            var text=File.ReadAllText(source);var start=text.IndexOf("# Copia somente arquivos aprovados pelo PDV.");var end=text.IndexOf("Start-Process -FilePath $exe",start);Check(start>=0&&end>start,"Script real de atualização não encontrado");
            var script="$ErrorActionPreference='Stop'\n$stage='"+stage.Replace("'","''")+"'\n$app='"+app.Replace("'","''")+"'\n"+text[start..end];
            var ps=Path.Combine(root,"apply-copy.ps1");File.WriteAllText(ps,script);
            using var p=Process.Start(new ProcessStartInfo("powershell.exe",$"-NoProfile -ExecutionPolicy Bypass -File \"{ps}\""){UseShellExecute=false})!;p.WaitForExit();Check(p.ExitCode==0,"Aplicação do bloco real falhou");
            foreach(var pair in before)Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(app,pair.Key))))==pair.Value,"Dado local alterado: "+pair.Key);
            Check(File.ReadAllText(outside)=="QA_LOGO_APPDATA","Logo externo alterado");
            Check(SHA256.HashData(File.ReadAllBytes(Path.Combine(app,"LealInfoPDV.exe"))).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(stage,"LealInfoPDV.exe")))),"Programa não foi atualizado");
            var bad=Path.Combine(root,"forbidden.zip");using(var a=ZipFile.Open(bad,ZipArchiveMode.Create)){using(var w=new StreamWriter(a.CreateEntry("LealInfoPDV.exe").Open()))w.Write("QA");using(var w=new StreamWriter(a.CreateEntry("lealinfo.db").Open()))w.Write("QA_LOCAL");}
            try{Policy(bad);throw new Exception("Banco em pacote foi aceito");}catch(TargetInvocationException e) when(e.InnerException is InvalidDataException){}
            Console.WriteLine("PASS: pacote real neutro, cópia real no Windows preservou banco/licença/configuração/identidade/logos/sessão; pacote com banco rejeitado");
        }
        finally{Directory.Delete(root,true);}
    }
    static async Task Main(string[] args)
    {
        await Test("A sem canal direcionado recebe global","","10.364",Manifest("10.371"),null,"10.371",requests:1);
        await Test("A ID comum sem manifesto recebe global","001","10.364",Manifest("10.371"),null,"10.371",requests:2);
        await Test("B 002 recebe pacote compatível na mesma versão","002","10.370",Manifest("10.371"),Manifest("10.371","002"),"10.371","002",2);
        await Test("C direcionada antiga mais nova que instalada não bloqueia global","002","10.369",Manifest("10.371"),Manifest("10.370","002"),"10.371",requests:2);
        await Test("C histórico direcionado igual à instalada não bloqueia global","002","10.370",Manifest("10.371"),Manifest("10.370","002"),"10.371",requests:2);
        await Test("D futura direcionada só para ID correspondente","003","10.371",Manifest("10.371"),Manifest("10.372","003"),"10.372","003",2);
        await Test("D manifesto de outro ID rejeitado","002","10.370",Manifest("10.371"),Manifest("10.372","003"),"10.371",requests:2);
        await Test("Global indisponível mantém canal direcionado","003","10.371",null,Manifest("10.372","003"),"10.372","003",2);
        await Test("Não regride instalada mais nova","002","10.373",Manifest("10.371"),Manifest("10.372","002"),null,requests:2);
        await Test("Global não pode conter target de outro cliente","001","10.364",Manifest("10.371","002"),null,null,requests:2);
        await Test("URL global inválida rejeitada","001","10.364",Manifest("10.371",url:"https://example.com/update.zip"),null,null,requests:2);
        if(args.Length==2)ApplyTest(args[0],args[1]);
    }
}
