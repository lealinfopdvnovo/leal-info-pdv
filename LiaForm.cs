using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Drawing;

namespace LealInfoPDV;

/// <summary>
/// Módulo isolado da LIA. Não reutiliza nenhum código/asset legado da antiga integração.
/// </summary>
public sealed class LiaForm : Form
{
    private readonly WebView2 web = new();

    public LiaForm()
    {
        Text = "LIA • LEAL AI";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(530, 794);
        MinimumSize = new Size(360, 540);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        var transparentKey = Color.FromArgb(1, 1, 1);
        BackColor = transparentKey;
        TransparencyKey = transparentKey;
        ShowInTaskbar = false;
        TopMost = true;

        web.Dock = DockStyle.Fill;
        // NÃO usar web.BackColor = Color.Transparent: o controle WinForms não suporta
        // BackColor transparente e lança exceção antes mesmo de o WebView2 iniciar.
        // A transparência do vídeo fica sob responsabilidade do WebView2/HTML.
        web.DefaultBackgroundColor = Color.Transparent;
        Controls.Add(web);

        Shown += async (_, _) => await StartLiaAsync();
    }

    private async Task StartLiaAsync()
    {
        var liaFolder = Path.Combine(AppContext.BaseDirectory, "Assets");
        var videoPath = Path.Combine(liaFolder, "LIA_OFICIAL_TRANSPARENTE.webm");

        // ETAPA 3: dupla garantia. O arquivo deve ir no publish/Setup e também
        // fica embutido no executável como reserva. Se o instalador não copiar
        // o WEBM por qualquer motivo, extraímos a cópia embutida em LocalAppData.
        if (!File.Exists(videoPath))
        {
            var localLiaFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LEAL INFO PDV", "LIA");
            Directory.CreateDirectory(localLiaFolder);

            var localVideoPath = Path.Combine(localLiaFolder, "LIA_OFICIAL_TRANSPARENTE.webm");
            if (!File.Exists(localVideoPath))
            {
                using var input = typeof(LiaForm).Assembly.GetManifestResourceStream(
                    "LealInfoPDV.Assets.LIA_OFICIAL_TRANSPARENTE.webm");
                if (input is null)
                {
                    MessageBox.Show(
                        "A LIA não foi incluída na publicação nem no executável.\n\n" +
                        "Caminho esperado: " + videoPath,
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
            await web.EnsureCoreWebView2Async();
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "lia.local",
                liaFolder,
                CoreWebView2HostResourceAccessKind.Allow);

            const string html = """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<style>
html,body{margin:0;width:100%;height:100%;overflow:hidden;background:transparent;}
body{display:flex;align-items:center;justify-content:center;}
video{width:100%;height:100%;object-fit:contain;background:transparent;}
</style>
</head>
<body>
<video id="lia" autoplay playsinline preload="auto">
  <source src="https://lia.local/LIA_OFICIAL_TRANSPARENTE.webm" type="video/webm">
</video>
<script>
const v=document.getElementById('lia');
v.addEventListener('ended',()=>{ v.currentTime=0; v.pause(); });
v.play().catch(()=>{});
</script>
</body>
</html>
""";
            web.NavigateToString(html);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível iniciar a LIA.\n\n" + ex.Message, "LIA • LEAL AI");
        }
    }
}
