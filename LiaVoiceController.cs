using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LealInfoPDV;

/// <summary>LIA VOZ V3: microfone do WebView2 -> LIA Core -> resposta falada. Sem painel de texto.</summary>
public sealed class LiaVoiceController : IDisposable
{
    private readonly MainForm main;
    private readonly LiaOrbForm orbe;
    private readonly WebView2 web = new();
    private static readonly HttpClient VozHttp = new() { Timeout = TimeSpan.FromSeconds(25) };
    private static readonly Dictionary<string,string> CacheVoz = new(StringComparer.Ordinal);
    private static readonly object CacheVozSync = new();
    private bool encerrado;
    private bool processando;
    private bool webPronto;
    private TaskCompletionSource<bool>? falaTerminou;
    private readonly string webFolder;
    private Form? fechamentoPendente;
    private bool fechamentoSistemaPendente;
    private DateTime fechamentoPendenteEm;
    public event EventHandler? Encerrado;

    public LiaVoiceController(MainForm mainForm, LiaOrbForm orbForm)
    {
        main = mainForm; orbe = orbForm;
        webFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO PDV", "LIA_VOZ_WEB");
        web.Size = new Size(2, 2); web.Location = new Point(-50, -50); web.Visible = true;
        web.CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO PDV", "WebView2", "LIA_VOZ_V3") };
        orbe.Controls.Add(web); web.SendToBack();
        // O clique na orbe funciona como botão liga/desliga. Se a LIA já está ativa,
        // um novo clique encerra toda a sessão (microfone, fala, WebView2 e orbe).
        orbe.OrbClicked += (_, _) => Encerrar();
    }

    public async Task IniciarAsync(){if(encerrado)return;try{orbe.SetEstado("PREPARANDO");await PrepararWebAsync();if(!encerrado)await IniciarEscutaAsync();}catch{if(!encerrado)orbe.SetEstado("ERRO");}}
    private async Task PrepararWebAsync(){Directory.CreateDirectory(webFolder);string html=Path.Combine(webFolder,"voice.html");await File.WriteAllTextAsync(html,HtmlVoz(),Encoding.UTF8);await web.EnsureCoreWebView2Async();if(web.CoreWebView2 is null)throw new InvalidOperationException("WebView2 indisponível.");web.CoreWebView2.SetVirtualHostNameToFolderMapping("lia.local",webFolder,CoreWebView2HostResourceAccessKind.Allow);web.CoreWebView2.PermissionRequested+=(_,e)=>{if(e.PermissionKind==CoreWebView2PermissionKind.Microphone){e.State=CoreWebView2PermissionState.Allow;e.Handled=true;}};web.CoreWebView2.WebMessageReceived+=AoReceberMensagem;var tcs=new TaskCompletionSource<bool>();void Navegou(object? s,CoreWebView2NavigationCompletedEventArgs e){web.CoreWebView2.NavigationCompleted-=Navegou;if(e.IsSuccess)tcs.TrySetResult(true);else tcs.TrySetException(new InvalidOperationException("Falha ao preparar a escuta."));}web.CoreWebView2.NavigationCompleted+=Navegou;web.Source=new Uri("https://lia.local/voice.html");await tcs.Task;webPronto=true;}
    private async Task IniciarEscutaAsync(){if(!webPronto||web.CoreWebView2 is null||encerrado||processando)return;orbe.SetEstado("OUVINDO");try{await web.CoreWebView2.ExecuteScriptAsync("window.liaStart && window.liaStart();");}catch{}}
    private async void AoReceberMensagem(object? sender,CoreWebView2WebMessageReceivedEventArgs e){if(encerrado)return;string msg;try{msg=e.TryGetWebMessageAsString();}catch{return;}if(msg.StartsWith("TXT|",StringComparison.Ordinal)){var texto=msg[4..].Trim();if(texto.Length==0||processando)return;processando=true;try{RegistrarLog("OUVIU",texto);orbe.SetEstado("PENSANDO");var resposta=await ProcessarAsync(texto);RegistrarLog("RESPOSTA",resposta);await FalarAsync(resposta);}finally{processando=false;if(!encerrado){orbe.SetEstado("PRONTA");await Task.Delay(90);await IniciarEscutaAsync();}}return;}if(msg=="SPKEND"){falaTerminou?.TrySetResult(true);return;}if(msg=="LISTEN_END"){if(!processando&&!encerrado){await Task.Delay(90);await IniciarEscutaAsync();}return;}if(msg.StartsWith("ERR|",StringComparison.Ordinal)){RegistrarLog("MIC",msg);if(!processando&&!encerrado){orbe.SetEstado("PRONTA");await Task.Delay(250);await IniciarEscutaAsync();}}}

