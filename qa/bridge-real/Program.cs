using LealInfoProtection;
using System.Diagnostics;
using System.Text.Json;

static void Assert(bool ok, string why) { if (!ok) throw new Exception(why); }
if (args.Length == 3 && args[0] == "--crash")
{
    var core = new RecoveryCore(args[1], point => { if (point == args[2]) { Process.GetCurrentProcess().Kill(); Environment.Exit(91); } });
    core.Rollback(core.Read()); throw new Exception("Crash injection missed");
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
}
finally
{
    Directory.CreateDirectory("restore-evidence");
    File.WriteAllText("restore-evidence/bridge.json", JsonSerializer.Serialize(new { results, limits = new[] { "Core uses real Windows files and processes with fixture executables", "Service installation/reboot and actual PDV startup remain unqualified" } }, new JsonSerializerOptions { WriteIndented = true }));
}
