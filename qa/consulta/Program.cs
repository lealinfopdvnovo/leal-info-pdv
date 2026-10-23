using System.Drawing;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using LealInfoPDV;
static class Tests
{
 static readonly List<string> checks=new();
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);Console.WriteLine("PASS "+text);}
 static IEnumerable<Control> Walk(Control parent){foreach(Control c in parent.Controls){yield return c;foreach(var n in Walk(c))yield return n;}}
 sealed class Feeds(string scoped):HttpMessageHandler{
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken token){return Task.FromResult(new HttpResponseMessage(req.RequestUri!.AbsolutePath.Contains("/global/")?HttpStatusCode.OK:HttpStatusCode.NotFound){Content=new StringContent(scoped)});}
 }
 [STAThread] static void Main(){
  if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")throw new Exception("Isolated Windows only");
  Application.EnableVisualStyles();Directory.CreateDirectory("evidence");
  string[] expected={"products","customers","sales_history","orders","deliveries","quotes"};
  var type=typeof(MainForm).Assembly.GetType("LealInfoPDV.ConsultaCentralForm",true)!;
  foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080)}){
   using var owner=new Form{ClientSize=size,StartPosition=FormStartPosition.Manual,Location=new Point(0,0)};owner.Show();
   var calls=new int[6];var permissions=new List<string>();
   Action<string,Action> allowed=(key,action)=>{permissions.Add(key);action();};
   var args=new object[]{allowed,(Action)(()=>calls[0]++),(Action)(()=>calls[1]++),(Action)(()=>calls[2]++),(Action)(()=>calls[3]++),(Action)(()=>calls[4]++),(Action)(()=>calls[5]++)};
   using var dialog=(Form)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,args,null)!;
   dialog.Show(owner);Application.DoEvents();var buttons=Walk(dialog).OfType<Button>().ToArray();
   Check(buttons.Length==6,"six existing query options "+size.Width);
   Check(buttons.Select(b=>b.Tag?.ToString()).SequenceEqual(expected),"original permissions and order retained "+size.Width);
   Check(Screen.FromControl(owner).WorkingArea.Contains(dialog.Bounds),"floating window stays inside screen "+size.Width);
   foreach(var b in buttons){Check(b.Visible&&b.Height>=70,"query action visible and readable "+b.Tag+" "+size.Width);b.PerformClick();Application.DoEvents();}
   Check(calls.All(n=>n==1)&&permissions.SequenceEqual(expected),"each option dispatches exactly its existing authorized action "+size.Width);
   using var image=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save($"evidence/consulta-{size.Width}.png");
   dialog.Close();owner.Close();
  }
  using(var owner=new Form()){
   owner.Show();int called=0;Action<string,Action> deny=(_,_)=>{};Action action=()=>called++;
   using var dialog=(Form)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{deny,action,action,action,action,action,action},null)!;
   dialog.Show(owner);Application.DoEvents();foreach(var b in Walk(dialog).OfType<Button>())b.PerformClick();
   Check(called==0&&dialog.Visible,"denied permissions cannot open any query action");dialog.Close();owner.Close();
  }
  var updater=typeof(MainForm).Assembly.GetType("LealInfoPDV.UpdateManager",true)!;
  var load=updater.GetMethod("LoadManifestAsync",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(HttpClient),typeof(string),typeof(string)},null)!;
  string feed=JsonSerializer.Serialize(new{Version="10.446",TargetClientCode="003",Component="GLOBAL",PetApiVersion=1,Sha256=new string('a',64),PackageUrl="https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v10.446-casa/common.zip"});
  foreach(string id in new[]{"001","002","003","004"}){
   using var http=new HttpClient(new Feeds(feed));var task=(Task)load.Invoke(null,new object[]{http,id,"10.445"})!;task.GetAwaiter().GetResult();var result=task.GetType().GetProperty("Result")!.GetValue(task);
   Check((result!=null)==(id=="003"),"10.445 updater selects Casa-only 10.446 without leaking to "+id);
  }
  File.WriteAllText("evidence/consulta-results.json",JsonSerializer.Serialize(new{Success=true,Checks=checks,Published=false,InstalledOnCustomer=false}));
 }
}