    private static string LimparTextoParaVoz(string texto)
    {
        if(string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var t = texto;
        t = Regex.Replace(t, @"```[\s\S]*?```", " ");
        t = Regex.Replace(t, @"`([^`]*)`", "$1");
        t = Regex.Replace(t, @"!\[([^\]]*)\]\([^\)]*\)", "$1");
        t = Regex.Replace(t, @"\[([^\]]+)\]\([^\)]*\)", "$1");
        t = Regex.Replace(t, @"^\s{0,3}#{1,6}\s*", "", RegexOptions.Multiline);
        t = Regex.Replace(t, @"^\s*[-+>]\s+", "", RegexOptions.Multiline);
        t = Regex.Replace(t, @"^\s*\d+[\.)]\s+", "", RegexOptions.Multiline);
        t = t.Replace("**", "").Replace("__", "").Replace("*", "").Replace("_", "");
        t = t.Replace("•", "").Replace("#", "").Replace("~", "");
        t = Regex.Replace(t, @"\s+", " ").Trim();
        return t;
    }

    private static string? ChaveOpenAi() => Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User) ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    private static async Task<string?> GerarAudioNaturalAsync(string texto)
    {
        lock(CacheVozSync) if(CacheVoz.TryGetValue(texto,out var pronto)) return pronto;
        var chave=ChaveOpenAi();if(string.IsNullOrWhiteSpace(chave))return null;
        try
        {
            var payload=new{model="gpt-4o-mini-tts",voice="marin",input=texto,instructions="Fale em português do Brasil, com voz feminina natural, calorosa, clara e profissional. Ritmo de conversa normal, sem soar robótica.",response_format="mp3",speed=1.04};
            using var req=new HttpRequestMessage(HttpMethod.Post,"https://api.openai.com/v1/audio/speech");
            req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",chave);
            req.Content=new StringContent(JsonSerializer.Serialize(payload),Encoding.UTF8,"application/json");
            using var resp=await VozHttp.SendAsync(req);if(!resp.IsSuccessStatusCode)return null;
            var bytes=await resp.Content.ReadAsByteArrayAsync();if(bytes.Length==0)return null;
            var audio=Convert.ToBase64String(bytes);
            if(texto.Length<=160) lock(CacheVozSync){if(CacheVoz.Count<48)CacheVoz[texto]=audio;}
            return audio;
        }
        catch{return null;}
    }

    private async Task FalarAsync(string texto)
    {
        if(encerrado||!webPronto||web.CoreWebView2 is null||string.IsNullOrWhiteSpace(texto))return;
        var textoVoz=LimparTextoParaVoz(texto);if(string.IsNullOrWhiteSpace(textoVoz))return;
        orbe.SetEstado("FALANDO");
        try{await web.CoreWebView2.ExecuteScriptAsync("window.liaStop && window.liaStop();");}catch{}
        falaTerminou=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var audio=await GerarAudioNaturalAsync(textoVoz);
        if(!string.IsNullOrWhiteSpace(audio))
        {
            var jsAudio=JsonSerializer.Serialize(audio);
            await web.CoreWebView2.ExecuteScriptAsync($"window.liaSpeakAudio && window.liaSpeakAudio({jsAudio});");
        }
        else
        {
            var jsTexto=JsonSerializer.Serialize(textoVoz);
            await web.CoreWebView2.ExecuteScriptAsync($"window.liaSpeak && window.liaSpeak({jsTexto});");
        }
        var limite=Task.Delay(Math.Clamp(textoVoz.Length*200,12000,70000));
        await Task.WhenAny(falaTerminou.Task,limite);
        falaTerminou=null;
    }

