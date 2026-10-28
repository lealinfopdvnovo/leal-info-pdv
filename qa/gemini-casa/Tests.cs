using System.Text.Json;
using LiaGeminiCasa;
class Tests{
 static int count;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static JsonElement Call(string id,string screen,string name="abrir_tela")=>JsonSerializer.SerializeToElement(new{id,name,args=new{tela=screen}});
 static async Task Main(){
  foreach(var model in LiveProtocol.Models){var s=JsonSerializer.Serialize(new LiveProtocol(true).Setup(model,"PDV"));Check(s.Contains("models/"+model),"model "+model);Check(s.Contains("NON_BLOCKING"),"async tools");Check(!s.Contains("proactiveAudio")&&!s.Contains("enableAffectiveDialog"),"unsupported flags absent");}
  var conversation=new LiveProtocol(false);Check(!JsonSerializer.Serialize(conversation.Setup(LiveProtocol.DefaultModel,"PDV")).Contains("functionDeclarations"),"conversation has no tools");
  int calls=0;Task<string> Execute(string c){calls++;return Task.FromResult("test only "+c);}
  await conversation.ToolAsync(Call("c","PRODUTOS"),Execute);Check(calls==0,"conversation refuses injected tool");
  var navigation=new LiveProtocol(true);
  foreach(var screen in LiveProtocol.Screens)await navigation.ToolAsync(Call(screen,screen),Execute);
  Check(calls==LiveProtocol.Screens.Length,"all legacy navigation targets dispatched");
  await navigation.ToolAsync(Call("PRODUTOS","PRODUTOS"),Execute);Check(calls==LiveProtocol.Screens.Length,"duplicate call does not repeat action");
  foreach(var screen in new[]{"VIDRACARIA","COZINHA","DELETE","produtos","../x"})await navigation.ToolAsync(Call("deny"+screen,screen),Execute);
  Check(calls==LiveProtocol.Screens.Length,"unsupported commands fail closed");
  await navigation.ToolAsync(Call("x","PRODUTOS","execute_code"),Execute);Check(calls==LiveProtocol.Screens.Length,"unknown function refused");
  await navigation.ToolAsync(Call("close","","fechar_tela"),Execute);Check(calls==LiveProtocol.Screens.Length+1,"close uses existing command");
  var audio=JsonSerializer.SerializeToElement(LiveProtocol.Audio(new byte[]{0,0,1,0}));Check(audio.GetProperty("realtimeInput").GetProperty("audio").GetProperty("mimeType").GetString()=="audio/pcm;rate=16000","input PCM format");
  try{LiveProtocol.Audio(new byte[]{0});throw new Exception("odd PCM accepted");}catch(ArgumentException){Check(true,"odd PCM refused");}
  try{navigation.Setup("paid-unknown","x");throw new Exception("unknown model accepted");}catch(ArgumentException){Check(true,"unknown model refused");}
  navigation.Reset();await navigation.ToolAsync(Call("PRODUTOS","PRODUTOS"),Execute);Check(calls==LiveProtocol.Screens.Length+2,"new session resets deduplication");
  string fake="unit-test-secret-only";
  var redacted=LiveDiagnostic.Redact("wss://test.invalid/?key="+fake+" key="+fake+" Bearer "+fake,fake);
  Check(!redacted.Contains(fake)&&!redacted.Contains("test.invalid"),"credentials and endpoint query redacted");
  var close=LiveDiagnostic.Closed("SETUP",System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation,"model unavailable",fake);
  Check(close.Contains("1008")&&close.Contains("model unavailable")&&close.Contains("SETUP"),"server close code reason and stage preserved");
  var api=LiveDiagnostic.ApiError(JsonSerializer.SerializeToElement(new{code=429,status="RESOURCE_EXHAUSTED",message="quota exceeded"}),fake);
  Check(api.Contains("429")&&api.Contains("RESOURCE_EXHAUSTED"),"API status preserved without guessing root cause");
  var err=LiveDiagnostic.Failure("CONNECT",new IOException("failed "+fake),fake);
  Check(err.Contains("CONNECT")&&!err.Contains(fake),"handshake diagnostic stage and redaction");
  Directory.CreateDirectory("evidence");File.WriteAllText("evidence/gemini-tests.json",JsonSerializer.Serialize(new{Pass=true,Assertions=count,NetworkUsed=false,RealApiTested=false,VoiceCompared=false,Published=false}));
 }
}
