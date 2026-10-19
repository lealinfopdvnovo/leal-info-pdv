using LealInfoPDV;
using LealInfoPDV.Network;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static class Program
{
    static readonly List<string> results = new();
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); results.Add(text); Console.WriteLine("PASS " + text); }
    static void Reject(Action action, string text) { try { action(); } catch (Exception e) when (e is InvalidOperationException or InvalidDataException or IOException or Microsoft.Data.Sqlite.SqliteException) { Check(true, text); return; } throw new Exception("Expected rejection: " + text); }
    static object? Sql(string sql, params (string, object)[] values) { using var db = Database.Open(); using var q = db.CreateCommand(); q.CommandText = sql; foreach (var (k,v) in values) q.Parameters.AddWithValue(k,v); return q.ExecuteScalar(); }
    static KitchenDraft Draft(string reference) => new(Guid.NewGuid(), reference, "SEM MOVIMENTAÇÃO FINANCEIRA — DADOS FICTÍCIOS", "OPERADOR QA", DateTimeOffset.UtcNow,
        new[] { new KitchenItem("LANCHE QA", 2m, "Sem cebola", 1), new KitchenItem("BEBIDA QA", 1.5m, "Sem gelo") });
    static string Snapshot()
    {
        using var db = Database.Open(); var names = new List<string>();
        using (var q = db.CreateCommand()) { q.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'kitchen_%' ORDER BY name"; using var r = q.ExecuteReader(); while (r.Read()) names.Add(r.GetString(0)); }
        var all = new List<object>();
        foreach (string name in names)
        {
            using var q = db.CreateCommand(); q.CommandText = "SELECT * FROM \"" + name.Replace("\"", "\"\"") + "\" ORDER BY rowid";
            using var r = q.ExecuteReader(); var rows = new List<string[]>();
            while (r.Read()) rows.Add(Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "<NULL>" : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture)!).ToArray());
            all.Add(new { name, rows });
        }
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(all)));
    }
    static string Marker => Path.Combine(Database.AppFolder, "kitchen-qa-isolated");
    static void Child(string mode, params string[] arguments)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(mode); info.ArgumentList.Add(File.ReadAllText(Marker)); foreach (var arg in arguments) info.ArgumentList.Add(arg);
        using var child = Process.Start(info)!;
        Check(child.WaitForExit(30000), "isolated child completes within timeout");
        string stdout = child.StandardOutput.ReadToEnd(), stderr = child.StandardError.ReadToEnd();
        int expected = mode == "--interrupt" ? 71 : mode == "--commit-exit" ? 72 : 0;
        Check(child.ExitCode == expected, "actual new process: " + mode + " " + stdout + stderr);
    }
    static void RunChild(string[] args)
    {
        Check(File.Exists(Marker) && File.ReadAllText(Marker) == args[1], "isolated fixture identity verified");
        var store = new KitchenStore(); string id = store.EnsureReady();
        var pending = new KitchenOutbox(Path.Combine(Database.AppFolder, "qa-outbox"));
        if (args[0] == "--commit-exit") { var entry = pending.Pending().Single(x => x.Draft.Id.ToString() == args[2]); store.Submit(id, entry.Draft); Environment.Exit(72); }
        if (args[0] == "--interrupt")
        {
            var draft = Draft("INTERRUPTED") with { Id = Guid.Parse(args[2]) }; using var db = Database.Open(); using var tx = db.BeginTransaction(); using var q = db.CreateCommand(); q.Transaction = tx;
            q.CommandText = "INSERT INTO kitchen_orders_v1(id,reference,notes,operator,status,revision,created_utc,received_utc,updated_utc,payload,payload_hash) VALUES($id,'INTERRUPTED','','QA','AGUARDANDO',1,'2026-01-01','2026-01-01','2026-01-01',$p,$h)";
            q.Parameters.AddWithValue("$id", draft.Id.ToString()); q.Parameters.AddWithValue("$p", draft.Json); q.Parameters.AddWithValue("$h", draft.Hash); q.ExecuteNonQuery(); Environment.Exit(71);
        }
        Check(store.Get(Guid.Parse(args[2])).Reference == "PERSISTENT QA", "order, items and observations restored in a genuine new Windows process");
        Check(pending.Flush(store) >= 1 && pending.Pending().Count == 0, "durable pending operations recovered after actual restart");
    }
    static void PumpUntil(Func<bool> predicate, int milliseconds = 15000)
    {
        var watch = Stopwatch.StartNew(); while (!predicate() && watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(20); }
        Check(predicate(), "actual nonmodal kitchen UI completes asynchronous operation");
    }
    static void UserInterface(KitchenStore store)
    {
        Application.EnableVisualStyles();
        typeof(LealInfoPDV.Licensing.InstallationLicense).GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null,
            new LicenseTerms("ISOLATED-QA", 2, "unico", null, "", 1, "QA", "standard", Environment.GetEnvironmentVariable("KITCHEN_CHANNEL") ?? "001"));
        using var main = new MainForm(); main.Show();
        ((System.Windows.Forms.Timer)typeof(MainForm).GetField("licenseTimer", Private)!.GetValue(main)!).Stop();
        typeof(MainForm).GetField("automaticBackupCompleted", Private)!.SetValue(main, true);
        typeof(MainForm).GetMethod("OpenKitchen", Private)!.Invoke(main, null);
        var form = Application.OpenForms.OfType<KitchenForm>().Single();
        Check(!form.Modal && form.Owner == main && main.Enabled, "real MainForm kitchen opens without blocking sales or desktop");
        var send = (Button)typeof(KitchenForm).GetField("send", Private)!.GetValue(form)!;
        var grid = (DataGridView)typeof(KitchenForm).GetField("items", Private)!.GetValue(form)!;
        var label = (Label)typeof(KitchenForm).GetField("message", Private)!.GetValue(form)!;
        PumpUntil(() => label.Text.Contains("Cozinha conectada"));
        int before = store.List().Count;
        grid.Rows.Add("ITEM UI QA", "1,500", "Observação QA"); send.PerformClick();
        PumpUntil(() => store.List().Count == before + 1 && label.Text.Contains("Cozinha conectada"));
        send.PerformClick(); Application.DoEvents(); Check(store.List().Count == before + 1 && !send.Enabled, "real send button cannot submit the same displayed draft twice");
        using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save("kitchen-stage1-evidence/kitchen.png"); }
        form.Close(); typeof(MainForm).GetMethod("OpenKitchen", Private)!.Invoke(main, null);
        var reopened = Application.OpenForms.OfType<KitchenForm>().Single(); var state = (Label)typeof(KitchenForm).GetField("message", Private)!.GetValue(reopened)!;
        PumpUntil(() => state.Text.Contains("Cozinha conectada")); Check(store.List().Count == before + 1, "closing and reopening actual kitchen preserves submitted data");
        reopened.Close(); main.Close();
    }
    [STAThread] static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true") throw new InvalidOperationException("Only disposable GitHub Windows runners; never run against a store database.");
        if (args.Length > 0) { RunChild(args); return 0; }
        Directory.CreateDirectory("kitchen-stage1-evidence");
        try
        {
            Check(!File.Exists(Database.DbPath), "empty disposable Windows profile; no production database used");
            Database.Initialize(); File.WriteAllText(Marker, Guid.NewGuid().ToString());
            Sql("INSERT INTO products(id,name,price,stock,active) VALUES(1,'PRODUTO QA',12.50,10,1); INSERT INTO customers(name) VALUES('CLIENTE FICTÍCIO QA'); INSERT INTO sales(id,sold_at,payment,subtotal,total,operator) VALUES(1,'2026-01-01','Dinheiro',12.50,12.50,'QA'); INSERT INTO sale_items(sale_id,product_id,description,qty,unit_price,total) VALUES(1,1,'PRODUTO QA',1,12.50,12.50); INSERT INTO sale_payments(sale_id,method,amount) VALUES(1,'Dinheiro',12.50); INSERT INTO cash_movements(occurred_at,type,description,amount,sale_id) VALUES('2026-01-01','ENTRADA','VENDA FICTÍCIA QA',12.50,1); INSERT OR REPLACE INTO settings(key,value) VALUES('company_registered','1'),('security_setup_completed','1'),('first_access_tutorial_completed','1'); SELECT 1");
            Auth.CreateUser("OPERADOR QA", "kitchenqa", "QA-isolated-123!", "ADMINISTRADOR", "qa@example.invalid", ""); Auth.Login("kitchenqa", "QA-isolated-123!");
            string before = Snapshot(); var store = new KitchenStore(); string identity = store.EnsureReady();
            Check(File.Exists(store.LastBackup) && new FileInfo(store.LastBackup!).Length > 0, "online SQLite backup exists before additive kitchen migration");
            using (var backup = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + store.LastBackup + ";Pooling=False")) { backup.Open(); using var q = backup.CreateCommand(); q.CommandText = "PRAGMA quick_check"; Check(Convert.ToString(q.ExecuteScalar()) == "ok", "actual backup integrity verified"); q.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'kitchen_%'"; Check(Convert.ToInt32(q.ExecuteScalar()) == 0, "backup contains pre-migration schema"); q.CommandText = "SELECT stock FROM products WHERE id=1"; Check(Convert.ToDecimal(q.ExecuteScalar()) == 10m, "online backup includes committed WAL business fixture rows"); }
            Check(store.EnsureReady() == identity && Snapshot() == before, "idempotent migration preserves all original business tables and rows");
            var order = Draft("PERSISTENT QA"); store.Submit(identity, order); store.Submit(identity, order);
            Check(store.List().Count == 1 && store.Get(order.Id).Items[1].Quantity == 1.5m && store.History(order.Id).Count == 1, "atomic header/items/events, decimal quantity and idempotent send");
            Reject(() => store.Submit(identity, order with { Notes = "different payload" }), "same UUID with different payload rejected");
            Reject(() => store.Submit(Guid.NewGuid().ToString(), Draft("WRONG STORE")), "wrong database identity rejected before writing");
            foreach (decimal value in new[] { 0m, -1m, 0.0001m }) Reject(() => store.Submit(identity, Draft("INVALID") with { Items = new[] { new KitchenItem("QA", value, "") } }), "invalid quantity rejected: " + value);
            Check(KitchenDraft.Quantity("1,500") == KitchenDraft.Quantity("1.500"), "comma and point represent the same exact decimal quantity");
            var race = Draft("CONCURRENT QA"); Parallel.For(0, 8, _ => new KitchenStore().Submit(identity, race));
            Check(store.List().Count(x => x.Id == race.Id) == 1 && store.History(race.Id).Count == 1, "concurrent sends persist one order and one initial event");
            var pending = new KitchenOutbox(Path.Combine(Database.AppFolder, "qa-outbox")); var queued = Draft("RESTART PENDING"); pending.Queue(identity, queued); pending.Queue(identity, queued);
            Child("--restart", order.Id.ToString()); Check(store.Get(queued.Id).Status == "AGUARDANDO", "pending order reaches central database after restart");
            var lostAck = Draft("COMMITTED BEFORE PROCESS EXIT"); pending.Queue(identity, lostAck); Child("--commit-exit", lostAck.Id.ToString());
            Check(new KitchenOutbox(Path.Combine(Database.AppFolder, "qa-outbox")).Flush(store) == 1 && store.History(lostAck.Id).Count == 1, "actual process exit after commit is recovered without duplicate after retry");
            Guid interrupted = Guid.NewGuid(); Child("--interrupt", interrupted.ToString()); Reject(() => store.Get(interrupted), "actual process exit before commit leaves no partial order");
            Sql("CREATE TRIGGER qa_kitchen_abort BEFORE INSERT ON kitchen_items_v1 WHEN NEW.description='FAIL_SECOND_ITEM' BEGIN SELECT RAISE(ABORT,'QA_INTERRUPTED'); END; SELECT 1");
            var atomic = Draft("ATOMIC FAILURE") with { Items = new[] { new KitchenItem("FIRST", 1, ""), new KitchenItem("FAIL_SECOND_ITEM", 1, "") } };
            Reject(() => store.Submit(identity, atomic), "real Submit fails during second item insertion"); Reject(() => store.Get(atomic.Id), "failed multi-item transaction leaves no header");
            Check(Convert.ToInt64(Sql("SELECT COUNT(*) FROM kitchen_items_v1 WHERE order_id=$id", ("$id", atomic.Id.ToString()))) == 0, "failed transaction leaves no partial items"); Sql("DROP TRIGGER qa_kitchen_abort; SELECT 1");
            var op = Guid.NewGuid(); var changed = store.ChangeStatus(order.Id, 1, "PREPARANDO", "COZINHEIRO QA", op); store.ChangeStatus(order.Id, 1, "PREPARANDO", "COZINHEIRO QA", op);
            Check(store.History(order.Id).Count == 2 && changed.Revision == 2, "status retry is idempotent and audited");
            Reject(() => store.ChangeStatus(order.Id, 1, "CANCELADO", "QA", Guid.NewGuid()), "stale terminal cannot overwrite another operator silently");
            Reject(() => store.ChangeStatus(order.Id, 2, "ENTREGUE", "QA", Guid.NewGuid()), "invalid status jump rejected");
            store.ChangeStatus(order.Id, 2, "PRONTO", "QA", Guid.NewGuid()); store.ChangeStatus(order.Id, 3, "ENTREGUE", "QA", Guid.NewGuid());
            Reject(() => store.ChangeStatus(order.Id, 4, "PREPARANDO", "QA", Guid.NewGuid()), "completed order cannot restart or change financial data");
            var corruptFolder = Path.Combine(Database.AppFolder, "qa-corrupt-outbox"); var corrupt = new KitchenOutbox(corruptFolder); File.WriteAllText(Path.Combine(corruptFolder, "damaged.pending"), "broken");
            try { corrupt.Flush(store); throw new Exception("Corrupt file accepted"); } catch (CryptographicException) { Check(File.Exists(Path.Combine(corruptFolder, "damaged.pending")), "damaged encrypted queue retained; never silently discarded"); }
            var foreign = new KitchenOutbox(Path.Combine(Database.AppFolder, "qa-foreign-outbox")); foreign.Queue(Guid.NewGuid().ToString(), Draft("FOREIGN")); Reject(() => foreign.Flush(store), "pending records from a different database are never forwarded");
            Network(store, identity);
            UserInterface(store);
            Check(Snapshot() == before, "all original tables unchanged: payments, cash, stock, customers, permissions and settings");
            Check(Convert.ToString(Sql("PRAGMA quick_check")) == "ok", "final real SQLite integrity, WAL and crash recovery validated");
            File.WriteAllText("kitchen-stage1-evidence/results.json", JsonSerializer.Serialize(new { success = true, channel = Environment.GetEnvironmentVariable("KITCHEN_CHANNEL"), checks = results, sourceVersion = UpdateManager.CurrentVersion, noPublication = true }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PASS KITCHEN STAGE 1 REAL WINDOWS SQLITE TLS RESTART REGRESSION"); return 0;
        }
        catch (Exception e) { File.WriteAllText("kitchen-stage1-evidence/failure.txt", e.ToString()); Console.Error.WriteLine(e); return 1; }
    }
    static void Network(KitchenStore local, string identity)
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        var config = NetworkConfiguration.NewServer(port); using var key = RSA.Create(2048); var device = new DeviceKey(key.ExportPkcs8PrivateKeyPem(), key.ExportSubjectPublicKeyInfoPem());
        var server = new NetworkDatabaseServer(config); server.Start();
        var terminal = new NetworkConfiguration { Mode = "terminal", Host = "127.0.0.1", Port = port, PairingSecret = config.PairingSecret, CertificateHash = server.CertificateHash };
        var remote = new KitchenStore(() => new PdvConnection(new NetworkDatabaseClient(terminal, "KITCHEN-QA-TERMINAL", device)), false);
        try
        {
            Check(remote.EnsureReady() == identity, "real terminal uses existing authenticated TLS channel and central database");
            var terms = server.License.Terms; var draft = Draft("NETWORK QA"); remote.Submit(identity, draft);
            Check(local.Get(draft.Id).Items.Length == 2 && remote.Get(draft.Id).Status == "AGUARDANDO", "actual terminal-to-server order and full item retrieval");
            var race = Draft("NETWORK STATUS RACE"); remote.Submit(identity, race); int successes = 0;
            Parallel.ForEach(new[] { "PREPARANDO", "CANCELADO" }, state => { try { remote.ChangeStatus(race.Id, 1, state, "TERMINAL QA", Guid.NewGuid()); Interlocked.Increment(ref successes); } catch (InvalidOperationException) { } });
            Check(successes == 1 && local.History(race.Id).Count == 2, "actual concurrent TLS sessions cannot overwrite status or duplicate events");
            var queue = new KitchenOutbox(Path.Combine(Database.AppFolder, "qa-network-outbox")); var offline = Draft("OFFLINE NETWORK QA"); queue.Queue(identity, offline);
            server.Dispose(); Reject(() => queue.Flush(remote), "network unavailable keeps durable pending request"); Check(queue.Pending().Count == 1, "offline request survives failed real TCP connection");
            server = new NetworkDatabaseServer(config); server.Start();
            Check(queue.Flush(remote) == 1 && local.Get(offline.Id).Status == "AGUARDANDO", "server restart reconnects and safely sends queued order through TLS");
            var acknowledged = Draft("NETWORK ACK DISCARDED QA"); queue.Queue(identity, acknowledged); remote.Submit(identity, acknowledged);
            Check(queue.Flush(remote) == 1 && local.History(acknowledged.Id).Count == 1, "lost confirmation retried through actual TLS produces exactly one order");
            Check(server.License.Terms == terms && server.License.RegisteredCount == 2, "existing license terms and two-computer limit preserved across reconnects");
        }
        finally { server.Dispose(); }
    }
}
