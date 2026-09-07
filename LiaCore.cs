namespace LealInfoPDV;

public enum LiaModo { Instrutora, Operacional, Gerencial }
public enum LiaRisco { Normal, Atencao, Critico }

public sealed record LiaDecisao(string Intencao, LiaRisco Risco, string? RespostaImediata = null, bool ExigeGerente = false);

/// <summary>
/// LIA CORE — comandos e segurança continuam locais. Conversa livre usa a camada online sem poder executar ações.
/// </summary>
public static class LiaCore
{
    private static readonly LiaAiClient Ai = new();
    public static LiaModo ModoAtual => Auth.IsManager ? LiaModo.Gerencial : LiaModo.Operacional;

    public static LiaDecisao Classificar(string n)
    {
        bool Tem(params string[] xs) => xs.Any(x => n.Contains(x, StringComparison.Ordinal));
        bool Exato(params string[] xs) => xs.Any(x => n.Trim().Equals(x, StringComparison.Ordinal));
        bool AcaoTela(params string[] alvos)
        {
            var alvo = alvos.Any(x => n.Contains(x, StringComparison.Ordinal));
            if (!alvo) return false;
            return Tem("abre", "abrir", "vai", "ir para", "ir pra", "entra", "entrar", "acessa", "acessar", "mostra", "mostrar", "quero", "preciso", "leva", "vá para", "va para") || Exato(alvos);
        }

        if (Tem("fechei o caixa", "encerrei o caixa", "caixa fechado", "fechou o caixa", "reabrir caixa", "reabre o caixa"))
            return new("CAIXA_ENCERRADO", LiaRisco.Critico, "Chame o gerente. Por segurança, o caixa encerrado só pode ser liberado com autorização.", true);

        if (Tem("apagar", "excluir", "estornar", "cancelar", "reabrir", "alterar preco", "mudar preco") && !Auth.IsManager)
            return new("ACAO_RESTRITA", LiaRisco.Critico, "Chame o gerente. Essa ação precisa de autorização superior.", true);

        if (Tem("fecha essa tela", "fechar essa tela", "fecha a tela", "fechar a tela", "fecha isso", "fechar isso", "fecha aqui", "fechar aqui", "pode fechar", "quero fechar", "volta da tela", "sair dessa tela", "sai dessa tela", "fechar janela", "fecha janela", "fecha o sistema", "fechar o sistema", "fecha o programa", "fechar o programa"))
            return new("FECHAR_TELA", LiaRisco.Atencao);

        if (Tem("o que posso", "minha permissao", "minhas permissoes", "tenho acesso", "posso mexer")) return new("PERMISSOES", LiaRisco.Normal);

        if (Tem("quanto tem de", "quantos tem de", "quantas tem de", "tem no estoque", "estoque do", "estoque da", "estoque de", "quantidade de") && !Tem("estoque baixo", "estoque minimo"))
            return new("CONSULTAR_ESTOQUE_PRODUTO", LiaRisco.Normal);

        if (Tem("cadastrar produto", "cadastro produto", "novo produto", "produto novo", "produto nao cadastrado", "produto nao existe", "me ensina produto", "quero cadastrar um produto", "quero cadastrar produto"))
            return new("CADASTRAR_PRODUTO", LiaRisco.Normal);

        if (AcaoTela("produtos", "produto", "cadastro de produtos", "cadastro dos produtos"))
            return new("ABRIR_PRODUTOS", LiaRisco.Normal);

        if (AcaoTela("pdv", "tela de vendas", "tela de venda", "vendas", "venda", "caixa de venda") || Tem("nova venda", "fazer uma venda", "fazer venda", "quero vender", "quero fazer uma venda", "vender"))
            return new("ABRIR_VENDAS", LiaRisco.Normal);

        if (AcaoTela("clientes", "cliente", "cadastro de cliente", "cadastro de clientes"))
            return new("ABRIR_CLIENTES", LiaRisco.Normal);

        if (AcaoTela("financeiro", "fluxo de caixa"))
            return new("ABRIR_FINANCEIRO", LiaRisco.Normal);

        if (AcaoTela("relatorios", "relatorio", "tela de relatorios"))
            return new("ABRIR_RELATORIOS", LiaRisco.Normal);

        if (AcaoTela("configuracoes", "configuracao", "config", "ajustes", "preferencias"))
            return new("ABRIR_CONFIGURACOES", LiaRisco.Normal);

        if (Tem("como faz pra cadastrar", "como cadastrar", "quero cadastrar", "onde cadastra", "nao sei cadastrar", "me ajuda a cadastrar", "abre cadastro", "abrir cadastro", "quero ir no cadastro"))
            return new("CADASTRO_AMBIGUO", LiaRisco.Normal);

        if (Tem("como esta minha empresa", "como esta a loja", "resumo", "situacao da loja", "como foi a loja")) return new("RESUMO_EMPRESA", LiaRisco.Normal);
        if (Tem("vendi", "vendas", "faturamento", "movimento") && Tem("hoje", "dia")) return new("VENDAS_HOJE", LiaRisco.Normal);
        if (Tem("estoque baixo", "acabando", "faltando", "estoque minimo", "repor", "reposicao")) return new("ESTOQUE_BAIXO", LiaRisco.Normal);
        if (Tem("cliente", "clientes") && Tem("quantos", "cadastrado", "cadastro", "tenho")) return new("CLIENTES", LiaRisco.Normal);
        if (Tem("o que tenho pra pagar", "o que tenho para pagar", "contas a pagar", "tenho pra pagar", "tenho para pagar", "pagamentos pendentes", "vencimentos")) return new("CONTAS_PAGAR", LiaRisco.Normal);
        if (Tem("caixa", "saldo") && !Tem("abrir")) return new("SALDO_CAIXA", LiaRisco.Normal);

        return Ai.Configurada ? new("CONVERSA_AI", LiaRisco.Normal) : new("DESCONHECIDA", LiaRisco.Atencao);
    }

    public static async Task<string?> ConversarAsync(string texto, CancellationToken cancellationToken = default)
    {
        if (!Ai.Configurada) return null;
        try { return await Ai.ConversarAsync(texto, cancellationToken); }
        catch { return null; }
    }

    public static string ResumoPermissoes()
    {
        if (Auth.IsAdmin) return "Você está como ADMINISTRADOR. Posso ajudar com operação e gestão. Ações críticas continuam exigindo confirmação.";
        if (Auth.IsManager) return "Você está como GERENTE. Posso ajudar com operação e funções gerenciais autorizadas.";
        return "Você está como OPERADOR. Posso ajudar com vendas e tarefas operacionais. Financeiro e ações críticas ficam protegidos e, quando necessário, eu chamo o gerente.";
    }
}
