using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using NAudio.Wave;
namespace LiaGeminiCasa;
public sealed class LiveSession : IAsyncDisposable
{
 readonly ClientWebSocket socket=new();
 readonly SemaphoreSlim sendLock=new(1,1);
 readonly CancellationTokenSource lifetime=new(TimeSpan.FromMinutes(5));
 readonly Channel<byte[]> input=Channel.CreateBounded<byte[]>(new BoundedChannelOptions(24){SingleReader=true,FullMode=BoundedChannelFullMode.DropOldest});
 readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
 readonly LiveProtocol protocol;
 readonly Func<string,Task<string>> execute;
 WaveInEvent? mic; WaveOutEvent? speaker; BufferedWaveProvider? output;
 Task? receive;Task? transmit;bool disposed;string stage="CONNECT";string? secret;string? lastFailure;
 public event Action<string>? Transcript;
 public event Action<string>? Status;
 public bool IsActive=>!disposed&&!lifetime.IsCancellationRequested;
 public long AudioChunks {get;private set;}
 public LiveSession(bool navigation,Func<string,Task<string>> execute){protocol=new(navigation);this.execute=execute;}
 public async Task StartAsync(string key,string model,string knowledge){
  if(disposed)throw new ObjectDisposedException(nameof(LiveSession));
  if(string.IsNullOrWhiteSpace(key)||key.Any(char.IsControl))throw new InvalidOperationException("Chave local ausente ou inválida.");
  secret=key.Trim();
  // Never log this URI or provider exceptions: they can contain authentication material.
  var uri=new Uri("wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key="+Uri.EscapeDataString(key.Trim()));
  try{
   await socket.ConnectAsync(uri,lifetime.Token);
   // Prepare playback before consuming any server frame, including immediate post-setup audio.
   stage="AUDIO_OUTPUT";
   output=new BufferedWaveProvider(new WaveFormat(24000,16,1)){BufferDuration=TimeSpan.FromSeconds(20),DiscardOnBufferOverflow=false,ReadFully=true};
   speaker=new WaveOutEvent();speaker.Init(output);speaker.Play();
   stage="SETUP";receive=ReceiveAsync();
   await SendAsync(protocol.Setup(model,knowledge));
   await ready.Task.WaitAsync(TimeSpan.FromSeconds(20),lifetime.Token);
   transmit=TransmitAsync();
   stage="AUDIO_INPUT";
   mic=new WaveInEvent{WaveFormat=new WaveFormat(16000,16,1),BufferMilliseconds=40,NumberOfBuffers=3};
   mic.DataAvailable+=(_,e)=>{if(!lifetime.IsCancellationRequested)input.Writer.TryWrite(e.Buffer.AsSpan(0,e.BytesRecorded).ToArray());};
   mic.RecordingStopped+=(_,e)=>{if(e.Exception!=null){Status?.Invoke("Microfone interrompido; sessão encerrada.");lifetime.Cancel();}};
   mic.StartRecording();stage="LIVE";Status?.Invoke("Ouvindo — sessão experimental de até 5 minutos.");
  }catch(Exception ex){var detail=lastFailure??LiveDiagnostic.Failure(stage,ex,secret);await DisposeAsync();throw new InvalidOperationException(detail+" Sem troca automática de provedor ou ativação de faturamento.");}
 }
 async Task SendAsync(object value){
  await sendLock.WaitAsync(lifetime.Token);
  try{await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(value).AsMemory(),WebSocketMessageType.Text,true,lifetime.Token);}finally{sendLock.Release();}
 }
 async Task TransmitAsync(){try{await foreach(var pcm in input.Reader.ReadAllAsync(lifetime.Token))await SendAsync(LiveProtocol.Audio(pcm));}catch(OperationCanceledException){}catch{Status?.Invoke("Envio de áudio interrompido; encerre e reinicie manualmente.");lifetime.Cancel();}}
 async Task ReceiveAsync(){
  var bytes=new byte[16384];
  try{while(!lifetime.IsCancellationRequested){
   using var message=new LiveFrameReader();ValueWebSocketReceiveResult part;
   do{part=await socket.ReceiveAsync(bytes.AsMemory(),lifetime.Token);if(part.MessageType==WebSocketMessageType.Close){lastFailure=LiveDiagnostic.Closed(stage,socket.CloseStatus,socket.CloseStatusDescription,secret);throw new IOException(lastFailure);}message.Append(bytes.AsSpan(0,part.Count),part.MessageType);}while(!part.EndOfMessage);
   using var doc=message.Parse();var root=doc.RootElement;
   if(root.TryGetProperty("error",out var apiError)){lastFailure=LiveDiagnostic.ApiError(apiError,secret);throw new InvalidOperationException(lastFailure);}
   if(root.TryGetProperty("setupComplete",out _)){ready.TrySetResult();continue;}
   if(root.TryGetProperty("goAway",out _)){Status?.Invoke("Sessão próxima do limite; interrompida sem reconexão automática.");lifetime.Cancel();break;}
   if(root.TryGetProperty("serverContent",out var content)){
    if(content.TryGetProperty("interrupted",out var interrupted)&&interrupted.ValueKind==JsonValueKind.True)output?.ClearBuffer();
    foreach(var field in new[]{"inputTranscription","outputTranscription"})if(content.TryGetProperty(field,out var tr)&&tr.TryGetProperty("text",out var text))Transcript?.Invoke((field=="inputTranscription"?"Você: ":"LIA: ")+text.GetString());
    if(content.TryGetProperty("modelTurn",out var turn)&&turn.TryGetProperty("parts",out var parts))foreach(var p in parts.EnumerateArray())if(p.TryGetProperty("inlineData",out var audio)){
     var mime=audio.GetProperty("mimeType").GetString()??"";if(!mime.StartsWith("audio/pcm")||!mime.Contains("rate=24000"))throw new InvalidDataException("Formato de saída inesperado.");
     var pcm=Convert.FromBase64String(audio.GetProperty("data").GetString()!);if(pcm.Length%2!=0)throw new InvalidDataException("PCM incompleto.");
     if(output==null)throw new InvalidOperationException("Áudio antes da inicialização.");output.AddSamples(pcm,0,pcm.Length);AudioChunks++;Status?.Invoke("LIA respondendo.");
    }
    // Extended Thinking can emit tools/audio after turnComplete; never close here.
   }
   if(root.TryGetProperty("toolCall",out var tools)){
    var responses=new List<object>();foreach(var call in tools.GetProperty("functionCalls").EnumerateArray())responses.Add(await protocol.ToolAsync(call,c=>execute(c).WaitAsync(lifetime.Token)));
    await SendAsync(new{toolResponse=new{functionResponses=responses}});
   }
  }}catch(OperationCanceledException){}catch(Exception ex){lastFailure??=LiveDiagnostic.Failure(stage,ex,secret);ready.TrySetException(new InvalidOperationException(lastFailure));Status?.Invoke(lastFailure+" Sem repetição automática.");lifetime.Cancel();}
  finally{mic?.StopRecording();speaker?.Stop();}
 }
 public async ValueTask DisposeAsync(){
  if(disposed)return;disposed=true;lifetime.Cancel();input.Writer.TryComplete();mic?.StopRecording();socket.Abort();
  foreach(var task in new[]{receive,transmit})if(task!=null)try{await task.WaitAsync(TimeSpan.FromSeconds(3));}catch{}
  mic?.Dispose();speaker?.Stop();speaker?.Dispose();socket.Dispose();lifetime.Dispose();sendLock.Dispose();
 }
}
