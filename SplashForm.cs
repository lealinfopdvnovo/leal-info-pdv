using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Drawing;
using System.Media;

namespace LealInfoPDV;

public sealed class SplashForm : Form
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly Panel introLayer = new();
    private readonly Panel pageEdge = new();
    private readonly WebView2 lia = new();
    private readonly Label brand = new();
    private readonly Label slogan = new();
    private readonly Label product = new();
    private readonly Label next = new();
    private int ticks;
    private SoundPlayer? player;
    private LoginForm? login;
    private bool loginLoaded;

    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(3, 13, 27);
        ShowInTaskbar = false;
        TopMost = true;
        Opacity = 0;
        KeyPreview = true;

        brand.Text = "LEAL INFO CONECTADO";
        brand.ForeColor = Color.White;
        brand.BackColor = Color.Transparent;
        brand.Font = new Font("Segoe UI", 34, FontStyle.Bold);
        brand.TextAlign = ContentAlignment.MiddleCenter;

        slogan.Text = "TECNOLOGIA QUE CONECTA";
        slogan.ForeColor = Color.FromArgb(55, 205, 255);
        slogan.BackColor = Color.Transparent;
        slogan.Font = new Font("Segoe UI", 15, FontStyle.Bold);
        slogan.TextAlign = ContentAlignment.MiddleCenter;

        product.Text = "LEAL INFO PDV PRO  •  LIA";
        product.ForeColor = Color.FromArgb(155, 180, 200);
        product.BackColor = Color.Transparent;
        product.Font = new Font("Segoe UI", 10, FontStyle.Regular);
        product.TextAlign = ContentAlignment.MiddleCenter;

        next.Text = "Na próxima tela, coloque suas credenciais";
        next.ForeColor = Color.FromArgb(190, 220, 235);
        next.BackColor = Color.Transparent;
        next.Font = new Font("Segoe UI", 11, FontStyle.Regular);
        next.TextAlign = ContentAlignment.MiddleCenter;

        introLayer.BackColor = Color.FromArgb(3, 13, 27);
        Controls.Add(introLayer);
        introLayer.Controls.Add(brand);
        introLayer.Controls.Add(slogan);
        introLayer.Controls.Add(product);
        introLayer.Controls.Add(next);

        // WebView2 WinForms não aceita BackColor transparente no próprio controle.
        // A transparência visual é aplicada pelo DefaultBackgroundColor/CoreWebView2.
        lia.BackColor = Color.FromArgb(3, 13, 27);
        lia.DefaultBackgroundColor = Color.Transparent;
        introLayer.Controls.Add(lia);
        lia.BringToFront();

        pageEdge.BackColor = Color.FromArgb(0, 163, 224);
        pageEdge.Width = 5;
        introLayer.Controls.Add(pageEdge);
        pageEdge.BringToFront();

        Resize += (_, _) => LayoutSplash();
        Shown += async (_, _) => await StartIntroAsync();
        timer.Tick += (_, _) => AnimateIntro();
        KeyDown += (_,e) => { if(e.KeyCode==Keys.Escape && loginLoaded) Close(); };
    }

    private void LayoutSplash()
    {
        introLayer.Bounds = ClientRectangle;
        pageEdge.SetBounds(Math.Max(0, introLayer.Width - 5), 0, 5, introLayer.Height);
        int w = Math.Min(900, Math.Max(580, ClientSize.Width / 2));
        int centerY = ClientSize.Height / 2;
        int liaW = Math.Min(390, Math.Max(280, ClientSize.Width / 5));
        int liaH = Math.Min(570, Math.Max(410, ClientSize.Height * 2 / 3));
        lia.SetBounds(Math.Max(30, ClientSize.Width / 2 - liaW - 80), Math.Max(30, centerY - liaH / 2), liaW, liaH);
        int textX = Math.Min(ClientSize.Width - w - 40, ClientSize.Width / 2 + 20);
        brand.Bounds = new Rectangle(textX, centerY - 120, w, 82);
        slogan.Bounds = new Rectangle(textX, centerY - 35, w, 48);
        product.Bounds = new Rectangle(textX, centerY + 25, w, 30);
        next.Bounds = new Rectangle(textX, centerY + 82, w, 42);
    }

    private async Task StartIntroAsync()
    {
        LayoutSplash();
        TryPlayOpeningSound();
        await StartLiaHologramAsync();
        timer.Start();
    }

    private async Task StartLiaHologramAsync()
    {
        try
        {
            var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
            var video = Path.Combine(assets, "leal_ai_feminino_holograma.webm");
            if(!File.Exists(video)) { lia.Visible=false; return; }
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO PDV", "WebView2", "LIA_SPLASH");
            Directory.CreateDirectory(data);
            var options = new CoreWebView2EnvironmentOptions(additionalBrowserArguments: "--autoplay-policy=no-user-gesture-required");
            var env = await CoreWebView2Environment.CreateAsync(null, data, options);
            await lia.EnsureCoreWebView2Async(env);
            lia.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
            lia.CoreWebView2.Settings.AreDevToolsEnabled=false;
            lia.CoreWebView2.Settings.IsStatusBarEnabled=false;
            lia.CoreWebView2.SetVirtualHostNameToFolderMapping("lia-splash.local", assets, CoreWebView2HostResourceAccessKind.Allow);
            const string html="""
<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;width:100%;height:100%;overflow:hidden;background:transparent}body{display:flex;align-items:center;justify-content:center}video{width:100%;height:100%;object-fit:contain;background:transparent;filter:drop-shadow(0 0 8px rgba(0,210,255,.65)) drop-shadow(0 0 24px rgba(0,125,255,.35))}</style></head><body><video autoplay muted loop playsinline preload="auto"><source src="https://lia-splash.local/leal_ai_feminino_holograma.webm" type="video/webm"></video></body></html>
""";
            lia.NavigateToString(html);
        }
        catch { lia.Visible=false; }
    }

    private void TryPlayOpeningSound()
    {
        try
        {
            var wav = Path.Combine(AppContext.BaseDirectory, "Assets", "abertura.wav");
            if (!File.Exists(wav)) return;
            player = new SoundPlayer(wav);
            player.Load();
            player.Play();
        }
        catch { }
    }

    private void LoadRealLogin()
    {
        if(loginLoaded) return;
        loginLoaded=true;
        login = new LoginForm { EmbeddedMode=true, TopLevel=false, FormBorderStyle=FormBorderStyle.None, WindowState=FormWindowState.Normal, Dock=DockStyle.Fill, TopMost=false };
        login.FormClosed += (_,_) =>
        {
            if(login.DialogResult == DialogResult.OK){DialogResult=DialogResult.OK;Close();}
            else if(!IsDisposed){DialogResult=DialogResult.Cancel;Close();}
        };
        Controls.Add(login); login.Show(); ApplyCurrentVersionToLogin(login); login.SendToBack(); introLayer.BringToFront();
    }

    private static void ApplyCurrentVersionToLogin(Control root)
    {
        foreach(Control control in root.Controls)
        {
            if(control is Label label && label.Text.Contains("ACESSO SEGURO",StringComparison.OrdinalIgnoreCase)) label.Text=$"LEAL INFO CONECTADO  •  ACESSO SEGURO  •  V{UpdateManager.CurrentVersion}";
            if(control.HasChildren)ApplyCurrentVersionToLogin(control);
        }
    }

    private void AnimateIntro()
    {
        ticks++;
        if(ticks<=28)Opacity=Math.Min(1,ticks/28.0);
        if(ticks==190)LoadRealLogin();
        if(ticks>285)
        {
            if(!loginLoaded)LoadRealLogin();
            timer.Stop(); player?.Stop(); introLayer.Visible=false; login?.BringToFront(); TopMost=false;
        }
    }
}
