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
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly List<(string role, string text)> historico = new();
    private const int MaxHistorico = 6;

    public bool Configurada => !string.IsNullOrWhiteSpace(Chave());

    public async Task<string?> ConversarAsync(string texto, CancellationToken cancellationToken = default)
    {
        var chave = Chave();
        if (string.IsNullOrWhiteSpace(chave)) return null;

        var entrada = new StringBuilder(900);
        entrada.AppendLine(PromptSistema());
        if (historico.Count > 0)
        {
            entrada.AppendLine("Contexto recente:");
            foreach (var h in historico)
                entrada.AppendLine($"{(h.role == "user" ? "Pessoa" : "LIA")}: {h.text}");
        }
        entrada.AppendLine($"Pessoa: {texto}");
        entrada.Append("LIA:");

        var payload = new Dictionary<string, object?>
        {
            ["model"] = "gpt-5.6-luna",
            ["input"] = entrada.ToString(),
            ["reasoning"] = new { effort = "none" },
            ["max_output_tokens"] = 100,
            ["store"] = false
        };

        if (PrecisaWeb(texto))
        {
            payload["tools"] = new object[] { new { type = "web_search" } };
            payload["tool_choice"] = "auto";
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
Você é a LIA do LEAL INFO PDV. O operador atual é {Auth.OperatorName}.
Fale em português do Brasil como conversa de voz: leve, espontânea, descontraída e curta. Não soe como atendente, manual ou robô. Pode acompanhar gírias e palavrões sem repreender. Em conversa comum, não puxe o assunto para o PDV. Responda normalmente em 1 frase curta; use 2 só quando necessário. Entenda o contexto recente.
Se a pergunta depender de fato atual, use a pesquisa disponível e não invente. A IA nunca concede permissões nem executa ações protegidas: segurança, autenticação e dados internos continuam exclusivamente no controle local do PDV.
""";
}
