$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$text = Get-Content $path -Raw
$anchor = @'
        Controls.Add(menu);
'@
if (-not $text.Contains($anchor)) { throw 'Ponto de insercao LIC AI nao encontrado' }
$insert = @'
        Controls.Add(menu);

        // V10.205: botao inteiro reposicionado abaixo do nome.
        var licAiButton = new Control
        {
            Size = new Size(210, 210),
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
            int safeY = Math.Max(menu.Bottom + 24, ClientSize.Height - licAiButton.Height - status.Height - 32);
            licAiButton.Location = new Point(centeredX, safeY);

            using var hitPath = new System.Drawing.Drawing2D.GraphicsPath();
            hitPath.AddEllipse(1, 1, licAiButton.Width - 3, licAiButton.Height - 3);
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
            float spin = (float)((licPhase * 28) % 360);
            float cx = licAiButton.Width / 2f;
            float cy = licAiButton.Height / 2f;

            using var outerFill = new System.Drawing.Drawing2D.LinearGradientBrush(
                new RectangleF(0, 0, licAiButton.Width, licAiButton.Height),
                Color.FromArgb(2, 13, 36), Color.FromArgb(0, 55, 105), 45f);
            g.FillEllipse(outerFill, 1, 1, licAiButton.Width - 3, licAiButton.Height - 3);

            using var halo = new Pen(Color.FromArgb(35 + (int)(pulse * 35), 0, 225, 255), 18f);
            g.DrawEllipse(halo, 14, 14, licAiButton.Width - 29, licAiButton.Height - 29);
            using var outerRing = new Pen(Color.FromArgb(210, 20, 165, 255), 2.2f);
            g.DrawEllipse(outerRing, 9, 9, licAiButton.Width - 19, licAiButton.Height - 19);
            using var dotted = new Pen(Color.FromArgb(150, 45, 205, 255), 1.3f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawEllipse(dotted, 18, 18, licAiButton.Width - 37, licAiButton.Height - 37);

            for (int i = 0; i < 36; i++)
            {
                double a = (i * 10 + spin) * Math.PI / 180.0;
                float r1 = 78f, r2 = i % 3 == 0 ? 91f : 86f;
                var p1 = new PointF(cx + (float)Math.Cos(a) * r1, cy + (float)Math.Sin(a) * r1);
                var p2 = new PointF(cx + (float)Math.Cos(a) * r2, cy + (float)Math.Sin(a) * r2);
                using var tick = new Pen(Color.FromArgb(i % 3 == 0 ? 210 : 105, 30, 205, 255), i % 3 == 0 ? 2f : 1f);
                g.DrawLine(tick, p1, p2);
            }

            var arcRect = new RectangleF(27, 27, licAiButton.Width - 55, licAiButton.Height - 55);
            using var arc = new Pen(Color.FromArgb(235, 0, 238, 255), 5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawArc(arc, arcRect, spin, 82);
            g.DrawArc(arc, arcRect, spin + 180, 82);

            var core = new RectangleF(47, 47, licAiButton.Width - 95, licAiButton.Height - 95);
            using var coreGlow = new Pen(Color.FromArgb(70 + (int)(pulse * 75), 0, 245, 255), 15f);
            g.DrawEllipse(coreGlow, core);
            using var coreFill = new System.Drawing.Drawing2D.LinearGradientBrush(core, Color.FromArgb(8, 105, 210), Color.FromArgb(1, 22, 74), 90f);
            g.FillEllipse(coreFill, core);
            using var coreEdge = new Pen(Color.FromArgb(245, 40, 245, 255), 3f);
            g.DrawEllipse(coreEdge, core);

            using var textBrush = new SolidBrush(Color.White);
            using var subBrush = new SolidBrush(Color.FromArgb(135, 240, 255));
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            using var licFont = new Font("Segoe UI", 20f, FontStyle.Bold, GraphicsUnit.Point);
            g.DrawString("LIC", licFont, textBrush, new RectangleF(core.Left, core.Top + 9, core.Width, 34), sf);

            using var subFont = new Font("Segoe UI", 7.2f, FontStyle.Bold, GraphicsUnit.Point);
            g.DrawString("ASSISTENTE", subFont, subBrush, new RectangleF(core.Left, core.Top + 46, core.Width, 15), sf);

            using var aiFont = new Font("Segoe UI", 20f, FontStyle.Bold, GraphicsUnit.Point);
            g.DrawString("AI", aiFont, textBrush, new RectangleF(core.Left, core.Top + 65, core.Width, 34), sf);
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
Write-Host 'LIC AI V10.205: botao deslocado para baixo e centralizado entre o nome e a borda.'