    private async Task<string> ProcessarAsync(string texto)
    {
        var n=Normalizar(texto);

        var respostaConfirmacao=ProcessarConfirmacaoFechamento(n);
        if(respostaConfirmacao is not null)return respostaConfirmacao;

        if(n is "lia" or "liah" or "leah" or "leia" or "li a" or "lhiya")return "Oi? Tô aqui.";
        if(Tem(n,"lia ta ai","lia esta ai","lia responde","lia me escuta","lia me ouve","ei lia","o lia","oh lia","lia vem ca"))return "Tô aqui. Fala comigo.";
        if(Tem(n,"oi lia","ola lia","oi","ola","bom dia","boa tarde","boa noite","bom dia lia","boa tarde lia","boa noite lia","lia bom dia","lia boa tarde","lia boa noite")){if(n.Contains("bom dia"))return $"Bom dia, {Auth.OperatorName}. Como posso ajudar?";if(n.Contains("boa tarde"))return $"Boa tarde, {Auth.OperatorName}. Como posso ajudar?";if(n.Contains("boa noite"))return $"Boa noite, {Auth.OperatorName}. Como posso ajudar?";return $"Oi, {Auth.OperatorName}. Como posso ajudar?";}
        if(Tem(n,"ta me ouvindo","esta me ouvindo","voce me ouve","consegue me ouvir"))return $"Sim, {Auth.OperatorName}. Estou ouvindo você.";
        if(Tem(n,"quem e voce","quem voce e","seu nome","qual seu nome","como voce se chama"))return "Eu sou a LIA, assistente do LEAL INFO PDV.";
        if(Tem(n,"obrigado","obrigada","valeu"))return "Por nada. Estou pronta para ajudar.";
        var d=LiaCore.Classificar(n);if(d.RespostaImediata is not null)return d.RespostaImediata;
        try
        {
            if(d.Intencao=="CONVERSA_AI"){var respostaAi=await LiaCore.ConversarAsync(texto);return string.IsNullOrWhiteSpace(respostaAi)?"Minha conversa online não respondeu agora. Os comandos do PDV continuam funcionando normalmente.":respostaAi;}
            return d.Intencao switch
            {
                "PERMISSOES"=>LiaCore.ResumoPermissoes(),
                "CADASTRO_AMBIGUO"=>"Claro. O que você quer cadastrar: produto, cliente, fornecedor ou outra coisa?",
                "CADASTRAR_PRODUTO"=>Acao("Abrindo o cadastro de novo produto.",main.LiaCadastrarNovoProduto),
                "CONSULTAR_ESTOQUE_PRODUTO"=>ConsultarEstoqueProduto(n),
                "RESUMO_EMPRESA"=>Auth.IsManager?ResumoEmpresa():"Essa visão geral é gerencial. Chame o gerente ou proprietário.",
                "VENDAS_HOJE"=>VendasHoje(),"ESTOQUE_BAIXO"=>EstoqueBaixo(),"CLIENTES"=>QuantidadeClientes(),
                "SALDO_CAIXA"=>Auth.IsManager?SaldoCaixa():"Essa informação é restrita. Chame o gerente.",
                "CONTAS_PAGAR"=>Auth.IsManager?"O PDV atual ainda não possui uma agenda separada de contas a pagar. Posso abrir o Financeiro.":"Essa informação é do Financeiro. Chame o gerente.",
                "ABRIR_PRODUTOS"=>Acao("Abrindo Produtos.",main.LiaAbrirProdutos),
                "ABRIR_CLIENTES"=>Acao("Abrindo Clientes.",main.LiaAbrirClientes),
                "ABRIR_FORNECEDORES"=>AbrirTelaMain("OpenSuppliers","Fornecedores"),
                "ABRIR_SERVICOS"=>AbrirTelaMain("OpenServices","Serviços"),
                "ABRIR_HISTORICO"=>AbrirTelaMain("OpenHistory","Histórico de vendas"),
                "ABRIR_FINANCEIRO"=>Auth.IsManager?Acao("Abrindo Financeiro.",main.LiaAbrirFinanceiro):"Seu perfil não tem acesso ao Financeiro. Chame o gerente.",
                "ABRIR_ORDENS"=>AbrirTelaMain("OpenOrders","Ordens de serviço"),
                "ABRIR_ORCAMENTOS"=>AbrirTelaMain("OpenQuotes","Orçamentos"),
                "ABRIR_VENDAS"=>Acao("Abrindo o PDV.",main.LiaAbrirVendas),
                "ABRIR_RELATORIOS"=>Auth.IsManager?Acao("Abrindo Relatórios.",main.LiaAbrirRelatorios):"Relatórios gerenciais exigem autorização. Chame o gerente.",
                "FAZER_BACKUP"=>AbrirTelaMain("Backup","Backup"),
                "ABRIR_CONFIGURACOES"=>AbrirConfiguracoes(),
                "FECHAR_TELA"=>PrepararFechamento(n),
                _=>"Ainda não aprendi esse pedido. Pode falar de outro jeito?"
            };
        }
        catch(Exception ex){RegistrarLog("ERRO_CORE",ex.Message);return "Não consegui consultar o PDV agora. Tente novamente.";}
    }

