using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
[assembly:AssemblyMetadata("PetIdentity","005")]
namespace AngraQaIntegration;
public static class Resume
{
 static readonly ConditionalWeakTable<Form,Session> sessions=new();
 public static void Apply(Form form){
  EmployeeTutorial.Observe();
  SalesHeaderLogo.Observe();
  SalesStyles.Observe();
  if(form.GetType().FullName!="LealInfoPDV.AngraPairingForm")return;
  var id=form.GetType().Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault(x=>x.Key=="PetIdentity")?.Value;
  if(id!="005"||sessions.TryGetValue(form,out _))return;
  sessions.Add(form,new Session(form));
  AddAdministration(form);
 }
 static void AddAdministration(Form form){
  var flow=All(form).OfType<FlowLayoutPanel>().FirstOrDefault();
  if(flow==null||All(form).Any(c=>c.Name=="AngraOwnerAdministration"))return;
  var button=new Button{Name="AngraOwnerAdministration",Text="FUNCIONÁRIOS / PROPRIETÁRIO",Width=420,Height=48};
  button.Click+=(_,_)=>{
   try{
    var service=form.GetType().GetField("service",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
    object? Find(object o,string key)=>o.GetType().GetProperty(key,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(o)??o.GetType().GetField(key,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(o);
    var transport=Find(service,"Transport")??Find(service,"transport")??throw new InvalidOperationException("Transporte de integração indisponível.");
    var id=Find(transport,"DeviceId") as string;
    if(id==null||!System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-f0-9]{64}$"))throw new InvalidOperationException("Conecte e autorize primeiro este PDV.");
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://angra-vidros-qa37.adriansantosangra.chatgpt.site/access/admin?device="+id){UseShellExecute=true});
   }catch(Exception e){MessageBox.Show(form,e.Message,"Administração da vidraçaria");}
  };
  flow.Controls.Add(button);flow.Controls.SetChildIndex(button,0);
  EmployeeTutorial.Attach(form);
 }
 static IEnumerable<Control> All(Control parent){foreach(Control c in parent.Controls){yield return c;foreach(var child in All(c))yield return child;}}
 sealed class Session
 {
  readonly Form form; readonly object service; readonly System.Windows.Forms.Timer timer=new(){Interval=2000};
  bool busy,finished,closed;
  public Session(Form form){this.form=form;service=form.GetType().GetField("service",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;form.Activated+=Activated;form.FormClosed+=Closed;timer.Tick+=Activated;timer.Start();}
  static object? Get(object o,string name)=>o.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(o);
  static async Task<object?> Call(object o,string name){var task=(Task)o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(o,null)!;await task;return task.GetType().GetProperty("Result")?.GetValue(task);}
  async void Activated(object? sender,EventArgs e){
   if(closed||busy||finished||form.IsDisposed||!form.Visible)return;
   busy=true;
   try{
    // Respect the original form's operation gate, including manual revoke/generate.
    if(form.GetType().GetField("busy",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form) is true)return;
    if(!string.IsNullOrEmpty((string?)Get(service,"Code"))){finished=true;timer.Stop();return;}
    await Call(service,"Refresh");if(closed||form.IsDisposed)return;
    string status=(string?)Get(service,"Status")??"";
    // This status is set by the existing signed /request-pdv + /status flow only after approval.
    if(!status.StartsWith("DESCONECTADO — aguardando o código",StringComparison.Ordinal)&&!status.StartsWith("CONECTADO — ",StringComparison.Ordinal))return;
    if(await Call(service,"Generate") is true){finished=true;timer.Stop();}
    if(!closed&&!form.IsDisposed)form.GetType().GetMethod("UpdateFields",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,null);
   }catch(Exception ex){
    if(!closed&&!form.IsDisposed){var label=form.Controls.Find("AngraPairingStatus",true).FirstOrDefault();if(label!=null)label.Text="SEM COMUNICAÇÃO — a autorização será consultada novamente.\n"+(ex.InnerException??ex).Message;}
   }finally{busy=false;}
  }
  void Closed(object? sender,FormClosedEventArgs e){closed=true;timer.Stop();timer.Dispose();form.Activated-=Activated;form.FormClosed-=Closed;}
 }
}

/// <summary>Preserves server-authoritative authorship when rebuilding a local PDV payload.</summary>
public static class Authorship
{
 public static object? Merge(object link,object? payload){
  if(payload is not System.Text.Json.Nodes.JsonObject json)return payload;
  var type=link.GetType();
  var entity=type.GetProperty("Entity",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(link) as string;
  if(entity!="projects")return payload;
  var baseline=type.GetProperty("Baseline",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(link) as System.Text.Json.Nodes.JsonObject;
  if(baseline?["authorship"] is System.Text.Json.Nodes.JsonObject author)json["authorship"]=author.DeepClone();
  return json;
 }
}
