using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;

namespace LealInfoPDV;

/// <summary>
/// Painel de conversa da LIA CORE V1.
/// A personagem holográfica continua isolada na LiaForm aprovada.
/// Este painel não contém vídeo: evita cortar ou alterar a LIA oficial.
/// </summary>
public sealed class LiaInteligenteForm : Form
{
    private readonly MainForm main;
    private readonly RichTextBox conversa = new();
    private readonly TextBox pergunta = new();
    private readonly FlowLayoutPanel acoes = new();
    private readonly Label status = new();

    private static readonly Color AzulEscuro = Color.FromArgb(4, 35, 62);
    private static readonly Color AzulPainel = Color.FromArgb(7, 55, 95);

    public LiaInteligenteForm(MainForm mainForm)
    {
        main = mainForm;
        Text = "LIA • LEAL AI — CONVERSA (TESTE CORE V1)";
        StartPosition = FormStartPosition.Manual;
        Width = 560;
        Height = 650;
        MinimumSize = new Size(500, 560);
        BackColor = AzulEscuro;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MaximizeBox = false;
        BuildUi();
        Shown += (_, _) =>
        {
            Posicionar();
            Responder($"Olá, {Auth.OperatorName}. {LiaCore.ResumoPermissoes()} Pode digitar do seu jeito. Nesta etapa, o microfone ainda não está ativo.");
            pergunta.Focus();
        };
    }

    private void Posicionar()
    {
        var area = Screen.FromControl(main).WorkingArea;
        Left = Math.Max(area.Left, main.Right - Width - 90);
        Top = Math.Max(area.Top, main.Bottom - Height - 105);
    }

    private void BuildUi()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = AzulPainel };
        top.Controls.Add(new Label { Text = "LIA • CONVERSA", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI", 17, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
        Controls.Add(top);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(14), BackColor = AzulEscuro };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
        Controls.Add(body);

        status.Text = $"● {Auth.Current?.Role ?? "OPERADOR"} • LIA CORE V1 • OFFLINE";
        status.Dock = DockStyle.Fill; status.ForeColor = Color.FromArgb(124,238,255); status.Font = new Font("Segoe UI",10,FontStyle.Bold); status.TextAlign = ContentAlignment.MiddleLeft;
        body.Controls.Add(status,0,0);

        conversa.Dock=DockStyle.Fill; conversa.ReadOnly=true; conversa.BackColor=Color.FromArgb(9,27,43); conversa.ForeColor=Color.White; conversa.BorderStyle=BorderStyle.FixedSingle; conversa.Font=new Font("Segoe UI",11); conversa.DetectUrls=false;
        body.Controls.Add(conversa,0,1);

        var input=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(0,8,0,6)};
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));
        pergunta.Dock=DockStyle.Fill; pergunta.Font=new Font("Segoe UI",11); pergunta.PlaceholderText="Digite do seu jeito...";
        pergunta.KeyDown += (_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;ProcessarPergunta();}};
        input.Controls.Add(pergunta,0,0); input.Controls.Add(Botao("ENVIAR",ProcessarPergunta),1,0); body.Controls.Add(input,0,2);

        acoes.Dock=DockStyle.Fill; acoes.AutoScroll=true; acoes.WrapContents=true; acoes.Padding=new Padding(0,6,0,0); body.Controls.Add(acoes,0,3); CriarAtalhos();
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
    }


}