    private string AbrirTelaMain(string metodo,string nome)
    {
        try
        {
            var m=typeof(MainForm).GetMethod(metodo,BindingFlags.Instance|BindingFlags.NonPublic);
            if(m is null)return $"Não encontrei a tela {nome} nesta versão.";
            main.BeginInvoke(new Action(()=>m.Invoke(main,null)));
            return $"Abrindo {nome}.";
        }
        catch{return $"Não consegui abrir {nome} agora.";}
    }

    private string AbrirConfiguracoes()
    {
        try
        {
            var metodo=typeof(MainForm).GetMethod("OpenSettings",BindingFlags.Instance|BindingFlags.NonPublic);
            if(metodo is null)return "Não encontrei a tela de Configurações nesta versão.";
            main.BeginInvoke(new Action(()=>metodo.Invoke(main,null)));
            return "Abrindo Configurações.";
        }
        catch{return "Não consegui abrir Configurações agora.";}
    }

    private string PrepararFechamento(string n)
    {
        bool querSistema=Tem(n,"fecha o sistema","fechar o sistema","fecha o programa","fechar o programa","sair do sistema","encerrar o sistema");
        var alvo=querSistema?main:ObterTelaParaFechar();
        if(alvo is null)return "Você já está na tela principal. Me diga qual tela quer abrir.";
        fechamentoPendente=alvo;
        fechamentoSistemaPendente=querSistema || ReferenceEquals(alvo,main);
        fechamentoPendenteEm=DateTime.Now;
        bool editando=TelaTemDadosEmEdicao(alvo);
        if(fechamentoSistemaPendente)return editando?"Tem informações em edição. Tem certeza que quer fechar o sistema?":"Tem certeza que quer fechar o sistema?";
        var nome=NomeTela(alvo);
        return editando?$"Tem informações preenchidas ou em edição na tela {nome}. Tem certeza que quer fechar?":$"Tem certeza que quer fechar a tela {nome}?";
    }

    private string? ProcessarConfirmacaoFechamento(string n)
    {
        if(fechamentoPendente is null)return null;
        if((DateTime.Now-fechamentoPendenteEm)>TimeSpan.FromSeconds(25)||fechamentoPendente.IsDisposed){LimparFechamentoPendente();return null;}
        if(Tem(n,"nao","não","cancela","cancelar","deixa","deixa aberta","nao fecha","não fecha")){LimparFechamentoPendente();return "Beleza, deixei a tela aberta.";}
        if(!Tem(n,"sim","pode","pode fechar","fecha","feche","confirmo","isso","isso mesmo","pode sim"))return "Só confirma pra mim: sim ou não?";
        var alvo=fechamentoPendente;var sistema=fechamentoSistemaPendente;LimparFechamentoPendente();
        try
        {
            if(sistema){main.BeginInvoke(new Action(()=>main.Close()));return "Certo, fechando o sistema.";}
            alvo.BeginInvoke(new Action(()=>{if(!alvo.IsDisposed)alvo.Close();}));
            return "Pronto, fechei a tela.";
        }
        catch{return "Não consegui fechar essa tela agora.";}
    }

