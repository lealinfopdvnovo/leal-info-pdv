using System.Drawing.Drawing2D;

namespace LealInfoPDV;

/// <summary>
/// Orbe visual da LIA para o modo conversa.
/// Leve, offline e sem vídeo: pulsa continuamente sem consumir recursos pesados.
/// </summary>
public sealed class LiaOrbForm : Form
{
    private readonly MainForm main;
    private readonly OrbView orb = new();

    public event EventHandler? OrbClicked;

    public void SetEstado(string estado) => orb.SetEstado(estado);

    public LiaOrbForm(MainForm mainForm)
    {
        main = mainForm;
        Text = string.Empty;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(168, 168);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        MaximizeBox = false;
        MinimizeBox = false;

        var key = Color.FromArgb(1, 1, 1);
        BackColor = key;
        TransparencyKey = key;

        orb.Dock = DockStyle.Fill;
        Controls.Add(orb);
        orb.Click += (_, _) => OrbClicked?.Invoke(this, EventArgs.Empty);

        Shown += (_, _) => Posicionar();
        main.Move += (_, _) => { if (!IsDisposed) Posicionar(); };
        main.Resize += (_, _) => { if (!IsDisposed) Posicionar(); };
    }

    private void Posicionar()
    {
        var area = Screen.FromControl(main).WorkingArea;
        int x = main.Right - Width - 8;
        int y = main.Bottom - Height - 70;
        x = Math.Max(area.Left + 8, Math.Min(x, area.Right - Width - 8));
        y = Math.Max(area.Top + 8, Math.Min(y, area.Bottom - Height - 8));
        Location = new Point(x, y);
    }

    private sealed class OrbView : Control
    {
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 35 };
        private double fase;
        private string estado = "PRONTA";

        public OrbView()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(1, 1, 1);
            timer.Tick += (_, _) => { fase += 0.10; Invalidate(); };
            timer.Start();
            Disposed += (_, _) => timer.Dispose();
        }

        public void SetEstado(string novo)
        {
            estado = string.IsNullOrWhiteSpace(novo) ? "PRONTA" : novo.ToUpperInvariant();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            float pulso = (float)((Math.Sin(fase) + 1.0) / 2.0);
            float cx = Width / 2f;
            float cy = Height / 2f;
            float raio = 50f + (pulso * 7f);

            using var halo2 = new Pen(Color.FromArgb(45 + (int)(pulso * 35), 0, 130, 255), 7f);
            using var halo1 = new Pen(Color.FromArgb(95 + (int)(pulso * 65), 0, 225, 255), 3f);
            e.Graphics.DrawEllipse(halo2, cx - raio - 9, cy - raio - 9, (raio + 9) * 2, (raio + 9) * 2);
            e.Graphics.DrawEllipse(halo1, cx - raio - 2, cy - raio - 2, (raio + 2) * 2, (raio + 2) * 2);

            var rect = new RectangleF(cx - raio, cy - raio, raio * 2, raio * 2);
            using var path = new GraphicsPath();
            path.AddEllipse(rect);
            using var brush = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(245, 18, 128, 210),
                SurroundColors = new[] { Color.FromArgb(220, 0, 24, 62) }
            };
            e.Graphics.FillEllipse(brush, rect);

            using var brilho = new SolidBrush(Color.FromArgb(55 + (int)(pulso * 40), 160, 245, 255));
            e.Graphics.FillEllipse(brilho, cx - 29, cy - 36, 58, 30);

            using var f1 = new Font("Segoe UI", 22, FontStyle.Bold);
            using var f2 = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var branco = new SolidBrush(Color.White);
            using var ciano = new SolidBrush(Color.FromArgb(155, 245, 255));
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("LIA", f1, branco, new RectangleF(0, cy - 24, Width, 42), sf);
            e.Graphics.DrawString(estado, f2, ciano, new RectangleF(0, cy + 16, Width, 24), sf);
        }
    }
}
