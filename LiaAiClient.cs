using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LealInfoPDV;

/// <summary>
/// Camada conversacional online da LIA. Conversa e pesquisa web ficam livres para testes.
/// Ações, dados sensíveis e permissões do PDV continuam exclusivamente no LiaCore/Auth.
/// </summary>
public sealed class LiaAiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(18) };
    private readonly List<(string role, string text)> historico = new();
    private const int MaxHistorico = 12;

    public bool Configurada => !string.IsNullOrWhiteSpace(Chave());

    public async Task<string?> ConversarAsync(string texto, CancellationToken cancellationToken = default)
    {
        var chave = Chave();
        if (string.IsNullOrWhiteSpace(chave)) return null;

        var entrada = new StringBuilder();
        entrada.AppendLine(PromptSistema());
        if (historico.Count > 0)
        {
            entrada.AppendLine("\nContexto recente da conversa:");
            foreach (var h in historico)
                entrada.AppendLine($"{(h.role == "user" ? "Pessoa" : "LIA")}: {h.text}");
        }
        entrada.AppendLine($"\nPessoa: {texto}");
        entrada.Append("LIA:");

        var payload = new Dictionary<string, object?>
        {
            ["model"] = "gpt-5.6-luna",
            ["input"] = entrada.ToString(),
            ["reasoning"] = new { effort = "none" },
            ["max_output_tokens"] = 220
        };

        // Pesquisa web só entra quando a pergunta realmente depende de informação atual.
        // Conversa comum fica bem mais rápida e barata.
        if (PrecisaWeb(texto))
        {
            payload["tools"] = new object[] { new { type = "web_search" } };
            payload["tool_choice"] = "auto";
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, cancellationToken);
        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException($"OpenAI HTTP {(int)resp.StatusCode}");

        using var doc = JsonDocument.Parse(json);
        var resposta = ExtrairTexto(doc.RootElement);
        if (string.IsNullOrWhiteSpace(resposta)) return null;

        historico.Add(("user", texto));
        historico.Add(("assistant", resposta));
        while (historico.Count > MaxHistorico) historico.RemoveAt(0);
        return resposta.Trim();
    }

    private static bool PrecisaWeb(string texto)
    {
        var t = texto.ToLowerInvariant();
        string[] sinais =
        {
            "hoje", "agora", "atual", "atualmente", "último", "ultimo", "última", "ultima",
            "placar", "jogo", "jogando", "resultado", "notícia", "noticia", "notícias", "noticias",
            "clima", "tempo em", "temperatura", "previsão", "previsao", "cotação", "cotacao",
            "dólar", "dolar", "euro", "preço hoje", "preco hoje", "horário", "horario",
            "quem ganhou", "quem venceu", "aconteceu", "pesquisa", "pesquise", "internet"
        };
        return sinais.Any(t.Contains);
    }

    private static string? Chave() => Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User)
                                      ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    private static string ExtrairTexto(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
            return outputText.GetString() ?? "";

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array) return "";
        var sb = new StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var type) || type.GetString() != "message") continue;
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
            foreach (var part in content.EnumerateArray())
            {
                if (!part.TryGetProperty("type", out var partType) || partType.GetString() != "output_text") continue;
                if (part.TryGetProperty("text", out var textPart) && textPart.ValueKind == JsonValueKind.String)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(textPart.GetString());
                }
            }
        }
        return sb.ToString();
    }

    private static string PromptSistema() => $"""
Você é LIA, uma assistente inteligente integrada ao LEAL INFO PDV. Nesta fase de TESTES, sua conversa é livre: converse sobre qualquer assunto permitido, responda dúvidas gerais e use pesquisa na internet quando a pergunta depender de informação atual, recente ou verificável. O operador atual se chama {Auth.OperatorName}.
Converse naturalmente em português do Brasil: inteligente, educada, descontraída, rápida e objetiva. Entenda contexto e referências das mensagens anteriores. Se a pessoa disser "só o placar", responda somente o placar. Se perguntar se você pesquisa na internet, diga que sim, que pode pesquisar informações atuais quando necessário. Não se apresente como limitada a consultar somente o PDV.
Para resultados esportivos, notícias, clima, preços, horários e outros fatos atuais, pesquise antes de responder. Nunca invente informação atual. Em voz, prefira respostas curtas, naturais e diretas, geralmente de 1 a 3 frases.
A liberdade desta fase vale para CONVERSA e PESQUISA, não para autoridade dentro do PDV. Nunca conceda permissões, autentique gerente, revele credenciais, nem afirme que executou cancelamento, alteração de preço, movimentação de caixa, financeiro ou outra ação protegida. Não peça senha, PIN, token ou chave de API. Dados internos de vendas, estoque, clientes, caixa e financeiro só podem vir do controle local autorizado do PDV.
""";
}