    private void LimparFechamentoPendente(){fechamentoPendente=null;fechamentoSistemaPendente=false;fechamentoPendenteEm=default;}

    private Form? ObterTelaParaFechar()
    {
        var ativa=Form.ActiveForm;
        if(ativa is not null && ativa.Visible && !ativa.IsDisposed && !ReferenceEquals(ativa,main) && !ReferenceEquals(ativa,orbe))return ativa;
        return Application.OpenForms.Cast<Form>().Reverse().FirstOrDefault(f=>f.Visible&&!f.IsDisposed&&!ReferenceEquals(f,main)&&!ReferenceEquals(f,orbe));
    }

    private static string NomeTela(Form f)
    {
        var t=(f.Text??"").Trim();
        if(string.IsNullOrWhiteSpace(t))return "atual";
        return t.Length>45?t[..45]:t;
    }

    private static bool TelaTemDadosEmEdicao(Control raiz)
    {
        foreach(Control c in raiz.Controls)
        {
            if(c is TextBoxBase tb && tb.Visible && tb.Enabled && !tb.ReadOnly && (tb.Modified || (tb.Focused && !string.IsNullOrWhiteSpace(tb.Text))))return true;
            if(c is ComboBox cb && cb.Visible && cb.Enabled && cb.Focused && cb.SelectedIndex>=0)return true;
            if(c is NumericUpDown nu && nu.Visible && nu.Enabled && nu.Focused)return true;
            if(c is DateTimePicker dt && dt.Visible && dt.Enabled && dt.Focused)return true;
            if(c is DataGridView dg && dg.Visible && dg.Enabled && (dg.IsCurrentCellDirty || dg.IsCurrentRowDirty))return true;
            if(c.HasChildren && TelaTemDadosEmEdicao(c))return true;
        }
        return false;
    }

