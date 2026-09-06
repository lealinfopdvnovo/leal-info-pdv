using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Drawing;

namespace LealInfoPDV;

/// <summary>
/// LIA holográfica: sem moldura, sem barra de título e com fundo transparente.
/// </summary>
public sealed class LiaForm : Form
{
    private readonly WebView2 web = new();

    public LiaForm()
    {
        // Janela invisível como "caixa": só a personagem deve aparecer.
        Text = string.Empty;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(530, 794);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        MaximizeBox = false;
        MinimizeBox = false;

        // Cor-chave do host WinForms. O WebView2 usa fundo transparente,
        // então o que sobra visualmente é apenas o vídeo com alpha.
        var transparentKey = Color.FromArgb(1, 1, 1);
        BackColor = transparentKey;
        TransparencyKey = transparentKey;

        web.Dock = DockStyle.Fill;
        web.DefaultBackgroundColor = Color.Transparent;
        Controls.Add(web);

        Shown += async (_, _) => await StartLiaAsync();
    }

    private async Task StartLiaAsync()
    {
        var liaFolder = Path.Combine(AppContext.BaseDirectory, "Assets");
        var videoPath = Path.Combine(liaFolder, "leal_ai_feminino_holograma.webm");

        // Reserva embutida: se o instalador não copiar o WEBM, extrai para LocalAppData.
        if (!File.Exists(videoPath))
        {
            var localLiaFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LEAL INFO PDV", "LIA");
            Directory.CreateDirectory(localLiaFolder);

            var localVideoPath = Path.Combine(localLiaFolder, "leal_ai_feminino_holograma.webm");
            if (!File.Exists(localVideoPath))
            {
                using var input = typeof(LiaForm).Assembly.GetManifestResourceStream(
                    "LealInfoPDV.Assets.leal_ai_feminino_holograma.webm");
                if (input is null)
                {
                    MessageBox.Show(
                        "A LIA não foi incluída na publicação.",
                        "LIA • LEAL AI");
                    Close();
                    return;
                }

                using var output = File.Create(localVideoPath);
                await input.CopyToAsync(output);
            }

            liaFolder = localLiaFolder;
            videoPath = localVideoPath;
        }

        try
        {
            var webViewUserDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LEAL INFO PDV", "WebView2", "LIA");
            Directory.CreateDirectory(webViewUserDataFolder);

            // Libera autoplay COM ÁUDIO. Sem isso o WebView2 pode tocar o vídeo mudo.
            var options = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments: "--autoplay-policy=no-user-gesture-required");

            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: webViewUserDataFolder,
                options: options);

            await web.EnsureCoreWebView2Async(environment);

            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            web.CoreWebView2.Settings.IsZoomControlEnabled = false;

            web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "lia.local",
                liaFolder,
                CoreWebView2HostResourceAccessKind.Allow);

            // Clique na própria LIA fecha o holograma.
            web.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                if (e.TryGetWebMessageAsString() == "close")
                    BeginInvoke(Close);
            };

            const string html = """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta name="color-scheme" content="dark light">
<style>
html,body{
  margin:0;
  width:100%;
  height:100%;
  overflow:hidden;
  background:rgba(0,0,0,0) !important;
}
body{
  display:flex;
  align-items:center;
  justify-content:center;
}
video{
  width:100%;
  height:100%;
  object-fit:contain;
  background:rgba(0,0,0,0) !important;
  filter:drop-shadow(0 0 7px rgba(0,210,255,.55)) drop-shadow(0 0 18px rgba(0,125,255,.30));
  cursor:pointer;
}
</style>
</head>
<body>
<video id="lia" autoplay playsinline preload="auto">
  <source src="https://lia.local/leal_ai_feminino_holograma.webm" type="video/webm">
</video>
<script>
const v = document.getElementById('lia');
let iniciou = false;
v.muted = false;
v.volume = 1.0;

function tocarUmaVez(){
  if (iniciou) return;
  iniciou = true;
  v.muted = false;
  v.volume = 1.0;
  const p = v.play();
  if (p && p.catch) {
    p.catch(() => {
      iniciou = false;
      setTimeout(tocarUmaVez, 250);
    });
  }
}

document.addEventListener('DOMContentLoaded', tocarUmaVez, { once:true });
v.addEventListener('canplay', tocarUmaVez, { once:true });

// Terminou o aceno: fecha o holograma. Sem seek, sem replay, sem falso loop.
v.addEventListener('ended', () => {
  v.pause();
  setTimeout(() => window.chrome.webview.postMessage('close'), 250);
}, { once:true });

// Clique na LIA também fecha.
v.addEventListener('click', () => window.chrome.webview.postMessage('close'));

// ESC fecha mesmo se o clique não pegar.
document.addEventListener('keydown', (e) => {
  if (e.key === 'Escape') window.chrome.webview.postMessage('close');
});
</script>
</body>
</html>
""";

            web.NavigateToString(html);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível iniciar a LIA.\n\n" + ex.Message, "LIA • LEAL AI");
            Close();
        }
    }
}
