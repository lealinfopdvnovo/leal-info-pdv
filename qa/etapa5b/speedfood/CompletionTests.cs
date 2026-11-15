using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using LealInfo.Qa37.SpeedFood;
class Tests {
 sealed class Wire:HttpMessageHandler {public object? Body;public HttpStatusCode Status=HttpStatusCode.OK;public bool Offline;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c){if(Offline)throw new HttpRequestException("SYNTHETIC_OFFLINE");return Task.FromResult(new HttpResponseMessage(Status){Content=new StringContent(JsonSerializer.Serialize(Body),Encoding.UTF8,"application/json")});}}
 static int count;static void Check(bool b,string n){if(!b)throw new Exception(n);count++;Console.WriteLine("PASS "+n);}
 static async Task Main(){using var db=new SqliteConnection("Data Source=:memory:");db.Open();using(var c=db.CreateCommand()){c.CommandText="CREATE TABLE deliveries(id INTEGER PRIMARY KEY,status TEXT,delivered_at TEXT);CREATE TABLE delivery_tracking(delivery_id INTEGER,code TEXT);INSERT INTO deliveries VALUES(1,'EM ROTA',null),(2,'CANCELADA',null),(3,'EM ROTA',null);INSERT INTO delivery_tracking VALUES(1,'ABC123DEF456'),(2,'CAN123CEL456'),(3,'OTH123ERX456');";c.ExecuteNonQuery();}
 var w=new Wire();using var service=new Completion(new Uri("http://127.0.0.1:9000/"),"qa-publisher",w);
 object Record(string code="ABC123DEF456",string uid="qa-publisher",string status="Entrega concluída",object? at=null)=>new{codigo=code,pdvUid=uid,status,finalizadoEm=at??1794760000000L};
 w.Body=Record();Check(await service.ReadAndApply(db,1,"ABC123DEF456","synthetic"),"authenticated completion updates matching delivery");Check(!await service.ReadAndApply(db,1,"ABC123DEF456","synthetic"),"retry does not duplicate completion");
 w.Body=Record("CAN123CEL456");Check(!await service.ReadAndApply(db,2,"CAN123CEL456","synthetic"),"cancelled delivery not reopened");
 w.Body=Record();Check(!await service.ReadAndApply(db,3,"OTH123ERX456","synthetic"),"another delivery code refused");
 w.Body=Record("OTH123ERX456","wrong");Check(!await service.ReadAndApply(db,3,"OTH123ERX456","synthetic"),"another publisher refused");
 w.Body=Record("OTH123ERX456",status:"Em rota");Check(!await service.ReadAndApply(db,3,"OTH123ERX456","synthetic"),"nonterminal order not marked delivered");
 w.Body=Record("OTH123ERX456",at:0L);Check(!await service.ReadAndApply(db,3,"OTH123ERX456","synthetic"),"missing server timestamp refused");
 w.Body=Record("OTH123ERX456");w.Status=HttpStatusCode.Unauthorized;Check(!await service.ReadAndApply(db,3,"OTH123ERX456","synthetic"),"unauthorized response preserves pending order");w.Status=HttpStatusCode.OK;
 Check(!await service.ReadAndApply(db,3,"OTH123ERX456",""),"missing session never opens a background login");
 w.Offline=true;try{await service.ReadAndApply(db,3,"OTH123ERX456","synthetic");throw new Exception("offline accepted");}catch(HttpRequestException){Check(true,"offline keeps delivery pending");}w.Offline=false;
 Check(await service.ReadAndApply(db,3,"OTH123ERX456","synthetic"),"reconnection recovers pending completion");
 try{using var blocked=new Completion(new Uri("https://novo-91da7436-default-rtdb.firebaseio.com/"),"qa",new Wire());throw new Exception("production allowed");}catch(ArgumentException){Check(true,"QA cannot contact production Firebase");}
 Directory.CreateDirectory("integration-evidence");File.WriteAllText("integration-evidence/speedfood-completion.json",JsonSerializer.Serialize(new{Pass=true,Checks=count,SQLiteDatabase="memory",Transport="fixture",RealFirebase=false,HostPetAttached=false}));
 }
}
