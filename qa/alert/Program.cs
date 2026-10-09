using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Text;
using LealInfoPDV;

static class Program
{
 const BindingFlags S=BindingFlags.Static|BindingFlags.NonPublic;
 static readonly Type U=typeof(Database).Assembly.GetType("LealInfoPDV.UpdateManager",true)!;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static string M(string v,string id="")=>JsonSerializer.Serialize(new{Version=v,TargetClientCode=id,Sha256=new string('a',64),PackageUrl=id==""?$"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v{v}/UPDATE.zip":$"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-{id}-v{v}/UPDATE.zip"});
 sealed class Feed(string? global,string? target,bool transient=false,bool slowTarget=false):HttpMessageHandler
 {
  public int G,T;public bool GlobalRequested;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
  {
   bool g=req.RequestUri!.AbsolutePath.EndsWith("/main/version.json");
   if(g){G++;GlobalRequested=true;}else {T++;if(slowTarget){Check(GlobalRequested,"Consulta global aguardou direcionada");await Task.Delay(200,ct);}}
   if(g && transient && G==1)return new(HttpStatusCode.ServiceUnavailable);
   var body=g?global:target;
   return new(body==null?HttpStatusCode.NotFound:HttpStatusCode.OK){Content=new StringContent(body??"404")};
  }
 }
 static object? Load(HttpClient h,string id,string installed)
 {
  var method=U.GetMethod("LoadManifestAsync",S,null,new[]{typeof(HttpClient),typeof(string),typeof(string)},null)!;
  return Task.Run(async()=>{var t=(Task)method.Invoke(null,new object[]{h,id,installed})!;await t;return t.GetType().GetProperty("Result")!.GetValue(t);}).GetAwaiter().GetResult();
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string? cls,string? title);
 [DllImport("user32.dll")]static extern IntPtr GetDlgItem(IntPtr h,int id);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out RECT r);
 [StructLayout(LayoutKind.Sequential)]struct RECT{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,EnumProc cb,IntPtr l);
 delegate bool EnumProc(IntPtr h,IntPtr l);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
 static void Alert(object m,string name)
 {
  bool seen=false;Exception? error=null;
  using var owner=new Form{Text="QA ISOLADO ALERTA"};owner.Show();
  using var timer=new System.Windows.Forms.Timer{Interval=80};int ticks=0;
  timer.Tick+=(_,_)=>{
   var h=FindWindow("#32770","Atualização disponível");if(h==IntPtr.Zero){if(++ticks>60)throw new Exception("Alerta nao apareceu");return;}
   timer.Stop();seen=true;
   try{
    var text=new List<string>();EnumChildWindows(h,(child,_)=>{var b=new StringBuilder(2048);GetWindowText(child,b,b.Capacity);text.Add(b.ToString());return true;},IntPtr.Zero);
    Check(text.Any(x=>x.Contains("NOVA ATUALIZAÇÃO DISPONÍVEL")&&x.Contains("10.379")),"Mensagem/versao do alerta ausente");
    GetWindowRect(h,out var r);using var bmp=new Bitmap(r.Right-r.Left,r.Bottom-r.Top);using(var gr=Graphics.FromImage(bmp))gr.CopyFromScreen(r.Left,r.Top,0,0,bmp.Size);bmp.Save("alerta-evidencias/"+name+".png");
   }catch(Exception e){error=e;}
   SendMessage(GetDlgItem(h,7),0xF5,IntPtr.Zero,IntPtr.Zero);
  };
  timer.Start();var result=(DialogResult)U.GetMethod("ConfirmUpdate",S)!.Invoke(null,new object[]{owner,m})!;
  Check(seen&&result==DialogResult.No,"Alerta nao foi exibido/recusado");if(error!=null)throw error;owner.Close();
 }
 static void Test(string name,string id,string installed,string? global,string? target,string? v,string expectedId="",bool transient=false,bool slow=false,bool alert=false)
 {
  using var feed=new Feed(global,target,transient,slow);using var http=new HttpClient(feed);var m=Load(http,id,installed);
  if(v==null)Check(m==null,"Falsa atualizacao: "+name);
  else{
   Check(m!=null,"Atualizacao ausente: "+name);string Read(string n)=>(string)m!.GetType().GetProperty(n)!.GetValue(m)!;
   Check(Read("Version")==v&&Read("TargetClientCode")==expectedId,"Selecao incorreta: "+name);
   if(alert)Alert(m!,name);
  }
  if(transient)Check(feed.G==2,"Falha transitoria nao recuperada");
  if(global==null)Check(feed.G==1,"404 repetido indevidamente");
  Console.WriteLine("PASS "+name+"; global="+feed.G+" direcionada="+feed.T);
 }
 [STAThread]static void Main()
 {
  Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","Somente Windows QA isolado");Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Directory.CreateDirectory("alerta-evidencias");
  foreach(var id in new[]{"001","002","003"}){
   Test("igual-"+id,id,"10.378",M("10.378"),M("10.378",id),null);
   Test("global-superior-"+id,id,"10.378",M("10.379"),M("10.378",id),"10.379",alert:true);
   Test("compativel-"+id,id,"10.378",M("10.379"),M("10.379",id),"10.379",id);
  }
  Test("direcionada-valida","001","10.378",M("10.378"),M("10.379","001"),"10.379","001",alert:true);
  Test("outro-cliente","002","10.378",M("10.379"),M("10.380","003"),"10.379");
  Test("global-transitoria","001","10.378",M("10.379"),null,"10.379",transient:true,alert:true);
  Test("direcionada-lenta-nao-atrasou-consulta-global","001","10.378",M("10.379"),null,"10.379",slow:true);
  Test("404-duplo","003","10.378",null,null,null);
  Test("global-indisponivel-direcionada","003","10.378",null,M("10.379","003"),"10.379","003");
  Test("downgrade","002","10.380",M("10.379"),M("10.379","002"),null);
 }
}
