using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using Windows.Media.SpeechRecognition;
using Windows.Globalization;

namespace LealInfoPDV;

/// <summary>
/// Painel de conversa da LIA CORE V1.
/// A personagem holográfica continua isolada na LiaForm aprovada.
/// Este painel não contém vídeo: evita cortar ou alterar a LIA oficial.
/// </summary>
public sealed class LiaInteligenteForm : Form
{
    private readonly MainForm main;
    private readonly LiaOrbForm? orbe;
    private readonly RichTextBox conversa = new();
    private readonly TextBox pergunta = new();
    private readonly FlowLayoutPanel acoes = new();
    private readonly Label status = new();
    private readonly WebView2 vozWeb = new();
    private readonly Button botaoEscrever = new();
    private readonly Button botaoFalar = new();
    private bool vozPronta;
    private bool ouvindo;
    private SpeechRecognizer? reconhecedor;
    private bool microfonePronto;

    private static readonly Color AzulEscuro = Color.FromArgb(4, 35, 62);
    private static readonly Color AzulPainel = Color.FromArgb(7, 55, 95);

    public LiaInteligenteForm(MainForm mainForm, LiaOrbForm? liaOrb = null)
    {
        main = mainForm;
        orbe = liaOrb;
        Text = "LIA • CONVERSA";
        StartPosition = FormStartPosition.Manual;
        Width = 430;
        Height = 460;
        MinimumSize = new Size(400, 430);
        BackColor = AzulEscuro;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MaximizeBox = false;
        FormClosed += (_, _) => { try { reconhecedor?.Dispose(); } catch { } };
        BuildUi();
        Shown += async (_, _) =>
        {
            Posicionar();
            await PrepararVozAsync();
            await PrepararMicrofoneAsync();
            Responder($"Olá, {Auth.OperatorName}. {LiaCore.ResumoPermissoes()} Escolha escrever ou falar. Você pode trocar quando quiser.");
            pergunta.Focus();
        };
    }

    private void Posicionar()
    {
        var area = Screen.FromControl(main).WorkingArea;

        if (orbe is not null && !orbe.IsDisposed)
        {
            // Painel SEMPRE ao lado da orbe, nunca em cima da LIA.
            int x = orbe.Left - Width - 14;
            int y = orbe.Bottom - Height;
            Left = Math.Max(area.Left + 8, x);
            Top = Math.Max(area.Top + 8, Math.Min(y, area.Bottom - Height - 8));
            return;
        }

        Left = Math.Max(area.Left + 8, main.Right - Width - 190);
        Top = Math.Max(area.Top + 8, main.Bottom - Height - 105);
    }

    private void BuildUi()
    {
        // Motor de voz via WebView2. Não usa System.Speech e mantém o PDV leve.
        vozWeb.Size = new Size(1, 1);
        vozWeb.Location = new Point(-10, -10);
        vozWeb.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO PDV", "WebView2", "LIA_VOZ")
        };
        Controls.Add(vozWeb);
        vozWeb.SendToBack();

