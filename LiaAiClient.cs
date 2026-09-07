using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LealInfoPDV;

/// <summary>
/// Camada conversacional online da LIA. Não executa ações do PDV e não decide permissões.
/// Ações e dados sensíveis continuam exclusivamente no LiaCore/Auth.
/// </summary>
public sealed class LiaAiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly List<(string role, string text)> historico = new();
    private const int MaxHistorico = 12;

    public bool Configurada => !string.IsNullOrWhiteSpace(Chave());

    public async Task<string?> ConversarAsync(string texto, CancellationToken cancellationToken = default)
    {
        var chave = Chave();
        if (string.IsNullOrWhiteSpace(chave)) return null;

        var mensagens = new List<object> { new { role = "system", content = PromptSistema() } };
        foreach (var h in historico) mensagens.Add(new { role = h.role, content = h.text });
        mensagens.Add(new { role = "user", content = texto });

        var payload = new
        {
            model = "gpt-5.6-luna",
            messages = mensagens,
            max_completion_tokens = 220,
            reasoning_effort = "none"
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
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

    private static string? Chave() => Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User)
                                      ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    private static string ExtrairTexto(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) return "";
        var first = choices[0];
        if (!first.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return "";
        if (!message.TryGetProperty("content", out var content)) return "";
        return content.ValueKind == JsonValueKind.String ? content.GetString() ?? "" : "";
    }

    private static string PromptSistema() => $"""
Você é LIA, assistente de voz do LEAL INFO PDV. Converse naturalmente em português do Brasil, como uma pessoa inteligente, educada, descontraída, rápida e objetiva. O operador atual se chama {Auth.OperatorName}.
Entenda o contexto da conversa e responda ao que a pessoa realmente perguntou. Se ela pedir somente o placar, responda somente o placar. Evite discursos, explicações desnecessárias e frases robóticas. Em voz, prefira respostas curtas e naturais.
Você é a camada de CONVERSA, não a camada de autorização do caixa. Nunca afirme que abriu tela, alterou cadastro, cancelou venda, mexeu em caixa, concedeu permissão, autenticou gerente ou executou qualquer ação no PDV. Nunca peça senha, PIN, token, chave de API ou credencial. Se pedirem uma ação operacional do PDV que chegou até você, diga de forma curta que o comando precisa ser tratado pelo controle seguro da LIA.
Não invente números de vendas, estoque, clientes, caixa, financeiro ou outros dados internos. Esses dados são consultados localmente pelo PDV quando autorizado.
Pode conversar normalmente sobre assuntos gerais. Quando uma pergunta depender de informação atual que você não possua com segurança, diga isso de forma curta em vez de inventar. Responda em geral com 1 a 3 frases, pois sua resposta será falada em voz alta.
""";
}
