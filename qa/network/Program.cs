using LealInfoPDV;
using LealInfoPDV.Network;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Reject(Action action, string text)
{
    try { action(); } catch(Exception ex) { Assert(ex.Message.Contains(text, StringComparison.OrdinalIgnoreCase), "Mensagem incorreta: " + ex.Message); return; }
    throw new Exception("Era necessário rejeitar: " + text);
}
static DeviceKey Identity() { using var rsa = RSA.Create(2048); return new(rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportSubjectPublicKeyInfoPem()); }
static string Signed(RSA seller, LicenseTerms terms)
{
    var payload = JsonSerializer.SerializeToUtf8Bytes(terms);
    return JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(payload), Convert.ToBase64String(seller.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))));
}
var folder = Path.Combine(Path.GetTempPath(), "leal-network-qa-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
try
{
    using var seller = RSA.Create(2048);
    var path = Path.Combine(folder, "license"); var publicKey = seller.ExportSubjectPublicKeyInfoPem();
    var licence = new NetworkLicense(path, "SERVIDOR", publicKey);
    Assert(licence.Terms.ComputerLimit == 2 && licence.RegisteredCount == 1, "Servidor deve contar como 1 de 2.");
    licence.Register("A", "KEY-A", "Terminal A");
    licence.Register("A", "KEY-A", "Terminal A");
    Assert(licence.RegisteredCount == 2, "Reconectar não pode consumir outro ponto.");
    Reject(() => licence.Register("B", "KEY-B", "Terminal B"), "somente 2 computadores");
    Reject(() => licence.Register("A", "KEY-OUTRA", "Terminal copiado"), "identidade mudou");
    licence = new NetworkLicense(path, "SERVIDOR", publicKey);
    Reject(() => licence.Register("B", "KEY-B", "Terminal B"), "somente 2 computadores");
    Assert(NetworkLicense.LimitNotice(2).Contains("Entre em contato com o vendedor"), "Aviso deve orientar contato.");
    Reject(() => licence.Import(Signed(seller, new("OUTRO", 3, "unico", null, "VENDEDOR", 1, "x"))), "não é válida");
    var unsigned = Signed(seller, new("SERVIDOR", 3, "unico", null, "VENDEDOR", 1, "x"));
    var bad = JsonSerializer.Deserialize<SignedLicense>(unsigned)!;
    Reject(() => licence.Import(JsonSerializer.Serialize(bad with { Payload = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new LicenseTerms("SERVIDOR", 99, "unico", null, "", 1, "x"))) })), "assinatura");
    licence.Import(unsigned); licence.Register("B", "KEY-B", "Terminal B");
    Assert(licence.Terms.BillingMode == "unico" && licence.RegisteredCount == 3, "Pagamento único deve liberar terceiro ponto.");
    Reject(() => licence.Import(unsigned), "mais recente");
    Reject(() => licence.Import(Signed(seller, new("SERVIDOR", 3, "mensal", null, "", 2, "m"))), "não é válida");
    Reject(() => licence.Import(Signed(seller, new("SERVIDOR", 3, "mensal", DateTimeOffset.UtcNow.AddDays(-1), "", 2, "m"))), "vencida");
    licence.Import(Signed(seller, new("SERVIDOR", 3, "mensal", DateTimeOffset.UtcNow.AddDays(30), "VENDEDOR", 2, "m")));
    licence.CheckAccess(); Assert(licence.Terms.BillingMode == "mensal", "Mensalidade não foi aplicada.");
    // O relógio persistido impede reativar licença mensal voltando o relógio.
    var state = ProtectedFile.Read<LicenseState>(path); state.LastSeenUtc = DateTimeOffset.UtcNow.AddDays(31); ProtectedFile.Write(path, state);
    var expired = new NetworkLicense(path, "SERVIDOR", publicKey); Reject(expired.CheckAccess, "mensal está vencida");
    expired.Import(Signed(seller, new("SERVIDOR", 3, "mensal", DateTimeOffset.UtcNow.AddDays(60), "VENDEDOR", 3, "renovada"))); expired.CheckAccess();
    // Duas inscrições simultâneas disputam a única vaga restante.
    var race = new NetworkLicense(Path.Combine(folder, "race"), "S", publicKey); var accepted = 0;
    Parallel.For(0, 8, i => { try { race.Register("T"+i, "K"+i, "T"+i); Interlocked.Increment(ref accepted); } catch(InvalidOperationException) { } });
    Assert(accepted == 1 && race.RegisteredCount == 2, "Inscrições simultâneas ultrapassaram limite.");
    var wire = new NetworkResponse { Columns = new() { "ID", "Nome", "Valor", "Vazio" }, Types = new() { "integer", "text", "real", "text" },
        Rows = new() { new[] { WireValue.From(5L), WireValue.From("Café"), WireValue.From(25.50), WireValue.From(DBNull.Value) } } };
    using (var reader = new NetworkDataReader(wire)) { Assert(reader.Read() && reader.GetInt32(0) == 5 && reader.GetDouble(2) == 25.50 && reader.IsDBNull(3), "Tipos do leitor incorretos."); }
    using (var reader = new NetworkDataReader(wire)) { var table = new DataTable(); table.Load(reader); Assert(table.Rows.Count == 1 && (long)table.Rows[0][0] == 5L, "Grid não carregou resultado remoto."); }
    // Integração TLS + SQLite real, incluindo transações e persistência do cadastro.
    NetworkConfiguration.Load(); Database.Initialize();
    using var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
    var config = NetworkConfiguration.NewServer(port);
    using var server = new NetworkDatabaseServer(config); server.Start();
    var terminal = new NetworkConfiguration { Mode = "terminal", Host = "127.0.0.1", Port = port, PairingSecret = config.PairingSecret, CertificateHash = server.CertificateHash };
    var firstIdentity = Identity();
    using var first = new NetworkDatabaseClient(terminal, "QA-TERMINAL-1", firstIdentity);
    Reject(() => { using var denied = new NetworkDatabaseClient(terminal, "QA-TERMINAL-2", Identity()); }, "somente 2 computadores");
    using (var reconnect = new NetworkDatabaseClient(terminal, "QA-TERMINAL-1", firstIdentity))
        Assert(reconnect.Send(new() { Operation="scalar", Sql="SELECT 1" }).Scalar!.ToObject().Equals(1L), "Reconexão falhou.");
    first.Send(new() { Operation="nonquery", Sql="CREATE TABLE IF NOT EXISTS qa_network(id INTEGER PRIMARY KEY,value TEXT); DELETE FROM qa_network;" });
    first.Send(new() { Operation="begin" });
    first.Send(new() { Operation="nonquery", Sql="INSERT INTO qa_network(value) VALUES($v)", Parameters=new() { ["$v"]=WireValue.From("Venda rede") } });
    var saleId = first.Send(new() { Operation="scalar", Sql="SELECT last_insert_rowid()" }).Scalar!.ToObject(); Assert(Convert.ToInt64(saleId) == 1, "Identificador não permaneceu na sessão.");
    first.Send(new() { Operation="rollback" });
    Assert(Convert.ToInt64(first.Send(new() { Operation="scalar", Sql="SELECT COUNT(*) FROM qa_network" }).Scalar!.ToObject()) == 0, "Rollback falhou.");
    first.Send(new() { Operation="begin" }); first.Send(new() { Operation="nonquery", Sql="INSERT INTO qa_network(value) VALUES('Confirmada')" }); first.Send(new() { Operation="commit" });
    using (var local = Database.Open()) { using var cmd = local.CreateCommand(); cmd.CommandText="SELECT COUNT(*) FROM qa_network"; Assert(Convert.ToInt64(cmd.ExecuteScalar())==1,"Gravação não chegou ao banco do servidor."); }
    first.Send(new() { Operation="begin" }); first.Send(new() { Operation="nonquery", Sql="INSERT INTO qa_network(value) VALUES('Interrompida')" }); first.Dispose();
    // Nova sessão aguarda SQLite liberar rollback da sessão desconectada.
    using (var again = new NetworkDatabaseClient(terminal, "QA-TERMINAL-1", firstIdentity))
        Assert(Convert.ToInt64(again.Send(new() { Operation="scalar", Sql="SELECT COUNT(*) FROM qa_network" }).Scalar!.ToObject()) == 1, "Desconexão não desfez transação pendente.");
    Reject(() => { var wrong = new NetworkConfiguration { Host="127.0.0.1",Port=port,PairingSecret=config.PairingSecret,CertificateHash=new string('0',64) }; using var client = new NetworkDatabaseClient(wrong,"QA-TERMINAL-1",firstIdentity); }, "Não foi possível conectar");
    Reject(() => { using var clone = new NetworkDatabaseClient(terminal, "QA-TERMINAL-1", Identity()); }, "identidade mudou");
    Console.WriteLine("PASS: limites, concorrência, persistência, assinatura, modalidades, renovação, relógio, tipos, TLS, banco central, commit, rollback e desconexão.");
}
finally { Directory.Delete(folder, true); }
