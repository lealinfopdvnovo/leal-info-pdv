using System.Runtime.CompilerServices;

namespace LealInfoPDV;

internal static class WebView2DataBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            // V10.174: cada execução do PDV recebe um perfil WebView2 novo e exclusivo.
            // Tutorial e LIA compartilham ESTE MESMO perfil durante a execução, mas o
            // próximo início nunca reaproveita locks/estado Chromium de uma sessão anterior.
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LEAL INFO", "PDV", "WebView2-Sessions");

            Directory.CreateDirectory(root);
            LimparSessoesAntigas(root);

            var folder = Path.Combine(
                root,
                $"session-{Environment.ProcessId}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);

            Environment.SetEnvironmentVariable(
                "WEBVIEW2_USER_DATA_FOLDER",
                folder,
                EnvironmentVariableTarget.Process);

            Environment.SetEnvironmentVariable(
                "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
                "--autoplay-policy=no-user-gesture-required",
                EnvironmentVariableTarget.Process);
        }
        catch
        {
            // Nunca derruba o PDV por falha de preparação do perfil do WebView2.
        }
    }

    private static void LimparSessoesAntigas(string root)
    {
        try
        {
            var limite = DateTime.UtcNow.AddDays(-2);
            foreach (var dir in Directory.EnumerateDirectories(root, "session-*"))
            {
                try
                {
                    if (Directory.GetLastWriteTimeUtc(dir) < limite)
                        Directory.Delete(dir, true);
                }
                catch
                {
                    // Perfil ainda em uso/bloqueado: deixa para uma próxima inicialização.
                }
            }
        }
        catch
        {
        }
    }
}
