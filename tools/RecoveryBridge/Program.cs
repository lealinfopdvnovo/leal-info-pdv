using LealInfoProtection;
using System.Diagnostics;
using System.Management;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text.Json;

internal static class Entry
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--service") { ServiceBase.Run(new ProtectionService()); return; }
        ApplicationConfiguration.Initialize();
        try
        {
            var app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LEAL INFO PDV");
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LealInfoPDV");
            if (!Directory.Exists(app) || !File.Exists(Path.Combine(data, "lealinfo.db")))
                throw new InvalidOperationException("Instalação esperada não encontrada. Nenhum arquivo do PDV foi alterado.");
            if (Process.GetProcessesByName("LealInfoPDV").Any())
                throw new InvalidOperationException("Feche o PDV no servidor e nos terminais antes de ativar a proteção.");
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LealInfoProtecao001.plan.json")
                ?? throw new InvalidDataException("Plano de atualização ausente. Esta compilação não pode proteger o cliente.");
            var plan = JsonSerializer.Deserialize<BridgePlan>(stream) ?? throw new InvalidDataException("Plano inválido.");
            var root = RecoveryCore.DefaultRoot;
            Directory.CreateDirectory(root);
            var security = new DirectorySecurity(); security.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(root).SetAccessControl(security);
            var core = new RecoveryCore(root); core.Arm(app, data, plan);
            var serviceExe = Path.Combine(root, "LealInfoProtecao001.exe");
            File.Copy(Environment.ProcessPath!, serviceExe, false);
            Command("sc.exe", ["create", RecoveryCore.ServiceName, "binPath=", "\"" + serviceExe + "\" --service", "start=", "auto", "DisplayName=", "LEAL INFO Proteção 001"]);
            Command("sc.exe", ["failure", RecoveryCore.ServiceName, "reset=", "0", "actions=", "restart/5000/restart/5000/restart/5000"]);
            Command("sc.exe", ["start", RecoveryCore.ServiceName]);
            using var service = new ServiceController(RecoveryCore.ServiceName);
            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            if (core.Read().Phase != "Armed") throw new IOException("A proteção não confirmou estado ativo.");
            MessageBox.Show("PROTEÇÃO ATIVA — CLIENTE 001\n\nCópia dos três arquivos 10.403 verificada. Serviço de recuperação iniciado e configurado para iniciar com o Windows.\n\nBanco, licença e configurações não foram alterados.\n\nEnvie um print desta confirmação. Aguarde a publicação antes de clicar em ATUALIZAR.", "LEAL INFO — Proteção 001", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show("PROTEÇÃO NÃO CONFIRMADA. NÃO ATUALIZE.\n\n" + ex.Message, "LEAL INFO — Proteção 001", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private static void Command(string file, string[] args)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) info.ArgumentList.Add(a);
        using var p = Process.Start(info)!;
        var output = p.StandardOutput.ReadToEnd(); var error = p.StandardError.ReadToEnd();
        if (!p.WaitForExit(30000) || p.ExitCode != 0) throw new IOException(file + ": " + output + error);
    }
}

internal sealed class ProtectionService : ServiceBase
{
    private readonly CancellationTokenSource stopping = new();
    public ProtectionService() { ServiceName = RecoveryCore.ServiceName; }
    protected override void OnStart(string[] args)
    {
        _ = Task.Run(async () =>
        {
            var core = new RecoveryCore(RecoveryCore.DefaultRoot);
            while (!stopping.IsCancellationRequested)
            {
                try { core.Tick(StopWriters); }
                catch (Exception ex)
                {
                    File.AppendAllText(Path.Combine(RecoveryCore.DefaultRoot, "recovery-errors.log"), DateTimeOffset.UtcNow + " " + ex + Environment.NewLine);
                }
                try { await Task.Delay(1000, stopping.Token); } catch (OperationCanceledException) { break; }
            }
        });
    }
    protected override void OnStop() { stopping.Cancel(); }
    private static void StopWriters(BridgeState state)
    {
        // Only stop the legacy updater whose generated script explicitly targets
        // this installation; never terminate unrelated PowerShell sessions.
        using var search = new ManagementObjectSearcher("SELECT ProcessId,CommandLine FROM Win32_Process WHERE Name='powershell.exe'");
        foreach (ManagementObject row in search.Get())
        {
            var line = Convert.ToString(row["CommandLine"]) ?? "";
            var match = System.Text.RegularExpressions.Regex.Match(line, "-File\\s+\"([^\"]+apply-update\\.ps1)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success || !File.Exists(match.Groups[1].Value)) continue;
            var script = File.ReadAllText(match.Groups[1].Value);
            if (!script.Contains("$app='" + state.App.Replace("'", "''") + "'", StringComparison.Ordinal)) continue;
            using var updater = Process.GetProcessById(Convert.ToInt32(row["ProcessId"]));
            updater.Kill(true); if (!updater.WaitForExit(15000)) throw new IOException("Aplicador não encerrou.");
        }
        foreach (var name in new[] { "LealInfoPDV", "LicAi" })
        foreach (var p in Process.GetProcessesByName(name))
        using (p)
        {
            if (!p.MainModule!.FileName.StartsWith(state.App + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            p.Kill(true); if (!p.WaitForExit(15000)) throw new IOException("PDV não encerrou para recuperação.");
        }
    }
}
