using System.Text.Json.Nodes;
using LealInfoPDV.Network;
namespace LealInfoPDV;
// Uses the app's existing signed gateway protocol. No database writes or sync store.
internal sealed class AngraPairing : IDisposable
{
 private readonly AngraSyncTransport transport;
 private readonly SemaphoreSlim gate=new(1,1);
 internal string Status {get;private set;}="DESCONECTADO — consultando vínculo…";
 internal string Code {get;private set;}="";
 internal DateTimeOffset? Expires {get;private set;}
 internal string AuthorizationUrl {get;private set;}="";
 internal AngraPairing(AngraSyncTransport? connection=null){transport=connection??new();}
 internal async Task Refresh(){if(!await gate.WaitAsync(0))return;try{await RefreshCore();}catch(Exception e){Status="DESCONECTADO / SEM COMUNICAÇÃO\n"+e.Message;}finally{gate.Release();}}
 private async Task RefreshCore(){var r=await transport.Call("/sync/request-pdv",new(){["name"]="LEAL INFO PDV • "+Environment.MachineName,["bindingOnly"]=true});AuthorizationUrl=r["authorizationUrl"]?.ToString()??"";if(r["approved"]?.GetValue<bool>()!=true){Status="DESCONECTADO — autorize este computador uma única vez para gerar o código.";return;}var s=await transport.Call("/sync/status",new());Status=s["connected"]?.GetValue<bool>()==true?"CONECTADO — "+s["linkedDevices"]+" aplicativo(s) vinculado(s).":"DESCONECTADO — aguardando o código ser informado no aplicativo.";}
 internal async Task<bool> Generate(){Guard();await gate.WaitAsync();try{Code="";Expires=null;await transport.Register();await RefreshCore();if(Status.Contains("autorize"))return false;var r=await transport.Call("/sync/ticket",new());string code=r["code"]?.ToString()??"";if(!System.Text.RegularExpressions.Regex.IsMatch(code,@"^[A-F0-9]{24}$"))throw new IOException("Código de conexão inválido.");Code=code;Expires=DateTimeOffset.FromUnixTimeMilliseconds(r["expires"]!.GetValue<long>());return true;}finally{gate.Release();}}
 internal async Task Revoke(){Guard();await gate.WaitAsync();try{await transport.Call("/sync/revoke",new());Code="";Expires=null;await RefreshCore();}finally{gate.Release();}}
 private static void Guard(){if(!Auth.IsAdmin)throw new InvalidOperationException("Somente o administrador pode configurar a integração.");if(NetworkConfiguration.Current.Mode=="terminal")throw new InvalidOperationException("Gere o código no computador servidor do PDV.");}
 public void Dispose(){transport.Dispose();}
}
