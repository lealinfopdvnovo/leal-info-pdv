using System.Reflection;
using System.Net;
using System.Text.Json;
using System.Security.Cryptography;
using System.IO.Compression;
using LealInfoPDV;

static class Program
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static readonly MethodInfo Load=typeof(LoginForm).Assembly.GetType("LealInfoPDV.UpdateManager",true)!.GetMethod("LoadManifestAsync",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(HttpClient),typeof(string),typeof(string)},null)!;
    static object? Read(HttpClient http,string id,string current)
    {
        var t=(Task)Load.Invoke(null,new object[]{http,id,current})!;t.GetAwaiter().GetResult();return t.GetType().GetProperty("Result")!.GetValue(t);
    }
    static string Field(object m,string field)=>(string)m.GetType().GetProperty(field)!.GetValue(m)!;
    static string Manifest(string id,string version)=>JsonSerializer.Serialize(new{Version=version,TargetClientCode=id,Sha256=new string('a',64),PackageUrl=id==""?$"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v{version}/UPDATE.zip":$"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-{id}-v{version}/UPDATE.zip"});
    sealed class Feed(bool wrong=false):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken t)
        {
            var p=r.RequestUri!.AbsolutePath;string? body=p.EndsWith("/main/version.json")?Manifest("","10.374"):p.Contains("/001/")?Manifest(wrong?"003":"001","10.375"):p.Contains("/003/")?Manifest("003","10.375"):p.Contains("/002/")?Manifest("002","10.374"):null;
            return Task.FromResult(new HttpResponseMessage(body==null?HttpStatusCode.NotFound:HttpStatusCode.OK){Content=new StringContent(body??"not found")});
        }
    }
    static void Main(string[] args)
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Runner descartavel exigido");
        if(args.Length==2&&args[0]=="--live")
        {
            var id=args[1];Check(id is "001" or "003","ID nao autorizado");using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(45)};http.DefaultRequestHeaders.UserAgent.ParseAdd("PDV-QA-Directed/10.375");
            object? m=null;for(int i=0;i<8&&m==null;i++){m=Read(http,id,"10.374");if(m==null)Thread.Sleep(10000);}
            Check(m!=null&&Field(m,"Version")=="10.375"&&Field(m,"TargetClientCode")==id,"Feed direcionado real incorreto");
            var bytes=http.GetByteArrayAsync(Field(m!,"PackageUrl")).GetAwaiter().GetResult();Check(Convert.ToHexString(SHA256.HashData(bytes)).Equals(Field(m!,"Sha256"),StringComparison.OrdinalIgnoreCase),"Hash remoto incorreto");
            using var stream=new MemoryStream(bytes);using var zip=new ZipArchive(stream);
            Check(zip.Entries.Select(x=>x.FullName).Order().SequenceEqual(new[]{"LealInfoPDV.deps.json","LealInfoPDV.dll","LealInfoPDV.exe"}),"Pacote remoto inesperado");
            foreach(var excluded in new[]{"002","004",""})Check(Read(http,excluded,"10.374")==null,"Personalizacao vazou para "+excluded);
            Check(Read(http,id,"10.375")==null,"Ofertou mesma versao");
            Console.WriteLine("PASS REAL: "+id+" recebe 10.375; download SHA correto; 002/004/universal excluidos");
            return;
        }
        using var fake=new HttpClient(new Feed());
        foreach(var id in new[]{"001","003"}){var m=Read(fake,id,"10.374");Check(m!=null&&Field(m,"Version")=="10.375"&&Field(m,"TargetClientCode")==id,"Selecao incorreta");Check(Read(fake,id,"10.375")==null,"Mesmo build ofertado");Check(Read(fake,id,"10.376")==null,"Downgrade");}
        foreach(var id in new[]{"002","004",""})Check(Read(fake,id,"10.374")==null,"ID excluido recebeu cristal");
        using var wrong=new HttpClient(new Feed(true));Check(Read(wrong,"001","10.374")==null,"ID errado aceito");
        Console.WriteLine("PASS pre-publicacao: updater real seleciona somente 001/003; recusa 002, comum, global, troca de ID, mesma versao e downgrade");
    }
}
