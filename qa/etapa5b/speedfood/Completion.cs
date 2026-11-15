using System.Data.Common;
using System.Net.Http;
using System.Text.Json;
namespace LealInfo.Qa37.SpeedFood;
// Prepared integration seam for the PET: no Firebase password, production default, schema change or financial effect.
public sealed class Completion : IDisposable {
 readonly HttpClient http;readonly Uri root;readonly string publisher;readonly CancellationTokenSource stop=new();
 public Completion(Uri isolatedRoot,string expectedTestPublisher,HttpMessageHandler transport){
  if(isolatedRoot.Scheme!="http"||isolatedRoot.Host!="127.0.0.1"||isolatedRoot.Query!=""||isolatedRoot.UserInfo!="")throw new ArgumentException("QA emulator endpoint required; production is forbidden");
  if(string.IsNullOrWhiteSpace(expectedTestPublisher))throw new ArgumentException("Test publisher required");
  root=isolatedRoot;publisher=expectedTestPublisher;http=new HttpClient(transport){Timeout=TimeSpan.FromSeconds(8)};
 }
 public async Task<bool> ReadAndApply(DbConnection db,long deliveryId,string code,string testIdToken){
  if(code.Length!=12||code.Any(c=>!char.IsAsciiLetterUpper(c)&&!char.IsAsciiDigit(c)))throw new ArgumentException("Invalid delivery code");
  if(string.IsNullOrEmpty(testIdToken))return false; // Never prompt for login in background.
  using var response=await http.GetAsync(new Uri(root,"pedidos/"+code+".json?auth="+Uri.EscapeDataString(testIdToken)),stop.Token);
  if(!response.IsSuccessStatusCode)return false; // Network/auth failures never mark an order delivered.
  byte[] raw=await response.Content.ReadAsByteArrayAsync(stop.Token);if(raw.Length>65536)throw new InvalidDataException("Bounded order response required");
  using var doc=JsonDocument.Parse(raw);var r=doc.RootElement;
  if(r.ValueKind!=JsonValueKind.Object||!r.TryGetProperty("codigo",out var c)||c.GetString()!=code||!r.TryGetProperty("pdvUid",out var p)||p.GetString()!=publisher)return false;
  string status=r.TryGetProperty("status",out var s)?s.GetString()??"":"";
  if(!new[]{"ENTREGA CONCLUÍDA","ENTREGUE","FINALIZADA"}.Contains(status.ToUpperInvariant()))return false;
  long stamp=0;foreach(string key in new[]{"finalizadoEm","atualizadoEm"})if(r.TryGetProperty(key,out var t)&&t.TryGetInt64(out stamp)&&stamp>0)break;
  if(stamp<=0)return false;DateTimeOffset ended;try{ended=DateTimeOffset.FromUnixTimeMilliseconds(stamp);}catch(ArgumentOutOfRangeException){return false;}
  using var update=db.CreateCommand();update.CommandText="UPDATE deliveries SET status='ENTREGUE',delivered_at=$date WHERE id=$id AND status NOT IN ('ENTREGUE','CANCELADA') AND EXISTS(SELECT 1 FROM delivery_tracking WHERE delivery_id=$id AND code=$code)";
  void Param(string name,object value){var param=update.CreateParameter();param.ParameterName=name;param.Value=value;update.Parameters.Add(param);}
  Param("$date",ended.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"));Param("$id",deliveryId);Param("$code",code);return await update.ExecuteNonQueryAsync(stop.Token)==1;
 }
 // One sequential poll per interval; cancellation terminates work when the owner closes its PET screen.
 public async Task Run(Func<Completion,CancellationToken,Task> poll,TimeSpan interval){if(interval<TimeSpan.FromSeconds(5))throw new ArgumentException("Bounded polling interval");using var timer=new PeriodicTimer(interval);try{do{try{await poll(this,stop.Token);}catch(HttpRequestException){}catch(TaskCanceledException)when(!stop.IsCancellationRequested){}}while(await timer.WaitForNextTickAsync(stop.Token));}catch(OperationCanceledException)when(stop.IsCancellationRequested){}}
 public void Dispose(){stop.Cancel();http.Dispose();}
}
