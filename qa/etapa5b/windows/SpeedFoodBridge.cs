using System.Data.Common;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using LealInfo.Qa37.SpeedFood;
namespace AngraQaIntegration;
// Disabled without a private QA-only emulator configuration. Never reuses the production Firebase session.
public static class SpeedFoodBridge {
 static readonly ConditionalWeakTable<Form,Session> sessions=new();
 public static void Apply(Form form){
  if(form.GetType().FullName!="LealInfoPDV.DeliveryManagementForm"||sessions.TryGetValue(form,out _))return;
  var host=form.GetType().Assembly;var isolation=host.GetType("LealInfoPDV.QaIsolation");
  if(isolation?.GetProperty("Isolated")?.GetValue(null) is not true)return;
  string root=(string)isolation.GetProperty("LocalRoot")!.GetValue(null)!;
  string config=Path.Combine(root,"speedfood-qa.json");if(!File.Exists(config))return;
  using var doc=JsonDocument.Parse(File.ReadAllText(config));string origin=doc.RootElement.GetProperty("EmulatorOrigin").GetString()!;string publisher=doc.RootElement.GetProperty("TestPublisherUid").GetString()!;
  var endpoint=new Uri(origin);if(endpoint.Scheme!="http"||endpoint.Host!="127.0.0.1"||endpoint.UserInfo!=""||endpoint.Query!="")throw new InvalidDataException("QA emulator only");
  sessions.Add(form,new Session(form,root,endpoint,publisher));
 }
 sealed class Session {
  readonly Form form;readonly string root;readonly Completion completion;readonly System.Windows.Forms.Timer timer=new(){Interval=15000};bool busy,closed;
  public Session(Form form,string root,Uri endpoint,string publisher){this.form=form;this.root=root;completion=new(endpoint,publisher,new HttpClientHandler{AllowAutoRedirect=false});timer.Tick+=Poll;form.Shown+=Poll;form.FormClosed+=Close;timer.Start();}
  async void Poll(object? sender,EventArgs e){if(busy||closed||form.IsDisposed)return;busy=true;try{
   var file=Path.Combine(root,"speedfood-qa.session");if(!File.Exists(file))return;
   string token=Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file),null,DataProtectionScope.CurrentUser));
   using var db=(DbConnection)form.GetType().Assembly.GetType("LealInfoPDV.Database")!.GetMethod("Open",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,null)!;
   using var query=db.CreateCommand();query.CommandText="SELECT d.id,t.code FROM deliveries d JOIN delivery_tracking t ON t.delivery_id=d.id WHERE d.status NOT IN ('ENTREGUE','CANCELADA')";
   var rows=new List<(long,string)>();using(var reader=await query.ExecuteReaderAsync())while(await reader.ReadAsync())rows.Add((reader.GetInt64(0),reader.GetString(1)));
   bool changed=false;foreach(var (id,code) in rows){if(closed)return;changed|=await completion.ReadAndApply(db,id,code,token);}
   if(changed&&!closed&&!form.IsDisposed)form.GetType().GetMethod("LoadDeliveries",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,null);
  }catch(Exception ex)when(ex is IOException or HttpRequestException or CryptographicException or JsonException or DbException or OperationCanceledException){System.Diagnostics.Trace.TraceInformation("SpeedFood QA: pending; "+ex.GetType().Name);}finally{busy=false;}}
  void Close(object? sender,FormClosedEventArgs e){closed=true;timer.Dispose();completion.Dispose();form.Shown-=Poll;form.FormClosed-=Close;}
 }
}
