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
    private const int MaxHistorico = 4;

    public bool Configurada => !string.IsNullOrWhiteSpace(Chave());

    public async Task<string?> ConversarAsync(string texto, CancellationToken cancellationToken = default)
    {
        var chave = Chave();
        if (string.IsNullOrWhiteSpace(chave)) return null;

        var entrada = new StringBuilder(760);
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
            ["max_output_tokens"] = 80,
            ["store"] = false,
            ["stream"] = true,
            ["prompt_cache_key"] = "lia-pdv-voz-v150"
        };

        if (PrecisaWeb(texto))
        {
            payload["tools"] = new object[] { new { type = "web_search" } };
            payload["tool_choice"] = "auto";
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI HTTP {(int)resp.StatusCode}");

        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var resposta = new StringBuilder(180);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line[5..].Trim();
            if (data == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var tipo)) continue;
                if (tipo.GetString() != "response.output_text.delta") continue;
                if (!root.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.String) continue;

                resposta.Append(delta.GetString());
                var parcial = resposta.ToString().Trim();

                // Voz precisa soar instantânea: a LIA responde assim que fecha a primeira frase útil.
                if (parcial.Length >= 18 && (parcial.EndsWith('.') || parcial.EndsWith('!') || parcial.EndsWith('?')))
                    break;
                if (parcial.Length >= 110)
                    break;
            }
            catch (JsonException) { }
        }

        var final = resposta.ToString().Trim();
        if (string.IsNullOrWhiteSpace(final)) return null;

        historico.Add(("user", texto));
        historico.Add(("assistant", final));
        while (historico.Count > MaxHistorico) historico.RemoveAt(0);
        return final;
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

    private static string PromptSistema() => $"""
Você é a LIA do LEAL INFO PDV. Seu nome é LIA. Nunca use outro nome para si mesma. O operador atual é {Auth.OperatorName}.
Converse em português do Brasil como voz ao vivo: espontânea, rápida, inteligente, descontraída e natural. Entenda intenção, contexto, gíria, frase incompleta e jeito informal de falar. Pode acompanhar palavrões sem repreender. Não arraste conversa comum para o PDV.
Responda normalmente em UMA frase curta e útil. Só use uma segunda frase se for indispensável. Vá direto ao ponto, sem introdução, sem repetir a pergunta e sem explicar seu raciocínio interno.
Se depender de fato atual, use a pesquisa disponível e não invente. A IA nunca concede permissões nem executa ações protegidas: segurança, autenticação e dados internos continuam exclusivamente no controle local do PDV.
""";
}
