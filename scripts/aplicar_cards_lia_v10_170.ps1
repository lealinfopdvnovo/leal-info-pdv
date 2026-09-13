$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$main = Get-Content $path -Raw
$start = $main.IndexOf('private void AddTool(')
$end = $main.IndexOf('private void ApplyFloatingTheme', $start)
if ($start -lt 0 -or $end -lt 0) { throw 'Metodo AddTool nao localizado' }
$before = $main.Substring(0,$start)
$after = $main.Substring($end)
$tool = @'
private void AddTool(Control parent, string text, string iconFile, Action action)
    {
        const int cardW = 92;
        const int cardH = 104;
        var card = new Panel
        {
            Width = cardW,
            Height = cardH,
            Margin = new Padding(1),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        bool hover = false;
        card.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(2, 2, card.Width - 5, card.Height - 5);
            const int radius = 20;
            using var gp = new System.Drawing.Drawing2D.GraphicsPath();
            gp.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            gp.AddArc(rect.Right-radius, rect.Y, radius, radius, 270, 90);
            gp.AddArc(rect.Right-radius, rect.Bottom-radius, radius, radius, 0, 90);
            gp.AddArc(rect.X, rect.Bottom-radius, radius, radius, 90, 90);
            gp.CloseFigure();
            using var bg = new System.Drawing.Drawing2D.LinearGradientBrush(rect,
                hover ? Color.FromArgb(20,150,220) : Color.FromArgb(8,105,175),
                Color.FromArgb(2,28,66), 90f);
            e.Graphics.FillPath(bg, gp);
            using var glow = new Pen(hover ? Color.FromArgb(210,240,255) : Color.FromArgb(75,210,255), hover ? 4f : 3f);
            e.Graphics.DrawPath(glow, gp);
            using var inner = new Pen(Color.FromArgb(105,225,250,255), 1.2f);
            var r2 = Rectangle.Inflate(rect, -4, -4);
            e.Graphics.DrawArc(inner, r2.X+7, r2.Y+5, r2.Width-14, 30, 195, 150);
        };
        var caption = new Label
        {
            Text = text.Replace("\n", " "),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            AutoEllipsis = false,
            Cursor = Cursors.Hand,
            Padding = new Padding(3)
        };
        card.Controls.Add(caption);
        void SetHover(bool on)
        {
            hover = on;
            caption.Font = new Font("Segoe UI", on ? 9.7f : 9.2f, FontStyle.Bold);
            card.Invalidate();
        }
        void Enter(object? s, EventArgs e) => SetHover(true);
        void Leave(object? s, EventArgs e)
        {
            var pt = card.PointToClient(Cursor.Position);
            if (!card.ClientRectangle.Contains(pt)) SetHover(false);
        }
        card.MouseEnter += Enter;
        card.MouseLeave += Leave;
        caption.MouseEnter += Enter;
        caption.MouseLeave += Leave;
        void Run(object? s, EventArgs e) => action();
        card.Click += Run;
        caption.Click += Run;
        parent.Controls.Add(card);
    }

'@
Set-Content $path ($before + $tool + $after) -Encoding UTF8
Write-Host 'Cards holograficos V10.170 aplicados: sem circulos, nomes centralizados.'
