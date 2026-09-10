using System.Drawing.Drawing2D;

namespace LealInfoPDV;

/// <summary>Orbe compacta oficial que substitui o antigo botão AI.</summary>
public sealed class LiaOrbLauncher : Control
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 35 };
    private double fase;

    public LiaOrbLauncher()
    {
        Size = new Size(112, 92);
        Cursor = Cursors.Hand;
        TabStop = false;

        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor, true);
        UpdateStyles();
        DoubleBuffered = true;
        BackColor = Color.Transparent;

        Resize += (_, _) => AplicarRecorte();
        HandleCreated += (_, _) => AplicarRecorte();
        timer.Tick += (_, _) => { fase += 0.10; Invalidate(); };
        timer.Start();
        Disposed += (_, _) => timer.Dispose();
    }

    private void AplicarRecorte()
    {
        if (Width <= 0 || Height <= 0) return;

        using var shape = new GraphicsPath();

        // Área circular da orbe/halo.
        shape.AddEllipse((Width - 66) / 2f, 0, 66, 66);

        // Área inferior do relógio em formato de cápsula.
        var timerRect = new RectangleF(5, 63, Width - 10, 26);
        const float d = 18f;
        shape.StartFigure();
        shape.AddArc(timerRect.X, timerRect.Y, d, d, 180, 90);
        shape.AddArc(timerRect.Right - d, timerRect.Y, d, d, 270, 90);
        shape.AddArc(timerRect.Right - d, timerRect.Bottom - d, d, d, 0, 90);
        shape.AddArc(timerRect.X, timerRect.Bottom - d, d, d, 90, 90);
        shape.CloseFigure();

        Region?.Dispose();
        Region = new Region(shape);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Fundo escuro somente dentro do recorte; fora dele o controle nem existe visualmente.
        // Assim desaparece de vez o painel retangular branco do WinForms.
        e.Graphics.Clear(Color.FromArgb(3, 18, 36));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        float p = (float)((Math.Sin(fase) + 1) / 2);
        float cx = Width / 2f, cy = 31f, r = 23f + p * 2f;

        using var haloOuter = new Pen(Color.FromArgb(45 + (int)(p * 45), 60, 220, 255), 2f);
        using var haloInner = new Pen(Color.FromArgb(155 + (int)(p * 70), 125, 238, 255), 2f);
        e.Graphics.DrawEllipse(haloOuter, cx-r-5, cy-r-5, (r+5)*2, (r+5)*2);
        e.Graphics.DrawEllipse(haloInner, cx-r-2, cy-r-2, (r+2)*2, (r+2)*2);

        var rect = new RectangleF(cx-r, cy-r, r*2, r*2);
        using var path = new GraphicsPath();
        path.AddEllipse(rect);
        using var fill = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(250, 20, 130, 205),
            SurroundColors = new[] { Color.FromArgb(245, 0, 24, 62) }
        };
        e.Graphics.FillEllipse(fill, rect);

        using var font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        using var br = new SolidBrush(Color.White);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString("LIA", font, br, new RectangleF(0, 6, Width, 50), sf);

        if (LiaUsageManager.HasConversationQuota)
        {
            var saldo = LiaUsageManager.RemainingText;
            using var clockFont = new Font("Segoe UI", 9, FontStyle.Bold);
            using var clockBrush = new SolidBrush(LiaUsageManager.HasTimeRemaining
                ? Color.FromArgb(124, 238, 255)
                : Color.FromArgb(255, 170, 170));
            e.Graphics.DrawString($"⏱ {saldo}", clockFont, clockBrush,
                new RectangleF(4, 65, Width - 8, 20), sf);
        }
    }
}
