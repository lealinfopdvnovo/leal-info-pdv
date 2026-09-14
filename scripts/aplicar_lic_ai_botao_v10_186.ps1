$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$text = Get-Content $path -Raw
$anchor = @'
        Controls.Add(menu);
'@
if (-not $text.Contains($anchor)) { throw 'Ponto de insercao LIC AI nao encontrado' }
$insert = @'
        Controls.Add(menu);

        // V10.199: botao LIC AI futurista, sem fundo quadrado.
        var licAiButton = new Control
        {
            Size = new Size(132, 132),
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
            licAiButton.Location = new Point((ClientSize.Width - licAiButton.Width) / 2, ClientSize.Height - licAiButton.Height - 10);
            licAiButton.BringToFront();
        }

        double licPhase = 0;
        licAiButton.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            float pulse = (float)((Math.Sin(licPhase) + 1.0) / 2.0);
            float cx = licAiButton.ClientSize.Width / 2f;
            float cy = licAiButton.ClientSize.Height / 2f;
            float d = 92f + pulse * 4f;
            float x = cx - d / 2f;
            float y = cy - d / 2f;

            using var glow3 = new Pen(Color.FromArgb(25 + (int)(pulse * 25), 0, 180, 255), 18f);
            e.Graphics.DrawEllipse(glow3, x - 8, y - 8, d + 16, d + 16);
            using var glow2 = new Pen(Color.FromArgb(70 + (int)(pulse * 50), 0, 220, 255), 9f);
            e.Graphics.DrawEllipse(glow2, x - 4, y - 4, d + 8, d + 8);
            using var glow1 = new Pen(Color.FromArgb(210, 45, 235, 255), 3.2f);
            e.Graphics.DrawEllipse(glow1, x - 1, y - 1, d + 2, d + 2);

            using var outer = new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(x, y, d, d), Color.FromArgb(0, 235, 255), Color.FromArgb(0, 65, 190), 55f);
            e.Graphics.FillEllipse(outer, x, y, d, d);
            using var mid = new SolidBrush(Color.FromArgb(3, 18, 50));
            e.Graphics.FillEllipse(mid, x + 7, y + 7, d - 14, d - 14);
            using var inner = new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(x + 12, y + 12, d - 24, d - 24), Color.FromArgb(15, 92, 190), Color.FromArgb(1, 18, 55), 90f);
            e.Graphics.FillEllipse(inner, x + 12, y + 12, d - 24, d - 24);

            using var ring1 = new Pen(Color.FromArgb(235, 50, 220, 255), 2.0f);
            e.Graphics.DrawEllipse(ring1, x + 12, y + 12, d - 24, d - 24);
            using var ring2 = new Pen(Color.FromArgb(130, 90, 245, 255), 1.2f);
            e.Graphics.DrawEllipse(ring2, x + 18, y + 18, d - 36, d - 36);

            var arcRect = new RectangleF(x - 7, y - 7, d + 14, d + 14);
            using var arcPen = new Pen(Color.FromArgb(245, 80, 245, 255), 4.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            float spin = (float)((licPhase * 32) % 360);
            e.Graphics.DrawArc(arcPen, arcRect, spin, 72);
            e.Graphics.DrawArc(arcPen, arcRect, spin + 180, 72);

            using var gloss = new SolidBrush(Color.FromArgb(42, 255, 255, 255));
            e.Graphics.FillEllipse(gloss, x + d * .24f, y + d * .16f, d * .40f, d * .13f);

            using var font = new Font("Segoe UI", 15.5f, FontStyle.Bold, GraphicsUnit.Point);
            using var textBrush = new SolidBrush(Color.White);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("LIC AI", font, textBrush, new RectangleF(x + 8, y + 8, d - 16, d - 16), sf);
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
Write-Host 'LIC AI V10.199: botao futurista transparente com aneis neon aplicado.'