    private string ConsultarEstoqueProduto(string n)
    {
        string termo=n;
        string[] prefixos={"quanto tem de ","quantos tem de ","quantas tem de ","quanto tem no estoque de ","quantos tem no estoque de ","quantas tem no estoque de ","estoque do ","estoque da ","estoque de ","quantidade de "};
        foreach(var p in prefixos){var i=termo.IndexOf(p,StringComparison.Ordinal);if(i>=0){termo=termo[(i+p.Length)..];break;}}
        termo=termo.Replace(" no estoque","").Replace(" em estoque","").Trim(' ','?','!','.');
        if(string.IsNullOrWhiteSpace(termo))return "Qual produto você quer consultar no estoque?";
        using var cn=Database.Open();using var c=cn.CreateCommand();c.CommandText="SELECT name, stock FROM products WHERE active=1 AND (lower(name) LIKE @q OR barcode=@exact) ORDER BY CASE WHEN lower(name)=@exact THEN 0 ELSE 1 END, name LIMIT 3";c.Parameters.AddWithValue("@q","%"+termo.ToLowerInvariant()+"%");c.Parameters.AddWithValue("@exact",termo.ToLowerInvariant());using var r=c.ExecuteReader();var achados=new List<(string Nome,double Estoque)>();while(r.Read())achados.Add((r.GetString(0),Convert.ToDouble(r.GetValue(1),CultureInfo.InvariantCulture)));if(achados.Count==0)return $"Não encontrei {termo} no cadastro de produtos.";if(achados.Count>1)return "Encontrei mais de um produto: "+string.Join(", ",achados.Select(x=>x.Nome))+". Qual deles?";var pdt=achados[0];return $"{pdt.Nome} está com {pdt.Estoque:0.##} unidades em estoque.";
    }
    private static void RegistrarLog(string tipo,string texto){try{var pasta=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LEAL INFO PDV");Directory.CreateDirectory(pasta);File.AppendAllText(Path.Combine(pasta,"lia_voice.log"),$"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {tipo} | {texto}{Environment.NewLine}",Encoding.UTF8);}catch{}}
    private string Acao(string resposta,Action acao){main.BeginInvoke(acao);return resposta;}
    private string ResumoEmpresa(){using var cn=Database.Open();int produtos=ScalarInt(cn,"SELECT COUNT(*) FROM products WHERE active=1");int baixos=ScalarInt(cn,"SELECT COUNT(*) FROM products WHERE active=1 AND stock <= min_stock");int clientes=ScalarInt(cn,"SELECT COUNT(*) FROM customers");int vendas=ScalarInt(cn,"SELECT COUNT(*) FROM sales WHERE date(sold_at)=date('now','localtime')");double total=ScalarDouble(cn,"SELECT COALESCE(SUM(total),0) FROM sales WHERE date(sold_at)=date('now','localtime')");return $"Hoje foram {vendas} vendas, totalizando {Moeda(total)}. Você tem {produtos} produtos ativos, {baixos} com estoque baixo e {clientes} clientes cadastrados.";}
    private string VendasHoje(){using var cn=Database.Open();int q=ScalarInt(cn,"SELECT COUNT(*) FROM sales WHERE date(sold_at)=date('now','localtime')");double t=ScalarDouble(cn,"SELECT COALESCE(SUM(total),0) FROM sales WHERE date(sold_at)=date('now','localtime')");return $"Hoje o PDV registra {q} vendas, totalizando {Moeda(t)}.";}
    private string EstoqueBaixo(){using var cn=Database.Open();int q=ScalarInt(cn,"SELECT COUNT(*) FROM products WHERE active=1 AND stock <= min_stock");return q==0?"Não encontrei produtos abaixo do estoque mínimo agora.":$"Existem {q} produtos no estoque mínimo ou abaixo dele.";}
    private string QuantidadeClientes(){using var cn=Database.Open();return $"Existem {ScalarInt(cn,"SELECT COUNT(*) FROM customers")} clientes cadastrados no PDV.";}
    private string SaldoCaixa(){using var cn=Database.Open();double e=ScalarDouble(cn,"SELECT COALESCE(SUM(amount),0) FROM cash_movements WHERE upper(type)='ENTRADA'");double s=ScalarDouble(cn,"SELECT COALESCE(SUM(amount),0) FROM cash_movements WHERE upper(type) IN ('SAÍDA','SAIDA')");return $"O saldo calculado do caixa é {Moeda(e-s)}.";}
    private static int ScalarInt(SqliteConnection cn,string sql){using var c=cn.CreateCommand();c.CommandText=sql;return Convert.ToInt32(c.ExecuteScalar()??0);}
    private static double ScalarDouble(SqliteConnection cn,string sql){using var c=cn.CreateCommand();c.CommandText=sql;return Convert.ToDouble(c.ExecuteScalar()??0,CultureInfo.InvariantCulture);}
    private static string Moeda(double v)=>v.ToString("C2",new CultureInfo("pt-BR"));
    private static bool Tem(string n,params string[] xs)=>xs.Any(x=>n.Contains(Normalizar(x),StringComparison.Ordinal));
    private static string Normalizar(string t){var f=t.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);var sb=new StringBuilder();foreach(var c in f)if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)sb.Append(c);return sb.ToString().Normalize(NormalizationForm.FormC);}

