using System.Net.WebSockets;
using System.Text.Json;
namespace LiaGeminiCasa;
public sealed class LiveFrameReader : IDisposable
{
 public const int MaximumBytes=4*1024*1024;
 readonly MemoryStream buffer=new();WebSocketMessageType? messageType;
 public void Append(ReadOnlySpan<byte> data,WebSocketMessageType type){
  if(type is not (WebSocketMessageType.Text or WebSocketMessageType.Binary))throw new InvalidDataException("Tipo de mensagem WebSocket inesperado: "+type);
  if(messageType.HasValue&&messageType!=type)throw new InvalidDataException("Tipo alterado durante mensagem fragmentada.");
  messageType=type;
  if(buffer.Length+data.Length>MaximumBytes)throw new InvalidDataException("Mensagem WebSocket excedeu o limite de 4 MiB.");
  buffer.Write(data);
 }
 public JsonDocument Parse(){
  var document=JsonDocument.Parse(buffer.ToArray());
  if(document.RootElement.ValueKind!=JsonValueKind.Object){document.Dispose();throw new InvalidDataException("Resposta Live deve ser um objeto JSON.");}
  return document;
 }
 public void Dispose()=>buffer.Dispose();
}
