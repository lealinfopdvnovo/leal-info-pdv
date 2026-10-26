using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace LealInfo.IFood.Groceries;

public sealed record HttpEvidence(DateTimeOffset AtUtc, string Method, string RouteTemplate,
    string OrderCorrelation, int? Status, long ElapsedMs, string Outcome);

// No login flow, token persistence, polling or live calls are started by this component.
// A future host supplies an OAuth token from its secure credential provider.
public sealed class ApiReader : IDisposable
{
    private readonly HttpClient http;
    private readonly Func<CancellationToken, Task<string>> token;
    private readonly Action<HttpEvidence> log;
    public ApiReader(Func<CancellationToken, Task<string>> tokenProvider, Action<HttpEvidence> evidence,
        HttpMessageHandler? isolatedTestHandler = null)
    {
        token = tokenProvider; log = evidence;
        http = new HttpClient(isolatedTestHandler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    }
    public async Task<string> ReadAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("Order id required");
        const string route = "/order/v1.0/orders/{id}/virtual-bag";
        var correlation = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id.ToString("D"))))[..16];
        var clock = Stopwatch.StartNew(); int? status = null; string outcome = "failed";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://merchant-api.ifood.com.br" + route.Replace("{id}", id.ToString("D")));
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await token(ct).ConfigureAwait(false));
            using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            status = (int)res.StatusCode;
            if (status != 200) throw new HttpRequestException("iFood Virtual Bag HTTP " + status, null, res.StatusCode);
            using var input = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var buffer = new MemoryStream(); byte[] chunk = new byte[8192]; int count;
            while ((count = await input.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
            { if (buffer.Length + count > 1_048_576) throw new ContractException("HTTP body size"); buffer.Write(chunk, 0, count); }
            var body = new UTF8Encoding(false, true).GetString(buffer.ToArray()); outcome = "received"; return body;
        }
        finally
        {
            // Log only allowlisted metadata. Never headers, tokens, bodies, names or addresses.
            try { log(new HttpEvidence(DateTimeOffset.UtcNow, "GET", route, correlation, status, clock.ElapsedMilliseconds, outcome)); } catch { /* logging cannot leak or change order outcome */ }
        }
    }
    public void Dispose() => http.Dispose();
}
