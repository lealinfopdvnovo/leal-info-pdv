using LealInfoProtection;
using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using LealInfoPDV.Network;
using System.ServiceProcess;

static void Assert(bool ok, string why) { if (!ok) throw new Exception(why); }
if (args.Length == 3 && args[0] == "--crash")
{
    var core = new RecoveryCore(args[1], point => { if (point == args[2]) { Process.GetCurrentProcess().Kill(); Environment.Exit(91); } });
    core.Rollback(core.Read()); throw new Exception("Crash injection missed");
}
if (args.Length == 3 && args[0] == "--ready")
{
    using var self = Process.GetCurrentProcess();
    File.WriteAllText(Path.Combine(args[1], "update-ready-" + args[2] + ".json"), JsonSerializer.Serialize(new StartupReceipt(args[2], Environment.ProcessId, self.StartTime.ToUniversalTime().Ticks, "10.404")));
    Thread.Sleep(20000); return;
}
if (args.Length == 3 && args[0] == "--crash-db")
{
    var engine = new LealInfoPDV.RestoreEngine(args[1], point => { if (point == "after-replace") { Process.GetCurrentProcess().Kill(); Environment.Exit(92); } });
    using var owner = engine.AcquireOwnership(); engine.RestorePrepared(args[2]); throw new Exception("Database crash injection missed");
}
var root = Path.Combine(Path.GetTempPath(), "bridge-real-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
var results = new List<object>();
(string app, RecoveryCore core, BridgePlan plan) Fixture(string name)
{
    var app = Path.Combine(root, name, "app"); Directory.CreateDirectory(app);
    var previous = new Dictionary<string, string>(); var target = new Dictionary<string, string>();
    foreach (var file in new[] { "LealInfoPDV.exe", "LealInfoPDV.dll", "LealInfoPDV.deps.json" })
    {
        File.WriteAllText(Path.Combine(app, file), "OLD-" + file); previous[file] = RecoveryCore.Hash(Path.Combine(app, file));
        var temp = Path.Combine(app, file + ".target"); File.WriteAllText(temp, "NEW-" + file); target[file] = RecoveryCore.Hash(temp);
    }
    File.WriteAllText(Path.Combine(app, "bank.db"), "DATA"); File.WriteAllText(Path.Combine(app, "network.license"), "LICENSE");
    var core = new RecoveryCore(Path.Combine(root, name, "protection"));
    var plan = new BridgePlan("10.404", "test", previous, target); core.Arm(app, Path.Combine(root, name, "data"), plan);
    return (app, core, plan);
}
void CheckOld(string app, BridgePlan plan)
{
    Assert(plan.Previous.All(x => RecoveryCore.Hash(Path.Combine(app, x.Key)) == x.Value), "Executable recovery incomplete");
    Assert(File.ReadAllText(Path.Combine(app, "bank.db")) == "DATA" && File.ReadAllText(Path.Combine(app, "network.license")) == "LICENSE", "Data or license touched");
}
void Run(string name, Action test) { test(); results.Add(new { name, result = "PASS" }); Console.WriteLine("PASS " + name); }
try
{
    Run("signed-client-001-identity-readonly", () =>
    {
        var data = Path.Combine(root, "identity"); Directory.CreateDirectory(data);
        using var rsa = RSA.Create(2048);
        void License(string client)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(new LicenseTerms("TEST-SERIAL", 2, "unico", null, "", 1, "QA", ClientCode: client));
            var envelope = JsonSerializer.Serialize(new SignedLicense(Convert.ToBase64String(payload), Convert.ToBase64String(rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))));
            File.WriteAllBytes(Path.Combine(data, "network.license"), ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(new { SignedLicense = envelope, Devices = Array.Empty<object>(), LastSeenUtc = DateTimeOffset.UtcNow }), null, DataProtectionScope.CurrentUser));
        }
        License("001"); var before = RecoveryCore.Hash(Path.Combine(data, "network.license"));
        ClientIdentity.Validate(data, "TEST-SERIAL", rsa.ExportSubjectPublicKeyInfoPem());
        Assert(before == RecoveryCore.Hash(Path.Combine(data, "network.license")), "License validation changed protected bytes");
        License("003");
        try { ClientIdentity.Validate(data, "TEST-SERIAL", rsa.ExportSubjectPublicKeyInfoPem()); throw new Exception("Other client accepted"); } catch (InvalidOperationException) { }
    });
    Run("armed-before-first-change", () =>
    {
        var (app, core, plan) = Fixture("armed"); Assert(core.Tick(_ => throw new Exception("Should not stop")) == "Armed", "Premature recovery"); CheckOld(app, plan);
    });
    foreach (var copied in new[] { 1, 2, 3 })
    Run("partial-legacy-copy-" + copied, () =>
    {
        var (app, core, plan) = Fixture("copy" + copied);
        foreach (var f in plan.Target.Keys.Take(copied)) File.Copy(Path.Combine(app, f + ".target"), Path.Combine(app, f), true);
        var start = DateTimeOffset.UtcNow; var stopped = false;
        Assert(core.Tick(_ => stopped = true, start) == "Applying", "Change not journaled");
        Assert(core.Tick(_ => stopped = true, start.AddMinutes(4)) == "Recovered", "Timeout did not restore");
        Assert(stopped, "Writers not stopped before recovering"); CheckOld(app, plan);
    });
    foreach (var point in new[] { "replaced-LealInfoPDV.exe", "replaced-LealInfoPDV.dll" })
    Run("hard-interruption-during-recovery-" + point, () =>
    {
        var (app, core, plan) = Fixture(point);
        foreach (var f in plan.Target.Keys) File.Copy(Path.Combine(app, f + ".target"), Path.Combine(app, f), true);
        var psi = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        foreach (var a in new[] { "--crash", Path.GetDirectoryName(core.StatePath)!, point }) psi.ArgumentList.Add(a);
        using var child = Process.Start(psi)!; Assert(child.WaitForExit(30000) && child.ExitCode != 0, "Crash did not execute");
        Assert(core.Read().Phase == "Recovering", "Recovery phase lost");
        Assert(core.Tick(_ => { }) == "Recovered", "Recovery did not resume"); CheckOld(app, plan);
    });
    Run("locked-target-refused-and-retried", () =>
    {
        var (app, core, plan) = Fixture("locked");
        File.WriteAllText(Path.Combine(app, "LealInfoPDV.exe"), "PARTIAL");
        using (var locked = new FileStream(Path.Combine(app, "LealInfoPDV.exe"), FileMode.Open, FileAccess.Read, FileShare.None))
        { try { core.Rollback(core.Read()); throw new Exception("Ignored locked target"); } catch (IOException) { } }
        core.Tick(_ => { }); CheckOld(app, plan);
    });
    Run("corrupt-safety-copy-refused", () =>
    {
        var (app, core, plan) = Fixture("corrupt"); var before = RecoveryCore.Hash(Path.Combine(app, "LealInfoPDV.exe"));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(core.StatePath)!, "previous", "LealInfoPDV.dll"), "CORRUPT");
        try { core.Rollback(core.Read()); throw new Exception("Corrupt safety accepted"); } catch (InvalidDataException) { }
        Assert(before == RecoveryCore.Hash(Path.Combine(app, "LealInfoPDV.exe")), "Rollback mutated without verified backup");
    });
    Run("forged-receipt-without-live-process-refused", () =>
    {
        var (app, core, plan) = Fixture("receipt"); var state = core.Read();
        foreach (var f in plan.Target.Keys) File.Copy(Path.Combine(app, f + ".target"), Path.Combine(app, f), true);
        Directory.CreateDirectory(state.Data); File.WriteAllText(core.ReceiptPath(state), JsonSerializer.Serialize(new StartupReceipt(state.Nonce, int.MaxValue, 1, "10.404")));
        Assert(!core.HasRealStartup(state), "Invalid process accepted as healthy");
    });
    Run("live-startup-receipt-confirmed", () =>
    {
        var app = Path.Combine(root, "healthy", "app"); Directory.CreateDirectory(app);
        foreach (var file in Directory.GetFiles(AppContext.BaseDirectory)) File.Copy(file, Path.Combine(app, Path.GetFileName(file)));
        var previous = new Dictionary<string, string>(); var target = new Dictionary<string, string>();
        foreach (var name in new[] { "LealInfoPDV.exe", "LealInfoPDV.dll", "LealInfoPDV.deps.json" })
        {
            File.WriteAllText(Path.Combine(app, name), "OLD-" + name); previous[name] = RecoveryCore.Hash(Path.Combine(app, name));
        }
        var data = Path.Combine(root, "healthy", "data"); Directory.CreateDirectory(data);
        var core = new RecoveryCore(Path.Combine(root, "healthy", "guard"));
        File.Copy(Environment.ProcessPath!, Path.Combine(app, "candidate.exe"));
        target["LealInfoPDV.exe"] = RecoveryCore.Hash(Path.Combine(app, "candidate.exe"));
        foreach (var name in new[] { "LealInfoPDV.dll", "LealInfoPDV.deps.json" })
        { File.WriteAllText(Path.Combine(app, name + ".target"), "NEW-" + name); target[name] = RecoveryCore.Hash(Path.Combine(app, name + ".target")); }
        core.Arm(app, data, new("10.404", "healthy-fixture", previous, target));
        File.Copy(Path.Combine(app, "candidate.exe"), Path.Combine(app, "LealInfoPDV.exe"), true);
        foreach (var name in new[] { "LealInfoPDV.dll", "LealInfoPDV.deps.json" }) File.Copy(Path.Combine(app, name + ".target"), Path.Combine(app, name), true);
        var state = core.Read();
        var start = new ProcessStartInfo(Path.Combine(app, "LealInfoPDV.exe")) { UseShellExecute = false };
        foreach (var a in new[] { "--ready", data, state.Nonce }) start.ArgumentList.Add(a);
        using var child = Process.Start(start)!;
        try
        {
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromSeconds(17) && core.Tick(_ => throw new Exception("Should not roll back healthy startup")) != "Completed") Thread.Sleep(200);
            Assert(core.Read().Phase == "Completed", "Live healthy child not accepted");
        }
        finally { if (!child.HasExited) { child.Kill(); child.WaitForExit(); } }
    });
    if (args.Length == 2 && args[0] == "--service-binary")
    Run("real-windows-service-recovery-and-restart", () =>
    {
        Assert(Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true", "Service test is restricted to disposable CI runner");
        var (app, fixtureCore, plan) = Fixture("service");
        var serviceRoot = RecoveryCore.DefaultRoot;
        Assert(!Directory.Exists(serviceRoot), "Service test requires clean runner"); Directory.CreateDirectory(serviceRoot);
        var core = new RecoveryCore(serviceRoot); core.Arm(app, Path.Combine(root, "service", "data"), plan);
        var binary = Path.Combine(serviceRoot, "LealInfoProtecao001.exe"); File.Copy(Path.GetFullPath(args[1]), binary);
        try
        {
            ServiceProvisioner.Install(binary);
            using var service = new ServiceController(RecoveryCore.ServiceName);
            Assert(service.Status == ServiceControllerStatus.Running, "SCM did not start protection");
            Console.WriteLine("PASS service-installed-and-running");
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + RecoveryCore.ServiceName))
                Assert(Convert.ToInt32(key!.GetValue("Start")) == 2, "Service not configured for Windows startup");
            // Keep a real matching legacy writer alive; recovery must stop it first.
            var work = Path.Combine(root, "LealInfoPDV_Update_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
            var script = Path.Combine(work, "apply-update.ps1");
            File.WriteAllText(script, "$app='" + app.Replace("'", "''") + "'\r\nStart-Sleep -Seconds 300\r\n");
            using var writer = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -File \"" + script + "\"") { UseShellExecute = false, CreateNoWindow = true })!;
            File.WriteAllText(Path.Combine(app, "LealInfoPDV.exe"), "PARTIAL");
            core.Save(core.Read() with { Phase = "Applying", ChangedUtc = DateTimeOffset.UtcNow.AddMinutes(-4) });
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromSeconds(35) && core.Read().Phase != "Recovered") Thread.Sleep(250);
            Assert(core.Read().Phase == "Recovered", "Real service did not recover"); Assert(writer.WaitForExit(5000), "Legacy writer still running"); CheckOld(app, plan);
            Console.WriteLine("PASS service-stopped-legacy-writer-and-restored-three-files");
            // Abrupt service termination: SCM recovery must start a new worker.
            using var worker = Process.GetProcessesByName("LealInfoProtecao001").Single(x => x.MainModule!.FileName == binary);
            var oldPid = worker.Id; worker.Kill(); worker.WaitForExit();
            timer.Restart();
            var restarted = false;
            while (timer.Elapsed < TimeSpan.FromSeconds(25))
            {
                foreach (var p in Process.GetProcessesByName("LealInfoProtecao001"))
                using (p) if (p.Id != oldPid && p.MainModule!.FileName == binary) restarted = true;
                if (restarted) break; Thread.Sleep(250);
            }
            Assert(restarted, "SCM failed to recover the worker");
            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
            Console.WriteLine("PASS scm-restarted-killed-protection-worker");
            service.Refresh(); service.Stop(); service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
            File.WriteAllText(Path.Combine(app, "LealInfoPDV.dll"), "PARTIAL-AFTER-RESTART");
            core.Save(core.Read() with { Phase = "Applying", ChangedUtc = DateTimeOffset.UtcNow.AddMinutes(-4) });
            service.Start(); service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
            timer.Restart(); while (timer.Elapsed < TimeSpan.FromSeconds(25) && core.Read().Phase != "Recovered") Thread.Sleep(250);
            Assert(core.Read().Phase == "Recovered", "Pending update not recovered after service restart"); CheckOld(app, plan);
            Console.WriteLine("PASS service-recovered-pending-transition-after-restart");
            var data = core.Read().Data; Directory.CreateDirectory(data);
            void Sql(string path, string sql)
            {
                using var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"); c.Open();
                using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
            }
            var live = Path.Combine(data, "lealinfo.db");
            Sql(live, "CREATE TABLE products(id INTEGER PRIMARY KEY,name TEXT); CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT); CREATE TABLE users(id INTEGER PRIMARY KEY); CREATE TABLE sales(id INTEGER PRIMARY KEY); CREATE TABLE sale_items(id INTEGER PRIMARY KEY); INSERT INTO products VALUES(1,'ORIGINAL'); INSERT INTO settings VALUES('client_update_id','001');");
            var source = Path.Combine(data, "source.db"); File.Copy(live, source);
            Sql(source, "UPDATE products SET name='RESTORED'");
            var engine = new LealInfoPDV.RestoreEngine(data); var stage = engine.Prepare(source);
            var crash = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            foreach (var a in new[] { "--crash-db", data, stage }) crash.ArgumentList.Add(a);
            using var dbChild = Process.Start(crash)!; Assert(dbChild.WaitForExit(15000) && dbChild.ExitCode != 0, "Maintenance crash did not occur");
            timer.Restart(); while (timer.Elapsed < TimeSpan.FromSeconds(20) && !File.Exists(Path.Combine(data, "restore-recovered.notice"))) Thread.Sleep(250);
            Assert(File.Exists(Path.Combine(data, "restore-recovered.notice")), "Service did not automatically recover interrupted database");
            using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={live};Mode=ReadOnly;Pooling=False"))
            { c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT name FROM products WHERE id=1"; Assert(Convert.ToString(cmd.ExecuteScalar()) == "ORIGINAL", "Automatic database recovery lost original"); }
            Console.WriteLine("PASS service-automatically-recovered-killed-restore-process");
        }
        finally
        {
            using var service = new ServiceController(RecoveryCore.ServiceName);
            service.Refresh();
            if (service.Status == ServiceControllerStatus.StartPending) service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(20));
            service.Refresh();
            if (service.Status != ServiceControllerStatus.Stopped) { service.Stop(); service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20)); }
            ServiceProvisioner.Command("sc.exe", "delete", RecoveryCore.ServiceName);
        }
    });
}
finally
{
    Directory.CreateDirectory("restore-evidence");
    File.WriteAllText("restore-evidence/bridge.json", JsonSerializer.Serialize(new { results, limits = new[] { "Core uses real Windows files and processes with fixture executables", "Service installation/reboot and actual PDV startup remain unqualified" } }, new JsonSerializerOptions { WriteIndented = true }));
}
