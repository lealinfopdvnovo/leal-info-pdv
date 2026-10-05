using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LealInfoPDV;

// Only the existing publisher account may create unassigned PDV orders.
internal static class FirebaseDeliverySync
{
    private const string ApiKey = "AIzaSyCnU5br609C2UuUNznxUSq154cbN-yNU78";
    private const string PublisherEmail = "lealinfopdvnovo@gmail.com";
    private const string PublisherUid = "Lme0kXlHalc9s6av0I9iJIoWxgJ2";
    private const string DatabaseUrl = "https://novo-91da7436-default-rtdb.firebaseio.com";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static string? _idToken;
    private static DateTime _expires;
    private static string SessionPath => Path.Combine(Database.AppFolder, "speedfood-session.dat");

    private static void StoreRefresh(string refresh)
    {
        Directory.CreateDirectory(Database.AppFolder);
        File.WriteAllBytes(SessionPath, ProtectedData.Protect(Encoding.UTF8.GetBytes(refresh), null, DataProtectionScope.CurrentUser));
    }

    private static async Task<string> TokenAsync(IWin32Window owner)
    {
        if (_idToken != null && DateTime.UtcNow < _expires) return _idToken;
        if (File.Exists(SessionPath))
        {
            string? refresh = null;
            try { refresh = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(SessionPath), null, DataProtectionScope.CurrentUser)); }
            catch (CryptographicException) { }
            if (refresh != null)
            {
                using var response = await Http.PostAsync($"https://securetoken.googleapis.com/v1/token?key={ApiKey}",
                    new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh }));
                if (response.IsSuccessStatusCode)
                {
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (json.RootElement.GetProperty("user_id").GetString() != PublisherUid)
                        throw new InvalidOperationException("A conta conectada não é a conta publicadora do PDV.");
                    _idToken = json.RootElement.GetProperty("id_token").GetString()!;
                    StoreRefresh(json.RootElement.GetProperty("refresh_token").GetString()!);
                    _expires = DateTime.UtcNow.AddSeconds(int.Parse(json.RootElement.GetProperty("expires_in").GetString()!) - 60);
                    return _idToken;
                }
                if (response.StatusCode != HttpStatusCode.BadRequest && response.StatusCode != HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("Não foi possível conectar o PDV ao SpeedFood. Tente novamente.");
            }
        }
        using var login = new Form { Text = "Conectar PDV ao SpeedFood", Width = 420, Height = 240,
            StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var password = new TextBox { Left = 20, Top = 65, Width = 365, UseSystemPasswordChar = true, PlaceholderText = "Senha do Firebase Authentication" };
        var submit = new Button { Left = 20, Top = 145, Width = 365, Text = "CONECTAR", DialogResult = DialogResult.OK };
        login.Controls.Add(new Label { Left = 20, Top = 15, Width = 365, Height = 35, Text = "Conecte uma vez para enviar os pedidos ao SpeedFood." });
        login.Controls.AddRange(new Control[] { password, submit });
        login.AcceptButton = submit;
        if (login.ShowDialog(owner) != DialogResult.OK) throw new OperationCanceledException("Conexão ao SpeedFood cancelada. O código não foi enviado.");
        using var signedIn = await Http.PostAsJsonAsync($"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={ApiKey}",
            new { email = PublisherEmail, password = password.Text, returnSecureToken = true });
        password.Clear();
        if (!signedIn.IsSuccessStatusCode) throw new InvalidOperationException("Não foi possível entrar no SpeedFood. Confira a conta e a senha do Firebase Authentication.");
        using var credentials = JsonDocument.Parse(await signedIn.Content.ReadAsStringAsync());
        if (credentials.RootElement.GetProperty("localId").GetString() != PublisherUid)
            throw new InvalidOperationException("Use a conta publicadora cadastrada para este PDV.");
        _idToken = credentials.RootElement.GetProperty("idToken").GetString()!;
        StoreRefresh(credentials.RootElement.GetProperty("refreshToken").GetString()!);
        _expires = DateTime.UtcNow.AddSeconds(int.Parse(credentials.RootElement.GetProperty("expiresIn").GetString()!) - 60);
        return _idToken;
    }

    internal static async Task PublishAsync(IWin32Window owner, string code, string address)
    {
        code = Regex.Replace(code.Trim().ToUpperInvariant(), @"[\s-]", "");
        if (code.Length != 12 || code.Any(c => !char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c)) || string.IsNullOrWhiteSpace(address))
            throw new InvalidOperationException("Pedido sem código ou endereço válido.");
        var token = await TokenAsync(owner);
        var url = $"{DatabaseUrl}/pedidos/{Uri.EscapeDataString(code)}.json?auth={Uri.EscapeDataString(token)}";
        using var get = new HttpRequestMessage(HttpMethod.Get, url);
        get.Headers.TryAddWithoutValidation("X-Firebase-ETag", "true");
        using var existing = await Http.SendAsync(get);
        if (!existing.IsSuccessStatusCode) throw new InvalidOperationException("O Firebase recusou a consulta do pedido. O código não foi enviado.");
        using var data = JsonDocument.Parse(await existing.Content.ReadAsStringAsync());
        if (data.RootElement.ValueKind != JsonValueKind.Null)
        {
            if (!data.RootElement.TryGetProperty("pdvUid", out var publisher) || publisher.GetString() != PublisherUid)
                throw new InvalidOperationException("Este código já pertence a outro pedido no Firebase.");
            if (!data.RootElement.TryGetProperty("destino", out var destination) || destination.GetString() != address.Trim())
                throw new InvalidOperationException("O endereço salvo no Firebase difere deste pedido. O código não foi enviado.");
            return;
        }
        using var put = new HttpRequestMessage(HttpMethod.Put, url);
        put.Headers.TryAddWithoutValidation("if-match", "null_etag");
        put.Content = JsonContent.Create(new Dictionary<string, object> {
            ["codigo"] = code, ["destino"] = address.Trim(), ["pdvUid"] = PublisherUid,
            ["status"] = "Aguardando motoboy", ["criadoEm"] = new Dictionary<string, string> { [".sv"] = "timestamp" }
        });
        using var saved = await Http.SendAsync(put);
        if (!saved.IsSuccessStatusCode)
            throw new InvalidOperationException("Não foi possível publicar o pedido no Firebase. O código não foi enviado; tente novamente.");
    }
}
