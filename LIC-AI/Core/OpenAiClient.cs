using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LicAi.Models;

namespace LicAi.Core;

public sealed class OpenAiClient
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.openai.com/v1/"),
        Timeout = TimeSpan.FromMinutes(3)
    };

    private readonly Func<string?> _apiKeyProvider;

    public OpenAiClient(Func<string?> apiKeyProvider)
    {
        _apiKeyProvider = apiKeyProvider;
    }

    public async Task<string> RespondAsync(string systemPrompt, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var key = _apiKeyProvider();
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("A chave da OpenAI ainda nao foi configurada.");

        var input = new List<object>
        {
            new { role = "system", content = new[] { new { type = "input_text", text = systemPrompt } } }
        };

        foreach (var message in messages)
        {
            input.Add(new
            {
                role = message.Role,
                content = new[] { new { type = "input_text", text = message.Content } }
            });
        }

        var payload = new
        {
            model = "gpt-5.6-sol",
            reasoning = new { effort = "medium" },
            input,
            max_output_tokens = 3000
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI retornou {(int)response.StatusCode}: {ExtractError(body)}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (root.TryGetProperty("output_text", out var directText) && directText.ValueKind == JsonValueKind.String)
            return directText.GetString() ?? string.Empty;

        var sb = new StringBuilder();
        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("type", out var type) && type.GetString() == "output_text" &&
                        part.TryGetProperty("text", out var text))
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(text.GetString());
                    }
                }
            }
        }

        var result = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("A OpenAI respondeu sem texto utilizavel.");
        return result;
    }

    private static string ExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
                return message.GetString() ?? "Erro desconhecido";
        }
        catch { }
        return body.Length > 500 ? body[..500] : body;
    }
}
