using System.Text.Json;
namespace LiaGeminiCasa;
public sealed class LiveProtocol
{
 public const string DefaultModel="gemini-3.8-live";
 public static readonly string[] Models={DefaultModel,"gemini-3.8-live-extended-thinking"};
 public static readonly string[] Screens={"PRODUTOS","CLIENTES","FORNECEDORES","SERVICOS","ORDENS_SERVICO","ORCAMENTOS","FLUXO_CAIXA","HISTORICO_VENDAS","TELA_VENDAS","RELATORIOS","ENTREGAS","USUARIOS","CONFIGURACOES","CADASTROS","AJUDA_CADASTRO"};
 readonly Dictionary<string,object> completed=new();
 public bool Navigation {get;}
 public LiveProtocol(bool navigation)=>Navigation=navigation;
 public object Setup(string model,string knowledge){
  if(!Models.Contains(model))throw new ArgumentException("Modelo fora da lista experimental.");
  var instruction=knowledge+"\nVocê é LIA. Fale naturalmente em português brasileiro. Não invente dados ou sucesso de comandos. Vidraçaria e Cozinha Premium existem, mas sua abertura por voz não é suportada pelo receptor atual; informe isso e oriente o botão manual. Só abra telas após pedido explícito e confirmação local do operador. Não execute compras, pagamentos, exclusões ou alterações de dados. "+(Navigation?"Modo Navegação: use abrir_tela para telas permitidas.":"Modo Conversa: nenhuma ação ou ferramenta está disponível.");
  var setup=new Dictionary<string,object>{["model"]="models/"+model,["generationConfig"]=new {responseModalities=new[]{"AUDIO"},speechConfig=new{voiceConfig=new{prebuiltVoiceConfig=new{voiceName="Aoede"}}}},["systemInstruction"]=new{parts=new[]{new{text=instruction}}},["inputAudioTranscription"]=new{},["outputAudioTranscription"]=new{}};
  if(model.EndsWith("extended-thinking"))setup["generationConfig"]=new {responseModalities=new[]{"AUDIO"},thinkingConfig=new{thinkingLevel="low"},speechConfig=new{voiceConfig=new{prebuiltVoiceConfig=new{voiceName="Aoede"}}}};
  if(Navigation)setup["tools"]=new[]{new{functionDeclarations=new object[]{new{name="abrir_tela",description="Solicita abertura de uma tela existente; exige confirmação local.",behavior="NON_BLOCKING",parameters=new{type="OBJECT",properties=new{tela=new{type="STRING",@enum=Screens}},required=new[]{"tela"}}},new{name="fechar_tela",description="Solicita fechar a janela atual; exige confirmação local.",behavior="NON_BLOCKING",parameters=new{type="OBJECT",properties=new{}}}}}};
  return new{setup};
 }
 public static object Audio(byte[] pcm){if(pcm.Length==0||pcm.Length%2!=0||pcm.Length>32000)throw new ArgumentException("PCM16 mono 16 kHz inválido.");return new{realtimeInput=new{audio=new{mimeType="audio/pcm;rate=16000",data=Convert.ToBase64String(pcm)}}};}
 public async Task<object> ToolAsync(JsonElement call,Func<string,Task<string>> execute){
  var id=call.TryGetProperty("id",out var i)?i.GetString():null;
  var name=call.TryGetProperty("name",out var n)?n.GetString():null;
  if(string.IsNullOrEmpty(id)||id.Length>200)throw new InvalidDataException("Chamada sem identidade válida.");
  if(completed.TryGetValue(id,out var old))return old;
  string? command=null;
  if(name=="fechar_tela")command="FECHAR_TELA";
  if(name=="abrir_tela"&&call.TryGetProperty("args",out var a)&&a.TryGetProperty("tela",out var t)&&t.ValueKind==JsonValueKind.String&&Screens.Contains(t.GetString()))command=t.GetString();
  string result=!Navigation?"Recusado: modo Conversa.":command==null?"Recusado: comando não suportado.":await execute(command);
  var response=new{id,name,response=new{result}};completed[id]=response;return response;
 }
 public void Reset()=>completed.Clear();
}
