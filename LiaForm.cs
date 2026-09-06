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
        BackColor = Color.FromArgb(3, 18, 36);
        ShowInTaskbar = false;
        TopMost = true;

        web.Dock = DockStyle.Fill;
        web.BackColor = Color.Transparent;
        web.DefaultBackgroundColor = Color.Transparent;
        Controls.Add(web);

        Shown += async (_, _) => await StartLiaAsync();
    }

    private async Task StartLiaAsync()
    {
        var liaFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "LIA");
        var videoPath = Path.Combine(liaFolder, "LIA_OFICIAL_TRANSPARENTE.webm");
        if (!File.Exists(videoPath))
        {
            MessageBox.Show("Arquivo oficial da LIA não encontrado.", "LIA • LEAL AI");
            Close();
            return;
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
