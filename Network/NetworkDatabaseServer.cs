using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace LealInfoPDV.Network;

public sealed class NetworkDatabaseServer : IDisposable
{
    private readonly TcpListener listener;
    private readonly ConcurrentDictionary<TcpClient, byte> clients = new();
    private readonly CancellationTokenSource stopping = new();
    private readonly X509Certificate2 certificate;
    private readonly NetworkConfiguration configuration;
    public NetworkLicense License { get; }
    public string CertificateHash => certificate.GetCertHashString(HashAlgorithmName.SHA256);
    public static NetworkDatabaseServer? Current { get; private set; }
    internal Action<Exception>? Diagnostic { get; set; }
    public NetworkDatabaseServer(NetworkConfiguration configuration)
    {
        this.configuration = configuration;
        var licencePath = Path.Combine(Database.AppFolder, "network.license");
        if (File.Exists(Path.Combine(Database.AppFolder, "network.certificate")) && !File.Exists(licencePath))
            throw new InvalidOperationException("O cadastro protegido de licença está ausente. Entre em contato com o vendedor para recuperar o servidor.");
        License = new NetworkLicense(licencePath, Database.DeviceSerial());
        certificate = LoadCertificate();
        listener = new TcpListener(IPAddress.Any, configuration.Port);
    }
    private static X509Certificate2 LoadCertificate()
    {
        var path = Path.Combine(Database.AppFolder, "network.certificate");
        if (File.Exists(path)) return new X509Certificate2(Convert.FromBase64String(ProtectedFile.Read<string>(path)), (string?)null, X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
        using var rsa = RSA.Create(3072);
        var request = new CertificateRequest("CN=LEAL INFO PDV Servidor", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        using var issued = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        var bytes = issued.Export(X509ContentType.Pfx);
        ProtectedFile.Write(path, Convert.ToBase64String(bytes));
        return new X509Certificate2(bytes, (string?)null, X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
    }
    public void Start()
    {
        License.CheckAccess();
        listener.Start(32); Current = this;
        _ = Task.Run(AcceptLoop);
    }
    private async Task AcceptLoop()
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(stopping.Token);
                if (clients.Count >= 64) { client.Dispose(); continue; }
                clients.TryAdd(client, 0);
                _ = Task.Run(() => Handle(client));
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) when (stopping.IsCancellationRequested) { break; }
        }
    }
    private void Handle(TcpClient client)
    {
        try
        {
            client.NoDelay = true; client.ReceiveTimeout = 45000; client.SendTimeout = 45000;
            using var tls = new SslStream(client.GetStream(), false);
            tls.ReadTimeout = 45000; tls.WriteTimeout = 45000;
            tls.AuthenticateAsServer(new SslServerAuthenticationOptions { ServerCertificate = certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13, ClientCertificateRequired = false });
            var challenge = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            NetworkProtocol.Write(tls, new NetworkResponse { Challenge = challenge });
            var auth = NetworkProtocol.Read<NetworkRequest>(tls);
            try
            {
                if (auth.Operation != "authenticate" || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(auth.Secret), Encoding.UTF8.GetBytes(configuration.PairingSecret)))
                    throw new InvalidOperationException("A chave de conexão está incorreta. Solicite o arquivo de conexão ao administrador.");
                using var rsa = RSA.Create(); rsa.ImportFromPem(auth.PublicKey);
                if (rsa.KeySize < 2048 || !rsa.VerifyData(Convert.FromBase64String(challenge), Convert.FromBase64String(auth.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                    throw new InvalidOperationException("Não foi possível validar a identidade deste computador.");
                License.Register(auth.Serial, auth.PublicKey, auth.Name);
                NetworkProtocol.Write(tls, new NetworkResponse());
            }
            catch (Exception ex) { NetworkProtocol.Write(tls, new NetworkResponse { Error = ex.Message }); return; }
            using var connection = new SqliteConnection(Database.ConnectionString);
            connection.Open();
            SqliteTransaction? transaction = null;
            try
            {
                while (!stopping.IsCancellationRequested)
                {
                    var request = NetworkProtocol.Read<NetworkRequest>(tls);
                    if (request.Operation == "close") break;
                    NetworkResponse response;
                    try
                    {
                        License.CheckAccess();
                        if (request.Operation == "begin")
                        {
                            if (transaction != null) throw new InvalidOperationException("Transação já está aberta.");
                            transaction = connection.BeginTransaction(); response = new();
                        }
                        else if (request.Operation is "commit" or "rollback")
                        {
                            if (transaction == null) throw new InvalidOperationException("Transação não está aberta.");
                            if (request.Operation == "commit") transaction.Commit(); else transaction.Rollback();
                            transaction.Dispose(); transaction = null; response = new();
                        }
                        else
                        {
                            if (request.Operation is not ("nonquery" or "scalar" or "reader")) throw new InvalidOperationException("Operação de rede inválida.");
                            // Clientes acessam somente o banco desta loja, nunca outros arquivos.
                            if (Regex.IsMatch(request.Sql, @"\b(ATTACH|DETACH|VACUUM|load_extension|readfile|writefile)\b", RegexOptions.IgnoreCase))
                                throw new InvalidOperationException("Esta operação deve ser realizada no servidor.");
                            using var command = connection.CreateCommand(); command.Transaction = transaction;
                            command.CommandText = request.Sql; command.CommandTimeout = 15;
                            foreach (var p in request.Parameters) command.Parameters.AddWithValue(p.Key, p.Value.ToObject());
                            if (request.Operation == "nonquery") response = new() { Affected = command.ExecuteNonQuery() };
                            else if (request.Operation == "scalar") response = new() { Scalar = WireValue.From(command.ExecuteScalar()) };
                            else { using var reader = command.ExecuteReader(); response = NetworkProtocol.ReadRows(reader); }
                        }
                    }
                    catch (SqliteException ex) { response = new() { Error = ex.Message, SqliteErrorCode = ex.SqliteErrorCode }; }
                    catch (Exception ex) { response = new() { Error = ex.Message }; }
                    NetworkProtocol.Write(tls, response);
                }
            }
            finally { transaction?.Dispose(); }
        }
        catch (Exception ex) { Diagnostic?.Invoke(ex); }
        finally { clients.TryRemove(client, out _); client.Dispose(); }
    }
    public void Dispose()
    {
        stopping.Cancel(); listener.Stop();
        foreach (var client in clients.Keys) client.Dispose();
        if (Current == this) Current = null;
        // As sessões podem ainda estar finalizando a autenticação; o certificado
        // permanece válido até o encerramento do processo.
    }
}
