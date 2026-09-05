using System.Drawing;
using System.Media;

namespace LealInfoPDV;

public sealed class SplashForm : Form
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly Panel introLayer = new();
    private readonly Panel pageEdge = new();
    private readonly WalkingRobotControl walker = new();
    private readonly Label brand = new();
    private readonly Label slogan = new();
    private readonly Label product = new();
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

        product.Text = "LEAL INFO PDV PRO";
        product.ForeColor = Color.FromArgb(155, 180, 200);
        product.BackColor = Color.Transparent;
        product.Font = new Font("Segoe UI", 10, FontStyle.Regular);
        product.TextAlign = ContentAlignment.MiddleCenter;

        introLayer.BackColor = Color.FromArgb(3, 13, 27);
        Controls.Add(introLayer);
        walker.Size = new Size(150, 190);
        walker.BackColor = Color.Transparent;
        walker.Visible = false;
        Controls.Add(walker);
        walker.BringToFront();

        introLayer.Controls.Add(brand);
        introLayer.Controls.Add(slogan);
        introLayer.Controls.Add(product);

        pageEdge.BackColor = Color.FromArgb(0, 163, 224);
        pageEdge.Width = 5;
        introLayer.Controls.Add(pageEdge);
        pageEdge.BringToFront();

        Resize += (_, _) => LayoutSplash();
        Shown += (_, _) => StartIntro();
        timer.Tick += (_, _) => AnimateIntro();
        KeyDown += (_,e) => { if(e.KeyCode==Keys.Escape && loginLoaded) Close(); };
    }

    private void LayoutSplash()
    {
        introLayer.Bounds = ClientRectangle;
        pageEdge.SetBounds(Math.Max(0, introLayer.Width - 5), 0, 5, introLayer.Height);
        int w = Math.Min(1100, Math.Max(650, ClientSize.Width - 240));
        int centerY = ClientSize.Height / 2;
        brand.Bounds = new Rectangle((ClientSize.Width - w) / 2, centerY - 105, w, 82);
        slogan.Bounds = new Rectangle((ClientSize.Width - w) / 2, centerY - 20, w, 48);
        product.Bounds = new Rectangle((ClientSize.Width - w) / 2, centerY + 42, w, 30);
    }

    private void StartIntro()
    {
        LayoutSplash();
        TryPlayOpeningSound();
        timer.Start();
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

        login = new LoginForm
        {
            EmbeddedMode = true,
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            WindowState = FormWindowState.Normal,
            Dock = DockStyle.Fill,
            TopMost = false
        };
        login.FormClosed += (_,_) =>
        {
            if(login.DialogResult == DialogResult.OK)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else if(!IsDisposed)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };

        Controls.Add(login);
        login.Show();
        login.SendToBack();
        introLayer.BringToFront();
    }

    private void AnimateIntro()
    {
        ticks++;

        if (ticks <= 28)
            Opacity = Math.Min(1, ticks / 28.0);

        // Login já fica carregado atrás da abertura.
        if (ticks == 150)
            LoadRealLogin();

        // Transição direta aprovada: abertura -> login.
        if (ticks > 210)
        {
            if (!loginLoaded) LoadRealLogin();
            timer.Stop();
            player?.Stop();
            introLayer.Visible = false;
            walker.Visible = false;
            login?.BringToFront();
            TopMost = false;
        }
    }
}

internal sealed class WalkingRobotControl : Control
{
    public int WalkPhase { get; set; }
    public bool IsPushing { get; set; }
    public double PushProgress { get; set; }

    public WalkingRobotControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        double phase = WalkPhase * 0.38;
        int swing = IsPushing ? 0 : (int)(Math.Sin(phase) * 12);
        int bob = IsPushing ? 0 : (int)(Math.Abs(Math.Sin(phase)) * 4);

        using var armorDark = new SolidBrush(Color.FromArgb(18,30,44));
        using var armor = new SolidBrush(Color.FromArgb(76,98,120));
        using var steel = new SolidBrush(Color.FromArgb(160,180,196));
        using var cyan = new SolidBrush(Color.FromArgb(45,210,255));
        using var outline = new Pen(Color.FromArgb(5,14,24),4f);

        using(var shadow = new SolidBrush(Color.FromArgb(65,0,0,0)))
            g.FillEllipse(shadow,32,172,88,12);

        int y = bob;

        DrawLimb(g,62,122+y,55-swing/2,166,16,steel,armorDark,outline);
        DrawLimb(g,89,122+y,96+swing/2,166,16,steel,armorDark,outline);

        var torso = new Rectangle(48,64+y,58,66);
        using(var torsoPath = Rounded(torso,14))
        {
            g.FillPath(armor, torsoPath);
            g.DrawPath(outline, torsoPath);
        }
        g.FillRectangle(armorDark,57,83+y,40,28);
        g.FillEllipse(cyan,70,91+y,14,14);

        var head = new Rectangle(51,22+y,54,48);
        using(var headPath = Rounded(head,16))
        {
            g.FillPath(steel, headPath);
            g.DrawPath(outline, headPath);
        }
        g.FillRectangle(armorDark,60,38+y,36,17);
        g.FillEllipse(cyan,67,43+y,8,6);
        g.FillEllipse(cyan,83,43+y,8,6);

        if(IsPushing)
        {
            int reach = 28 + (int)(8*Math.Sin(Math.Min(1.0,PushProgress)*Math.PI));
            DrawLimb(g,100,79+y,128+reach,82+y,13,steel,armorDark,outline);
            DrawLimb(g,101,101+y,130+reach,105+y,13,steel,armorDark,outline);
        }
        else
        {
            DrawLimb(g,49,78+y,32+swing,115+y,13,steel,armorDark,outline);
            DrawLimb(g,105,78+y,121-swing,113+y,13,steel,armorDark,outline);
        }
    }

    private static void DrawLimb(Graphics g,int x1,int y1,int x2,int y2,int width,
        Brush steel,Brush joint,Pen outline)
    {
        using var p = new Pen(Color.FromArgb(150,170,188),width);
        p.StartCap = System.Drawing.Drawing2D.LineCap.Round;
        p.EndCap = System.Drawing.Drawing2D.LineCap.Round;
        g.DrawLine(p,x1,y1,x2,y2);
        g.FillEllipse(joint,x1-7,y1-7,14,14);
        g.FillEllipse(joint,x2-7,y2-7,14,14);
        g.DrawEllipse(outline,x1-7,y1-7,14,14);
        g.DrawEllipse(outline,x2-7,y2-7,14,14);
    }

    private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle r,int radius)
    {
        int d=radius*2;
        var p=new System.Drawing.Drawing2D.GraphicsPath();
        p.AddArc(r.X,r.Y,d,d,180,90);
        p.AddArc(r.Right-d,r.Y,d,d,270,90);
        p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);
        p.AddArc(r.X,r.Bottom-d,d,d,90,90);
        p.CloseFigure();
        return p;
    }
}

