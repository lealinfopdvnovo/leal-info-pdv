namespace LealInfoPDV;

public enum LiaModo { Instrutora, Operacional, Gerencial }
public enum LiaRisco { Normal, Atencao, Critico }
public enum LiaInteracaoModo { Conversa, Trabalho }

public sealed record LiaDecisao(string Intencao, LiaRisco Risco, string? RespostaImediata = null, bool ExigeGerente = false);

/// <summary>
/// LIA CORE — conversa e trabalho são estados diferentes.
/// Segurança e permissões continuam exclusivamente no Auth/local.
/// </summary>
public static class LiaCore
{
    private static readonly LiaAiClient Ai = new();
    private static LiaInteracaoModo modoInteracao = LiaInteracaoModo.Conversa;
    private static string operadorDoModo = "";

    public static LiaModo ModoAtual => Auth.IsManager ? LiaModo.Gerencial : LiaModo.Operacional;
    public static bool ModoAdminLivre => Auth.IsAdmin;
    public static LiaInteracaoModo ModoInteracaoAtual => modoInteracao;

    public static LiaDecisao Classificar(string n)
    {
        SincronizarOperador();

        bool Tem(params string[] xs) => xs.Any(x => n.Contains(x, StringComparison.Ordinal));
        string LimparFinal(string s) => s.Trim().Trim(' ', '.', ',', '?', '!', ';', ':', '-', '–', '—');
        bool Exato(params string[] xs)
        {
            var atual = LimparFinal(n);
            return xs.Any(x => atual.Equals(LimparFinal(x), StringComparison.Ordinal));
        }
        bool AcaoTela(params string[] alvos)
        {
            var alvo = alvos.Any(x => n.Contains(x, StringComparison.Ordinal));
            if (!alvo) return false;
            return Tem("abre", "abrir", "abri ", "abra", "vai", "ir para", "ir pra", "entra", "entrar", "acessa", "acessar", "mostra", "mostrar", "leva", "vá para", "va para") || Exato(alvos);
        }
        bool ComandoExplicito()
        {
            string[] verbos =
            {
                "abre ", "abrir ", "abri ", "abra ", "fecha ", "fechar ", "feche ",
                "entra ", "entrar ", "acessa ", "acessar ", "vai para ", "vai pra ", "va para ", "vá para ",
                "mostra ", "mostrar ", "leva ", "cadastra ", "cadastrar ", "consulta ", "consultar ",
                "faz backup", "fazer backup", "quero abrir ", "quero fechar ", "preciso abrir ", "preciso fechar "
            };
            string[] alvos =
            {
                "pdv", "tela de venda", "tela de vendas", "produto", "produtos", "cliente", "clientes",
                "fornecedor", "fornecedores", "servico", "servicos", "historico", "financeiro", "ordem", "ordens",
                "orcamento", "orcamentos", "relatorio", "relatorios", "backup", "configuracao", "configuracoes",
                "essa tela", "a tela", "janela", "sistema", "programa", "estoque"
            };
            if (verbos.Any(v => n.Contains(v, StringComparison.Ordinal)) && alvos.Any(a => n.Contains(a, StringComparison.Ordinal))) return true;
            if (Tem("quanto tem de", "quantos tem de", "quantas tem de", "tem no estoque", "estoque do", "estoque da", "estoque de", "quantidade de")) return true;
            if (Tem("vendas de hoje", "vendi hoje", "faturamento de hoje", "estoque baixo", "contas a pagar", "saldo do caixa", "quanto tem no caixa", "quantos clientes")) return true;
            return false;
        }

        // Alternância explícita: conversa é o padrão; trabalho prioriza operação do PDV.
        if (Tem("vamos trabalhar", "vamo trabalhar", "vamos trabalha", "modo trabalho", "ativa modo trabalho", "ativar modo trabalho", "hora de trabalhar", "bora trabalhar", "bora trabalha"))
        {
            modoInteracao = LiaInteracaoModo.Trabalho;
            return new("MODO_TRABALHO", LiaRisco.Normal, "Modo trabalho ativado. Manda.");
        }
        if (Tem("vamos conversar", "vamo conversar", "vamos conversa", "modo conversa", "ativa modo conversa", "ativar modo conversa", "pode relaxar", "pode descansar", "sai do modo trabalho", "sair do modo trabalho", "para de trabalhar", "volta pro modo conversa", "voltar pro modo conversa"))
        {
            modoInteracao = LiaInteracaoModo.Conversa;
            return new("MODO_CONVERSA", LiaRisco.Normal, "Modo conversa ativado. Fala comigo.");
        }

        if (Exato("lia", "liá", "lea", "leah", "leia", "liah", "vagabunda", "sua vagabunda", "piranha", "sua piranha", "gostosa", "sua gostosa", "filha da puta", "o filha da puta", "ô filha da puta", "sua filha da puta", "fdp", "sua fdp") ||
            Tem("lia ta ai", "lia esta ai", "lia voce ta ai", "lia voce esta ai", "lia responde", "lia me ouve", "lia me escuta", "ei lia", "e ai lia"))
            return new("CHAMADO_LIA", LiaRisco.Normal, "Oi? Tô aqui.");

        if (Tem("quem e voce", "quem voce e", "qual seu nome", "qual e seu nome", "qual e o seu nome", "como voce se chama", "como se chama", "voce e a lia", "seu nome e lia", "seu nome"))
            return new("IDENTIDADE_LIA", LiaRisco.Normal, "Eu sou a LIA, assistente do LEAL INFO PDV.");

        if (Tem("fechei o caixa", "encerrei o caixa", "caixa fechado", "fechou o caixa", "reabrir caixa", "reabre o caixa") && !Auth.IsAdmin)
            return new("CAIXA_ENCERRADO", LiaRisco.Critico, "Chame o gerente. Por segurança, o caixa encerrado só pode ser liberado com autorização.", true);

        if (Tem("apagar", "excluir", "estornar", "cancelar", "reabrir", "alterar preco", "mudar preco") && !Auth.IsManager)
            return new("ACAO_RESTRITA", LiaRisco.Critico, "Chame o gerente. Essa ação precisa de autorização superior.", true);

        // No modo conversa, só entra no PDV quando o pedido for claramente operacional.
        // Assim, mencionar produtos, vendas ou clientes em uma conversa não abre telas sozinho.
        if (modoInteracao == LiaInteracaoModo.Conversa && !ComandoExplicito())
            return Ai.Configurada ? new("CONVERSA_AI", LiaRisco.Normal) : new("DESCONHECIDA", LiaRisco.Atencao);

        if (Tem("fecha essa tela", "fechar essa tela", "fecha a tela", "fechar a tela", "fecha isso", "fechar isso", "fecha aqui", "fechar aqui", "pode fechar", "quero fechar", "volta da tela", "sair dessa tela", "sai dessa tela", "fechar janela", "fecha janela", "fecha o sistema", "fechar o sistema", "fecha o programa", "fechar o programa"))
            return new("FECHAR_TELA", LiaRisco.Atencao);

        if (Tem("o que posso", "minha permissao", "minhas permissoes", "tenho acesso", "posso mexer")) return new("PERMISSOES", LiaRisco.Normal);
        if (Tem("quanto tem de", "quantos tem de", "quantas tem de", "tem no estoque", "estoque do", "estoque da", "estoque de", "quantidade de") && !Tem("estoque baixo", "estoque minimo")) return new("CONSULTAR_ESTOQUE_PRODUTO", LiaRisco.Normal);
        if (Tem("cadastrar produto", "cadastro produto", "novo produto", "produto novo", "produto nao cadastrado", "produto nao existe", "me ensina produto", "quero cadastrar um produto", "quero cadastrar produto")) return new("CADASTRAR_PRODUTO", LiaRisco.Normal);
        if (AcaoTela("produtos", "produto", "cadastro de produtos", "cadastro dos produtos")) return new("ABRIR_PRODUTOS", LiaRisco.Normal);
        if (AcaoTela("pdv", "tela de vendas", "tela de venda", "vendas", "venda", "caixa de venda") || Tem("nova venda", "fazer uma venda", "fazer venda", "quero vender", "quero fazer uma venda", "vender")) return new("ABRIR_VENDAS", LiaRisco.Normal);
        if (AcaoTela("clientes", "cliente", "cadastro de cliente", "cadastro de clientes")) return new("ABRIR_CLIENTES", LiaRisco.Normal);
        if (AcaoTela("financeiro", "fluxo de caixa")) return new("ABRIR_FINANCEIRO", LiaRisco.Normal);
        if (AcaoTela("relatorios", "relatorio", "tela de relatorios")) return new("ABRIR_RELATORIOS", LiaRisco.Normal);
        if (AcaoTela("configuracoes", "configuracao", "config", "ajustes", "preferencias")) return new("ABRIR_CONFIGURACOES", LiaRisco.Normal);
        if (Tem("como faz pra cadastrar", "como cadastrar", "quero cadastrar", "onde cadastra", "nao sei cadastrar", "me ajuda a cadastrar", "abre cadastro", "abrir cadastro", "quero ir no cadastro")) return new("CADASTRO_AMBIGUO", LiaRisco.Normal);
        if (Tem("como esta minha empresa", "como esta a loja", "resumo", "situacao da loja", "como foi a loja")) return new("RESUMO_EMPRESA", LiaRisco.Normal);
        if (Tem("vendi", "vendas", "faturamento", "movimento") && Tem("hoje", "dia")) return new("VENDAS_HOJE", LiaRisco.Normal);
        if (Tem("estoque baixo", "acabando", "faltando", "estoque minimo", "repor", "reposicao")) return new("ESTOQUE_BAIXO", LiaRisco.Normal);
        if (Tem("cliente", "clientes") && Tem("quantos", "cadastrado", "cadastro", "tenho")) return new("CLIENTES", LiaRisco.Normal);
        if (Tem("o que tenho pra pagar", "o que tenho para pagar", "contas a pagar", "tenho pra pagar", "tenho para pagar", "pagamentos pendentes", "vencimentos")) return new("CONTAS_PAGAR", LiaRisco.Normal);
        if (Tem("caixa", "saldo") && !Tem("abrir")) return new("SALDO_CAIXA", LiaRisco.Normal);

        var semantica = LiaSemanticRouter.Interpretar(n);
        if (!string.IsNullOrWhiteSpace(semantica)) return new(semantica, semantica == "FECHAR_TELA" ? LiaRisco.Atencao : LiaRisco.Normal);
        return Ai.Configurada ? new("CONVERSA_AI", LiaRisco.Normal) : new("DESCONHECIDA", LiaRisco.Atencao);
    }

    private static void SincronizarOperador()
    {
        var atual = Auth.OperatorName ?? "";
        if (string.Equals(operadorDoModo, atual, StringComparison.Ordinal)) return;
        operadorDoModo = atual;
        modoInteracao = LiaInteracaoModo.Conversa;
    }

    public static async Task<string?> ConversarAsync(string texto, CancellationToken cancellationToken = default)
    {
        if (!Ai.Configurada) return null;
        try { return await Ai.ConversarAsync(texto, cancellationToken); }
        catch { return null; }
    }

    public static string ResumoPermissoes()
    {
        var estado = modoInteracao == LiaInteracaoModo.Trabalho ? "Modo trabalho ativo." : "Modo conversa ativo.";
        if (Auth.IsAdmin) return $"{estado} Você está como ADMINISTRADOR e pode comandar as funções disponíveis no PDV. Só mantenho confirmação quando houver risco real de perda de dados.";
        if (Auth.IsManager) return $"{estado} Você está como GERENTE. Posso ajudar com operação e funções gerenciais autorizadas.";
        return $"{estado} Você está como OPERADOR. Posso ajudar com vendas e tarefas operacionais. Financeiro e ações críticas continuam protegidos.";
    }
}
