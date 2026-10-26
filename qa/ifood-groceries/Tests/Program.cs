using LealInfo.IFood.Groceries;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using System.Security.Cryptography;

var orderId = Guid.NewGuid(); var merchant = Guid.NewGuid(); int passed = 0;
void Check(bool ok, string title) { if (!ok) throw new Exception("FAIL " + title); Console.WriteLine("PASS " + title); passed++; }
object Money(int value) => new { value, currency = "BRL" };
string Fixture() => JsonSerializer.Serialize(new {
 id = orderId, merchant = new { id = merchant }, test = true, category = "GROCERY",
 bag = new { items = Enumerable.Range(1,3).Select(i => new { uniqueId = Guid.NewGuid(), name = "Produto teste " + i,
 quantity = i == 3 ? 1 : 3, unavailable = i == 1, product = new { isVariableWeight = false },
 prices = new { unitValue = Money(1000), grossValue = Money(i == 3 ? 1000 : 3000) } }), prices = new { grossValue = Money(4000) } },
 operationMode = new { type = "DELIVERY", schedulingType = "IMMEDIATE", delivery = new { } },
 payment = new { methods = new[] { new { amount = Money(3500) } } },
 benefit = new { benefits = new[] { new { target = "CART", targetId = (string?)null,
 sponsorships = new[] { new { liability = "OWN", amount = Money(500) } } } } }
});
var fixture = Fixture();
Order Parse(string json) => VirtualBag.Parse(json, orderId, merchant);
void Reject(Action<JsonNode> mutate, string name) { var n = JsonNode.Parse(fixture)!; mutate(n); bool rejected = false; try { Parse(n.ToJsonString()); } catch (ContractException) { rejected = true; } Check(rejected,name); }
var order = Parse(fixture);
Check(order.Items.Select(i=>i.AvailableQuantity).SequenceEqual(new[]{0,3,1}),"scenario final quantities 0/3/1 with unavailable=true");
Check(order.Items[0].ReportedQuantity==3 && order.Items[0].Unavailable,"cancelled line retained without restoring stock quantity");
Check(order.Benefits.Single().Cents==500 && order.PaymentCents==3500 && order.BagGrossCents==4000,"coupon sponsorship and server money preserved in cents");
Reject(n=>n["bag"]!["items"]![0]!.AsObject().Remove("unavailable"),"missing unavailable rejected");
Reject(n=>n["bag"]!["items"]![0]!["unavailable"]="false","string boolean rejected");
Reject(n=>n["bag"]!["items"]![1]!["quantity"]=0,"available zero quantity rejected");
Reject(n=>n["bag"]!["items"]![1]!["quantity"]=1.5,"fractional unit quantity rejected");
Reject(n=>n["bag"]!["items"]![1]!["product"]!["isVariableWeight"]=true,"unsupported weight fails explicitly");
Reject(n=>n["bag"]!["items"]![1]!["uniqueId"]=n["bag"]!["items"]![0]!["uniqueId"]!.DeepClone(),"duplicate item identity rejected");
Reject(n=>n["merchant"]!["id"]=Guid.NewGuid().ToString(),"other merchant blocked");
Reject(n=>n["test"]=false,"production order blocked");
Reject(n=>n["category"]="FOOD","food schema not confused with groceries");
var scheduled=JsonNode.Parse(fixture)!; scheduled["operationMode"]!["schedulingType"]="TIME_SLOT";
scheduled["operationMode"]!["delivery"] = JsonNode.Parse("{\"deliveryTime\":{\"window\":{\"from\":\"2026-11-01T15:00:00-03:00\",\"to\":\"2026-11-01T16:00:00-03:00\"}}}");
Check(Parse(scheduled.ToJsonString()).WindowFrom!.Value.Offset==TimeSpan.FromHours(-3),"scheduled window preserves timezone");
Reject(n=>n["operationMode"]!["schedulingType"]="TIME_SLOT","scheduled without window rejected");
var takeout=JsonNode.Parse(fixture)!;takeout["operationMode"]!["type"]="TAKEOUT";takeout["operationMode"]!["takeout"]=new JsonObject();takeout["operationMode"]!["delivery"]=null;
Check(Parse(takeout.ToJsonString()).Operation=="TAKEOUT","takeout does not require delivery address");
bool duplicateRejected=false;try{Parse(fixture.Replace("\"test\":true","\"test\":true,\"test\":false"));}catch(ContractException){duplicateRejected=true;}Check(duplicateRejected,"duplicate JSON keys rejected");

