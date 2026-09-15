$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$text = Get-Content $path -Raw
$anchor = @'
        Controls.Add(menu);
'@
if (-not $text.Contains($anchor)) { throw 'Ponto de insercao LIC AI nao encontrado' }
$insert = @'
        Controls.Add(menu);

        // V10.202: botao LIC AI HUD centralizado, elevado e recortado sem fundo.
        var licAiButton = new Control
        {
            Size = new Size(220, 90),
            Cursor = Cursors.Hand,
            TabStop = false,
            Anchor = AnchorStyles.Bottom
        };
        var setStyle = licAiButton.GetType().GetMethod("SetStyle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        setStyle!.Invoke(licAiButton, new object[] { ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true });
        licAiButton.BackColor = Color.Transparent;

        void PositionLicAiButton()
        {
            licAiButton.BackColor = Color.Transparent;
            int centeredX = Math.Max(0, (ClientSize.Width - licAiButton.Width) / 2);
            int safeY = Math.Max(menu.Bottom + 24, ClientSize.Height - licAiButton.Height - status.Height - 70);
            licAiButton.Location = new Point(centeredX, safeY);

            using var hitPath = new System.Drawing.Drawing2D.GraphicsPath();
            float hitCut = 22f;
            hitPath.AddPolygon(new PointF[] {
                new(hitCut, 0), new(licAiButton.Width - hitCut, 0),
                new(licAiButton.Width - 1, licAiButton.Height / 2f),
                new(licAiButton.Width - hitCut, licAiButton.Height - 1),
                new(hitCut, licAiButton.Height - 1), new(0, licAiButton.Height / 2f)
            });
            licAiButton.Region?.Dispose();
            licAiButton.Region = new Region(hitPath);
            licAiButton.BringToFront();
        }

        double licPhase = 0;
        licAiButton.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            float pulse = (float)((Math.Sin(licPhase) + 1.0) / 2.0);
            var body = new RectangleF(3, 3, licAiButton.Width - 7, licAiButton.Height - 7);

            using var pathHud = new System.Drawing.Drawing2D.GraphicsPath();
            float cut = 18f;
            pathHud.AddPolygon(new PointF[] {
                new(body.Left + cut, body.Top), new(body.Right - cut, body.Top),
                new(body.Right, body.Top + body.Height / 2f),
                new(body.Right - cut, body.Bottom), new(body.Left + cut, body.Bottom),
                new(body.Left, body.Top + body.Height / 2f)
            });

            using var glowWide = new Pen(Color.FromArgb(35 + (int)(pulse * 25), 0, 210, 255), 14f);
            g.DrawPath(glowWide, pathHud);
            using var glow = new Pen(Color.FromArgb(125 + (int)(pulse * 80), 0, 235, 255), 5f);
            g.DrawPath(glow, pathHud);
            using var fill = new System.Drawing.Drawing2D.LinearGradientBrush(body, Color.FromArgb(8, 44, 82), Color.FromArgb(2, 12, 30), 90f);
            g.FillPath(fill, pathHud);
            using var edge = new Pen(Color.FromArgb(240, 55, 238, 255), 2.2f);
            g.DrawPath(edge, pathHud);

            float scanX = body.Left + 20 + (float)((Math.Sin(licPhase * .7) + 1) / 2) * (body.Width - 40);
            using var scan = new Pen(Color.FromArgb(80, 120, 250, 255), 2f);
            g.DrawLine(scan, scanX, body.Top + 8, scanX, body.Bottom - 8);

            using var dot = new SolidBrush(Color.FromArgb(80, 255, 185));
            g.FillEllipse(dot, body.Left + 18, body.Top + body.Height / 2f - 4, 8, 8);
            using var font = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Point);
            using var textBrush = new SolidBrush(Color.White);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("LIC  AI", font, textBrush, body, sf);

            using var subFont = new Font("Segoe UI", 6.8f, FontStyle.Regular, GraphicsUnit.Point);
            using var subBrush = new SolidBrush(Color.FromArgb(145, 220, 245));
            g.DrawString("ASSISTENTE INTELIGENTE", subFont, subBrush, new RectangleF(body.Left, body.Bottom - 17, body.Width, 12), sf);
        };

        licAiButton.Click += (_, _) =>
        {
            try
            {
                var licExe = Path.Combine(AppContext.BaseDirectory, "LIC-AI", "LicAi.exe");
                if (!File.Exists(licExe)) { MessageBox.Show("LIC AI nao foi encontrada nesta instalacao. Atualize o PDV e tente novamente.", "LIC AI", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = licExe, WorkingDirectory = Path.GetDirectoryName(licExe) ?? AppContext.BaseDirectory, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("Nao foi possivel abrir a LIC AI.\n\n" + ex.Message, "LIC AI", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };

        var licPulseTimer = new System.Windows.Forms.Timer { Interval = 35 };
        licPulseTimer.Tick += (_, _) => { licPhase += 0.09; licAiButton.Invalidate(); };
        Controls.Add(licAiButton);
        PositionLicAiButton();
        Resize += (_, _) => PositionLicAiButton();
        Shown += (_, _) => { PositionLicAiButton(); licPulseTimer.Start(); };
        FormClosed += (_, _) => { licPulseTimer.Stop(); licPulseTimer.Dispose(); };
'@
$text = $text.Replace($anchor, $insert)
Set-Content $path $text -Encoding UTF8
Write-Host 'LIC AI V10.202: botao HUD centralizado, elevado e sem fundo retangular aplicado.'
