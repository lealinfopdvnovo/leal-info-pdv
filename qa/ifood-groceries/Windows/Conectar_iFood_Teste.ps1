# LEAL INFO: diagnostico isolado do aplicativo iFood Teste (D).
# Nao requer administrador, SDK, instalacao ou alteracao do PDV.
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Este assistente requer Windows.' }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$source = @'
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

namespace LealInfo.IFood.Assistant
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Windows.Forms;
    using LealInfo.IFood.Groceries;

    public sealed class AssistantForm : Form
    {
        private readonly Guid merchant = new Guid("84a8976b-0559-48c6-84ce-fe2502f1364a");
        private readonly List<AuthenticationEvidence> logs = new List<AuthenticationEvidence>();
        private readonly TextBox id = new TextBox(), secret = new TextBox(), authorization = new TextBox();
        private readonly Label status = new Label(), link = new Label();
        private readonly Button begin = new Button(), open = new Button(), finish = new Button(), save = new Button();
        private readonly CheckBox testApp = new CheckBox();
        private DistributedAuthentication auth;
        private LinkingCode code;
        private bool busy, authenticated, merchantVerified;
        private string safeError = "Nenhuma tentativa realizada.";
        public AssistantForm()
        {
            Text = "LEAL INFO - Conectar iFood de TESTES";
            ClientSize = new Size(730, 620); MinimumSize = new Size(650, 610);
            StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(24,26,30); ForeColor = Color.White;
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 15, AutoScroll = true };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); Controls.Add(panel);
            Add(panel, new Label { Text="iFood | Aplicativo de testes distribuido (D)", AutoSize=true, Font=new Font(Font,FontStyle.Bold), ForeColor=Color.FromArgb(224,191,113) });
            Add(panel, new Label { Text="Use as credenciais do aplicativo com nome terminado em Teste (D).\nEste assistente verifica o login e a loja de testes. Nao altera o PDV.", AutoSize=true });
            Add(panel, new Label { Text="Client ID do aplicativo Teste (D)", AutoSize=true }); Add(panel,id);
            Add(panel, new Label { Text="Client Secret (fica somente na memoria desta janela)", AutoSize=true }); secret.UseSystemPasswordChar=true; Add(panel,secret);
            testApp.Text="Confirmo que estou usando o aplicativo Teste (D).";testApp.AutoSize=true;Add(panel,testApp);
            begin.Text="1. Gerar codigo de autorizacao";begin.AutoSize=true;Add(panel,begin);
            link.Text="O codigo temporario aparecera aqui.";link.AutoSize=true;Add(panel,link);
            open.Text="2. Abrir autorizacao no navegador";open.AutoSize=true;open.Enabled=false;Add(panel,open);
            Add(panel,new Label { Text="Depois de autorizar no portal, cole o codigo recebido abaixo:",AutoSize=true });
            authorization.UseSystemPasswordChar=true;authorization.Enabled=false;Add(panel,authorization);
            finish.Text="3. Validar conexao com a loja de testes";finish.AutoSize=true;finish.Enabled=false;Add(panel,finish);
            status.Text="Aguardando. Nenhuma chamada foi feita.";status.AutoSize=true;status.MaximumSize=new Size(660,0);Add(panel,status);
            save.Text="Salvar resultado para enviar ao Work";save.AutoSize=true;Add(panel,save);
            begin.Click += async (s,e) => {
                if(busy)return;
                if(!testApp.Checked || string.IsNullOrWhiteSpace(id.Text) || string.IsNullOrWhiteSpace(secret.Text))
                {status.Text="Preencha os campos e confirme o uso do aplicativo de testes.";return;}
                SetBusy(true);begin.Enabled=false;
                try {
                    auth=new DistributedAuthentication(id.Text.Trim(),secret.Text.Trim(),entry=>{lock(logs){logs.Add(entry);}});
                    secret.Clear();id.ReadOnly=true;secret.Enabled=false;testApp.Enabled=false;
                    code=await auth.BeginAsync(CancellationToken.None);
                    link.Text="Codigo: "+code.UserCode+" | Valido ate "+code.ExpiresAt.ToLocalTime().ToString("HH:mm:ss");
                    open.Enabled=true;authorization.Enabled=true;finish.Enabled=true;
                    safeError="Aguardando autorizacao no portal.";status.Text="Codigo gerado. Clique no passo 2 e autorize o aplicativo de testes.";
                } catch(Exception ex){Fail(ex);}
                finally{SetBusy(false);}
            };
            open.Click += (s,e) => {
                if(code==null || busy)return;
                if(DateTimeOffset.UtcNow>=code.ExpiresAt){status.Text="Codigo expirado. Salve o resultado e feche a janela.";return;}
                try{Process.Start(new ProcessStartInfo(code.VerificationUrl.AbsoluteUri){UseShellExecute=true});}
                catch{status.Text="Nao foi possivel abrir o navegador. Acesse https://portal.ifood.com.br/apps/code e use o codigo exibido.";}
            };
            finish.Click += async (s,e) => {
                if(busy || auth==null || code==null)return;
                if(string.IsNullOrWhiteSpace(authorization.Text)){status.Text="Cole o codigo retornado pelo portal no campo acima.";return;}
                SetBusy(true);finish.Enabled=false;open.Enabled=false;
                var value=authorization.Text.Trim();authorization.Clear();authorization.Enabled=false;
                try{
                    await auth.CompleteAsync(code,value,CancellationToken.None);authenticated=true;value=null;
                    status.Text="Autenticacao aceita. Consultando apenas a loja de testes...";
                    await auth.VerifyMerchantAsync(merchant,CancellationToken.None);merchantVerified=true;
                    safeError="Nenhum.";status.Text="AUTENTICADO. Acesso a loja de testes confirmado. Salve o resultado.\nIsso ainda nao valida pedidos, separacao ou homologacao.";
                }catch(Exception ex){Fail(ex);}
                finally{value=null;SetBusy(false);}
            };
            save.Click += (s,e)=> {
                if(busy)return;
                using(var dialog=new SaveFileDialog { Filter="Resultado de teste (*.txt)|*.txt",FileName="RESULTADO_IFOOD_TESTE.txt",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) }){
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                    try{File.WriteAllText(dialog.FileName,Report(),new UTF8Encoding(true));status.Text="Resultado salvo. Envie somente esse arquivo ao Work.";}
                    catch{status.Text="Nao foi possivel salvar. Escolha uma pasta com permissao de gravacao.";}
                }
            };
            FormClosing += (s,e)=>{if(busy){e.Cancel=true;status.Text="Aguarde a operacao terminar para fechar (limite de tempo de rede).";}};
            FormClosed += (s,e)=>{if(auth!=null)auth.Dispose();secret.Clear();authorization.Clear();};
        }
        private static void Add(TableLayoutPanel panel,Control control)
        {control.Dock=DockStyle.Top;control.Margin=new Padding(0,0,0,8);panel.Controls.Add(control);}
        private void SetBusy(bool value){busy=value;save.Enabled=!value;UseWaitCursor=value;}
        private void Fail(Exception ex)
        {
            var known=ex as AuthenticationFailure;
            safeError=known!=null?known.Message:"Falha local inesperada; nenhum detalhe privado foi registrado.";
            status.Text=(authenticated?"Autenticacao aceita; acesso a loja NAO confirmado. ":"BLOQUEADO. ")+safeError+"\nSalve o resultado. Nao ha repeticao automatica.";
        }
        private string Report()
        {
            var text=new StringBuilder();text.AppendLine("LEAL INFO - DIAGNOSTICO IFOOD TESTE (D)");
            text.AppendLine("UTC: "+DateTimeOffset.UtcNow.ToString("O"));
            text.AppendLine("Autenticacao: "+(authenticated?"CONFIRMADA":"NAO CONFIRMADA"));
            text.AppendLine("Acesso a loja de testes: "+(merchantVerified?"CONFIRMADO":"NAO CONFIRMADO"));
            text.AppendLine("Resultado: "+safeError);
            text.AppendLine("Pedidos, separacao, videos e homologacao: NAO VALIDADOS por este assistente.");
            text.AppendLine("HTTP (metadados; sem corpos, cabecalhos, chaves ou tokens):");
            lock(logs){foreach(var row in logs)text.AppendLine(row.AtUtc.ToString("O")+" | "+row.Method+" | "+row.Route+" | HTTP "+(row.Status.HasValue?row.Status.Value.ToString():"sem resposta")+" | "+row.ElapsedMs+" ms | "+row.Outcome);}
            return text.ToString();
        }
        [STAThread] public static void Run(){Application.EnableVisualStyles();Application.Run(new AssistantForm());}
    }
}

'@
try {
    Add-Type -TypeDefinition $source -ReferencedAssemblies @('System.dll','System.Core.dll','System.Net.Http.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.Windows.Forms.dll','System.Drawing.dll') -ErrorAction Stop
    [LealInfo.IFood.Assistant.AssistantForm]::Run()
} catch {
    Write-Host 'Nao foi possivel iniciar o assistente. Nenhuma credencial foi salva.'
    Write-Host 'Informe ao Work: falha local ao iniciar assistente Windows.'
    Read-Host 'Pressione Enter para fechar'
}
