using Microsoft.Web.WebView2.Core;
using System.Runtime.CompilerServices;

namespace LealInfoPDV;

internal static class WebView2DataBootstrap
{
    private static CoreWebView2Environment? sharedEnvironment;
    private static Task<CoreWebView2Environment>? sharedEnvironmentTask;
    private static string? sessionFolder;

    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LEAL INFO", "PDV", "WebView2-Sessions");

            Directory.CreateDirectory(root);
            LimparSessoesAntigas(root);

            sessionFolder = Path.Combine(root, $"session-{Environment.ProcessId}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(sessionFolder);

            Environment.SetEnvironmentVariable(
                "WEBVIEW2_USER_DATA_FOLDER",
                sessionFolder,
                EnvironmentVariableTarget.Process);

            Environment.SetEnvironmentVariable(
                "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
                "--autoplay-policy=no-user-gesture-required",
                EnvironmentVariableTarget.Process);
        }
        catch
        {
            // Nunca derruba o PDV por falha de preparacao do perfil do WebView2.
        }
    }

    // Um unico CoreWebView2Environment por processo. Isso evita o 0x8007139F
    // quando Tutorial e LIA inicializam WebView2 na mesma execucao do PDV.
    internal static Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        if (sharedEnvironment is not null)
            return Task.FromResult(sharedEnvironment);

        return sharedEnvironmentTask ??= CreateEnvironmentAsync();
    }

    private static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
        var env = await CoreWebView2Environment.CreateAsync(null, sessionFolder, options);
        sharedEnvironment = env;
        return env;
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
                    // Perfil ainda em uso/bloqueado: deixa para uma proxima inicializacao.
                }
            }
        }
        catch
        {
        }
    }
}
