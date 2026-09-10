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
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(340, 510);
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

        Shown += async (_, _) =>
        {
            PosicionarAoLadoDoBotaoAi();
            var destino = Location;
            Location = new Point(destino.X, destino.Y + 260);
            await StartLiaAsync();
            AnimarSubida(destino);
        };
    }

    private void PosicionarAoLadoDoBotaoAi()
    {
        // Tamanho reduzido e posição presa ao botão AI do canto inferior direito.
        var owner = Owner as Form;
        var area = owner is not null
            ? Screen.FromControl(owner).WorkingArea
            : Screen.FromControl(this).WorkingArea;

        // O botão AI mede 58x58, fica a 22 px da direita e acima da barra de status.
        // A personagem fica imediatamente acima dele, alinhada pelo centro do botão.
        int botaoCentroX = owner is not null ? owner.Right - 22 - 29 : area.Right - 22 - 29;
        int botaoTopo = owner is not null ? owner.Bottom - 98 : area.Bottom - 98;

        int x = botaoCentroX - (Width / 2);
        int y = botaoTopo - Height + 12;

        // Mantém a LIA inteira visível sem perder a ligação visual com o botão.
        x = Math.Max(area.Left + 6, Math.Min(x, area.Right - Width - 6));
        y = Math.Max(area.Top + 6, Math.Min(y, area.Bottom - Height - 6));
        Location = new Point(x, y);
    }

    private void AnimarSubida(Point destino)
    {
        // Entrada holográfica: sobe de baixo para cima sem tocar no vídeo/áudio.
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            int restante = Location.Y - destino.Y;
            if (restante <= 0)
            {
                Location = destino;
                timer.Stop();
                timer.Dispose();
                return;
            }

            int passo = Math.Max(8, restante / 5);
            Location = new Point(destino.X, Math.Max(destino.Y, Location.Y - passo));
        };
        timer.Start();
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
            // V10.170: usa o ambiente padrão compartilhado do processo.
            // O perfil e o autoplay são definidos por WebView2DataBootstrap antes de qualquer formulário.
            // Não criar um segundo CoreWebView2Environment aqui: ambientes diferentes no mesmo processo
            // podem causar HRESULT 0x8007139F quando o Tutorial abre outro WebView2.
            await web.EnsureCoreWebView2Async();

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
