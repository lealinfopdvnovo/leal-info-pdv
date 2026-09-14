using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Drawing;

namespace LealInfoPDV;

public sealed class SplashForm : Form
{
    private static readonly Color Fundo = Color.Black;
    private readonly Panel introLayer = new();
    private readonly WebView2 videoView = new();
    private readonly System.Windows.Forms.Timer fallbackTimer = new() { Interval = 1000 };
    private int fallbackSeconds;
    private LoginForm? login;
    private bool loginLoaded;
    private bool introStarted;
    private bool introFinished;

    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Fundo;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        introLayer.BackColor = Fundo;
        Controls.Add(introLayer);
        videoView.BackColor = Fundo;
        videoView.DefaultBackgroundColor = Fundo;
        introLayer.Controls.Add(videoView);
        Resize += (_, _) => LayoutSplash();
        Shown += async (_, _) => await StartIntroAsync();
        fallbackTimer.Tick += (_, _) => { fallbackSeconds++; if (fallbackSeconds >= 15) FinishIntro(); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape && loginLoaded) FinishIntro(); };
    }

    private void LayoutSplash()
    {
        introLayer.Bounds = ClientRectangle;
        videoView.Bounds = introLayer.ClientRectangle;
    }

    private async Task StartIntroAsync()
    {
        if (introStarted) return;
        introStarted = true;
        LayoutSplash();
        LoadRealLogin();
        if (!await StartOpeningVideoAsync()) { FinishIntro(); return; }
        fallbackTimer.Start();
    }

    private async Task<bool> StartOpeningVideoAsync()
    {
        try
        {
            var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
            var video = Path.Combine(assets, "abertura.mp4");
            if (!File.Exists(video)) return false;
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO PDV", "WebView2", "PDV_SPLASH_VIDEO");
            Directory.CreateDirectory(data);
            var options = new CoreWebView2EnvironmentOptions(additionalBrowserArguments: "--autoplay-policy=no-user-gesture-required");
            var env = await CoreWebView2Environment.CreateAsync(null, data, options);
            await videoView.EnsureCoreWebView2Async(env);
            videoView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            videoView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            videoView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            videoView.CoreWebView2.SetVirtualHostNameToFolderMapping("pdv-splash.local", assets, CoreWebView2HostResourceAccessKind.Allow);
            videoView.CoreWebView2.WebMessageReceived += (_, e) => { if (e.TryGetWebMessageAsString() == "pdv-video-ended") FinishIntro(); };
            const string html = """
<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#000}body{display:flex;align-items:center;justify-content:center}video{width:100%;height:100%;object-fit:contain;background:#000}</style></head><body><video id="pdvVideo" autoplay playsinline preload="auto"><source src="https://pdv-splash.local/abertura.mp4" type="video/mp4"></video><script>const v=document.getElementById('pdvVideo');let sent=false;const done=()=>{if(sent)return;sent=true;v.pause();chrome.webview.postMessage('pdv-video-ended')};v.loop=false;v.addEventListener('timeupdate',()=>{if(v.currentTime>=10.0)done()});v.addEventListener('ended',done,{once:true});v.addEventListener('error',done,{once:true});v.currentTime=0;v.play().catch(()=>{});</script></body></html>
""";
            videoView.NavigateToString(html);
            return true;
        }
        catch { return false; }
    }

    private void FinishIntro()
    {
        if (introFinished) return;
        introFinished = true;
        fallbackTimer.Stop();
        if (!loginLoaded) LoadRealLogin();
        try { videoView.CoreWebView2?.Stop(); } catch { }
        introLayer.Visible = false;
        login?.BringToFront();
        TopMost = false;
    }

    private void LoadRealLogin()
    {
        if (loginLoaded) return;
        loginLoaded = true;
        login = new LoginForm { EmbeddedMode = true, TopLevel = false, FormBorderStyle = FormBorderStyle.None, WindowState = FormWindowState.Normal, Dock = DockStyle.Fill, TopMost = false };
        login.FormClosed += (_, _) => { if (login.DialogResult == DialogResult.OK) { DialogResult = DialogResult.OK; Close(); } else if (!IsDisposed) { DialogResult = DialogResult.Cancel; Close(); } };
        Controls.Add(login);
        login.Show();
        ApplyCurrentVersionToLogin(login);
        login.SendToBack();
        introLayer.BringToFront();
    }

    private static void ApplyCurrentVersionToLogin(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Label label && label.Text.Contains("ACESSO SEGURO", StringComparison.OrdinalIgnoreCase))
                label.Text = $"LEAL INFO CONECTADO  •  ACESSO SEGURO  •  V{UpdateManager.CurrentVersion}";
            if (control.HasChildren) ApplyCurrentVersionToLogin(control);
        }
    }
}
