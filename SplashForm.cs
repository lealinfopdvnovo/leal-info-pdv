using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;

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
    private bool markDailyIntro;
    private LogoIntroControl? logoIntro;

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
        if (logoIntro is not null)
            logoIntro.Bounds = introLayer.ClientRectangle;
    }

    private async Task StartIntroAsync()
    {
        LayoutSplash();
        LoadRealLogin();
        SelectOpeningForToday();

        if (!markDailyIntro)
        {
            StartLogoIntro();
            return;
        }

        var ok = await StartOpeningVideoAsync();
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
        var stateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LEAL INFO PDV");
        Directory.CreateDirectory(stateDir);

        var stateFile = Path.Combine(stateDir, "lia-abertura-diaria.txt");
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        string last = "";
        try
        {
            if (File.Exists(stateFile))
                last = File.ReadAllText(stateFile).Trim();
        }
        catch { }

        markDailyIntro = !string.Equals(last, today, StringComparison.Ordinal);
    }

    private void StartLogoIntro()
    {
        try
        {
            videoView.Visible = false;
            logoIntro?.Dispose();
            logoIntro = new LogoIntroControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            logoIntro.Completed += (_, _) => FinishIntro();
            introLayer.Controls.Add(logoIntro);
            logoIntro.BringToFront();
            logoIntro.Start();

            fallbackSeconds = 0;
            fallbackTimer.Start();
        }
        catch
        {
            FinishIntro();
        }
    }

    private async Task<bool> StartOpeningVideoAsync()
    {
        try
        {
            videoView.Visible = true;
            var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
            var video = Path.Combine(assets, "lia_abertura_pro.mp4");
            if (!File.Exists(video))
                return false;

            var data = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LEAL INFO PDV", "WebView2", "LIA_SPLASH_VIDEO");
            Directory.CreateDirectory(data);

            var options = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments: "--autoplay-policy=no-user-gesture-required");
            var env = await CoreWebView2Environment.CreateAsync(null, data, options);
            await videoView.EnsureCoreWebView2Async(env);

            videoView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            videoView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            videoView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            videoView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "lia-splash.local", assets, CoreWebView2HostResourceAccessKind.Allow);

            videoView.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                if (e.TryGetWebMessageAsString() == "lia-video-ended")
                    FinishIntro();
            };

            const string html = """
<!doctype html><html><head><meta charset="utf-8"><style>
html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#000}
body{display:flex;align-items:center;justify-content:center}
video{width:100%;height:100%;object-fit:contain;background:#000}
</style></head><body>
<video id="liaVideo" autoplay playsinline preload="auto"><source src="https://lia-splash.local/lia_abertura_pro.mp4" type="video/mp4"></video>
<script>const v=document.getElementById('liaVideo');v.addEventListener('ended',()=>chrome.webview.postMessage('lia-video-ended'));v.addEventListener('error',()=>chrome.webview.postMessage('lia-video-ended'));v.play().catch(()=>{});</script>
</body></html>
""";

            videoView.NavigateToString(html);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void MarkDailyIntroCompleted()
    {
        if (!markDailyIntro)
            return;

        try
        {
            var stateDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LEAL INFO PDV");
            Directory.CreateDirectory(stateDir);
            File.WriteAllText(
                Path.Combine(stateDir, "lia-abertura-diaria.txt"),
                DateTime.Now.ToString("yyyy-MM-dd"));
            markDailyIntro = false;
        }
        catch { }
    }

    private void FinishIntro()
    {
        if (introFinished)
            return;

        introFinished = true;
        fallbackTimer.Stop();
        MarkDailyIntroCompleted();

        try
        {
            logoIntro?.Stop();
            logoIntro?.Dispose();
            logoIntro = null;
        }
        catch { }

        if (!loginLoaded)
            LoadRealLogin();

        introLayer.Visible = false;
        login?.BringToFront();
        TopMost = false;
    }

    private void LoadRealLogin()
    {
        if (loginLoaded)
            return;

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
            if (login.DialogResult == DialogResult.OK)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else if (!IsDisposed)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
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
            if (control is Label label &&
                label.Text.Contains("ACESSO SEGURO", StringComparison.OrdinalIgnoreCase))
            {
                label.Text = $"LEAL INFO CONECTADO  •  ACESSO SEGURO  •  V{UpdateManager.CurrentVersion}";
            }

            if (control.HasChildren)
                ApplyCurrentVersionToLogin(control);
        }
    }

    private sealed class LogoIntroControl : Control
    {
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
        private readonly Stopwatch watch = new();
        private Image? logo;
        private bool completed;

        public event EventHandler? Completed;

        public LogoIntroControl()
        {
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            timer.Tick += (_, _) =>
            {
                if (!watch.IsRunning)
                    return;

                Invalidate();
                if (watch.Elapsed.TotalMilliseconds >= 5000)
                {
                    Stop();
                    if (!completed)
                    {
                        completed = true;
                        Completed?.Invoke(this, EventArgs.Empty);
                    }
                }
            };
        }

        public void Start()
        {
            LoadLogo();
            completed = false;
            watch.Restart();
            timer.Start();
            Invalidate();
        }

        public void Stop()
        {
            timer.Stop();
            watch.Stop();
        }

        private void LoadLogo()
        {
            if (logo is not null)
                return;

            var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
            var candidates = new[]
            {
                Path.Combine(assets, "logo.png"),
                Path.Combine(assets, "lealinfo_app_icon.png")
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    using var source = Image.FromFile(path);
                    logo = new Bitmap(source);
                    return;
                }
                catch { }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(Color.Black);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var elapsed = watch.IsRunning ? watch.Elapsed.TotalMilliseconds : 5000.0;
            var t = Math.Clamp(elapsed / 5000.0, 0.0, 1.0);

            var zoomT = Math.Clamp(t / 0.72, 0.0, 1.0);
            var ease = 1.0 - Math.Pow(1.0 - zoomT, 3.0);

            if (logo is not null)
            {
                var fit = Math.Min(
                    ClientSize.Width * 0.78 / logo.Width,
                    ClientSize.Height * 0.72 / logo.Height);
                var scale = fit * (0.045 + 1.08 * ease);
                var w = Math.Max(1, (int)(logo.Width * scale));
                var h = Math.Max(1, (int)(logo.Height * scale));
                var x = (ClientSize.Width - w) / 2;
                var y = (ClientSize.Height - h) / 2;

                var glow = (int)(70 + 110 * ease);
                using var glowPen1 = new Pen(Color.FromArgb(Math.Min(150, glow), 0, 190, 255), Math.Max(2f, 3f + (float)ease * 8f));
                using var glowPen2 = new Pen(Color.FromArgb(Math.Min(90, glow / 2), 80, 225, 255), Math.Max(1f, 1.5f + (float)ease * 4f));
                var pad = (int)(18 + 35 * ease);
                g.DrawEllipse(glowPen1, x - pad, y - pad, w + pad * 2, h + pad * 2);
                g.DrawEllipse(glowPen2, x - pad / 2, y - pad / 2, w + pad, h + pad);
                g.DrawImage(logo, new Rectangle(x, y, w, h));
            }

            if (t >= 0.68)
            {
                var p = Math.Clamp((t - 0.68) / 0.32, 0.0, 1.0);
                var maxRadius = Math.Sqrt(ClientSize.Width * ClientSize.Width + ClientSize.Height * ClientSize.Height);
                var radius = (float)(30 + maxRadius * 0.82 * p);
                var cx = ClientSize.Width / 2f;
                var cy = ClientSize.Height / 2f;

                using var ring1 = new Pen(Color.FromArgb((int)(230 * (1 - p * 0.5)), 0, 190, 255), Math.Max(4f, 18f * (float)(1 - p) + 4f));
                using var ring2 = new Pen(Color.FromArgb((int)(210 * (1 - p * 0.35)), 220, 250, 255), Math.Max(2f, 9f * (float)(1 - p) + 2f));
                g.DrawEllipse(ring1, cx - radius, cy - radius, radius * 2, radius * 2);
                g.DrawEllipse(ring2, cx - radius * 0.72f, cy - radius * 0.72f, radius * 1.44f, radius * 1.44f);

                var flashProgress = Math.Clamp((p - 0.15) / 0.85, 0.0, 1.0);
                var flashAlpha = (int)(255 * Math.Pow(flashProgress, 0.72));
                using var flash = new SolidBrush(Color.FromArgb(flashAlpha, 225, 248, 255));
                g.FillRectangle(flash, ClientRectangle);

                if (p > 0.72)
                {
                    var whiteAlpha = (int)(255 * Math.Clamp((p - 0.72) / 0.28, 0.0, 1.0));
                    using var white = new SolidBrush(Color.FromArgb(whiteAlpha, 248, 253, 255));
                    g.FillRectangle(white, ClientRectangle);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Stop();
                timer.Dispose();
                logo?.Dispose();
                logo = null;
            }
            base.Dispose(disposing);
        }
    }
}
