using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
namespace LealInfoPDV;

internal sealed class AngraSyncTransport : IDisposable
{
 internal const string Origin="https://angra-vidros-gateway.classyglobe9.chatgpt.site";
 private readonly ECDsa key;
 private readonly HttpClient http=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(30)};
 internal string DeviceId{get;}
 internal string PublicKey{get;}
 internal AngraSyncTransport(string? testPkcs8=null)
 {
  key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
  string path=Path.Combine(Database.AppFolder,"angra-sync.identity");
  if(testPkcs8!=null)key.ImportPkcs8PrivateKey(Convert.FromBase64String(testPkcs8),out _);
  else if(File.Exists(path))key.ImportPkcs8PrivateKey(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser),out _);
  else{Directory.CreateDirectory(Database.AppFolder);byte[] bytes=ProtectedData.Protect(key.ExportPkcs8PrivateKey(),null,DataProtectionScope.CurrentUser);using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);file.Write(bytes);file.Flush(true);}
  byte[] publicKey=key.ExportSubjectPublicKeyInfo();PublicKey=Convert.ToBase64String(publicKey);DeviceId=Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();
 }
 internal async Task<JsonObject> Call(string path,JsonObject body,bool sign=true)
 {
  if(!path.StartsWith("/sync/",StringComparison.Ordinal)&&path!="/device/register")throw new InvalidOperationException("Rota inválida.");
  byte[] bytes=Encoding.UTF8.GetBytes(body.ToJsonString());using var request=new HttpRequestMessage(HttpMethod.Post,Origin+path);request.Content=new ByteArrayContent(bytes);request.Content.Headers.ContentType=new("application/json");
  if(sign){string time=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),nonce=Guid.NewGuid().ToString();string canonical="POST\n"+path+"\n"+time+"\n"+nonce+"\n"+Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();byte[] signature=key.SignData(Encoding.UTF8.GetBytes(canonical),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);request.Headers.Add("X-Angra-Device",DeviceId);request.Headers.Add("X-Angra-Time",time);request.Headers.Add("X-Angra-Nonce",nonce);request.Headers.Add("X-Angra-Signature",Convert.ToBase64String(signature));}
  using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead);await using var stream=await response.Content.ReadAsStreamAsync();byte[] result=await Read(stream,20_000_000);var json=JsonNode.Parse(result)?.AsObject()??throw new IOException("Resposta de integração inválida.");if(!response.IsSuccessStatusCode)throw new IOException(json["error"]?.ToString()??"Conexão indisponível; dados preservados.");return json;
 }
 public void Dispose(){http.Dispose();key.Dispose();}
 internal Task<JsonObject> Register()=>Call("/device/register",new(){["publicKey"]=PublicKey},false);
 private static async Task<byte[]> Read(Stream stream,int maximum){using var output=new MemoryStream();byte[] buffer=new byte[16384];int n;while((n=await stream.ReadAsync(buffer))!=0){if(output.Length+n>maximum)throw new IOException("Resposta excede o limite seguro.");await output.WriteAsync(buffer.AsMemory(0,n));}return output.ToArray();}
 internal async Task<byte[]> Binary(string address,byte[]? upload=null)
 {
  var uri=new Uri(address);if(uri.Scheme!="https"||uri.Host!=new Uri(Origin).Host||!System.Text.RegularExpressions.Regex.IsMatch(uri.AbsolutePath,@"^/sync-file/[a-f0-9]{64}$")||uri.Query!="")throw new IOException("Endereço de anexo inválido.");
  using var request=new HttpRequestMessage(upload==null?HttpMethod.Get:HttpMethod.Put,uri);if(upload!=null){request.Content=new ByteArrayContent(upload);request.Content.Headers.ContentType=new("image/jpeg");}using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead);if(!response.IsSuccessStatusCode)throw new IOException("Foto ainda pendente de transferência.");await using var stream=await response.Content.ReadAsStreamAsync();return await Read(stream,8_000_000);
 }
}