        var top = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = AzulPainel };
        top.Controls.Add(new Label { Text = "LIA • CONVERSA", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI", 17, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
        Controls.Add(top);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(10), BackColor = AzulEscuro };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        Controls.Add(body);

        status.Text = $"● {Auth.Current?.Role ?? "OPERADOR"} • LIA CORE • VOZ";
        status.Dock = DockStyle.Fill; status.ForeColor = Color.FromArgb(124,238,255); status.Font = new Font("Segoe UI",10,FontStyle.Bold); status.TextAlign = ContentAlignment.MiddleLeft;
        body.Controls.Add(status,0,0);

        var modos = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 2, 0, 4) };
        modos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        modos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        ConfigurarBotaoModo(botaoEscrever, "⌨ ESCREVER", () => AtivarEscrita());
        ConfigurarBotaoModo(botaoFalar, "🎙 FALAR", () => AlternarEscuta());
        modos.Controls.Add(botaoEscrever, 0, 0);
        modos.Controls.Add(botaoFalar, 1, 0);
        body.Controls.Add(modos, 0, 1);

        conversa.Dock=DockStyle.Fill; conversa.ReadOnly=true; conversa.BackColor=Color.FromArgb(9,27,43); conversa.ForeColor=Color.White; conversa.BorderStyle=BorderStyle.FixedSingle; conversa.Font=new Font("Segoe UI",10); conversa.DetectUrls=false;
        body.Controls.Add(conversa,0,2);

        var input=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,Padding=new Padding(0,6,0,4)};
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,52)); input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,82));
        pergunta.Dock=DockStyle.Fill; pergunta.Font=new Font("Segoe UI",10); pergunta.PlaceholderText="Digite...";
        pergunta.KeyDown += (_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;ProcessarPergunta();}};
        var mic = Botao("🎙", AlternarEscuta); mic.Width=46;
        var enviar = Botao("ENVIAR",ProcessarPergunta); enviar.Width=78;
        input.Controls.Add(pergunta,0,0); input.Controls.Add(mic,1,0); input.Controls.Add(enviar,2,0); body.Controls.Add(input,0,3);

        acoes.Dock=DockStyle.Fill; acoes.AutoScroll=true; acoes.WrapContents=false; acoes.FlowDirection=FlowDirection.LeftToRight; acoes.Padding=new Padding(0,4,0,0); body.Controls.Add(acoes,0,4); CriarAtalhos();
        AtivarEscrita();
    }

    private void ConfigurarBotaoModo(Button b, string texto, Action acao)
    {
        b.Text = texto; b.Dock = DockStyle.Fill; b.Margin = new Padding(3); b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0; b.ForeColor = Color.White; b.Cursor = Cursors.Hand;
        b.Font = new Font("Segoe UI", 9, FontStyle.Bold); b.Click += (_, _) => acao();
    }

    private void AtivarEscrita()
    {
        if (ouvindo) PararEscuta();
        botaoEscrever.BackColor = Color.FromArgb(0, 138, 190);
        botaoFalar.BackColor = AzulPainel;
        status.Text = $"● {Auth.Current?.Role ?? "OPERADOR"} • ESCREVENDO";
        pergunta.Enabled = true;
        pergunta.Focus();
        orbe?.SetEstado("PRONTA");
    }

    private async void AlternarEscuta()
    {
        if (ouvindo) { PararEscuta(); AtivarEscrita(); return; }
        if (!microfonePronto || reconhecedor is null)
        {
            Responder("O microfone não ficou disponível no Windows. Verifique a permissão de microfone para aplicativos da área de trabalho e tente novamente.");
            return;
        }

        try
        {
            ouvindo = true;
            pergunta.Enabled = false;
            botaoEscrever.BackColor = AzulPainel;
            botaoFalar.BackColor = Color.FromArgb(0, 138, 190);
            status.Text = $"● {Auth.Current?.Role ?? "OPERADOR"} • OUVINDO...";
            orbe?.SetEstado("OUVINDO");

            var resultado = await reconhecedor.RecognizeAsync();
            ouvindo = false;

            if (resultado.Status == SpeechRecognitionResultStatus.Success && !string.IsNullOrWhiteSpace(resultado.Text))
            {
                ExecutarTexto(resultado.Text.Trim());
            }
            else
            {
                status.Text = $"● {Auth.Current?.Role ?? "OPERADOR"} • NÃO ENTENDI • TENTE DE NOVO";
            }
        }
        catch (UnauthorizedAccessException)
        {
            Responder("O Windows bloqueou o microfone. Ative o acesso ao microfone para aplicativos da área de trabalho e tente novamente.");
        }
        catch (Exception ex)
        {
            Responder("Não consegui ouvir agora. Detalhe técnico: " + ex.Message);
        }
        finally
        {
            ouvindo = false;
            pergunta.Enabled = true;
            botaoEscrever.BackColor = AzulPainel;
            botaoFalar.BackColor = Color.FromArgb(0, 138, 190);
            orbe?.SetEstado("PRONTA");
        }
    }

    private void PararEscuta()
    {
        ouvindo = false;
        pergunta.Enabled = true;
        try { reconhecedor?.StopRecognitionAsync(); } catch { }
        orbe?.SetEstado("PRONTA");
    }

    private async Task PrepararMicrofoneAsync()
    {
        try
        {
            reconhecedor?.Dispose();
            reconhecedor = new SpeechRecognizer(new Language("pt-BR"));
            reconhecedor.Constraints.Clear();
            reconhecedor.Constraints.Add(new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "LIA"));
            var compilacao = await reconhecedor.CompileConstraintsAsync();
            microfonePronto = compilacao.Status == SpeechRecognitionResultStatus.Success;
        }
        catch
        {
            microfonePronto = false;
            reconhecedor = null;
        }
    }

    private Button Botao(string texto, Action acao)
    {
        var b=new Button{Text=texto,Width=155,Height=40,Margin=new Padding(4),BackColor=Color.FromArgb(0,138,190),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,Font=new Font("Segoe UI",9,FontStyle.Bold)};
        b.FlatAppearance.BorderSize=0; b.Click+=(_,_)=>acao(); return b;
    }

    private void CriarAtalhos()
    {
        acoes.Controls.Clear();
        acoes.Controls.Add(Botao("MINHAS PERMISSÕES",()=>ExecutarTexto("o que posso fazer")));
        acoes.Controls.Add(Botao("VENDAS HOJE",()=>ExecutarTexto("quanto vendi hoje")));
        acoes.Controls.Add(Botao("ESTOQUE BAIXO",()=>ExecutarTexto("o que está acabando")));
    }

    private void ProcessarPergunta(){var t=pergunta.Text.Trim();if(t.Length==0)return;pergunta.Clear();ExecutarTexto(t);}

    private void ExecutarTexto(string texto)
    {
        FalarUsuario(texto);
        var n=Normalizar(texto);
        try
        {
            var d=LiaCore.Classificar(n);
            if(d.RespostaImediata is not null){Responder(d.RespostaImediata);return;}
            switch(d.Intencao)
            {
                case "PERMISSOES": Responder(LiaCore.ResumoPermissoes()); break;
                case "CADASTRO_AMBIGUO": Responder("Claro. O que você quer cadastrar: produto, cliente, fornecedor ou outra coisa?"); break;
                case "CADASTRAR_PRODUTO": MostrarTutorialProduto(); break;
                case "RESUMO_EMPRESA": if(!Auth.IsManager){Responder("Essa visão geral é gerencial. Chame o gerente ou proprietário para autorizar o acesso.");break;} Responder(ResumoEmpresa()); break;
                case "VENDAS_HOJE": Responder(VendasHoje()); break;
                case "ESTOQUE_BAIXO": Responder(EstoqueBaixo()); break;
                case "CLIENTES": Responder(QuantidadeClientes()); break;
                case "SALDO_CAIXA": if(!Auth.IsManager){Responder("Essa informação é restrita. Chame o gerente.");break;} Responder(SaldoCaixa()); break;
                case "CONTAS_PAGAR": if(!Auth.IsManager){Responder("Essa informação é do Financeiro. Chame o gerente.");break;} Responder("O PDV atual ainda não possui uma agenda separada de contas a pagar. Posso abrir o Financeiro para consultar as saídas registradas."); break;
                case "ABRIR_PRODUTOS": Responder("Abrindo Produtos."); main.BeginInvoke(new Action(main.LiaAbrirProdutos)); break;
                case "ABRIR_VENDAS": Responder("Abrindo a Tela de Vendas."); main.BeginInvoke(new Action(main.LiaAbrirVendas)); break;
                case "ABRIR_CLIENTES": Responder("Abrindo Clientes."); main.BeginInvoke(new Action(main.LiaAbrirClientes)); break;
                case "ABRIR_FINANCEIRO": if(!Auth.IsManager){Responder("Seu perfil não tem acesso ao Financeiro. Chame o gerente.");break;} Responder("Abrindo Financeiro."); main.BeginInvoke(new Action(main.LiaAbrirFinanceiro)); break;
                case "ABRIR_RELATORIOS": if(!Auth.IsManager){Responder("Relatórios gerenciais exigem autorização. Chame o gerente.");break;} Responder("Abrindo Relatórios."); main.BeginInvoke(new Action(main.LiaAbrirRelatorios)); break;
                default: Responder("Não quero adivinhar o que você quis dizer. Me diga de outro jeito ou diga o que você está tentando fazer no PDV."); break;
            }
        }
        catch(Exception ex){Responder("Não consegui consultar o PDV agora. Detalhe técnico: "+ex.Message);}
    }

    private string ResumoEmpresa()
    {
        using var cn = Database.Open();
        int produtos = ScalarInt(cn, "SELECT COUNT(*) FROM products WHERE active=1");
        int baixos = ScalarInt(cn, "SELECT COUNT(*) FROM products WHERE active=1 AND stock <= min_stock");
        int clientes = ScalarInt(cn, "SELECT COUNT(*) FROM customers");
        int vendasHoje = ScalarInt(cn, "SELECT COUNT(*) FROM sales WHERE date(sold_at)=date('now','localtime')");
        double totalHoje = ScalarDouble(cn, "SELECT COALESCE(SUM(total),0) FROM sales WHERE date(sold_at)=date('now','localtime')");
        double totalMes = ScalarDouble(cn, "SELECT COALESCE(SUM(total),0) FROM sales WHERE strftime('%Y-%m',sold_at)=strftime('%Y-%m','now','localtime')");
        double caixa = ScalarDouble(cn, "SELECT COALESCE(SUM(CASE WHEN upper(type)='ENTRADA' THEN amount WHEN upper(type) IN ('SAÍDA','SAIDA') THEN -amount ELSE 0 END),0) FROM cash_movements");

        return $"Olhei o PDV inteiro para um resumo rápido. Você tem {produtos} produto(s) ativo(s), {baixos} abaixo do estoque mínimo e {clientes} cliente(s) cadastrado(s). Hoje foram {vendasHoje} venda(s), totalizando {Moeda(totalHoje)}. No mês, o total vendido é {Moeda(totalMes)}. O saldo acumulado dos movimentos de caixa está em {Moeda(caixa)}. Quer que eu aprofunde vendas, estoque ou caixa?";
    }

    private string VendasHoje()
    {
        using var cn = Database.Open();
        int qtd = ScalarInt(cn, "SELECT COUNT(*) FROM sales WHERE date(sold_at)=date('now','localtime')");
        double total = ScalarDouble(cn, "SELECT COALESCE(SUM(total),0) FROM sales WHERE date(sold_at)=date('now','localtime')");
        double ticket = qtd == 0 ? 0 : total / qtd;
        return $"Hoje o PDV registra {qtd} venda(s), com total de {Moeda(total)} e ticket médio de {Moeda(ticket)}.";
    }

    private string EstoqueBaixo()
    {
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT name, stock, min_stock FROM products WHERE active=1 AND stock <= min_stock ORDER BY stock ASC, name LIMIT 8";
        using var rd = cmd.ExecuteReader();
        var itens = new List<string>();
        while (rd.Read())
            itens.Add($"• {rd.GetString(0)} — estoque {rd.GetDouble(1):N3} / mínimo {rd.GetDouble(2):N3}");
        if (itens.Count == 0)
            return "Boa notícia: não encontrei produtos abaixo do estoque mínimo agora.";
        return $"Encontrei {itens.Count} item(ns) prioritário(s) para reposição:\n" + string.Join("\n", itens) + "\n\nSe quiser, diga ‘abrir produtos’.";
    }

    private string QuantidadeClientes()
    {
        using var cn = Database.Open();
        int qtd = ScalarInt(cn, "SELECT COUNT(*) FROM customers");
        return $"Existem {qtd} cliente(s) cadastrados no PDV.";
    }

    private string SaldoCaixa()
    {
        using var cn = Database.Open();
        double entradas = ScalarDouble(cn, "SELECT COALESCE(SUM(amount),0) FROM cash_movements WHERE upper(type)='ENTRADA'");
        double saidas = ScalarDouble(cn, "SELECT COALESCE(SUM(amount),0) FROM cash_movements WHERE upper(type) IN ('SAÍDA','SAIDA')");
        return $"Nos movimentos registrados, há {Moeda(entradas)} de entradas e {Moeda(saidas)} de saídas. Saldo calculado: {Moeda(entradas - saidas)}.";
    }

    private void MostrarTutorialProduto()
    {
        Responder("Filha, vem comigo 😄. Para cadastrar um produto: 1) abra PRODUTOS; 2) clique em NOVO; 3) passe ou digite o código de barras; 4) informe nome, categoria, custo, preço de venda, estoque e estoque mínimo; 5) salve. Eu posso abrir direto a tela de novo produto para você agora. Clique em ‘CADASTRAR PRODUTO’. ");
        acoes.Controls.Clear();
        acoes.Controls.Add(Botao("CADASTRAR PRODUTO", () =>
        {
            Responder("Pronto. Vou abrir Novo Produto. Eu só conduzo; quem confirma e salva é você.");
            main.BeginInvoke(new Action(main.LiaCadastrarNovoProduto));
        }));
        acoes.Controls.Add(Botao("ABRIR PRODUTOS", () => main.BeginInvoke(new Action(main.LiaAbrirProdutos))));
        acoes.Controls.Add(Botao("VOLTAR ÀS PERGUNTAS", CriarAtalhos));
    }

    private static int ScalarInt(SqliteConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    private static double ScalarDouble(SqliteConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToDouble(cmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
    }

    private static string Moeda(double valor) => valor.ToString("C2", new CultureInfo("pt-BR"));

    private static bool Tem(string normalizado, params string[] opcoes)
        => opcoes.Any(x => normalizado.Contains(Normalizar(x), StringComparison.Ordinal));

    private static string Normalizar(string texto)
    {
        var f = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in f)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private async Task PrepararVozAsync()
    {
        try
        {
            await vozWeb.EnsureCoreWebView2Async();
            vozWeb.CoreWebView2.NavigateToString(@"<html><body><script>
                window.liaEscolherVoz = () => {
                  const vs = speechSynthesis.getVoices();
                  const br = vs.filter(v => (v.lang || '').toLowerCase().startsWith('pt-br'));
                  return (br.find(v => /francisca/i.test(v.name)) ||
                          br.find(v => /maria/i.test(v.name)) ||
                          br.find(v => /female|feminina/i.test(v.name)) ||
                          br.find(v => /natural|online/i.test(v.name)) || null)?.name || '';
                };
            </script></body></html>");
            await Task.Delay(700);
            vozPronta = true;
        }
        catch
        {
            vozPronta = false; // Texto continua funcionando mesmo se a voz não estiver disponível.
        }
    }

    private async void FalarResposta(string texto)
    {
        if (!vozPronta || vozWeb.CoreWebView2 is null) return;
        try
        {
            orbe?.SetEstado("FALANDO");
            string jsTexto = JsonSerializer.Serialize(texto.Replace("•", ""));
            string script = $@"(() => {{
                speechSynthesis.cancel();
                const u = new SpeechSynthesisUtterance({jsTexto});
                u.lang = 'pt-BR';
                u.rate = 1.06;
                u.pitch = 1.02;
                const falar = () => {
                    const vs = speechSynthesis.getVoices();
                    const br = vs.filter(v => (v.lang || '').toLowerCase().startsWith('pt-br'));
                    const feminina = br.find(v => /francisca/i.test(v.name)) ||
                                     br.find(v => /maria/i.test(v.name)) ||
                                     br.find(v => /female|feminina/i.test(v.name)) ||
                                     br.find(v => /natural|online/i.test(v.name));
                    if (feminina) u.voice = feminina;
                    speechSynthesis.speak(u);
                };
                if (speechSynthesis.getVoices().length) falar();
                else speechSynthesis.addEventListener('voiceschanged', falar, { once:true });
            }})()";
            await vozWeb.CoreWebView2.ExecuteScriptAsync(script);
            // Estado visual volta sozinho; a fala continua no mecanismo do navegador.
            var t = new System.Windows.Forms.Timer { Interval = Math.Clamp(texto.Length * 55, 1200, 12000) };
            t.Tick += (_, _) => { t.Stop(); t.Dispose(); orbe?.SetEstado("PRONTA"); };
            t.Start();
        }
        catch { orbe?.SetEstado("PRONTA"); }
    }

    private void FalarUsuario(string texto)
    {
        conversa.SelectionColor = Color.FromArgb(178, 226, 255);
        conversa.SelectionFont = new Font(conversa.Font, FontStyle.Bold);
        conversa.AppendText("\nVOCÊ: ");
        conversa.SelectionColor = Color.White;
        conversa.SelectionFont = conversa.Font;
        conversa.AppendText(texto + "\n");
        conversa.ScrollToCaret();
    }

    private void Responder(string texto)
    {
        conversa.SelectionColor = Color.FromArgb(94, 235, 255);
        conversa.SelectionFont = new Font(conversa.Font, FontStyle.Bold);
        conversa.AppendText("\nLIA: ");
        conversa.SelectionColor = Color.White;
        conversa.SelectionFont = conversa.Font;
        conversa.AppendText(texto + "\n");
        conversa.ScrollToCaret();
        FalarResposta(texto);
    }



    private sealed class LiaVozMensagem
    {
        public string? tipo { get; set; }
        public string? texto { get; set; }
    }
}
