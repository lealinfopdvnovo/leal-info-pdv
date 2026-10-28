using System.Net.WebSockets;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace LiaGeminiCasa;
public static class LiveDiagnostic
{
 public static string Redact(string? text,string? secret){
  var s=text??"";
  if(!string.IsNullOrEmpty(secret)){s=s.Replace(secret,"[CHAVE OCULTA]",StringComparison.Ordinal);s=s.Replace(Uri.EscapeDataString(secret),"[CHAVE OCULTA]",StringComparison.Ordinal);}
  s=Regex.Replace(s,@"(?:https?|wss?)://\S+","[ENDPOINT]",RegexOptions.IgnoreCase);
  s=Regex.Replace(s,@"AIza[A-Za-z0-9_-]+","[CHAVE OCULTA]");
  s=Regex.Replace(s,@"(?i)(bearer\s+|(?:key|api[_-]?key|access_token|token|authorization)\s*[=:]\s*)[^\s,;]+","[CREDENCIAL OCULTA]");
  s=Regex.Replace(s,@"[\r\n\t]+"," ");return s.Length>400?s[..400]:s;
 }
 public static string Failure(string stage,Exception error,string? secret)=>$"Etapa {stage} — {error.GetType().Name}: {Redact(error.Message,secret)}";
 public static string Closed(string stage,WebSocketCloseStatus? code,string? reason,string? secret)=>$"Etapa {stage} — WebSocket encerrado: {(code.HasValue?((int)code.Value).ToString():"sem código")} ({code?.ToString()??"sem status"}). {Redact(reason,secret)}";
 public static string ApiError(JsonElement error,string? secret){
  string Field(string name)=>error.TryGetProperty(name,out var v)?v.ToString():"não informado";
  return "Gemini API — código "+Redact(Field("code"),secret)+"; status "+Redact(Field("status"),secret)+"; "+Redact(Field("message"),secret);
 }
}
