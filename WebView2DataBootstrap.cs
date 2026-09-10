using System.Runtime.CompilerServices;

namespace LealInfoPDV;

internal static class WebView2DataBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            // V10.172: usa um perfil compartilhado NOVO e exclusivo para esta geração.
            // Isso evita reaproveitar estado/locks de perfis anteriores que podem deixar
            // o WebView2 em estado inválido (HRESULT 0x8007139F) após atualização/reinício.
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LEAL INFO", "PDV", "WebView2-V10.172");

            Directory.CreateDirectory(folder);

            // LIA e Tutorial continuam obrigatoriamente no mesmo perfil e com os mesmos
            // argumentos de processo. Não cria ambientes incompatíveis em paralelo.
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
}
