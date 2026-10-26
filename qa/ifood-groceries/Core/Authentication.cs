#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LealInfo.IFood.Groceries
{
    // C# 5 compatible after removing the nullable directive; Windows PowerShell needs no SDK.
    // Secret material is held in process memory only. Never serialize these objects or raw responses.
    public sealed class AuthenticationFailure : Exception
    {
        public int? Status { get; private set; }
        public AuthenticationFailure(string safeMessage, int? status = null) : base(safeMessage) { Status = status; }
    }
    public sealed class AuthenticationEvidence
    {
        public DateTimeOffset AtUtc { get; private set; }
        public string Method { get; private set; }
        public string Route { get; private set; }
        public int? Status { get; private set; }
        public long ElapsedMs { get; private set; }
        public string Outcome { get; private set; }
        internal AuthenticationEvidence(string method, string route, int? status, long elapsed, string outcome)
        { AtUtc = DateTimeOffset.UtcNow; Method = method; Route = route; Status = status; ElapsedMs = elapsed; Outcome = outcome; }
    }
    public sealed class LinkingCode
    {
        public string UserCode { get; private set; }
        public Uri VerificationUrl { get; private set; }
        public DateTimeOffset ExpiresAt { get; private set; }
        internal string Verifier { get; private set; }
        internal string ClientId { get; private set; }
        internal bool Consumed { get; set; }
        internal LinkingCode(string id, string code, Uri url, string verifier, DateTimeOffset expires)
        { ClientId = id; UserCode = code; VerificationUrl = url; Verifier = verifier; ExpiresAt = expires; }
        public override string ToString() { return "iFood linking code [private]"; }
    }
    public sealed class AccessSession
    {
        internal string AccessToken { get; private set; }
        internal string RefreshToken { get; private set; }
        public DateTimeOffset ExpiresAt { get; private set; }
        internal AccessSession(string access, string refresh, DateTimeOffset expires)
        { AccessToken = access; RefreshToken = refresh; ExpiresAt = expires; }
        public override string ToString() { return "iFood session [private]"; }
    }
    public sealed class DistributedAuthentication : IDisposable
    {
        private const string Origin = "https://merchant-api.ifood.com.br";
        private const string CodeRoute = "/authentication/v1.0/oauth/userCode";
        private const string TokenRoute = "/authentication/v1.0/oauth/token";
        private readonly HttpClient http;
        private readonly Action<AuthenticationEvidence> evidence;
        private readonly Func<DateTimeOffset> now;
        private readonly SemaphoreSlim serial = new SemaphoreSlim(1, 1);
        private readonly string clientId;
        private string clientSecret;
        private AccessSession session;
        public DistributedAuthentication(string id, string secret, Action<AuthenticationEvidence> log,
            HttpMessageHandler isolatedTestHandler = null, Func<DateTimeOffset> clock = null)
        {
            ValidateSecret(id, 1024); ValidateSecret(secret, 8000);
            clientId = id; clientSecret = secret; evidence = log; now = clock ?? (() => DateTimeOffset.UtcNow);
            http = new HttpClient(isolatedTestHandler ?? new HttpClientHandler { AllowAutoRedirect = false });
            http.Timeout = TimeSpan.FromSeconds(25);
        }
        public async Task<LinkingCode> BeginAsync(CancellationToken ct)
        {
            var started = now();
            var dto = await SendAsync<CodeResponse>(HttpMethod.Post, CodeRoute, CodeRoute,
                new Dictionary<string, string> { { "clientId", clientId } }, null, ct).ConfigureAwait(false);
            ValidateSecret(dto.userCode, 128); ValidateSecret(dto.authorizationCodeVerifier, 8000);
            ValidateExpiry(dto.expiresIn); ValidatePortal(dto.verificationUrl);
            if (!string.IsNullOrEmpty(dto.verificationUrlComplete)) ValidatePortal(dto.verificationUrlComplete);
            var url = new Uri("https://portal.ifood.com.br/apps/code?c=" + Uri.EscapeDataString(dto.userCode));
            return new LinkingCode(clientId, dto.userCode, url, dto.authorizationCodeVerifier, started.AddSeconds(dto.expiresIn));
        }
        public async Task CompleteAsync(LinkingCode code, string authorizationCode, CancellationToken ct)
        {
            ValidateSecret(authorizationCode, 8000);
            await serial.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (code == null || code.ClientId != clientId || code.Consumed || now() >= code.ExpiresAt)
                    throw new AuthenticationFailure("Codigo de vinculacao expirado, usado ou de outro aplicativo.");
                code.Consumed = true; session = null;
                session = await ExchangeAsync(new Dictionary<string, string> {
                    { "grantType", "authorization_code" }, { "clientId", clientId }, { "clientSecret", clientSecret },
                    { "authorizationCode", authorizationCode }, { "authorizationCodeVerifier", code.Verifier }
                }, ct).ConfigureAwait(false);
            }
            finally { serial.Release(); }
        }
        public async Task<string> GetAccessTokenAsync(CancellationToken ct)
        {
            await serial.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (session == null) throw new AuthenticationFailure("Autenticacao ainda nao concluida.");
                if (now() < session.ExpiresAt) return session.AccessToken;
                var refresh = session.RefreshToken; session = null;
                session = await ExchangeAsync(new Dictionary<string, string> {
                    { "grantType", "refresh_token" }, { "clientId", clientId }, { "clientSecret", clientSecret },
                    { "refreshToken", refresh }
                }, ct).ConfigureAwait(false);
                return session.AccessToken;
            }
            finally { serial.Release(); }
        }
        public async Task VerifyMerchantAsync(Guid expectedTestMerchant, CancellationToken ct)
        {
            if (expectedTestMerchant == Guid.Empty) throw new AuthenticationFailure("Loja de teste obrigatoria.");
            const string route = "/merchant/v1.0/merchants/{merchantId}";
            var dto = await SendAsync<MerchantResponse>(HttpMethod.Get,
                route.Replace("{merchantId}", expectedTestMerchant.ToString("D")), route, null,
                await GetAccessTokenAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
            Guid returned;
            if (!Guid.TryParse(dto.id, out returned) || returned != expectedTestMerchant)
                throw new AuthenticationFailure("Resposta nao corresponde a loja de teste selecionada.");
        }
        private async Task<AccessSession> ExchangeAsync(Dictionary<string, string> form, CancellationToken ct)
        {
            var started = now();
            var dto = await SendAsync<TokenResponse>(HttpMethod.Post, TokenRoute, TokenRoute, form, null, ct).ConfigureAwait(false);
            ValidateSecret(dto.accessToken, 8000); ValidateSecret(dto.refreshToken, 8000); ValidateExpiry(dto.expiresIn);
            if (!string.Equals(dto.type, "bearer", StringComparison.OrdinalIgnoreCase))
                throw new AuthenticationFailure("Tipo de token inesperado.");
            return new AccessSession(dto.accessToken, dto.refreshToken, started.AddSeconds(dto.expiresIn));
        }
        private async Task<T> SendAsync<T>(HttpMethod method, string path, string route,
            Dictionary<string, string> form, string bearer, CancellationToken ct)
        {
            var timer = Stopwatch.StartNew(); int? status = null; string outcome = "failed";
            try
            {
                using (var request = new HttpRequestMessage(method, Origin + path))
                {
                    if (form != null) request.Content = new FormUrlEncodedContent(form);
                    if (bearer != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                    using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                    {
                        status = (int)response.StatusCode;
                        if (status != 200) throw new AuthenticationFailure("iFood HTTP " + status + " em " + route, status);
                        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct))
                        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var buffer = new MemoryStream())
                        {
                            deadline.CancelAfter(TimeSpan.FromSeconds(25));
                            var chunk = new byte[4096]; int read;
                            while ((read = await input.ReadAsync(chunk, 0, chunk.Length, deadline.Token).ConfigureAwait(false)) > 0)
                            {
                                if (buffer.Length + read > 262144) throw new AuthenticationFailure("Resposta excedeu limite permitido.");
                                buffer.Write(chunk, 0, read);
                            }
                            buffer.Position = 0;
                            var result = (T)new DataContractJsonSerializer(typeof(T)).ReadObject(buffer);
                            if (result == null) throw new AuthenticationFailure("Resposta vazia ou invalida.");
                            outcome = "received"; return result;
                        }
                    }
                }
            }
            catch (AuthenticationFailure) { throw; }
            catch (OperationCanceledException) { throw new AuthenticationFailure("Operacao cancelada ou tempo de rede esgotado."); }
            catch { throw new AuthenticationFailure("Falha de rede, TLS ou formato da resposta."); }
            finally
            {
                try { if (evidence != null) evidence(new AuthenticationEvidence(method.Method, route, status, timer.ElapsedMilliseconds, outcome)); }
                catch { }
                if (form != null) form.Clear();
            }
        }
        private static void ValidateSecret(string value, int maximum)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
                throw new AuthenticationFailure("Campo obrigatorio ausente ou invalido.");
        }
        private static void ValidateExpiry(int seconds)
        { if (seconds <= 0 || seconds > 604800) throw new AuthenticationFailure("Validade de credencial invalida."); }
        private static void ValidatePortal(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != "https" ||
                uri.Host != "portal.ifood.com.br" || uri.Port != 443 || uri.UserInfo.Length != 0 ||
                uri.AbsolutePath != "/apps/code" || uri.Fragment.Length != 0)
                throw new AuthenticationFailure("Endereco de autorizacao inesperado; navegador nao aberto.");
        }
        public void Dispose() { session = null; clientSecret = null; http.Dispose(); serial.Dispose(); }
#pragma warning disable CS0649
        [DataContract] private sealed class CodeResponse
        {
            [DataMember(IsRequired = true)] public string userCode;
            [DataMember(IsRequired = true)] public string authorizationCodeVerifier;
            [DataMember(IsRequired = true)] public string verificationUrl;
            [DataMember] public string verificationUrlComplete;
            [DataMember(IsRequired = true)] public int expiresIn;
        }
        [DataContract] private sealed class TokenResponse
        {
            [DataMember(IsRequired = true)] public string accessToken;
            [DataMember(IsRequired = true)] public string refreshToken;
            [DataMember(IsRequired = true)] public string type;
            [DataMember(IsRequired = true)] public int expiresIn;
        }
        [DataContract] private sealed class MerchantResponse { [DataMember(IsRequired = true)] public string id; }
#pragma warning restore CS0649
    }
}
