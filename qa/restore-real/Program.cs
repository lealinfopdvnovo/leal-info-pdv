using LealInfoPDV;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
static void Reject(Action action)
{
    try { action(); } catch (Exception ex) when (ex is IOException or InvalidOperationException or SqliteException) { return; }
    throw new Exception("Operation should have been refused");
}
static void Sql(string path, string sql)
{
    using var c = new SqliteConnection($"Data Source={path};Pooling=False"); c.Open();
    using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
}
static string Value(string path, string sql)
{
    using var c = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False"); c.Open();
    using var cmd = c.CreateCommand(); cmd.CommandText = sql; return Convert.ToString(cmd.ExecuteScalar())!;
}
static string Marker(string folder) => Value(Path.Combine(folder, "lealinfo.db"), "SELECT name FROM products WHERE id=1");
static string Hash(string p) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
if (args.Length == 4 && args[0] == "--crash")
{
    var engine = new RestoreEngine(args[1], stage => { if (stage == args[3]) { Process.GetCurrentProcess().Kill(); Environment.Exit(91); } });
    using var owner = engine.AcquireOwnership();
    engine.RestorePrepared(args[2]);
    throw new Exception("Crash point did not execute");
}

var evidence = new List<object>();
var root = Path.Combine(Path.GetTempPath(), "pdv-real-restore-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
// Initialize the real production schema in this disposable Windows runner profile.
Assert(!File.Exists(Database.DbPath), "Runner must have no existing PDV data");
Database.Initialize();
var seed = Path.Combine(root, "seed.db");
using (var source = new SqliteConnection(Database.ConnectionString))
using (var target = new SqliteConnection($"Data Source={seed}"))
{ source.Open(); target.Open(); source.BackupDatabase(target); }
SqliteConnection.ClearAllPools();
string Fixture(string name)
{
    var p = Path.Combine(root, name); Directory.CreateDirectory(p);
    File.Copy(seed, Path.Combine(p, "lealinfo.db"));
    Sql(Path.Combine(p, "lealinfo.db"), "PRAGMA journal_mode=DELETE; INSERT INTO products(id,name,price) VALUES(1,'ORIGINAL',10); INSERT OR REPLACE INTO settings VALUES('client_update_id','001'); INSERT OR REPLACE INTO settings VALUES('smtp_host','CURRENT');");
    File.Copy(seed, Path.Combine(p, "source.db"));
    Sql(Path.Combine(p, "source.db"), "PRAGMA journal_mode=DELETE; INSERT INTO products(id,name,price) VALUES(1,'RESTORED',20); INSERT OR REPLACE INTO settings VALUES('client_update_id','OTHER'); INSERT OR REPLACE INTO settings VALUES('smtp_host','OLD');");
    File.WriteAllText(Path.Combine(p, "network.license"), "LICENSE-SENTINEL");
    File.WriteAllText(Path.Combine(p, "network.config"), "CONFIG-SENTINEL");
    return p;
}
void Run(string name, Action test)
{
    var timer = Stopwatch.StartNew(); test();
    evidence.Add(new { name, result = "PASS", elapsedMs = timer.ElapsedMilliseconds });
    Console.WriteLine("PASS " + name);
}
try
{
    Run("valid-restore-and-real-schema", () =>
    {
        var p = Fixture("valid"); var engine = new RestoreEngine(p); using var owner = engine.AcquireOwnership();
        var safety = engine.RestorePrepared(engine.Prepare(Path.Combine(p, "source.db")));
        Assert(Marker(p) == "RESTORED", "Selected data did not restore"); RestoreEngine.Validate(Path.Combine(p, "lealinfo.db"));
        Assert(Value(safety, "SELECT name FROM products WHERE id=1") == "ORIGINAL", "Safety copy lost");
        Sql(Path.Combine(p, "lealinfo.db"), "INSERT INTO sales(id,sold_at,payment,subtotal,total) VALUES(1,'2026-10-16','PIX',20,20); INSERT INTO sale_items(sale_id,description,qty,unit_price,total) VALUES(1,'QA',1,20,20);");
        RestoreEngine.Validate(Path.Combine(p, "lealinfo.db"));
    });
    Run("invalid-backup", () =>
    {
        var p = Fixture("invalid"); var engine = new RestoreEngine(p); var hash = Hash(Path.Combine(p, "lealinfo.db"));
        File.WriteAllText(Path.Combine(p, "invalid.db"), "not sqlite"); Reject(() => engine.Prepare(Path.Combine(p, "invalid.db")));
        Assert(hash == Hash(Path.Combine(p, "lealinfo.db")), "Invalid input changed live data");
    });
    foreach (var point in new[] { "before-replace", "after-replace", "validated" })
        Run("hard-interruption-" + point, () =>
        {
            var p = Fixture("crash-" + point); var engine = new RestoreEngine(p); var stage = engine.Prepare(Path.Combine(p, "source.db"));
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            foreach (var a in new[] { "--crash", p, stage, point }) start.ArgumentList.Add(a);
            using var child = Process.Start(start)!;
            Assert(child.WaitForExit(30000), "Fault child timed out"); Assert(child.ExitCode != 0, "Expected hard failure");
            using var owner = engine.AcquireOwnership(); Assert(engine.Recover(), "Durable recovery did not run");
            Assert(Marker(p) == "ORIGINAL", "Previous data not recovered"); Assert(!engine.Recover(), "Recovery not idempotent");
        });
    Run("failed-postcheck-automatic-rollback", () =>
    {
        var p = Fixture("postcheck"); var engine = new RestoreEngine(p, stage => { if (stage == "after-replace") throw new IOException("Injected disk failure"); });
        using var owner = engine.AcquireOwnership(); Reject(() => engine.RestorePrepared(engine.Prepare(Path.Combine(p, "source.db"))));
        Assert(Marker(p) == "ORIGINAL", "Rollback did not run");
    });
    Run("wal-with-committed-data", () =>
    {
        var p = Fixture("wal"); var live = Path.Combine(p, "lealinfo.db");
        using (var c = new SqliteConnection($"Data Source={live};Pooling=False"))
        {
            c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; UPDATE products SET name='LATEST-WAL' WHERE id=1;"; cmd.ExecuteNonQuery();
            Assert(File.Exists(live + "-wal"), "WAL test not actually using WAL");
        }
        var engine = new RestoreEngine(p); using var owner = engine.AcquireOwnership();
        var safety = engine.RestorePrepared(engine.Prepare(Path.Combine(p, "source.db")));
        Assert(Value(safety, "SELECT name FROM products WHERE id=1") == "LATEST-WAL", "Committed WAL lost");
        Assert(Marker(p) == "RESTORED", "Restore failed in WAL mode");
    });
    Run("open-sqlite-reader-refused", () =>
    {
        var p = Fixture("reader"); var engine = new RestoreEngine(p); var stage = engine.Prepare(Path.Combine(p, "source.db")); using var owner = engine.AcquireOwnership();
        using var c = new SqliteConnection($"Data Source={Path.Combine(p, "lealinfo.db")};Pooling=False;Default Timeout=1"); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT * FROM products"; using var reader = cmd.ExecuteReader(); Assert(reader.Read(), "No reader");
        Reject(() => engine.RestorePrepared(stage)); Assert(Marker(p) == "ORIGINAL", "Active connection was not respected");
    });
    Run("second-server-process-refused", () =>
    {
        var p = Fixture("ownership"); var engine = new RestoreEngine(p); using var owner = engine.AcquireOwnership();
        Reject(() => new RestoreEngine(p).AcquireOwnership());
    });
    Run("insufficient-space-before-mutation", () =>
    {
        var p = Fixture("space"); var hash = Hash(Path.Combine(p, "lealinfo.db")); var engine = new RestoreEngine(p, freeBytes: () => 0);
        Reject(() => engine.Prepare(Path.Combine(p, "source.db"))); Assert(hash == Hash(Path.Combine(p, "lealinfo.db")), "No-space changed database");
    });
    Run("cancel-before-replacement", () =>
    {
        var p = Fixture("cancel"); var engine = new RestoreEngine(p); var staged = engine.Prepare(Path.Combine(p, "source.db"));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        try { engine.RestorePrepared(staged, cancel.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { }
        Assert(Marker(p) == "ORIGINAL", "Cancellation changed database");
    });
    Run("settings-and-external-files-preserved", () =>
    {
        var p = Fixture("settings"); var engine = new RestoreEngine(p); using var owner = engine.AcquireOwnership();
        var license = Hash(Path.Combine(p, "network.license")); var config = Hash(Path.Combine(p, "network.config"));
        engine.RestorePrepared(engine.Prepare(Path.Combine(p, "source.db")));
        Assert(Value(Path.Combine(p, "lealinfo.db"), "SELECT value FROM settings WHERE key='client_update_id'") == "001", "Client identity replaced");
        Assert(Value(Path.Combine(p, "lealinfo.db"), "SELECT value FROM settings WHERE key='smtp_host'") == "CURRENT", "SMTP settings replaced");
        Assert(license == Hash(Path.Combine(p, "network.license")) && config == Hash(Path.Combine(p, "network.config")), "External protected files changed");
    });
    Run("real-process-handoff-and-automatic-restart", WindowsFlow.AutomaticRestart);
    Run("real-pdv-ui-after-restoration", WindowsFlow.MainFormAfterRestore);
}
finally
{
    Directory.CreateDirectory("restore-evidence");
    File.WriteAllText("restore-evidence/results.json", JsonSerializer.Serialize(new { implementation = "ProjectReference to production LealInfoPDV", scenarios = evidence, limits = new[] { "No customer data used", "No complete UI/activation or restart qualification yet", "No-space uses injected OS capacity result", "Hard process failure is not a physical power loss" } }, new JsonSerializerOptions { WriteIndented = true }));
    SqliteConnection.ClearAllPools();
}
