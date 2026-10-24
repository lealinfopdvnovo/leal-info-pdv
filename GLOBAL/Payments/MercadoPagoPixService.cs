using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LealInfoPDV;

public sealed record MercadoPagoPixCharge(string OrderId,string QrCode,string? QrCodeBase64,string? TicketUrl);

public sealed class MercadoPagoPixService
{
    private readonly HttpClient http;
    public MercadoPagoPixService(string accessToken)
    {
        if(string.IsNullOrWhiteSpace(accessToken)) throw new InvalidOperationException("Access Token do Mercado Pago não configurado.");
        http=new HttpClient{BaseAddress=new Uri("https://api.mercadopago.com/"),Timeout=TimeSpan.FromSeconds(25)};
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",accessToken.Trim());
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LEAL-INFO-PDV/10.299");
    }

    public async Task TestarConexaoAsync(CancellationToken ct=default)
    {
        using var r=await http.GetAsync("users/me",ct);
        if(r.IsSuccessStatusCode)return;
        throw new HttpRequestException($"Mercado Pago recusou a credencial ({(int)r.StatusCode}).");
    }

    public async Task<MercadoPagoPixCharge> GerarAsync(decimal amount,string payerEmail,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(payerEmail)) throw new InvalidOperationException("Informe o e-mail do pagador para gerar o PIX automático.");
        var body=JsonSerializer.Serialize(new{
            type="online",total_amount=amount.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),
            external_reference="LEAL-"+DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),processing_mode="automatic",
            transactions=new{payments=new[]{new{amount=amount.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),payment_method=new{id="pix",type="bank_transfer"}}}},
            payer=new{email=payerEmail.Trim()}
        });
        using var req=new HttpRequestMessage(HttpMethod.Post,"v1/orders");
        req.Headers.TryAddWithoutValidation("X-Idempotency-Key",Guid.NewGuid().ToString());
        req.Content=new StringContent(body,Encoding.UTF8,"application/json");
        using var r=await http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);
        var json=await r.Content.ReadAsStringAsync(ct);
        if(!r.IsSuccessStatusCode)throw new HttpRequestException($"Mercado Pago: erro ao gerar PIX ({(int)r.StatusCode}): {json}");
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        var id=root.GetProperty("id").GetString()??throw new InvalidOperationException("Mercado Pago não retornou a order.");
        string qr="",b64="",ticket="";
        if(root.TryGetProperty("transactions",out var tr)&&tr.TryGetProperty("payments",out var ps)&&ps.ValueKind==JsonValueKind.Array&&ps.GetArrayLength()>0)
        {
            var p=ps[0];
            if(p.TryGetProperty("payment_method",out var pm))
            {
                if(pm.TryGetProperty("qr_code",out var q))qr=q.GetString()??"";
                if(pm.TryGetProperty("qr_code_base64",out var b))b64=b.GetString()??"";
                if(pm.TryGetProperty("ticket_url",out var t))ticket=t.GetString()??"";
            }
        }
        // APIs podem devolver os campos PIX em transaction_data.
        if(string.IsNullOrWhiteSpace(qr)&&root.TryGetProperty("transaction_data",out var td))
        {
            if(td.TryGetProperty("qr_code",out var q))qr=q.GetString()??"";
            if(td.TryGetProperty("qr_code_base64",out var b))b64=b.GetString()??"";
            if(td.TryGetProperty("ticket_url",out var t))ticket=t.GetString()??"";
        }
        if(string.IsNullOrWhiteSpace(qr))throw new InvalidOperationException("Mercado Pago não retornou o QR Code PIX.");
        return new(id,qr,string.IsNullOrWhiteSpace(b64)?null:b64,string.IsNullOrWhiteSpace(ticket)?null:ticket);
    }

    public async Task<bool> AguardarPagamentoAsync(string orderId,CancellationToken ct)
    {
        while(true)
        {
            ct.ThrowIfCancellationRequested();
            using var r=await http.GetAsync($"v1/orders/{Uri.EscapeDataString(orderId)}",ct);
            var json=await r.Content.ReadAsStringAsync(ct);
            if(r.IsSuccessStatusCode)
            {
                using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
                var status=root.TryGetProperty("status",out var s)?s.GetString()?.ToLowerInvariant():"";
                if(status is "processed" or "approved")return true;
                if(status is "canceled" or "cancelled" or "refunded" or "failed")return false;
            }
            await Task.Delay(TimeSpan.FromSeconds(3),ct);
        }
    }
}
