using System.Diagnostics;
using System.Text.Json;

namespace LealInfoPDV;

internal static class UpdateProtectionReceipt
{
    // A receipt is emitted only by the ordinary startup, after real license access,
    // database initialization and network startup have all succeeded.
    internal static void ReportReady()
    {
        var statePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LealInfoProtection001", "state.json");
        if (!File.Exists(statePath)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(statePath));
        var state = doc.RootElement;
        if (state.GetProperty("Phase").GetString() is not ("Armed" or "Applying")) return;
        if (Licensing.InstallationLicense.Current?.ClientCode != "001")
            throw new InvalidOperationException("A proteção desta atualização é exclusiva do cliente 001.");
        if (!string.Equals(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar), state.GetProperty("App").GetString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A instalação não corresponde à proteção ativa.");
        if (state.GetProperty("Plan").GetProperty("Version").GetString() != UpdateManager.CurrentVersion)
            throw new InvalidOperationException("A versão não corresponde à proteção ativa.");
        RestoreEngine.Validate(Database.DbPath);
        using var current = Process.GetCurrentProcess();
        var nonce = state.GetProperty("Nonce").GetString()!;
        var path = Path.Combine(Database.AppFolder, "update-ready-" + nonce + ".json");
        using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, new { Nonce = nonce, Pid = Environment.ProcessId, StartedUtcTicks = current.StartTime.ToUniversalTime().Ticks, Version = UpdateManager.CurrentVersion });
            stream.Flush(true);
        }
        File.Move(path + ".tmp", path, true);
    }
}
