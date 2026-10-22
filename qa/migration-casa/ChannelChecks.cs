using System.Net;
using System.Reflection;
using System.Text.Json;
using LealInfoPDV;
static partial class Tests
{
 sealed class Feeds(string? global,string? scoped):HttpMessageHandler{
  internal readonly List<string> paths=new();
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){string p=request.RequestUri!.AbsolutePath;paths.Add(p);string? body=p.Contains("/global/")?scoped:global;return Task.FromResult(new HttpResponseMessage(body==null?HttpStatusCode.NotFound:HttpStatusCode.OK){Content=new StringContent(body??"")});}
 }
 static void ChannelChecks(){
  string Manifest(string identity="003",string component="GLOBAL",int api=1,string version="10.445",string? url=null)=>JsonSerializer.Serialize(new{Version=version,TargetClientCode=identity,Component=component,PetApiVersion=api,Sha256=new string('a',64),PackageUrl=url??"https://github.com/lealinfopdvnovo/leal-info-pdv-updates/releases/download/v10.445-pilot/common.zip"});
  var select=typeof(MainForm).Assembly.GetType("LealInfoPDV.UpdateManager")!.GetMethod("LoadManifestAsync",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(HttpClient),typeof(string),typeof(string)},null)!;
  object? Select(string identity,string? global,string? scoped){using var handler=new Feeds(global,scoped);using var http=new HttpClient(handler);var task=(Task)select.Invoke(null,new object[]{http,identity,"10.444"})!;task.GetAwaiter().GetResult();Check(!handler.paths.Any(p=>p.EndsWith("/clients/"+identity+"/version.json")),"common updater never consumes legacy full-client feed "+identity);return task.GetType().GetProperty("Result")!.GetValue(task);}
  Check(Select("003",null,Manifest())!=null,"scoped common update available exclusively to matching Casa identity");
  foreach(string identity in new[]{"001","002","004"})Check(Select(identity,null,Manifest())==null,"Casa scoped common manifest cannot cross to another identity "+identity);
  foreach(string invalid in new[]{Manifest(component:"PET"),Manifest(api:2),Manifest(version:"10.444"),Manifest(identity:"999"),Manifest(url:"https://example.invalid/update.zip")})Check(Select("003",null,invalid)==null,"incompatible scoped common manifest rejected");
  Check(Select("003",Manifest(identity:""),null)!=null,"neutral shared GLOBAL route retained when scoped route is absent");
 }
}
