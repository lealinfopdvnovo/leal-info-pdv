using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LealInfoPDV;

/// <summary>LIA VOZ V3: microfone do WebView2 -> LIA Core -> resposta falada. Sem painel de texto.</summary>
public sealed class LiaVoiceController : IDisposable
{
    private readonly MainForm main;
    private readonly LiaOrbForm orbe;
    private readonly WebView2 web = new();
    private bool encerrado;
    private bool processando;
    private bool webPronto;
    private TaskCompletionSource<bool>? falaTerminou;
    private readonly string webFolder;

    public event EventHandler? Encerrado;

    public LiaVoiceController(MainForm mainForm, LiaOrbForm orbForm)
    {
        main = mainForm;
        orbe = orbForm;
        webFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO PDV", "LIA_VOZ_WEB");

        web.Size = new Size(2, 2);
        web.Location = new Point(-50, -50);
        web.Visible = true;
        web.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO PDV", "WebView2", "LIA_VOZ_V3")
        };
        orbe.Controls.Add(web);
        web.SendToBack();
        orbe.OrbClicked += async (_, _) => { if (!encerrado && !processando) await IniciarEscutaAsync(); };
    }

    public async Task IniciarAsync()
    {
        if (encerrado) return;
        try
        {
            orbe.SetEstado("PREPARANDO");
            await PrepararWebAsync();
            if (encerrado) return;
            await IniciarEscutaAsync();
        }
        catch
        {
            if (encerrado) return;
            orbe.SetEstado("ERRO");
        }
    }

    private async Task PrepararWebAsync()
    {
        Directory.CreateDirectory(webFolder);
        string html = Path.Combine(webFolder, "voice.html");
        await File.WriteAllTextAsync(html, HtmlVoz(), Encoding.UTF8);

        await web.EnsureCoreWebView2Async();
        if (web.CoreWebView2 is null) throw new InvalidOperationException("WebView2 indisponível.");

        web.CoreWebView2.SetVirtualHostNameToFolderMapping("lia.local", webFolder, CoreWebView2HostResourceAccessKind.Allow);
        web.CoreWebView2.PermissionRequested += (_, e) =>
        {
            if (e.PermissionKind == CoreWebView2PermissionKind.Microphone)
            {
                e.State = CoreWebView2PermissionState.Allow;
                e.Handled = true;
            }
        };
        web.CoreWebView2.WebMessageReceived += AoReceberMensagem;

        var tcs = new TaskCompletionSource<bool>();
        void Navegou(object? s, CoreWebView2NavigationCompletedEventArgs e)
        {
            web.CoreWebView2.NavigationCompleted -= Navegou;
            if (e.IsSuccess) tcs.TrySetResult(true);
            else tcs.TrySetException(new InvalidOperationException("Falha ao preparar a escuta."));
        }
        web.CoreWebView2.NavigationCompleted += Navegou;
        web.Source = new Uri("https://lia.local/voice.html");
        await tcs.Task;
        webPronto = true;
    }

    private async Task IniciarEscutaAsync()
    {
        if (!webPronto || web.CoreWebView2 is null || encerrado) return;
        orbe.SetEstado("OUVINDO");
        await web.CoreWebView2.ExecuteScriptAsync("window.liaStart && window.liaStart();");
    }

    private async void AoReceberMensagem(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (encerrado) return;
        string msg;
        try { msg = e.TryGetWebMessageAsString(); } catch { return; }

        if (msg.StartsWith("TXT|", StringComparison.Ordinal))
        {
            var texto = msg[4..].Trim();
            if (texto.Length == 0 || processando) return;
            processando = true;
            try
            {
                RegistrarLog("OUVIU", texto);
                orbe.SetEstado("PENSANDO");
                var resposta = await ProcessarAsync(texto);
                RegistrarLog("RESPOSTA", resposta);
                await FalarAsync(resposta);
            }
            finally
            {
                processando = false;
                if (!encerrado)
                {
                    orbe.SetEstado("PRONTA");
                    await Task.Delay(300);
                    await IniciarEscutaAsync();
                }
            }
            return;
        }

        if (msg == "SPKEND")
        {
            falaTerminou?.TrySetResult(true);
            return;
        }

        if (msg.StartsWith("ERR|", StringComparison.Ordinal))
        {
            orbe.SetEstado("PRONTA");
        }
    }

    private async Task FalarAsync(string texto)
    {
        if (encerrado || !webPronto || web.CoreWebView2 is null || string.IsNullOrWhiteSpace(texto)) return;
        orbe.SetEstado("FALANDO");
        try { await web.CoreWebView2.ExecuteScriptAsync("window.liaStop && window.liaStop();"); } catch { }
        string jsTexto = JsonSerializer.Serialize(texto.Replace("•", ""));
        falaTerminou = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await web.CoreWebView2.ExecuteScriptAsync($"window.liaSpeak && window.liaSpeak({jsTexto});");
        var limite = Task.Delay(Math.Clamp(texto.Length * 95, 2500, 15000));
        await Task.WhenAny(falaTerminou.Task, limite);
        falaTerminou = null;
    }

    private async Task<string> ProcessarAsync(string texto)
    {
        var n = Normalizar(texto);
        if (Tem(n, "oi lia", "ola lia", "oi", "ola", "bom dia", "boa tarde", "boa noite", "bom dia lia", "boa tarde lia", "boa noite lia", "lia bom dia", "lia boa tarde", "lia boa noite"))
        {
            if (n.Contains("bom dia")) return $"Bom dia, {Auth.OperatorName}. Como posso ajudar?";
            if (n.Contains("boa tarde")) return $"Boa tarde, {Auth.OperatorName}. Como posso ajudar?";
            if (n.Contains("boa noite")) return $"Boa noite, {Auth.OperatorName}. Como posso ajudar?";
            return $"Oi, {Auth.OperatorName}. Como posso ajudar?";
        }

        if (Tem(n, "ta me ouvindo", "esta me ouvindo", "voce me ouve", "consegue me ouvir")) return $"Sim, {Auth.OperatorName}. Estou ouvindo você.";
        if (Tem(n, "quem e voce", "quem voce e", "seu nome")) return "Eu sou a LIA, assistente do LEAL INFO PDV.";
        if (Tem(n, "obrigado", "obrigada", "valeu")) return "Por nada. Estou pronta para ajudar.";

        var d = LiaCore.Classificar(n);
        if (d.RespostaImediata is not null) return d.RespostaImediata;
        try
        {
            if (d.Intencao == "CONVERSA_AI")
            {
                var respostaAi = await LiaCore.ConversarAsync(texto);
                return string.IsNullOrWhiteSpace(respostaAi)
                    ? "Minha conversa online não respondeu agora. Os comandos do PDV continuam funcionando normalmente."
                    : respostaAi;
            }

            return d.Intencao switch
            {
                "PERMISSOES" => LiaCore.ResumoPermissoes(),
                "CADASTRO_AMBIGUO" => "Claro. O que você quer cadastrar: produto, cliente, fornecedor ou outra coisa?",
                "CADASTRAR_PRODUTO" => "Para cadastrar produto, abra Produtos e escolha Novo. Se quiser, diga: abrir produtos.",
                "RESUMO_EMPRESA" => Auth.IsManager ? ResumoEmpresa() : "Essa visão geral é gerencial. Chame o gerente ou proprietário.",
                "VENDAS_HOJE" => VendasHoje(),
                "ESTOQUE_BAIXO" => EstoqueBaixo(),
                "CLIENTES" => QuantidadeClientes(),
                "SALDO_CAIXA" => Auth.IsManager ? SaldoCaixa() : "Essa informação é restrita. Chame o gerente.",
                "CONTAS_PAGAR" => Auth.IsManager ? "O PDV atual ainda não possui uma agenda separada de contas a pagar. Posso abrir o Financeiro." : "Essa informação é do Financeiro. Chame o gerente.",
                "ABRIR_PRODUTOS" => Acao("Abrindo Produtos.", main.LiaAbrirProdutos),
                "ABRIR_VENDAS" => Acao("Abrindo a Tela de Vendas.", main.LiaAbrirVendas),
                "ABRIR_CLIENTES" => Acao("Abrindo Clientes.", main.LiaAbrirClientes),
                "ABRIR_FINANCEIRO" => Auth.IsManager ? Acao("Abrindo Financeiro.", main.LiaAbrirFinanceiro) : "Seu perfil não tem acesso ao Financeiro. Chame o gerente.",
                "ABRIR_RELATORIOS" => Auth.IsManager ? Acao("Abrindo Relatórios.", main.LiaAbrirRelatorios) : "Relatórios gerenciais exigem autorização. Chame o gerente.",
                _ => "Ainda não aprendi esse pedido. Pode falar de outro jeito?"
            };
        }
        catch (Exception ex) { RegistrarLog("ERRO_CORE", ex.Message); return "Não consegui consultar o PDV agora. Tente novamente."; }
    }

    private static void RegistrarLog(string tipo, string texto)
    {
        try
        {
            var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO PDV");
            Directory.CreateDirectory(pasta);
            File.AppendAllText(Path.Combine(pasta, "lia_voice.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {tipo} | {texto}{Environment.NewLine}", Encoding.UTF8);
        }
        catch { }
    }

    private string Acao(string resposta, Action acao) { main.BeginInvoke(acao); return resposta; }
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

    private static string HtmlVoz() => """
<!doctype html><html><head><meta charset="utf-8"></head><body>
<script>
let rec=null, ativo=false;
function post(x){ try{ chrome.webview.postMessage(x); }catch(e){} }
window.liaStop=function(){try{if(rec){rec.onend=null;rec.onerror=null;rec.abort();}}catch(e){} ativo=false;};
window.liaStart=function(){if(ativo)return;try{speechSynthesis.cancel();}catch(e){} const SR=window.SpeechRecognition||window.webkitSpeechRecognition;if(!SR){post('ERR|speech-recognition-indisponivel');return;}try{rec=new SR();rec.lang='pt-BR';rec.continuous=false;rec.interimResults=false;rec.maxAlternatives=1;rec.onstart=()=>{ativo=true;};rec.onresult=(e)=>{ativo=false;const t=(e.results?.[0]?.[0]?.transcript||'').trim();if(t)post('TXT|'+t);};rec.onerror=(e)=>{ativo=false;post('ERR|'+(e.error||'erro-desconhecido'));};rec.onend=()=>{ativo=false;};rec.start();}catch(e){ativo=false;post('ERR|'+(e.message||String(e)));}};
function vozPreferida(){const vs=speechSynthesis.getVoices();const br=vs.filter(v=>(v.lang||'').toLowerCase().startsWith('pt-br'));const pt=vs.filter(v=>(v.lang||'').toLowerCase().startsWith('pt'));const nomes=[/francisca/i,/maria/i,/thalita/i,/luciana/i,/fernanda/i,/female/i,/feminina/i];for(const rx of nomes){const v=br.find(x=>rx.test(x.name||''));if(v)return v;}for(const rx of nomes){const v=pt.find(x=>rx.test(x.name||''));if(v)return v;}return br[0]||pt[0]||vs[0]||null;}
window.liaSpeak=function(texto){try{window.liaStop();speechSynthesis.cancel();const falar=()=>{const u=new SpeechSynthesisUtterance(texto);const v=vozPreferida();u.lang='pt-BR';if(v)u.voice=v;u.rate=1.0;u.pitch=1.16;u.onend=()=>post('SPKEND');u.onerror=()=>post('SPKEND');speechSynthesis.speak(u);};if(speechSynthesis.getVoices().length)falar();else{let foi=false;const uma=()=>{if(foi)return;foi=true;falar();};speechSynthesis.addEventListener('voiceschanged',uma,{once:true});setTimeout(uma,700);}}catch(e){}};
</script></body></html>
""";

    public void Encerrar()
    {
        if (encerrado) return;
        encerrado=true;
        try { if (web.CoreWebView2 is not null) web.CoreWebView2.WebMessageReceived -= AoReceberMensagem; } catch { }
        try { if (!orbe.IsDisposed) orbe.Close(); } catch { }
        Encerrado?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose(){ Encerrar(); try{web.Dispose();}catch{} }
}
