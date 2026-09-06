using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;

namespace LealInfoPDV;

/// <summary>LIA VOZ: microfone -> LIA Core -> resposta falada. Sem painel de texto.</summary>
public sealed class LiaVoiceController : IDisposable
{
    private readonly MainForm main;
    private readonly LiaOrbForm orbe;
    private readonly WebView2 vozWeb = new();
    private SpeechRecognizer? reconhecedor;
    private bool encerrado;
    private bool vozPronta;
    private bool processando;
    private readonly SynchronizationContext ui;

    public event EventHandler? Encerrado;

    public LiaVoiceController(MainForm mainForm, LiaOrbForm orbForm)
    {
        main = mainForm;
        orbe = orbForm;
        ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        vozWeb.Size = new Size(1, 1);
        vozWeb.Location = new Point(-20, -20);
        vozWeb.Visible = true;
        vozWeb.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO PDV", "WebView2", "LIA_VOZ")
        };
        orbe.Controls.Add(vozWeb);
        vozWeb.SendToBack();
        orbe.OrbClicked += (_, _) => Encerrar();
    }

    public async Task IniciarAsync()
    {
        if (encerrado) return;
        try
        {
            await PrepararVozAsync();
            await PrepararMicrofoneAsync();
            await FalarAsync($"Olá, {Auth.OperatorName}. Estou ouvindo.");
            await IniciarEscutaContinuaAsync();
        }
        catch (Exception ex)
        {
            orbe.SetEstado("ERRO");
            if (EhErroPrivacidadeFala(ex)) AbrirPrivacidadeDeFalaWindows();
            MessageBox.Show("A LIA não conseguiu iniciar o microfone. Verifique Reconhecimento de fala online e o acesso ao microfone no Windows.", "LIA", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Encerrar();
        }
    }

    private async Task IniciarEscutaContinuaAsync()
    {
        if (reconhecedor is null || encerrado) return;

        reconhecedor.ContinuousRecognitionSession.ResultGenerated += AoReconhecer;
        reconhecedor.ContinuousRecognitionSession.Completed += AoEncerrarReconhecimento;
        await reconhecedor.ContinuousRecognitionSession.StartAsync();
        orbe.SetEstado("OUVINDO");
    }

    private async void AoReconhecer(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        if (encerrado || processando) return;
        var r = args.Result;
        if (r.Status != SpeechRecognitionResultStatus.Success || string.IsNullOrWhiteSpace(r.Text)) return;

        processando = true;
        try
        {
            var texto = r.Text.Trim();
            await NoUiAsync(async () =>
            {
                if (encerrado) return;
                orbe.SetEstado("PENSANDO");
                var resposta = Processar(texto);
                if (!string.IsNullOrWhiteSpace(resposta)) await FalarAsync(resposta);
                if (!encerrado) orbe.SetEstado("OUVINDO");
            });
        }
        catch { }
        finally { processando = false; }
    }

    private async void AoEncerrarReconhecimento(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
    {
        if (encerrado) return;
        if (args.Status == SpeechRecognitionResultStatus.Success) return;
        await NoUiAsync(() =>
        {
            if (!encerrado)
            {
                orbe.SetEstado("ERRO");
                MessageBox.Show("A escuta da LIA foi interrompida pelo Windows. Toque na Orbe novamente para reiniciar.", "LIA", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Encerrar();
            }
            return Task.CompletedTask;
        });
    }

    private Task NoUiAsync(Func<Task> acao)
    {
        var tcs = new TaskCompletionSource<bool>();
        ui.Post(async _ =>
        {
            try { await acao(); tcs.TrySetResult(true); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }, null);
        return tcs.Task;
    }

    private string Processar(string texto)
    {
        var n = Normalizar(texto);
        if (Tem(n, "oi lia", "ola lia", "bom dia lia", "boa tarde lia", "boa noite lia"))
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
                _ => "Não entendi. Pode falar de outro jeito?"
            };
        }
        catch { return "Não consegui consultar o PDV agora."; }
    }

    private string Acao(string resposta, Action acao) { main.BeginInvoke(acao); return resposta; }

    private async Task PrepararMicrofoneAsync()
    {
        reconhecedor?.Dispose();
        reconhecedor = new SpeechRecognizer(new Language("pt-BR"));
        reconhecedor.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(20);
        reconhecedor.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(1.2);
        reconhecedor.Timeouts.BabbleTimeout = TimeSpan.FromSeconds(20);
        reconhecedor.Constraints.Add(new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "LIA"));
        var compilacao = await reconhecedor.CompileConstraintsAsync();
        if (compilacao.Status != SpeechRecognitionResultStatus.Success)
            throw new InvalidOperationException("Reconhecimento de fala indisponível.");
    }

    private async Task PrepararVozAsync()
    {
        await vozWeb.EnsureCoreWebView2Async();
        vozWeb.CoreWebView2.NavigateToString("<html><body></body></html>");
        await Task.Delay(250);
        vozPronta = true;
    }

    private async Task FalarAsync(string texto)
    {
        if (encerrado || !vozPronta || vozWeb.CoreWebView2 is null) return;
        orbe.SetEstado("FALANDO");
        string jsTexto = JsonSerializer.Serialize(texto.Replace("•", ""));
        string script = $@"(() => {{
            speechSynthesis.cancel();
            const u = new SpeechSynthesisUtterance({jsTexto});
            u.lang='pt-BR'; u.rate=1.03; u.pitch=1.08;
            const vs=speechSynthesis.getVoices();
            const br=vs.filter(v=>(v.lang||'').toLowerCase().startsWith('pt-br'));
            const feminina=br.find(v=>/francisca|maria|female|feminina|natural/i.test(v.name) && !/antonio|antônio|daniel|fabio|fábio|ricardo|male|masculin/i.test(v.name)) ||
                           br.find(v=>!/antonio|antônio|daniel|fabio|fábio|ricardo|male|masculin/i.test(v.name));
            if(feminina) u.voice=feminina;
            speechSynthesis.speak(u);
        }})()";
        await vozWeb.CoreWebView2.ExecuteScriptAsync(script);
        await Task.Delay(Math.Clamp(texto.Length * 58, 900, 9000));
        if (!encerrado) orbe.SetEstado("OUVINDO");
    }

    private string ResumoEmpresa()
    {
        using var cn=Database.Open();
        int produtos=ScalarInt(cn,"SELECT COUNT(*) FROM products WHERE active=1");
        int baixos=ScalarInt(cn,"SELECT COUNT(*) FROM products WHERE active=1 AND stock <= min_stock");
        int clientes=ScalarInt(cn,"SELECT COUNT(*) FROM customers");
        int vendas=ScalarInt(cn,"SELECT COUNT(*) FROM sales WHERE date(sold_at)=date('now','localtime')");
        double total=ScalarDouble(cn,"SELECT COALESCE(SUM(total),0) FROM sales WHERE date(sold_at)=date('now','localtime')");
        return $"Hoje foram {vendas} vendas, totalizando {Moeda(total)}. Você tem {produtos} produtos ativos, {baixos} com estoque baixo e {clientes} clientes cadastrados.";
    }
    private string VendasHoje(){using var cn=Database.Open();int q=ScalarInt(cn,"SELECT COUNT(*) FROM sales WHERE date(sold_at)=date('now','localtime')");double t=ScalarDouble(cn,"SELECT COALESCE(SUM(total),0) FROM sales WHERE date(sold_at)=date('now','localtime')");return $"Hoje o PDV registra {q} vendas, totalizando {Moeda(t)}.";}
    private string EstoqueBaixo(){using var cn=Database.Open();int q=ScalarInt(cn,"SELECT COUNT(*) FROM products WHERE active=1 AND stock <= min_stock");return q==0?"Não encontrei produtos abaixo do estoque mínimo agora.":$"Existem {q} produtos no estoque mínimo ou abaixo dele.";}
    private string QuantidadeClientes(){using var cn=Database.Open();return $"Existem {ScalarInt(cn,"SELECT COUNT(*) FROM customers")} clientes cadastrados no PDV.";}
    private string SaldoCaixa(){using var cn=Database.Open();double e=ScalarDouble(cn,"SELECT COALESCE(SUM(amount),0) FROM cash_movements WHERE upper(type)='ENTRADA'");double s=ScalarDouble(cn,"SELECT COALESCE(SUM(amount),0) FROM cash_movements WHERE upper(type) IN ('SAÍDA','SAIDA')");return $"O saldo calculado do caixa é {Moeda(e-s)}.";}
    private static int ScalarInt(SqliteConnection cn,string sql){using var c=cn.CreateCommand();c.CommandText=sql;return Convert.ToInt32(c.ExecuteScalar()??0);}
    private static double ScalarDouble(SqliteConnection cn,string sql){using var c=cn.CreateCommand();c.CommandText=sql;return Convert.ToDouble(c.ExecuteScalar()??0,CultureInfo.InvariantCulture);}
    private static string Moeda(double v)=>v.ToString("C2",new CultureInfo("pt-BR"));
    private static bool Tem(string n,params string[] xs)=>xs.Any(x=>n.Contains(Normalizar(x),StringComparison.Ordinal));
    private static string Normalizar(string t){var f=t.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);var sb=new StringBuilder();foreach(var c in f)if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)sb.Append(c);return sb.ToString().Normalize(NormalizationForm.FormC);}
    private static bool EhErroPrivacidadeFala(Exception ex){var m=(ex.Message??"").ToLowerInvariant();return ex is UnauthorizedAccessException||m.Contains("speech privacy")||m.Contains("privacy policy")||(m.Contains("speech recognition")&&m.Contains("accepted"));}
    private static void AbrirPrivacidadeDeFalaWindows(){try{Process.Start(new ProcessStartInfo{FileName="ms-settings:privacy-speech",UseShellExecute=true});}catch{}}

    public void Encerrar()
    {
        if (encerrado) return;
        encerrado=true;
        try
        {
            if (reconhecedor is not null)
            {
                reconhecedor.ContinuousRecognitionSession.ResultGenerated -= AoReconhecer;
                reconhecedor.ContinuousRecognitionSession.Completed -= AoEncerrarReconhecimento;
                _ = reconhecedor.ContinuousRecognitionSession.StopAsync();
            }
        }
        catch { }
        try { reconhecedor?.Dispose(); } catch { }
        try { if (!orbe.IsDisposed) orbe.Close(); } catch { }
        Encerrado?.Invoke(this, EventArgs.Empty);
    }
    public void Dispose(){Encerrar();vozWeb.Dispose();}
}
