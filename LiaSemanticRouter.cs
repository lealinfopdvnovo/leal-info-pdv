using System.Globalization;
using System.Text;

namespace LealInfoPDV;

/// <summary>
/// Roteador semântico local da LIA. Entende pedidos naturais sem depender de frase exata.
/// Só devolve intenções permitidas; segurança e permissões continuam no LiaCore/Auth.
/// </summary>
public static class LiaSemanticRouter
{
    private static readonly Dictionary<string, string[]> Conceitos = new(StringComparer.Ordinal)
    {
        ["ABRIR_PRODUTOS"] = new[] { "produto", "produtos", "mercadoria", "mercadorias", "item", "itens", "estoque", "preco", "precos", "codigo de barras", "cadastro de produto" },
        ["ABRIR_CLIENTES"] = new[] { "cliente", "clientes", "consumidor", "comprador", "cadastro de cliente" },
        ["ABRIR_FORNECEDORES"] = new[] { "fornecedor", "fornecedores", "empresa fornecedora", "parceiro", "parceiros", "cadastro de fornecedor" },
        ["ABRIR_SERVICOS"] = new[] { "servico", "servicos", "prestacao de servico", "cadastro de servico" },
        ["ABRIR_HISTORICO"] = new[] { "historico de vendas", "historico vendas", "vendas antigas", "vendas anteriores", "consultar vendas", "movimentacoes de venda" },
        ["ABRIR_FINANCEIRO"] = new[] { "financeiro", "financas", "fluxo de caixa", "contas", "pagamentos", "recebimentos" },
        ["ABRIR_ORDENS"] = new[] { "ordem de servico", "ordens de servico", "ordem", "ordens", "os", "o s", "chamado de servico" },
        ["ABRIR_ORCAMENTOS"] = new[] { "orcamento", "orcamentos", "cotacao para cliente", "proposta", "propostas" },
        ["ABRIR_VENDAS"] = new[] { "pdv", "venda", "vendas", "caixa", "vender", "passar compra", "registrar venda", "lancar venda", "fazer venda", "atendimento" },
        ["ABRIR_RELATORIOS"] = new[] { "relatorio", "relatorios", "relatorio gerencial", "resumo gerencial", "indicadores" },
        ["FAZER_BACKUP"] = new[] { "backup", "copia de seguranca", "salvar copia", "fazer copia do sistema" },
        ["ABRIR_CONFIGURACOES"] = new[] { "configuracao", "configuracoes", "config", "ajustes", "preferencias", "parametros", "opcoes do sistema" },
        ["FECHAR_TELA"] = new[] { "fechar tela", "fecha tela", "fechar janela", "fecha janela", "sair dessa tela", "voltar dessa tela", "fecha isso", "fechar isso", "sair do sistema", "fecha o sistema", "fechar o sistema", "encerrar o sistema" },
        ["PERMISSOES"] = new[] { "permissao", "permissoes", "o que posso fazer", "meu acesso", "meus acessos", "tenho acesso" },
        ["CONSULTAR_ESTOQUE_PRODUTO"] = new[] { "quanto tem", "quantidade em estoque", "estoque de", "tem no estoque", "quantos tem" },
        ["CADASTRAR_PRODUTO"] = new[] { "cadastrar produto", "novo produto", "adicionar produto", "criar produto", "incluir produto", "cadastrar mercadoria", "nova mercadoria" },
        ["VENDAS_HOJE"] = new[] { "vendas de hoje", "vendi hoje", "faturamento de hoje", "movimento de hoje", "quanto vendeu hoje" },
        ["ESTOQUE_BAIXO"] = new[] { "estoque baixo", "produto acabando", "produto faltando", "precisa repor", "reposicao", "estoque minimo" },
        ["CLIENTES"] = new[] { "quantos clientes", "clientes cadastrados", "total de clientes" },
        ["SALDO_CAIXA"] = new[] { "saldo do caixa", "quanto tem no caixa", "saldo caixa" },
        ["CONTAS_PAGAR"] = new[] { "contas a pagar", "o que tenho pra pagar", "pagamentos pendentes", "vencimentos" },
        ["RESUMO_EMPRESA"] = new[] { "como esta a empresa", "como esta a loja", "resumo da empresa", "situacao da loja", "visao geral" }
    };

    private static readonly string[] VerbosAcao =
    {
        "abre", "abrir", "entra", "entrar", "vai", "ir", "leva", "mostrar", "mostra", "quero", "preciso",
        "acessa", "acessar", "cadastro", "cadastrar", "criar", "novo", "nova", "lancar", "registrar", "fazer",
        "consulta", "consultar", "ver", "visualizar"
    };

    public static string? Interpretar(string texto)
    {
        var n = Normalizar(texto);
        if (string.IsNullOrWhiteSpace(n)) return null;

        var tokens = Tokenizar(n);
        var temVerboAcao = VerbosAcao.Any(v => n.Contains(v, StringComparison.Ordinal));

        string? melhor = null;
        double melhorScore = 0;

        foreach (var par in Conceitos)
        {
            double score = 0;
            foreach (var frase in par.Value)
            {
                var f = Normalizar(frase);
                if (n.Contains(f, StringComparison.Ordinal)) score = Math.Max(score, 6 + f.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

                var ft = Tokenizar(f);
                if (ft.Count > 0)
                {
                    var comuns = ft.Count(t => tokens.Contains(t));
                    score = Math.Max(score, comuns * 2.0 / ft.Count * 4.0);
                }
            }

            if (par.Key.StartsWith("ABRIR_", StringComparison.Ordinal) && !temVerboAcao)
                score -= 2.2;

            if (score > melhorScore)
            {
                melhorScore = score;
                melhor = par.Key;
            }
        }

        if (melhor is null) return null;
        var minimo = melhor.StartsWith("ABRIR_", StringComparison.Ordinal) ? 4.8 : 4.0;
        return melhorScore >= minimo ? melhor : null;
    }

    private static HashSet<string> Tokenizar(string texto)
    {
        var stop = new HashSet<string>(new[] { "a", "o", "as", "os", "de", "da", "do", "das", "dos", "um", "uma", "pra", "para", "por", "no", "na", "nos", "nas", "eu", "me", "mim", "essa", "esse", "isso", "aquela", "aquele", "ali", "la", "aqui" }, StringComparer.Ordinal);
        return texto.Split(new[] { ' ', ',', '.', ';', ':', '?', '!', '-', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !stop.Contains(t)).ToHashSet(StringComparer.Ordinal);
    }

    private static string Normalizar(string t)
    {
        var f = (t ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(f.Length);
        foreach (var c in f)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
