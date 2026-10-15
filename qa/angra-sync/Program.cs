using LealInfoPDV;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
static class Program {
 static void Check(bool yes,string msg){if(!yes)throw new Exception(msg);Console.WriteLine("PASS "+msg);}
 static object? Sql(string sql){using var db=Database.Open();using var q=db.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
 static async Task Main(){
 Check(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true","disposable Windows only");Database.Initialize();
 Sql("INSERT INTO customers(name,document,phone) VALUES('BOB','QA-BOB','24999990000'); INSERT INTO suppliers(name) VALUES('FORNECEDOR QA'); INSERT INTO products(name,price,stock,active) VALUES('VIDRO QA',100,5,1);SELECT 1");
 using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);var transport=new AngraSyncTransport(Convert.ToBase64String(key.ExportPkcs8PrivateKey()));await transport.Register();
 using var http=new HttpClient();string url=Environment.GetEnvironmentVariable("ACTIONS_ID_TOKEN_REQUEST_URL")!+"&audience="+Uri.EscapeDataString("angra-sync-ci:"+transport.DeviceId);using var req=new HttpRequestMessage(HttpMethod.Get,url);req.Headers.Authorization=new("Bearer",Environment.GetEnvironmentVariable("ACTIONS_ID_TOKEN_REQUEST_TOKEN"));var oidc=JsonNode.Parse(await (await http.SendAsync(req)).Content.ReadAsStringAsync())!;
 using var grantRequest=new HttpRequestMessage(HttpMethod.Post,AngraSyncTransport.Origin+"/sync/ci-grant");grantRequest.Headers.Authorization=new("Bearer",oidc["value"]!.ToString());grantRequest.Content=new StringContent(new JsonObject{["deviceId"]=transport.DeviceId,["role"]="pdv"}.ToJsonString(),System.Text.Encoding.UTF8,"application/json");using var grant=await http.SendAsync(grantRequest);string grantText=await grant.Content.ReadAsStringAsync();Check(grant.IsSuccessStatusCode,"GitHub OIDC grant: "+grant.StatusCode);var g=JsonNode.Parse(grantText)!;string tenant=g["tenant"]!.ToString();
 using var service=new AngraSyncService(transport,false);service.UseTestTenant(tenant);await service.Tick();Check(service.Status.Contains("PDV CONECTADO"),service.Status);var store=service.Store!;var bob=store.Links().Single(x=>x.Entity=="clients"&&store.ReadLocal(x)?["name"]?.ToString()=="BOB");Check(bob.Revision>0,"BOB real SQLite uploaded to real HTTPS backend");long before=Convert.ToInt64(Sql("SELECT COUNT(*) FROM customers"));await service.Tick();Check(Convert.ToInt64(Sql("SELECT COUNT(*) FROM customers"))==before,"retry no customer duplicate");Check(Convert.ToInt64(Sql("SELECT COUNT(*) FROM cash_movements"))==0,"sync no financial operation");Check(Convert.ToDecimal(Sql("SELECT stock FROM products WHERE name='VIDRO QA'"))==5,"stock unchanged");Check(Directory.GetFiles(Database.BackupFolder,"ANTES_ANGRA_SYNC_*.db").Length>0,"backup before additive migration");
 string outdir="qa/angra-sync/evidence";Directory.CreateDirectory(outdir);File.WriteAllText(Path.Combine(outdir,"real-windows.json"),new JsonObject{["tenant"]=tenant,["bobId"]=bob.Id,["status"]=service.Status,["realHttps"]=true}.ToJsonString());
 }
}
