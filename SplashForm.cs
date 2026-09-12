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
    private bool introFinished;
    private string openingVideoName = "lia_abertura_pro.mp4";
    private bool markDailyIntro;

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
        fallbackTimer.Tick += (_, _) =>
        {
            fallbackSeconds++;
            if (fallbackSeconds >= 35)
                FinishIntro();
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape && loginLoaded)
                FinishIntro();
        };
    }

    private void LayoutSplash()
    {
        introLayer.Bounds = ClientRectangle;
        videoView.Bounds = introLayer.ClientRectangle;
    }

    private async Task StartIntroAsync()
    {
        LayoutSplash();
        LoadRealLogin();
        SelectOpeningForToday();

        var ok = await StartOpeningVideoAsync();
        if (!ok)
        {
            FinishIntro();
            return;
        }

        fallbackTimer.Start();
    }

    private void SelectOpeningForToday()
    {
        var stateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO PDV");
        Directory.CreateDirectory(stateDir);
        var stateFile = Path.Combine(stateDir, "lia-abertura-diaria.txt");
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        string last = "";
        try { if (File.Exists(stateFile)) last = File.ReadAllText(stateFile).Trim(); } catch { }

        if (!string.Equals(last, today, StringComparison.Ordinal))
        {
            openingVideoName = "lia_abertura_pro.mp4";
            markDailyIntro = true;
        }
        else
        {
            openingVideoName = "logo_abertura_login.mp4";
            markDailyIntro = false;
        }
    }

    private async Task<bool> StartOpeningVideoAsync()
    {
        try
        {
            var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
            var video = Path.Combine(assets, openingVideoName);
            if (!File.Exists(video))
            {
                if (!markDailyIntro) return false;
                video = Path.Combine(assets, "lia_abertura_pro.mp4");
                openingVideoName = "lia_abertura_pro.mp4";
                if (!File.Exists(video)) return false;
            }

            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO PDV", "WebView2", "LIA_SPLASH_VIDEO");
            Directory.CreateDirectory(data);

            var options = new CoreWebView2EnvironmentOptions(additionalBrowserArguments: "--autoplay-policy=no-user-gesture-required");
            var env = await CoreWebView2Environment.CreateAsync(null, data, options);
            await videoView.EnsureCoreWebView2Async(env);

            videoView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            videoView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            videoView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            videoView.CoreWebView2.SetVirtualHostNameToFolderMapping("lia-splash.local", assets, CoreWebView2HostResourceAccessKind.Allow);

            videoView.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                if (e.TryGetWebMessageAsString() == "lia-video-ended") FinishIntro();
            };

            var html = $"""
<!doctype html><html><head><meta charset="utf-8"><style>
html,body{{margin:0;width:100%;height:100%;overflow:hidden;background:#000}}
body{{display:flex;align-items:center;justify-content:center}}
video{{width:100%;height:100%;object-fit:contain;background:#000}}
</style></head><body>
<video id="liaVideo" autoplay playsinline preload="auto"><source src="https://lia-splash.local/{openingVideoName}" type="video/mp4"></video>
<script>const v=document.getElementById('liaVideo');v.addEventListener('ended',()=>chrome.webview.postMessage('lia-video-ended'));v.addEventListener('error',()=>chrome.webview.postMessage('lia-video-ended'));v.play().catch(()=>{{}});</script>
</body></html>
""";
            videoView.NavigateToString(html);
            return true;
        }
        catch { return false; }
    }

    private void MarkDailyIntroCompleted()
    {
        if (!markDailyIntro) return;
        try
        {
            var stateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LEAL INFO PDV");
            Directory.CreateDirectory(stateDir);
            File.WriteAllText(Path.Combine(stateDir, "lia-abertura-diaria.txt"), DateTime.Now.ToString("yyyy-MM-dd"));
            markDailyIntro = false;
        }
        catch { }
    }

    private void FinishIntro()
    {
        if (introFinished) return;
        introFinished = true;
        fallbackTimer.Stop();
        MarkDailyIntroCompleted();
        if (!loginLoaded) LoadRealLogin();
        introLayer.Visible = false;
        login?.BringToFront();
        TopMost = false;
    }

    private void LoadRealLogin()
    {
        if (loginLoaded) return;
        loginLoaded = true;
        login = new LoginForm { EmbeddedMode = true, TopLevel = false, FormBorderStyle = FormBorderStyle.None, WindowState = FormWindowState.Normal, Dock = DockStyle.Fill, TopMost = false };
        login.FormClosed += (_, _) =>
        {
            if (login.DialogResult == DialogResult.OK) { DialogResult = DialogResult.OK; Close(); }
            else if (!IsDisposed) { DialogResult = DialogResult.Cancel; Close(); }
        };
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
