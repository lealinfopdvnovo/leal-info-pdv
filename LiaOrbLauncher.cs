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
        timer.Tick += (_, _) => { fase += 0.10; Invalidate(); };
        timer.Start();
        Disposed += (_, _) => timer.Dispose();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Não pinta retângulo próprio: reproduz o fundo real do controle pai.
        // Isso elimina o bloco branco ao redor da orbe no canto do PDV.
        if (Parent is null)
        {
            e.Graphics.Clear(Color.Black);
            return;
        }

        var state = e.Graphics.Save();
        try
        {
            e.Graphics.TranslateTransform(-Left, -Top);
            var pea = new PaintEventArgs(e.Graphics, Parent.ClientRectangle);
            InvokePaintBackground(Parent, pea);
            InvokePaint(Parent, pea);
        }
        finally
        {
            e.Graphics.Restore(state);
        }
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
                new RectangleF(0, 64, Width, 22), sf);
        }
    }
}
