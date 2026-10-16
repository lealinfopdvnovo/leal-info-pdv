using System.Diagnostics;
using System.ServiceProcess;

namespace LealInfoProtection;

public static class ServiceProvisioner
{
    public static void Command(string file, params string[] args)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) info.ArgumentList.Add(a);
        using var p = Process.Start(info)!;
        var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(30000)) { p.Kill(true); throw new IOException("Tempo esgotado: " + file); }
        if (p.ExitCode != 0) throw new IOException(file + ": " + output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
    }
    public static void Install(string serviceExe)
    {
        if (!ServiceController.GetServices().Any(x => x.ServiceName == RecoveryCore.ServiceName))
            Command("sc.exe", "create", RecoveryCore.ServiceName, "binPath=", "\"" + serviceExe + "\" --service", "start=", "auto", "DisplayName=", "LEAL INFO Proteção 001");
        Command("sc.exe", "failure", RecoveryCore.ServiceName, "reset=", "0", "actions=", "restart/5000/restart/5000/restart/5000");
        using var service = new ServiceController(RecoveryCore.ServiceName);
        if (service.Status != ServiceControllerStatus.Running) Command("sc.exe", "start", RecoveryCore.ServiceName);
        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
    }
}
