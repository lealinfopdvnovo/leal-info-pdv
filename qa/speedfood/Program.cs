using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using LealInfoPDV;

internal static class Program
{
    private const string DatabaseUrl = "https://novo-91da7436-default-rtdb.firebaseio.com";
    private static readonly Assembly Pdv = typeof(Database).Assembly;
    private static readonly Type FormType = Pdv.GetType("LealInfoPDV.DeliveryManagementForm", true)!;
    private static readonly Type SyncType = Pdv.GetType("LealInfoPDV.FirebaseDeliverySync", true)!;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    [STAThread]
    private static async Task Main()
    {
        var tokenFile = Environment.GetEnvironmentVariable("SPEEDFOOD_TEST_TOKEN_FILE");
        Database.Initialize();
        FormType.GetMethod("EnsureTrackingSchema", StaticPrivate)!.Invoke(null, null);
        if (string.IsNullOrEmpty(tokenFile))
        {
            await VerifyOfflineAsync();
            return;
        }
        using var tokens = JsonDocument.Parse(File.ReadAllText(tokenFile));
        var publisher = tokens.RootElement.GetProperty("publisher").GetString()!;
        SyncType.GetField("_idToken", StaticPrivate)!.SetValue(null, publisher);
        SyncType.GetField("_expires", StaticPrivate)!.SetValue(null, DateTime.UtcNow.AddMinutes(15));
        var address = "ENDEREÇO FICTÍCIO DE TESTE SPEEDFOOD — Japuíba, Angra dos Reis/RJ";
        var code = CreatePdvOrder(address);
        await PublishAsync(code.ToLowerInvariant(), address);
        using var http = new HttpClient();
        var orderUrl = $"{DatabaseUrl}/pedidos/{code}.json";
        using var read = await http.GetAsync(orderUrl + "?auth=" + Uri.EscapeDataString(publisher));
        Check(read.IsSuccessStatusCode, "O Firebase recusou a leitura do pedido criado pelo PDV.");
        using var order = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Check(order.RootElement.GetProperty("codigo").GetString() == code, "Código divergente.");
        Check(order.RootElement.GetProperty("destino").GetString() == address, "Endereço divergente.");
        Check(order.RootElement.GetProperty("pdvUid").GetString() == "Lme0kXlHalc9s6av0I9iJIoWxgJ2", "Publicador divergente.");
        Check(!order.RootElement.TryGetProperty("motoboyUid", out _), "Pedido nasceu atribuído a um motoboy.");
        var created = order.RootElement.GetProperty("criadoEm").GetInt64();
        await PublishAsync(code, address);
        using var repeated = JsonDocument.Parse(await http.GetStringAsync(orderUrl + "?auth=" + Uri.EscapeDataString(publisher)));
        Check(repeated.RootElement.GetProperty("criadoEm").GetInt64() == created, "Reenvio recriou o pedido.");
        var driverToken = tokens.RootElement.GetProperty("driver").GetString()!;
        var driverUid = tokens.RootElement.GetProperty("driverUid").GetString()!;
        using var appLookup = await http.GetAsync(orderUrl + "?auth=" + Uri.EscapeDataString(driverToken));
        Check(appLookup.IsSuccessStatusCode, "Motoboy não consegue consultar o caminho usado pelo app.");
        using var ownerRead = new HttpRequestMessage(HttpMethod.Get, $"{DatabaseUrl}/pedidos/{code}/motoboyUid.json?auth=" + Uri.EscapeDataString(driverToken));
        ownerRead.Headers.TryAddWithoutValidation("X-Firebase-ETag", "true");
        using var owner = await http.SendAsync(ownerRead);
        Check(owner.IsSuccessStatusCode, "Motoboy não consegue consultar a atribuição.");
        Check(owner.Headers.TryGetValues("ETag", out var etags), "ETag da transação ausente.");
        using var claim = new HttpRequestMessage(HttpMethod.Put, $"{DatabaseUrl}/pedidos/{code}/motoboyUid.json?auth=" + Uri.EscapeDataString(driverToken));
        claim.Headers.TryAddWithoutValidation("if-match", etags!.Single());
        claim.Content = new StringContent(JsonSerializer.Serialize(driverUid), System.Text.Encoding.UTF8, "application/json");
        using var claimed = await http.SendAsync(claim);
        Check(claimed.IsSuccessStatusCode, "Motoboy não conseguiu assumir o pedido.");
        await PublishAsync(code, address);
        using var afterClaim = JsonDocument.Parse(await http.GetStringAsync(orderUrl + "?auth=" + Uri.EscapeDataString(driverToken)));
        Check(afterClaim.RootElement.GetProperty("motoboyUid").GetString() == driverUid, "Reenvio apagou o motoboy.");
        Check(afterClaim.RootElement.GetProperty("destino").GetString() == address, "Reenvio apagou o endereço.");
        var freeCode = CreatePdvOrder(address);
        await PublishAsync(freeCode, address);
        var output = JsonSerializer.Serialize(new { codigo = freeCode, destino = address, caminho = $"pedidos/{freeCode}", origem = "Gerado pelo código real do PDV, salvo no SQLite e enviado pelo FirebaseDeliverySync", testeAssumirPedido = "PASS", testadoEmUtc = DateTime.UtcNow }, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory("speedfood-qa");
        File.WriteAllText("speedfood-qa/pedido-confirmado.json", output);
        Console.WriteLine("PASS: PDV gerou e publicou pedido; motoboy consultou e assumiu sem sobrescrever endereço.");
        Console.WriteLine("CODIGO_PARA_TESTE_NO_CELULAR=" + freeCode);
    }

    private static async Task VerifyOfflineAsync()
    {
        var codes = Enumerable.Range(0, 100).Select(_ => CreatePdvOrder("Endereço fictício de teste")).ToArray();
        Check(codes.Distinct().Count() == codes.Length, "Código duplicado.");
        Check(codes.All(c => c.Length == 12 && c.All(x => char.IsAsciiLetterUpper(x) || char.IsAsciiDigit(x))), "Formato incompatível com o app.");
        await Task.CompletedTask;
        Console.WriteLine("PASS: geração real de códigos no SQLite do PDV.");
    }
    private static string CreatePdvOrder(string address)
    {
        var method = FormType.GetMethod("InsertDeliveryWithTracking", StaticPrivate)!;
        var id = (long)method.Invoke(null, new object[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "TESTE SPEEDFOOD", "", address, "TESTE SEM ENTREGA REAL", "Teste de integração", 0d, 0d, "TESTE", DBNull.Value, "Pedido fictício criado pelo teste automatizado do PDV", "QA" })!;
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT code FROM delivery_tracking WHERE delivery_id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        return Convert.ToString(cmd.ExecuteScalar())!;
    }
    private static Task PublishAsync(string code, string address) => (Task)SyncType.GetMethod("PublishAsync", StaticPrivate)!.Invoke(null, new object?[] { null, code, address })!;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
