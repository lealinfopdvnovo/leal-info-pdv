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
    private int fallbackLimitSeconds = 35;
    private LoginForm? login;
    private bool loginLoaded;
    private bool introFinished;
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
        introLayer.Dock = DockStyle.Fill;
        Controls.Add(introLayer);

        videoView.BackColor = Fundo;
        videoView.DefaultBackgroundColor = Fundo;
        videoView.Dock = DockStyle.Fill;
        introLayer.Controls.Add(videoView);

        Shown += async (_, _) => await StartIntroAsync();
        fallbackTimer.Tick += (_, _) =>
        {
            fallbackSeconds++;
            if (fallbackSeconds >= fallbackLimitSeconds)
                FinishIntro();
        };
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && introFinished)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task StartIntroAsync()
    {
        SelectOpeningForToday();

        string videoName;
        if (markDailyIntro)
        {
            videoName = "lia_abertura_pro.mp4";
            fallbackLimitSeconds = 35;
        }
        else
        {
            videoName = "logo_abertura_login.mp4";
            fallbackLimitSeconds = 12;
        }

        introLayer.Visible = true;
        introLayer.BringToFront();
        videoView.Visible = true;
        videoView.BringToFront();

        var ok = await StartVideoAsync(videoName, !markDailyIntro);
        if (!ok)
        {
            FinishIntro();
            return;
        }

        fallbackSeconds = 0;
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
        markDailyIntro = !string.Equals(last, today, StringComparison.Ordinal);
    }

    private async Task<bool> StartVideoAsync(string videoName, bool fillScreen)
    {
        try
        {
            var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
            var video = Path.Combine(assets, videoName);
            if (!File.Exists(video)) return false;

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
                string msg = "";
                try { msg = e.TryGetWebMessageAsString(); } catch { }
                if (msg == "splash-video-ended") FinishIntro();
            };

            var safeVideoName = videoName.Replace("'", "").Replace("\"", "");
            var fit = fillScreen ? "cover" : "contain";
            var html =
                "<!doctype html><html><head><meta charset=\"utf-8\"><style>" +
                "html,body{margin:0;padding:0;width:100vw;height:100vh;overflow:hidden;background:#000}" +
                "body{position:fixed;inset:0;background:#000}" +
                "video{position:absolute;inset:0;width:100vw;height:100vh;object-fit:" + fit + ";object-position:center center;background:#000}" +
                "</style></head><body>" +
                "<video id=\"splashVideo\" autoplay playsinline preload=\"auto\"><source src=\"https://lia-splash.local/" + safeVideoName + "\" type=\"video/mp4\"></video>" +
                "<script>const v=document.getElementById('splashVideo');const done=()=>chrome.webview.postMessage('splash-video-ended');v.addEventListener('ended',done);v.addEventListener('error',done);v.play().catch(done);</script>" +
                "</body></html>";
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
        videoView.Visible = false;
        introLayer.Visible = false;
        LoadRealLogin();
        login?.BringToFront();
        TopMost = false;
    }

    private void LoadRealLogin()
    {
        if (loginLoaded) return;
        loginLoaded = true;
        login = new LoginForm
        {
            EmbeddedMode = true,
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            WindowState = FormWindowState.Normal,
            Dock = DockStyle.Fill,
            TopMost = false
        };
        login.FormClosed += (_, _) =>
        {
            if (login.DialogResult == DialogResult.OK) { DialogResult = DialogResult.OK; Close(); }
            else if (!IsDisposed) { DialogResult = DialogResult.Cancel; Close(); }
        };
        Controls.Add(login);
        login.Show();
        ApplyCurrentVersionToLogin(login);
        login.BringToFront();
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
