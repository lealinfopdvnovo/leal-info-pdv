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
            ClientIdentity.Validate(data, ClientIdentity.LocalSerial());
            if (Process.GetProcessesByName("LealInfoPDV").Any())
                throw new InvalidOperationException("Feche o PDV no servidor e nos terminais antes de ativar a proteção.");
            var plan = EmbeddedPlan.Load();
            var root = RecoveryCore.DefaultRoot;
            Directory.CreateDirectory(root);
            var security = new DirectorySecurity(); security.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(root).SetAccessControl(security);
            var core = new RecoveryCore(root);
            if (!File.Exists(core.StatePath)) core.Arm(app, data, plan);
            else
            {
                var state = core.Read();
                if (state.App != app || state.Plan.SourceCommit != plan.SourceCommit || state.Phase != "Armed")
                    throw new InvalidOperationException("Já existe uma proteção de outra execução. Nenhuma cópia anterior foi alterada.");
            }
            var serviceExe = Path.Combine(root, "LealInfoProtecao001.exe");
            if (!File.Exists(serviceExe)) File.Copy(Environment.ProcessPath!, serviceExe, false);
            else if (RecoveryCore.Hash(serviceExe) != RecoveryCore.Hash(Environment.ProcessPath!)) throw new IOException("O serviço existente possui outra compilação.");
            ServiceProvisioner.Install(serviceExe);
            if (core.Read().Phase != "Armed") throw new IOException("A proteção não confirmou estado ativo.");
            MessageBox.Show("PROTEÇÃO ATIVA — CLIENTE 001\n\nCópia dos três arquivos 10.403 verificada. Serviço de recuperação iniciado e configurado para iniciar com o Windows.\n\nBanco, licença e configurações não foram alterados.\n\nEnvie um print desta confirmação. Aguarde a publicação antes de clicar em ATUALIZAR.", "LEAL INFO — Proteção 001", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show("PROTEÇÃO NÃO CONFIRMADA. NÃO ATUALIZE.\n\n" + ex.Message, "LEAL INFO — Proteção 001", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

}

internal sealed class ProtectionService : ServiceBase
{
    private readonly CancellationTokenSource stopping = new();
    public ProtectionService() { ServiceName = RecoveryCore.ServiceName; CanStop = true; }
    protected override void OnStart(string[] args)
    {
        _ = Task.Run(async () =>
        {
            var core = new RecoveryCore(RecoveryCore.DefaultRoot);
            while (!stopping.IsCancellationRequested)
            {
                try { core.Tick(StopWriters); core.RecoverInterruptedRestore(); }
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
