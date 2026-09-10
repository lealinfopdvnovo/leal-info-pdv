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

            // Todos os WebView2 do PDV usam o MESMO ambiente do processo.
            // Isso evita o HRESULT 0x8007139F ao abrir LIA + Tutorial com ambientes diferentes.
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", folder, EnvironmentVariableTarget.Process);
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
}
