$ErrorActionPreference='Stop'
$p='LIC-AI/Core/NavigationAssistantClient.cs'
$code=@'
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LicAi.Models;

namespace LicAi.Core;

public sealed class NavigationAssistantClient
{
    private readonly Func<string?> _apiKeyProvider;
    public NavigationAssistantClient(Func<string?> apiKeyProvider) => _apiKeyProvider = apiKeyProvider;

    public async Task<NavigationAssistantReply> AskAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var last = messages.LastOrDefault()?.Content?.Trim() ?? "";
        var local = LocalReply(last);
        if (local != null) return local;

        var key = (_apiKeyProvider() ?? "").Replace("\r", "").Replace("\n", "").Trim();
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("A chave da OpenAI ainda não foi configurada.");

        var chat = new List<object> { new { role = "system", content = SystemManual } };
        foreach (var message in messages.TakeLast(12))
            chat.Add(new { role = message.Role, content = message.Content });

        var payload = new
        {
            model = "gpt-4o-mini",
            messages = chat,
            response_format = new { type = "json_object" },
            temperature = 0.3,
            max_tokens = 450
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(18));
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI retornou {(int)response.StatusCode}: {ExtractError(body)}");

        using var envelope = JsonDocument.Parse(body);
        var json = envelope.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        json = json.Trim().Trim('`');
        if (json.StartsWith("json", StringComparison.OrdinalIgnoreCase)) json = json[4..].Trim();
        using var result = JsonDocument.Parse(json);
        var root = result.RootElement;
        var mensagem = root.GetProperty("mensagem").GetString()?.Trim();
        string? comando = null;
        if (root.TryGetProperty("comando_abrir_tela", out var command) && command.ValueKind == JsonValueKind.String)
            comando = command.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(mensagem)) throw new InvalidOperationException("A OpenAI respondeu sem texto utilizável.");
        return new NavigationAssistantReply(mensagem, comando);
    }

    private static NavigationAssistantReply? LocalReply(string text)
    {
        var normalized = text.Trim().Trim('.', '!', '?').ToLowerInvariant();
        if (normalized is "oi" or "olá" or "ola") return new("Oi! Como posso ajudar você?", null);
        if (normalized.StartsWith("bom dia")) return new("Bom dia! Como posso ajudar você?", null);
        if (normalized.StartsWith("boa tarde")) return new("Boa tarde! Como posso ajudar você?", null);
        if (normalized.StartsWith("boa noite")) return new("Boa noite! Como posso ajudar você?", null);
        if (normalized is "tchau" or "até logo" or "ate logo") return new("Até logo! Quando precisar, é só me chamar.", null);
        return null;
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
Você é a LIA, assistente virtual do LEAL INFO PDV. Responda em português do Brasil, de forma natural, acolhedora, curta e profissional.
Retorne somente JSON válido neste formato: {"mensagem":"resposta","comando_abrir_tela":null}.
Para abrir uma tela, comando_abrir_tela pode ser: PRODUTOS, CLIENTES, FORNECEDORES, SERVICOS, ORDENS_SERVICO, ORCAMENTOS, FLUXO_CAIXA, HISTORICO_VENDAS, TELA_VENDAS, RELATORIOS, USUARIOS, CONFIGURACOES, CADASTROS ou AJUDA_CADASTRO.
Use null quando o usuário estiver apenas conversando ou perguntando. Nunca execute exclusões, vendas, alterações financeiras ou mudanças de segurança.
""";
}
'@
Set-Content $p $code -Encoding UTF8
Write-Host 'Resposta rapida da LIA V10.221 aplicada.'
