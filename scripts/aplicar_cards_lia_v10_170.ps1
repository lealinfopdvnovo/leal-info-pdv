$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$main = Get-Content $path -Raw

# 1) Cards superiores: sem circulos/arcos antigos, brilho holografico vivo e pulsacao suave.
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
        int pulse = 0;
        bool pulseUp = true;
        var pulseTimer = new System.Windows.Forms.Timer { Interval = 70 };

        card.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int inset = Math.Max(1, 3 - pulse / 4);
            var rect = new Rectangle(inset, inset, card.Width - inset * 2 - 1, card.Height - inset * 2 - 1);
            const int radius = 20;
            using var gp = new System.Drawing.Drawing2D.GraphicsPath();
            gp.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            gp.AddArc(rect.Right-radius, rect.Y, radius, radius, 270, 90);
            gp.AddArc(rect.Right-radius, rect.Bottom-radius, radius, radius, 0, 90);
            gp.AddArc(rect.X, rect.Bottom-radius, radius, radius, 90, 90);
            gp.CloseFigure();

            int lift = pulse * 5;
            using var bg = new System.Drawing.Drawing2D.LinearGradientBrush(rect,
                hover ? Color.FromArgb(22, 170, 235) : Color.FromArgb(8, 115 + lift, 180 + lift),
                Color.FromArgb(2, 28, 66), 90f);
            e.Graphics.FillPath(bg, gp);

            int alpha = Math.Min(255, 105 + pulse * 18 + (hover ? 45 : 0));
            using var glow = new Pen(Color.FromArgb(alpha, 80, 225, 255), hover ? 4.5f : 3.2f + pulse * 0.12f);
            e.Graphics.DrawPath(glow, gp);

            var innerRect = Rectangle.Inflate(rect, -4, -4);
            using var innerPath = new System.Drawing.Drawing2D.GraphicsPath();
            innerPath.AddArc(innerRect.X, innerRect.Y, radius - 4, radius - 4, 180, 90);
            innerPath.AddArc(innerRect.Right-(radius-4), innerRect.Y, radius - 4, radius - 4, 270, 90);
            innerPath.AddArc(innerRect.Right-(radius-4), innerRect.Bottom-(radius-4), radius - 4, radius - 4, 0, 90);
            innerPath.AddArc(innerRect.X, innerRect.Bottom-(radius-4), radius - 4, radius - 4, 90, 90);
            innerPath.CloseFigure();
            using var innerGlow = new Pen(Color.FromArgb(70 + pulse * 10, 210, 250, 255), 1.2f);
            e.Graphics.DrawPath(innerGlow, innerPath);
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

        pulseTimer.Tick += (_, _) =>
        {
            pulse += pulseUp ? 1 : -1;
            if (pulse >= 7) { pulse = 7; pulseUp = false; }
            if (pulse <= 0) { pulse = 0; pulseUp = true; }
            card.Invalidate();
        };
        pulseTimer.Start();

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
        card.Disposed += (_, _) => pulseTimer.Dispose();
        parent.Controls.Add(card);
    }

'@
$main = $before + $tool + $after

# 2) Remove o quadro antigo Monitor de Estoque da tela principal.
$main = $main.Replace('        body.Controls.Add(monitor);', '        // V10.171: monitor antigo removido da tela principal.')
$main = $main.Replace('        monitor.BringToFront();', '        // monitor antigo nao e mais exibido.')

# 3) Restaura o tema Verde Texturizado no seletor e na logica de temas.
$greenCase = @'
                case "Verde Texturizado":
                    bg = Color.FromArgb(8, 45, 34);
                    headerBg = Color.FromArgb(10, 92, 63);
                    accent = Color.FromArgb(32, 190, 118);
                    accentHover = Color.FromArgb(72, 225, 150);
                    leftBg = Color.FromArgb(18, 105, 72);
                    rightBg = Color.FromArgb(232, 248, 239);
                    fieldBg = Color.FromArgb(250, 255, 252);
                    textDark = Color.FromArgb(12, 65, 45);
                    soft = Color.FromArgb(205, 238, 220);
                    secondary = Color.FromArgb(38, 125, 86);
                    break;

'@
$anchor = '                case "PDV Rosa":'
if ($main.Contains($anchor) -and -not $main.Contains('case "Verde Texturizado":')) {
    $main = $main.Replace($anchor, $greenCase + $anchor)
}

# Textura verde profissional usando o gerador ja existente.
$textureAnchor = '            if (theme == "PDV Rosa")'
$greenTexture = @'
            if (theme == "Verde Texturizado")
            {
                SetTexture(f, Color.FromArgb(8, 58, 42), Color.FromArgb(18, 118, 78), Color.FromArgb(70, 235, 155), false, 1711);
                SetTexture(body, Color.FromArgb(10, 62, 44), Color.FromArgb(20, 112, 76), Color.FromArgb(70, 235, 155), false, 1712);
                SetTexture(left, Color.FromArgb(16, 92, 62), Color.FromArgb(9, 58, 42), Color.FromArgb(90, 245, 170), false, 1713);
            }

'@
if ($main.Contains($textureAnchor) -and -not $main.Contains('SetTexture(f, Color.FromArgb(8, 58, 42)')) {
    $main = $main.Replace($textureAnchor, $greenTexture + $textureAnchor)
}

# 4) PDV Rosa: letras pretas, como no padrao aprovado anteriormente.
$main = $main.Replace('lbl.ForeColor = theme == "Clean Pro" ? textDark : Color.White;', 'lbl.ForeColor = (theme == "Clean Pro" || theme == "PDV Rosa") ? (theme == "PDV Rosa" ? Color.Black : textDark) : Color.White;')

# 5) Adiciona o card Verde Texturizado no espaco livre do seletor.
$themeAnchor = '            options.Controls.Add(ThemeCard("PDV Rosa", "Rosé texturizado + vinho acetinado", Color.FromArgb(125, 20, 86), Color.FromArgb(255, 72, 165)), 0, 2);'
$greenCard = '            options.Controls.Add(ThemeCard("Verde Texturizado", "Verde profundo + textura acetinada", Color.FromArgb(18, 105, 72), Color.FromArgb(72, 225, 150)), 1, 2);'
if ($main.Contains($themeAnchor) -and -not $main.Contains('ThemeCard("Verde Texturizado"')) {
    $main = $main.Replace($themeAnchor, $themeAnchor + "`r`n" + $greenCard)
}

Set-Content $path $main -Encoding UTF8
Write-Host 'V10.171 aplicada: cards pulsantes e brilhantes, monitor removido, verde restaurado e rosa com letras pretas.'
