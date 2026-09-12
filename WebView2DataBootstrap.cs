using Microsoft.Web.WebView2.Core;
using System.Runtime.CompilerServices;

namespace LealInfoPDV;

internal static class WebView2DataBootstrap
{
    private static CoreWebView2Environment? sharedEnvironment;
    private static Task<CoreWebView2Environment>? sharedEnvironmentTask;
    private static string? sharedFolder;

    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            // V10.181: um único perfil físico para toda a LIA nesta execução.
            // Alinha LiaForm e LiaVoiceController no mesmo UserDataFolder e evita 0x8007139F.
            sharedFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LEAL INFO PDV", "WebView2", "LIA_VOZ_V3");

            Directory.CreateDirectory(sharedFolder);

            Environment.SetEnvironmentVariable(
                "WEBVIEW2_USER_DATA_FOLDER",
                sharedFolder,
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

    internal static Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        if (sharedEnvironment is not null)
            return Task.FromResult(sharedEnvironment);

        return sharedEnvironmentTask ??= CreateEnvironmentAsync();
    }

    private static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        sharedFolder ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LEAL INFO PDV", "WebView2", "LIA_VOZ_V3");

        Directory.CreateDirectory(sharedFolder);

        var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
        var env = await CoreWebView2Environment.CreateAsync(null, sharedFolder, options);
        sharedEnvironment = env;
        return env;
    }
}
