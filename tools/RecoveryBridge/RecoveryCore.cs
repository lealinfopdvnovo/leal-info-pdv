using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace LealInfoProtection;

public sealed record BridgePlan(string Version, string SourceCommit, Dictionary<string, string> Previous, Dictionary<string, string> Target);
public sealed record BridgeState(string App, string Data, string Nonce, BridgePlan Plan, string Phase, DateTimeOffset? ChangedUtc);
public sealed record StartupReceipt(string Nonce, int Pid, long StartedUtcTicks, string Version);

// Shared verbatim by the service and its process-interruption tests.
public sealed class RecoveryCore(string root, Action<string>? checkpoint = null)
{
    public const string ServiceName = "LealInfoProtection001";
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LealInfoProtection001");
    public string StatePath => Path.Combine(root, "state.json");
    private static readonly string[] Files = ["LealInfoPDV.exe", "LealInfoPDV.dll", "LealInfoPDV.deps.json"];
    public static string Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    private static void Flush(string path)
    { using var f = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); f.Flush(true); }
    public BridgeState Read() => JsonSerializer.Deserialize<BridgeState>(File.ReadAllText(StatePath)) ?? throw new InvalidDataException("Estado da proteção inválido.");
    public void Save(BridgeState value)
    {
        Directory.CreateDirectory(root);
        using (var file = new FileStream(StatePath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(file, value); file.Flush(true); }
        File.Move(StatePath + ".tmp", StatePath, true);
    }
    public void Arm(string app, string data, BridgePlan plan)
    {
        if (File.Exists(StatePath)) throw new InvalidOperationException("Já existe uma proteção neste computador. Nenhuma cópia anterior foi alterada.");
        if (!Files.Order().SequenceEqual(plan.Previous.Keys.Order()) || !Files.Order().SequenceEqual(plan.Target.Keys.Order()))
            throw new InvalidDataException("O plano deve conter somente os três executáveis/metadados aprovados.");
        app = Path.GetFullPath(app); data = Path.GetFullPath(data);
        if (plan.Version != "10.404" || plan.Previous.Values.Any(x => x.Length != 64) || plan.Target.Values.Any(x => x.Length != 64))
            throw new InvalidDataException("Plano inválido para a primeira transição do 001.");
        var backups = Path.Combine(root, "previous"); Directory.CreateDirectory(backups);
        foreach (var name in Files)
        {
            var source = Path.Combine(app, name);
            using var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = Convert.ToHexString(SHA256.HashData(locked)).ToLowerInvariant();
            if (hash != plan.Previous[name]) throw new InvalidDataException("O arquivo instalado difere da versão 10.403 publicada: " + name);
            locked.Position = 0;
            var target = Path.Combine(backups, name);
            using (var copy = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None)) { locked.CopyTo(copy); copy.Flush(true); }
            if (Hash(target) != hash) throw new IOException("A cópia de segurança não pôde ser verificada.");
        }
        if (!Matches(app, plan.Previous)) throw new IOException("Os arquivos do PDV mudaram durante a proteção. Nenhuma atualização foi autorizada.");
        Save(new(app, data, Guid.NewGuid().ToString("N"), plan, "Armed", null));
        checkpoint?.Invoke("armed");
    }
    private static bool Matches(string folder, Dictionary<string, string> hashes)
    {
        try { return hashes.All(pair => Hash(Path.Combine(folder, pair.Key)) == pair.Value); }
        catch (IOException) { return false; }
    }
    public bool TargetMatches(BridgeState state) => Matches(state.App, state.Plan.Target);
    public string ReceiptPath(BridgeState state) => Path.Combine(state.Data, "update-ready-" + state.Nonce + ".json");
    public bool RecoverInterruptedRestore()
    {
        var state = Read();
        if (!File.Exists(Path.Combine(state.Data, "restore-journal.json"))) return false;
        var engine = new LealInfoPDV.RestoreEngine(state.Data);
        IDisposable ownership;
        try { ownership = engine.AcquireOwnership(); }
        catch (InvalidOperationException) { return false; } // Operational/maintenance process is still alive.
        using (ownership)
        {
            if (!engine.Recover()) return false;
            File.WriteAllText(Path.Combine(state.Data, "restore-recovered.notice"), DateTimeOffset.UtcNow.ToString("O"));
            return true;
        }
    }
    public bool HasRealStartup(BridgeState state)
    {
        if (!TargetMatches(state)) return false;
        try
        {
            var receipt = JsonSerializer.Deserialize<StartupReceipt>(File.ReadAllText(ReceiptPath(state)));
            if (receipt == null || receipt.Nonce != state.Nonce || receipt.Version != state.Plan.Version) return false;
            using var p = Process.GetProcessById(receipt.Pid);
            return !p.HasExited && p.StartTime.ToUniversalTime().Ticks == receipt.StartedUtcTicks &&
                string.Equals(p.MainModule!.FileName, Path.Combine(state.App, "LealInfoPDV.exe"), StringComparison.OrdinalIgnoreCase) &&
                DateTime.UtcNow - p.StartTime.ToUniversalTime() >= TimeSpan.FromSeconds(10);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or JsonException) { return false; }
    }
    // Each pass is short. Service Control Manager restarts a crashed worker; the
    // durable phase permits idempotent rollback after Windows restarts as well.
    public string Tick(Action<BridgeState> stopWriters, DateTimeOffset? now = null)
    {
        var state = Read(); var clock = now ?? DateTimeOffset.UtcNow;
        if (state.Phase is "Completed" or "Recovered") return state.Phase;
        if (state.Phase == "Armed")
        {
            // A transient sharing violation is not evidence of changed bytes.
            try { if (state.Plan.Previous.All(x => Hash(Path.Combine(state.App, x.Key)) == x.Value)) return "Armed"; }
            catch (FileNotFoundException) { /* A missing executable needs recovery. */ }
            catch (IOException) { return "Armed"; }
            state = state with { Phase = "Applying", ChangedUtc = clock }; Save(state);
            checkpoint?.Invoke("changed");
        }
        if (state.Phase == "Applying" && Matches(state.App, state.Plan.Previous))
        { Save(state with { Phase = "Armed", ChangedUtc = null }); return "Armed"; }
        if (state.Phase == "Applying" && HasRealStartup(state))
        { Save(state with { Phase = "Completed" }); checkpoint?.Invoke("completed"); return "Completed"; }
        if (state.Phase == "Recovering" || clock - state.ChangedUtc >= TimeSpan.FromMinutes(3))
        {
            stopWriters(state);
            Rollback(state);
            return "Recovered";
        }
        return "Applying";
    }
    public void Rollback(BridgeState state)
    {
        if (!Matches(Path.Combine(root, "previous"), state.Plan.Previous))
            throw new InvalidDataException("Backup dos executáveis inválido. Recuperação interrompida sem descartar cópias.");
        Save(state with { Phase = "Recovering" });
        foreach (var name in Files)
        {
            var target = Path.Combine(state.App, name);
            var temp = target + ".recover001";
            File.Copy(Path.Combine(root, "previous", name), temp, true); Flush(temp);
            // Fail closed if another application still has the target open.
            if (File.Exists(target))
            {
                using (new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete))
                    File.Replace(temp, target, null, true);
            }
            else File.Move(temp, target);
            checkpoint?.Invoke("replaced-" + name);
        }
        if (!Matches(state.App, state.Plan.Previous)) throw new IOException("Recuperação incompleta. A proteção tentará novamente.");
        Save(state with { Phase = "Recovered" }); checkpoint?.Invoke("recovered");
    }
}
