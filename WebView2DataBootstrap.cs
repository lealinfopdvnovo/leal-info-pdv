using System.Runtime.CompilerServices;

namespace LealInfoPDV;

internal static class WebView2DataBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LEAL INFO", "PDV", "WebView2");

            Directory.CreateDirectory(folder);
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", folder, EnvironmentVariableTarget.Process);
        }
        catch
        {
            // O WebView2 ainda poderá usar seu fallback; nunca derruba o PDV na inicialização.
        }
    }
}
