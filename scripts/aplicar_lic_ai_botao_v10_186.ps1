$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$text = Get-Content $path -Raw
$anchor = @'
        Controls.Add(menu);
'@
if (-not $text.Contains($anchor)) { throw 'Ponto de insercao LIC AI nao encontrado' }
$insert = @'
        Controls.Add(menu);

        // V10.197: abandona o coracao. Botao circular futurista LIC AI inspirado na referencia aprovada.
        var licAiButton = new Control
        {
            Size = new Size(124, 124),
            Cursor = Cursors.Hand,
            TabStop = false,
            Anchor = AnchorStyles.Bottom
        };
        var setStyle = licAiButton.GetType().GetMethod("SetStyle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        setStyle!.Invoke(licAiButton, new object[] { ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true });
        licAiButton.BackColor = BackColor;

        void PositionLicAiButton()
        {
            licAiButton.BackColor = BackColor;
            licAiButton.Location = new Point((ClientSize.Width - licAiButton.Width) / 2, ClientSize.Height - licAiButton.Height - 12);
            licAiButton.BringToFront();
        }

        double licPhase = 0;
        licAiButton.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            e.Graphics.Clear(licAiButton.BackColor);

            float pulse = (float)((Math.Sin(licPhase) + 1.0) / 2.0);
            float scale = 0.94f + pulse * 0.06f;
            float d = 94f * scale;
            float x = (licAiButton.ClientSize.Width - d) / 2f;
            float y = (licAiButton.ClientSize.Height - d) / 2f;

            using var halo2 = new Pen(Color.FromArgb(45 + (int)(pulse * 55), 0, 225, 255), 9f + pulse * 3f);
            e.Graphics.DrawEllipse(halo2, x - 5, y - 5, d + 10, d + 10);
            using var halo1 = new Pen(Color.FromArgb(155 + (int)(pulse * 90), 0, 205, 255), 4.5f);
            e.Graphics.DrawEllipse(halo1, x - 1.5f, y - 1.5f, d + 3, d + 3);

            using var outer = new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(x, y, d, d), Color.FromArgb(0, 245, 255), Color.FromArgb(0, 70, 225), 55f);
            e.Graphics.FillEllipse(outer, x, y, d, d);
            float inset = 7f;
            using var inner = new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(x + inset, y + inset, d - inset * 2, d - inset * 2), Color.FromArgb(12, 92, 210), Color.FromArgb(1, 28, 96), 90f);
            e.Graphics.FillEllipse(inner, x + inset, y + inset, d - inset * 2, d - inset * 2);

            using var ring = new Pen(Color.FromArgb(220, 95, 235, 255), 2.2f);
            e.Graphics.DrawEllipse(ring, x + 12, y + 12, d - 24, d - 24);
            using var gloss = new SolidBrush(Color.FromArgb(55, 255, 255, 255));
            e.Graphics.FillEllipse(gloss, x + d * .22f, y + d * .15f, d * .42f, d * .16f);

            using var font = new Font("Segoe UI", 16.5f * scale, FontStyle.Bold, GraphicsUnit.Point);
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
Write-Host 'LIC AI V10.197: botao circular futurista azul aplicado; coracao removido.'
