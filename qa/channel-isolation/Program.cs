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
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern IntPtr FindWindow(string? cls,string? title);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr h,int id);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
    static void RealConfirmation(string channel)
    {
        Exception? error=null;
        var thread=new Thread(()=>{
            try{
                Application.EnableVisualStyles();
                using var owner=new Form{Text="QA ISOLADO ATUALIZAR"};owner.Show();
                using var timer=new System.Windows.Forms.Timer{Interval=80};bool seen=false;int ticks=0;
                timer.Tick+=(_,_)=>{var h=FindWindow("#32770","Atualização disponível");if(h==IntPtr.Zero){if(++ticks>60){timer.Stop();throw new Exception("Confirmation did not open");}return;}seen=true;timer.Stop();SendMessage(GetDlgItem(h,7),0xF5,IntPtr.Zero,IntPtr.Zero);};
                timer.Start();
                var type=Updater.GetNestedType("UpdateManifest",BindingFlags.NonPublic)!;
                var manifest=JsonSerializer.Deserialize(Manifest("10.408",channel=="GLOBAL"?"":channel),type)!;
                var result=(DialogResult)Updater.GetMethod("ConfirmUpdate",S)!.Invoke(null,new[]{(object)owner,manifest})!;
                Check(seen&&result==DialogResult.No,"Real update confirmation did not work");owner.Close();
            }catch(Exception ex){error=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Check(thread.Join(15000),"Native confirmation timeout");if(error!=null)throw error;
        Console.WriteLine("PASS real native ATUALIZAR confirmation and cancel; no operational PDV started");
    }
    static async Task Main(string[] args)
    {
        Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Only isolated Windows CI");
        var channel=(string)Updater.GetField("DistributionChannel",S)!.GetRawConstantValue()!;
        var clients=channel=="GLOBAL"?new[]{"001","002","003","004"}:new[]{channel};
        foreach(var id in clients)
        {
            await Test("higher global must not override "+id,id,"10.406",Manifest("99.999"),Manifest("10.407",id),"10.407",id);
            await Test("current client blocks higher global "+id,id,"10.407",Manifest("99.999"),Manifest("10.407",id),null);
            await Test("older client feed blocks global "+id,id,"10.407",Manifest("99.999"),Manifest("10.385",id),null);
            await Test("missing client fails closed "+id,id,"10.406",Manifest("99.999"),null,null);
            await Test("wrong identity fails closed "+id,id,"10.406",Manifest("99.999"),Manifest("10.408","999"),null);
            await Test("malformed client fails closed "+id,id,"10.406",Manifest("99.999"),"{bad json",null);
            await Test("invalid URL fails closed "+id,id,"10.406",Manifest("99.999"),Manifest("10.408",id,"https://example.com/unsafe.zip"),null);
            await Test("invalid SHA fails closed "+id,id,"10.406",Manifest("99.999"),Manifest("10.408",id).Replace(new string('a',64),new string('x',64)),null);
            await Test("global offline client works "+id,id,"10.406",null,Manifest("10.407",id),"10.407",id);
        }
        if(channel=="GLOBAL")
        {
            await Test("neutral receives GLOBAL","","10.385",Manifest("10.407"),null,"10.407");
            await Test("ordinary client receives GLOBAL","005","10.385",Manifest("10.407"),null,"10.407");
            await Test("no downgrade neutral","","10.408",Manifest("10.407"),null,null);
            await Test("global wrong ID rejected","","10.385",Manifest("10.407","002"),null,null);
            await Test("future personalized feed remains independent","005","10.385",Manifest("99.999"),Manifest("10.407","005"),"10.407","005");
        }
        else await Test("client build refuses wrong license channel","999","10.385",Manifest("99.999"),Manifest("10.407","999"),null);
        RealConfirmation(channel);
        if(args.Length==2)ApplyTest(args[0],args[1]);
        Console.WriteLine("PASS CHANNEL ISOLATION "+channel);
    }
}
