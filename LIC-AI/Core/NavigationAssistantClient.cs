using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LicAi.Models;

namespace LicAi.Core;

public sealed class NavigationAssistantClient
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.openai.com/v1/"),
        Timeout = TimeSpan.FromMinutes(2)
    };

    private readonly Func<string?> _apiKeyProvider;

    public NavigationAssistantClient(Func<string?> apiKeyProvider) => _apiKeyProvider = apiKeyProvider;

    public async Task<NavigationAssistantReply> AskAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var key = (_apiKeyProvider() ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("A chave da OpenAI ainda não foi configurada.");

        var input = new List<object>
        {
            new { role = "system", content = new[] { new { type = "input_text", text = SystemManual } } }
        };
        foreach (var message in messages)
            input.Add(new { role = message.Role, content = new[] { new { type = "input_text", text = message.Content } } });

        var payload = new
        {
            model = "gpt-4o-mini",
            input,
            max_output_tokens = 900,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "assistente_navegacao_pdv",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            mensagem = new { type = "string", description = "Explicação amigável e objetiva, em português do Brasil." },
                            comando_abrir_tela = new { type = new[] { "string", "null" }, description = "Nome exato de uma tela permitida ou null." }
                        },
                        required = new[] { "mensagem", "comando_abrir_tela" },
                        additionalProperties = false
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var body = Encoding.UTF8.GetString(bytes);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI retornou {(int)response.StatusCode}: {ExtractError(body)}");

        var json = ExtractOutputText(body);
        using var result = JsonDocument.Parse(json);
        var root = result.RootElement;
        var mensagem = root.GetProperty("mensagem").GetString()?.Trim();
        string? comando = null;
        if (root.TryGetProperty("comando_abrir_tela", out var commandElement) && commandElement.ValueKind == JsonValueKind.String)
            comando = commandElement.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(mensagem)) throw new InvalidOperationException("A OpenAI respondeu sem uma explicação utilizável.");
        return new NavigationAssistantReply(mensagem, comando);
    }

    private static string ExtractOutputText(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (root.TryGetProperty("output_text", out var direct) && direct.ValueKind == JsonValueKind.String)
            return direct.GetString() ?? string.Empty;
        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
                foreach (var part in content.EnumerateArray())
                    if (part.TryGetProperty("type", out var type) && type.GetString() == "output_text" && part.TryGetProperty("text", out var text))
                        return text.GetString() ?? string.Empty;
            }
        throw new InvalidOperationException("A OpenAI respondeu sem JSON utilizável.");
    }

    private static string ExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var message))
                return message.GetString() ?? body;
        }
        catch { }
        return body.Length > 500 ? body[..500] : body;
    }

    private const string SystemManual = """
Você é a LIC AI, assistente de navegação e explicação do LEAL INFO PDV.
Responda sempre em português do Brasil e estritamente pelo JSON Schema fornecido.
Explique passos de forma curta, clara e segura. Nunca invente botões ou funções.
Quando o usuário pedir para abrir uma tela, use exatamente um destes comandos:
PRODUTOS, CLIENTES, FORNECEDORES, SERVICOS, ORDENS_SERVICO, ORCAMENTOS,
FLUXO_CAIXA, HISTORICO_VENDAS, TELA_VENDAS, RELATORIOS, USUARIOS,
CONFIGURACOES, CADASTROS ou AJUDA_CADASTRO.
Se a pergunta for apenas explicativa ou não corresponder a uma tela, retorne null.

MANUAL DAS TELAS:
- TELA_VENDAS: registra vendas. Localize o produto por código ou catálogo, informe quantidade, adicione ao carrinho, escolha pagamento e finalize. F2 finaliza, F5 abre catálogo, F7 remove item e Esc fecha.
- PRODUTOS: cadastra, consulta, edita e inativa produtos; contém código, nome, categoria, custo, preço de venda, estoque e estoque mínimo.
- CLIENTES: cadastra e consulta nome, CPF/CNPJ, telefone, e-mail e endereço.
- FORNECEDORES: cadastra e consulta os dados de fornecedores.
- SERVICOS: cadastra serviços, valor e descrição.
- ORDENS_SERVICO: cria e acompanha OS com cliente, equipamento, defeito, serviço realizado, status, valor e observações.
- ORCAMENTOS: cria e acompanha orçamento, cliente, descrição, valor e status.
- FLUXO_CAIXA: registra e consulta entradas e saídas. Exige nível gerencial.
- HISTORICO_VENDAS: consulta vendas realizadas, pagamento, totais e operador.
- RELATORIOS: mostra resumo de produtos, clientes, vendas, entradas, saídas, saldo e estoque baixo.
- USUARIOS: administra usuários, senhas, nível de acesso, permissões e situação. Exige administrador.
- CONFIGURACOES: mostra dados do sistema, serial e localização do banco.
- CADASTROS: central com atalhos para produtos, clientes, fornecedores e serviços.
- AJUDA_CADASTRO: tutorial visual da área de cadastros.
Não execute exclusões, vendas, alterações financeiras ou mudanças de segurança; apenas abra telas.
""";
}