    private static string HtmlVoz()=>"""
<!doctype html><html><head><meta charset="utf-8"></head><body><script>
let rec=null,ativo=false,parando=false,audioAtual=null,ultimoInterim='',enviou=false;function post(x){try{chrome.webview.postMessage(x);}catch(e){}}
window.liaStop=function(){parando=true;try{if(rec){rec.onend=null;rec.onerror=null;rec.abort();}}catch(e){}try{speechSynthesis.cancel();}catch(e){}try{if(audioAtual){audioAtual.pause();audioAtual.src='';audioAtual=null;}}catch(e){}ativo=false;rec=null;ultimoInterim='';enviou=false;};
window.liaStart=function(){if(ativo)return;parando=false;ultimoInterim='';enviou=false;try{speechSynthesis.cancel();}catch(e){}try{if(audioAtual){audioAtual.pause();audioAtual=null;}}catch(e){}const SR=window.SpeechRecognition||window.webkitSpeechRecognition;if(!SR){post('ERR|speech-recognition-indisponivel');return;}try{rec=new SR();rec.lang='pt-BR';rec.continuous=false;rec.interimResults=true;rec.maxAlternatives=3;rec.onstart=()=>{ativo=true;};rec.onresult=(e)=>{for(let i=e.resultIndex;i<e.results.length;i++){const t=(e.results[i]?.[0]?.transcript||'').trim();if(!t)continue;if(e.results[i].isFinal){enviou=true;post('TXT|'+t);}else{ultimoInterim=t;}}};rec.onerror=(e)=>{ativo=false;if(!parando)post('ERR|'+(e.error||'erro-desconhecido'));};rec.onend=()=>{ativo=false;rec=null;if(!parando){if(!enviou&&ultimoInterim.trim()){enviou=true;post('TXT|'+ultimoInterim.trim());}else post('LISTEN_END');}};rec.start();}catch(e){ativo=false;rec=null;if(!parando)post('ERR|'+(e.message||String(e)));}};
window.liaSpeakAudio=function(base64){try{window.liaStop();parando=false;audioAtual=new Audio('data:audio/mpeg;base64,'+base64);audioAtual.onended=()=>{audioAtual=null;post('SPKEND');};audioAtual.onerror=()=>{audioAtual=null;post('SPKEND');};audioAtual.play().catch(()=>post('SPKEND'));}catch(e){post('SPKEND');}};
function vozPreferida(){const vs=speechSynthesis.getVoices();const br=vs.filter(v=>(v.lang||'').toLowerCase().startsWith('pt-br'));const pt=vs.filter(v=>(v.lang||'').toLowerCase().startsWith('pt'));const base=br.length?br:(pt.length?pt:vs);const score=v=>{const n=(v.name||'').toLowerCase();let s=0;if((v.lang||'').toLowerCase().startsWith('pt-br'))s+=100;if(/natural|online/.test(n))s+=80;if(/thalita/.test(n))s+=70;if(/francisca/.test(n))s+=65;if(/maria/.test(n))s+=55;if(/luciana|fernanda/.test(n))s+=45;if(/female|feminina/.test(n))s+=30;if(/daniel|antonio|male|masculin/.test(n))s-=100;return s;};return [...base].sort((a,b)=>score(b)-score(a))[0]||null;}
window.liaSpeak=function(texto){try{window.liaStop();parando=false;speechSynthesis.cancel();const falar=()=>{const u=new SpeechSynthesisUtterance(texto);const v=vozPreferida();u.lang='pt-BR';if(v)u.voice=v;u.rate=0.98;u.pitch=1.04;u.volume=1.0;u.onend=()=>post('SPKEND');u.onerror=()=>post('SPKEND');speechSynthesis.speak(u);};if(speechSynthesis.getVoices().length)falar();else{let foi=false;const uma=()=>{if(foi)return;foi=true;falar();};speechSynthesis.addEventListener('voiceschanged',uma,{once:true});setTimeout(uma,500);}}catch(e){post('SPKEND');}};
</script></body></html>
""";
    public void Encerrar()
    {
        if(encerrado)return;
        encerrado=true;
        falaTerminou?.TrySetResult(true);
        falaTerminou=null;
        try{if(web.CoreWebView2 is not null)web.CoreWebView2.WebMessageReceived-=AoReceberMensagem;}catch{}
        // Dispose do WebView2 corta imediatamente captura do microfone e qualquer áudio em execução.
        try{web.Dispose();}catch{}
        try{if(!orbe.IsDisposed)orbe.Close();}catch{}
        Encerrado?.Invoke(this,EventArgs.Empty);
    }
    public void Dispose(){Encerrar();try{web.Dispose();}catch{}}
}
