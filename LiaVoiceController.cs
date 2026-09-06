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
        orbe.OrbClicked += (_, _) => Encerrar();
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
        catch (Exception ex)
        {
            if (encerrado) return;
            orbe.SetEstado("ERRO");
            MessageBox.Show("A LIA não conseguiu acessar o microfone.\n\n" + MensagemCurta(ex), "LIA — Microfone", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Encerrar();
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
                orbe.SetEstado("PENSANDO");
                var resposta = Processar(texto);
                await FalarAsync(resposta);
            }
            finally
            {
                processando = false;
                if (!encerrado) await IniciarEscutaAsync();
            }
            return;
        }

        if (msg.StartsWith("VOICEERR|", StringComparison.Ordinal))
        {
            orbe.SetEstado("SEM VOZ");
            MessageBox.Show("Não encontrei uma voz feminina em português do Brasil instalada neste Windows. A LIA não vai mais usar voz masculina como substituta.", "LIA — Voz feminina", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (msg.StartsWith("ERR|", StringComparison.Ordinal))
        {
            var erro = msg[4..];
            if (erro.Contains("no-speech", StringComparison.OrdinalIgnoreCase) || erro.Contains("aborted", StringComparison.OrdinalIgnoreCase))
            {
                if (!encerrado) await IniciarEscutaAsync();
                return;
            }

            orbe.SetEstado("ERRO");
            MessageBox.Show(
                erro.Contains("not-allowed", StringComparison.OrdinalIgnoreCase)
                    ? "O Windows/Edge bloqueou o microfone para a LIA. Libere o acesso ao microfone para aplicativos de área de trabalho nas configurações do Windows."
                    : "A escuta da LIA falhou: " + erro,
                "LIA — Microfone", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async Task FalarAsync(string texto)
    {
        if (encerrado || !webPronto || web.CoreWebView2 is null || string.IsNullOrWhiteSpace(texto)) return;
        orbe.SetEstado("FALANDO");
        string jsTexto = JsonSerializer.Serialize(texto.Replace("•", ""));
        await web.CoreWebView2.ExecuteScriptAsync($"window.liaSpeak && window.liaSpeak({jsTexto});");
        await Task.Delay(Math.Clamp(texto.Length * 58, 900, 9000));
    }

    private string Processar(string texto)
    {
        var n = Normalizar(texto);
        if (Tem(n, "oi lia", "ola lia", "bom dia lia", "boa tarde lia", "boa noite lia", "lia bom dia", "lia boa tarde", "lia boa noite"))
        {
            if (n.Contains("bom dia")) return $"Bom dia, {Auth.OperatorName}. Como posso ajudar?";
            if (n.Contains("boa tarde")) return $"Boa tarde, {Auth.OperatorName}. Como posso ajudar?";
            if (n.Contains("boa noite")) return $"Boa noite, {Auth.OperatorName}. Como posso ajudar?";
            return $"Oi, {Auth.OperatorName}. Como posso ajudar?";
        }

        var d = LiaCore.Classificar(n);
        if (d.RespostaImediata is not null) return d.RespostaImediata;
        try
        {
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
                _ => "Eu ouvi você, mas ainda não entendi esse pedido. Pode falar de outro jeito?"
            };
        }
        catch { return "Eu ouvi você, mas não consegui consultar o PDV agora."; }
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
    private static string MensagemCurta(Exception ex)=>string.IsNullOrWhiteSpace(ex.Message)?"Falha ao iniciar a escuta.":ex.Message;

    private static string HtmlVoz() => """
<!doctype html><html><head><meta charset="utf-8"></head><body>
<script>
let rec=null, ativo=false;
function post(x){ try{ chrome.webview.postMessage(x); }catch(e){} }
window.liaStart=function(){
  if(ativo) return;
  const SR=window.SpeechRecognition||window.webkitSpeechRecognition;
  if(!SR){ post('ERR|speech-recognition-indisponivel'); return; }
  try{
    rec=new SR(); rec.lang='pt-BR'; rec.continuous=false; rec.interimResults=false; rec.maxAlternatives=1;
    rec.onstart=()=>{ativo=true;};
    rec.onresult=(e)=>{ const t=(e.results?.[0]?.[0]?.transcript||'').trim(); if(t) post('TXT|'+t); };
    rec.onerror=(e)=>{ativo=false; post('ERR|'+(e.error||'erro-desconhecido'));};
    rec.onend=()=>{ativo=false;};
    rec.start();
  }catch(e){ ativo=false; post('ERR|'+(e.message||String(e))); }
};
function liaVozFeminina(){
  const vs=speechSynthesis.getVoices();
  const br=vs.filter(v=>(v.lang||'').toLowerCase().startsWith('pt-br'));
  const nomes=[/francisca/i,/maria/i,/thalita/i,/female/i,/feminina/i];
  for(const rx of nomes){ const v=br.find(x=>rx.test(x.name||'')); if(v)return v; }
  return null;
}
window.liaSpeak=function(texto){
  try{
    speechSynthesis.cancel();
    const falar=()=>{
      const fem=liaVozFeminina();
      if(!fem){ post('VOICEERR|SEM_VOZ_FEMININA_PTBR'); return; }
      const u=new SpeechSynthesisUtterance(texto);
      u.lang='pt-BR'; u.voice=fem; u.rate=1.02; u.pitch=1.0;
      speechSynthesis.speak(u);
    };
    if(speechSynthesis.getVoices().length){ falar(); }
    else{
      const pronto=()=>{ speechSynthesis.removeEventListener('voiceschanged',pronto); falar(); };
      speechSynthesis.addEventListener('voiceschanged',pronto,{once:true});
      setTimeout(falar,800);
    }
  }catch(e){}
};
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
