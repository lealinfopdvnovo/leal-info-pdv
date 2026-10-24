using Microsoft.Data.Sqlite;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;

namespace LealInfoPDV.Network;
public sealed class NetworkAccessException : IOException
{
    public NetworkAccessException(string message) : base(message) { }
}
internal sealed class NetworkDatabaseClient : IDisposable
{
    private readonly TcpClient client = new();
    private SslStream? tls;
    internal NetworkDatabaseClient(NetworkConfiguration configuration, string? deviceSerial = null, DeviceKey? deviceKey = null)
    {
        try
        {
            client.NoDelay = true;
            client.ConnectAsync(configuration.Host, configuration.Port).WaitAsync(TimeSpan.FromSeconds(8)).GetAwaiter().GetResult();
            client.ReceiveTimeout = 45000; client.SendTimeout = 45000;
            tls = new SslStream(client.GetStream(), false, (_, certificate, _, _) => certificate != null &&
                string.Equals(certificate.GetCertHashString(HashAlgorithmName.SHA256), configuration.CertificateHash, StringComparison.OrdinalIgnoreCase));
            tls.ReadTimeout = 45000; tls.WriteTimeout = 45000;
            tls.AuthenticateAsClient(new SslClientAuthenticationOptions { TargetHost = configuration.Host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 });
            var challenge = NetworkProtocol.Read<NetworkResponse>(tls).Challenge;
            var identity = deviceKey ?? NetworkIdentity.Key;
            using var rsa = RSA.Create(); rsa.ImportFromPem(identity.PrivateKey);
            var signature = Convert.ToBase64String(rsa.SignData(Convert.FromBase64String(challenge), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
            var authenticated=Send(new() { Operation = "authenticate", Secret = configuration.PairingSecret,
                Serial = deviceSerial ?? Database.DeviceSerial(), Name = Environment.MachineName, PublicKey = identity.PublicKey,
                Signature = signature });
            if(authenticated.SignedLicense.Length>0) Licensing.InstallationLicense.AcceptServer(authenticated.SignedLicense,authenticated.ServerSerial);
            else if(deviceSerial==null)throw new NetworkAccessException("Atualize e ative o PDV servidor antes de conectar este terminal.");
        }
        catch (NetworkAccessException) { Dispose(); throw; }
        catch (Exception ex) { Dispose(); throw new NetworkAccessException("Não foi possível conectar ao servidor. Verifique se o PDV do servidor está aberto e os computadores estão na mesma rede.\n\n" + ex.Message); }
    }
    internal NetworkResponse Send(NetworkRequest request)
    {
        try
        {
            NetworkProtocol.Write(tls!, request);
            var response = NetworkProtocol.Read<NetworkResponse>(tls!);
            if (response.Error.Length > 0)
            {
                if (response.SqliteErrorCode > 0) throw new SqliteException(response.Error, response.SqliteErrorCode);
                throw new NetworkAccessException(response.Error);
            }
            return response;
        }
        catch (Exception ex) when (ex is IOException && ex is not NetworkAccessException || ex is SocketException or TimeoutException)
        {
            Dispose();
            // Nunca repetir automaticamente uma gravação: pode já ter sido confirmada no servidor.
            throw new NetworkAccessException("A conexão com o servidor foi interrompida. Confira se a operação foi registrada antes de tentar novamente.");
        }
    }
    public void Dispose() { tls?.Dispose(); client.Dispose(); }
}
