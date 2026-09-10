using System.Drawing.Drawing2D;

namespace LealInfoPDV;

/// <summary>Orbe compacta oficial que substitui o antigo botão AI.</summary>
public sealed class LiaOrbLauncher : Control
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 35 };
    private double fase;

    public LiaOrbLauncher()
    {
        Size = new Size(92, 86);
        Cursor = Cursors.Hand;
        TabStop = false;

        // WinForms exige este estilo antes de aceitar Color.Transparent
        // em um Control personalizado. Sem isso, o PDV falha na inicializacao.
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor, true);
        UpdateStyles();
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        timer.Tick += (_, _) => { fase += 0.10; Invalidate(); };
        timer.Start();
        Disposed += (_, _) => timer.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float p = (float)((Math.Sin(fase) + 1) / 2);
        float cx = Width / 2f, cy = 32f, r = 24f + p * 3f;
        using var halo = new Pen(Color.FromArgb(80 + (int)(p * 80), 0, 220, 255), 3f);
        e.Graphics.DrawEllipse(halo, cx-r-3, cy-r-3, (r+3)*2, (r+3)*2);
        var rect = new RectangleF(cx-r, cy-r, r*2, r*2);
        using var path = new GraphicsPath(); path.AddEllipse(rect);
        using var fill = new PathGradientBrush(path) { CenterColor = Color.FromArgb(245,18,128,210), SurroundColors = new[] { Color.FromArgb(230,0,24,62) } };
        e.Graphics.FillEllipse(fill, rect);
        using var font = new Font("Segoe UI", 12, FontStyle.Bold);
        using var br = new SolidBrush(Color.White);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString("LIA", font, br, new RectangleF(0, 7, Width, 50), sf);

        if (LiaUsageManager.HasConversationQuota)
        {
            var saldo = LiaUsageManager.RemainingText;
            using var clockFont = new Font("Segoe UI", 9, FontStyle.Bold);
            using var clockBrush = new SolidBrush(LiaUsageManager.HasTimeRemaining
                ? Color.FromArgb(124, 238, 255)
                : Color.FromArgb(255, 170, 170));
            e.Graphics.DrawString($"⏱ {saldo}", clockFont, clockBrush,
                new RectangleF(0, 62, Width, 22), sf);
        }
    }
}
