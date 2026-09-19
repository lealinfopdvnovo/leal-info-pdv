using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LealInfoPDV;

public sealed record PixCharge(string IdTransacao, string PayloadQrCode, string? EncodedImage, DateTime? Expiracao);

public sealed class AsaasPixService
{
    private static readonly HttpClient Http = CreateHttpClient();
    private readonly string apiKey;

    public AsaasPixService(string? apiKey = null)
    {
        this.apiKey = apiKey ?? Environment.GetEnvironmentVariable("LEAL_ASAAS_API_KEY")
            ?? throw new InvalidOperationException("Configure LEAL_ASAAS_API_KEY no Windows antes de habilitar o PIX.");
    }

    private static HttpClient CreateHttpClient()
    {
        var sandbox = string.Equals(Environment.GetEnvironmentVariable("LEAL_ASAAS_SANDBOX"), "1", StringComparison.OrdinalIgnoreCase);
        var client = new HttpClient
        {
            BaseAddress = new Uri(sandbox ? "https://api-sandbox.asaas.com/v3/" : "https://api.asaas.com/v3/"),
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private HttpRequestMessage Request(HttpMethod method, string endpoint)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.TryAddWithoutValidation("access_token", apiKey);
        return request;
    }

    public async Task<PixCharge> GerarCobrancaPixAsync(decimal valorVenda, string customerId, CancellationToken ct = default)
    {
        if (valorVenda <= 0) throw new ArgumentOutOfRangeException(nameof(valorVenda));
        if (string.IsNullOrWhiteSpace(customerId)) throw new ArgumentException("Cliente Asaas não informado.", nameof(customerId));

        var body = JsonSerializer.Serialize(new
        {
            customer = customerId,
            billingType = "PIX",
            value = valorVenda,
            dueDate = DateTime.Today.ToString("yyyy-MM-dd"),
            description = "Venda LEAL INFO PDV"
        });

        using var post = Request(HttpMethod.Post, "payments");
        post.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await Http.SendAsync(post, HttpCompletionOption.ResponseHeadersRead, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Falha ao criar PIX ({(int)response.StatusCode}): {json}");

        using var doc = JsonDocument.Parse(json);
        var id = doc.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Asaas não retornou o ID da cobrança.");

        using var getQr = Request(HttpMethod.Get, $"payments/{Uri.EscapeDataString(id)}/pixQrCode");
        using var qrResponse = await Http.SendAsync(getQr, HttpCompletionOption.ResponseHeadersRead, ct);
        var qrJson = await qrResponse.Content.ReadAsStringAsync(ct);
        if (!qrResponse.IsSuccessStatusCode) throw new HttpRequestException($"Falha ao obter QR PIX ({(int)qrResponse.StatusCode}): {qrJson}");

        using var qrDoc = JsonDocument.Parse(qrJson);
        var root = qrDoc.RootElement;
        var payload = root.GetProperty("payload").GetString()
            ?? throw new InvalidOperationException("Asaas não retornou o payload PIX.");
        var image = root.TryGetProperty("encodedImage", out var img) ? img.GetString() : null;
        DateTime? expiration = root.TryGetProperty("expirationDate", out var exp) && DateTime.TryParse(exp.GetString(), out var parsed) ? parsed : null;
        return new PixCharge(id, payload, image, expiration);
    }

    public async Task<bool> VerificarStatusPagamentoAsync(string idTransacao, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idTransacao)) throw new ArgumentException("ID PIX inválido.", nameof(idTransacao));
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            using var request = Request(HttpMethod.Get, $"payments/{Uri.EscapeDataString(idTransacao)}");
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(json);
                var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString()?.ToUpperInvariant() : null;
                if (status is "RECEIVED" or "CONFIRMED" or "PAID" or "APPROVED") return true;
                if (status is "REFUNDED" or "DELETED" or "CANCELED") return false;
            }
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }
    }
}