var dir=Path.Combine(Path.GetTempPath(),"ifood-core-test-"+Guid.NewGuid());
var signal=new Signal(Guid.NewGuid(),orderId,merchant,"RFI");int reads=0;
Task<string> Fetch(Guid id,CancellationToken ct){reads++;return Task.FromResult(fixture);}
try {
 using(var inbox=new Inbox(dir,merchant)){
  Check(!(await inbox.ReceiveAsync(signal with {Code="PLC"},Fetch)).ReadyToAcknowledge && reads==0,"no final import before RFI");
  var receipts=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>inbox.ReceiveAsync(signal,Fetch)));
  Check(reads==1 && receipts.Count(r=>!r.Duplicate)==1,"12 concurrent deliveries persist one event/order");
 }
 using(var inbox=new Inbox(dir,merchant)){
  Check((await inbox.ReceiveAsync(signal,Fetch)).Duplicate && reads==1,"restart replays safely without refetch");
  bool conflict=false;try{await inbox.ReceiveAsync(signal with {OrderId=Guid.NewGuid()},Fetch);}catch(ContractException){conflict=true;}Check(conflict,"event id reused for another order rejected");
  bool failed=false;try{await inbox.ReceiveAsync(signal with {EventId=Guid.NewGuid()},(_,_)=>throw new IOException("offline"));}catch(IOException){failed=true;}Check(failed,"network failure never acknowledged");
 }
 var text=File.ReadAllText(Path.Combine(dir,"ifood-test-inbox.json"));Check(!text.Contains("customer")&&!text.Contains("Authorization"),"snapshot excludes customer and auth data");
} finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var logs=new List<HttpEvidence>();
var stub=new Stub(async req=>{Check(req.RequestUri!.Host=="merchant-api.ifood.com.br" && req.RequestUri.AbsolutePath.EndsWith("/virtual-bag"),"reader uses official virtual-bag route");Check(req.Headers.Authorization?.Parameter==secret,"token used in header only");await Task.Yield();return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(fixture)};});
using(var api=new ApiReader(_=>Task.FromResult(secret),logs.Add,stub)){Check(Parse(await api.ReadAsync(orderId)).Id==orderId,"reader and parser contract integration");}
Check(!JsonSerializer.Serialize(logs).Contains(secret)&&logs.Single().Status==200,"HTTP evidence contains no token/body");
foreach(var code in new[]{401,403,429,500,302}){
 int calls=0;using var api=new ApiReader(_=>Task.FromResult(secret),logs.Add,new Stub(_=>{calls++;return Task.FromResult(new HttpResponseMessage((HttpStatusCode)code));}));
 bool failed=false;try{await api.ReadAsync(orderId);}catch(HttpRequestException){failed=true;}Check(failed&&calls==1,"HTTP "+code+" fails without retry loop or redirect");
}
passed += await AuthenticationTests.Run();
passed += await CentralizedTests.Run();
passed += await ReceptionTests.Run();
passed += await PartialPickingTests.Run();
passed += await PortableBagTests.Run();
Console.WriteLine($"TOTAL {passed} PASS; isolated fixtures and fake HTTP only; NO live iFood, PDV or Windows UI test.");
sealed class Stub(Func<HttpRequestMessage,Task<HttpResponseMessage>> handler):HttpMessageHandler
{ protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c)=>handler(r); }
