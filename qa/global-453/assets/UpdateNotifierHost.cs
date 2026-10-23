using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace LealInfoPDV;

internal static class UpdateNotifierHost
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal sealed record Installation(string Agent, string Config, string StartupKey, string Folder);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string windowName);
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    private static void NoLink(string path)
    {
        if ((Directory.Exists(path) || File.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Pasta do notificador redirecionada.");
    }
    internal static Installation Prepare(string application, string identity, string? qaRoot = null)
    {
        if (!ClientUpdateIdentity.IsValidClientCode(identity)) throw new InvalidDataException("Identidade do notificador inválida.");
        application = Path.GetFullPath(application);
        if (!Path.GetFileName(application).Equals("LealInfoPDV.exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(application) || application.IndexOfAny(new[] { '\r', '\n', '"' }) >= 0)
            throw new InvalidDataException("Aplicativo do PDV inválido.");
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LealInfoPDV", "Notificador");
        if (qaRoot is not null)
        {
            var runner = Environment.GetEnvironmentVariable("RUNNER_TEMP");
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || string.IsNullOrEmpty(runner) ||
                !Path.GetFullPath(qaRoot).StartsWith(Path.GetFullPath(runner).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Pasta isolada recusada.");
            root = Path.GetFullPath(qaRoot);
        }
        var key = "LealInfoPDV.Notificador." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(application.ToUpperInvariant())))[..16];
        var folder = Path.Combine(root, key);
        NoLink(root); Directory.CreateDirectory(root); NoLink(folder); Directory.CreateDirectory(folder);
        using var resource = typeof(UpdateNotifierHost).Assembly.GetManifestResourceStream("LealInfoPDV.UpdateNotifier.Native") ?? throw new InvalidDataException("Notificador não incluído nesta compilação.");
        using var stream = new MemoryStream(); resource.CopyTo(stream); byte[] bytes = stream.ToArray();
        if (bytes.Length < 1000 || bytes.Length > 2 * 1024 * 1024 || bytes[0] != 'M' || bytes[1] != 'Z') throw new InvalidDataException("Componente do notificador inválido.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)); var binaryFolder = Path.Combine(folder, hash);
        NoLink(binaryFolder); Directory.CreateDirectory(binaryFolder);
        string agent = Path.Combine(binaryFolder, "LealInfoPDV.Notificador.exe"); NoLink(agent);
        if (!File.Exists(agent))
        {
            string temp = agent + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); } File.Move(temp, agent); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        if (!SHA256.HashData(File.ReadAllBytes(agent)).SequenceEqual(SHA256.HashData(bytes))) throw new InvalidDataException("Integridade do notificador inválida.");
        string config = Path.Combine(folder, "agent.ini"); NoLink(config);
        string content = $"[Notifier]\r\nIdentity={identity}\r\nRoute={UpdateManager.NotifierRoute}\r\nApplication={application}\r\nAgent={agent}\r\nStartupKey={key}\r\n";
        string pending = config + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(pending, content, Encoding.Unicode); File.Move(pending, config, true); }
        finally { if (File.Exists(pending)) File.Delete(pending); }
        return new Installation(agent, config, key, folder);
    }
    internal static void RegisterStartup(Installation installation, string? qaRegistry = null)
    {
        string path = RunPath;
        if (qaRegistry is not null)
        {
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || !qaRegistry.StartsWith(@"Software\LealInfoPDV\QA\", StringComparison.Ordinal)) throw new InvalidOperationException("Registro isolado recusado.");
            path = qaRegistry;
        }
        string preferences = Path.Combine(installation.Folder, "preferences.ini"); NoLink(preferences);
        bool enabled = !File.Exists(preferences) || !File.ReadAllLines(preferences).Any(line => line.Trim().Equals("StartupEnabled=0", StringComparison.OrdinalIgnoreCase));
        using var key = Registry.CurrentUser.CreateSubKey(path);
        if (enabled) key.SetValue(installation.StartupKey, $"\"{installation.Agent}\" --config \"{installation.Config}\"", RegistryValueKind.String);
        else key.DeleteValue(installation.StartupKey, false);
    }
    internal static async Task EnsureStartedAsync()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" && Environment.GetEnvironmentVariable("LEAL_NOTIFIER_ISOLATED_TEST") == "1") return;
        try
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "LealInfoPDV.exe");
            var install = Prepare(exe, Licensing.InstallationLicense.Current?.ClientCode ?? "");
            RegisterStartup(install);
            var existing = FindWindow("LealInfoPDV.UpdateNotifier.v1", install.StartupKey);
            if (existing != IntPtr.Zero)
            {
                GetWindowThreadProcessId(existing, out uint pid);
                try
                {
                    using var previous = Process.GetProcessById((int)pid);
                    if (string.Equals(previous.MainModule?.FileName, install.Agent, StringComparison.OrdinalIgnoreCase)) return;
                    PostMessage(existing, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
                    await previous.WaitForExitAsync(timeout.Token);
                }
                catch (ArgumentException) { /* Older process already exited. */ }
            }
            Process.Start(new ProcessStartInfo(install.Agent) { Arguments = $"--config \"{install.Config}\"", UseShellExecute = false, WorkingDirectory = install.Folder });
        }
        catch (Exception ex) { Trace.WriteLine("Notificador: " + ex.GetType().Name); } // Never block login, licensing, or sales.
    }
}
