$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$text = Get-Content $path -Raw
$anchor = @'
        Controls.Add(menu);
'@
if (-not $text.Contains($anchor)) { throw 'Ponto de insercao LIC AI nao encontrado' }
$insert = @'
        Controls.Add(menu);

        // V10.194: coracao LIC AI inspirado na referencia: cheio, organico, sem moldura e com batimento natural.
        var licHeart = new Label
        {
            Text = string.Empty,
            AutoSize = false,
            Size = new Size(126, 112),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom
        };

        void PositionLicHeart()
        {
            licHeart.Location = new Point((ClientSize.Width - licHeart.Width) / 2, ClientSize.Height - licHeart.Height - 26);
            licHeart.BringToFront();
        }

        licHeart.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            float w = licHeart.ClientSize.Width;
            float h = licHeart.ClientSize.Height;
            using var heart = new System.Drawing.Drawing2D.GraphicsPath();
            heart.StartFigure();
            heart.AddBezier(w*0.50f,h*0.91f, w*0.45f,h*0.82f, w*0.10f,h*0.62f, w*0.10f,h*0.34f);
            heart.AddBezier(w*0.10f,h*0.34f, w*0.10f,h*0.13f, w*0.27f,h*0.07f, w*0.39f,h*0.12f);
            heart.AddBezier(w*0.39f,h*0.12f, w*0.45f,h*0.15f, w*0.49f,h*0.21f, w*0.50f,h*0.26f);
            heart.AddBezier(w*0.50f,h*0.26f, w*0.51f,h*0.21f, w*0.55f,h*0.15f, w*0.61f,h*0.12f);
            heart.AddBezier(w*0.61f,h*0.12f, w*0.73f,h*0.07f, w*0.90f,h*0.13f, w*0.90f,h*0.34f);
            heart.AddBezier(w*0.90f,h*0.34f, w*0.90f,h*0.62f, w*0.55f,h*0.82f, w*0.50f,h*0.91f);
            heart.CloseFigure();
            using var fill = new SolidBrush(Color.FromArgb(225, 18, 52));
            using var shadow = new SolidBrush(Color.FromArgb(28, 150, 0, 20));
            using var highlight = new SolidBrush(Color.FromArgb(55, 255, 255, 255));
            var state = e.Graphics.Save();
            e.Graphics.TranslateTransform(0, 3);
            e.Graphics.FillPath(shadow, heart);
            e.Graphics.Restore(state);
            e.Graphics.FillPath(fill, heart);
            e.Graphics.FillEllipse(highlight, w*0.27f, h*0.22f, w*0.12f, h*0.09f);
            using var font = new Font("Segoe UI", Math.Max(12f, h * 0.145f), FontStyle.Bold);
            var textRect = new Rectangle(0, (int)(h*0.27f), (int)w, (int)(h*0.42f));
            TextRenderer.DrawText(e.Graphics, "LIC AI", font, textRect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        };

        licHeart.Click += (_, _) =>
        {
            try
            {
                var licExe = Path.Combine(AppContext.BaseDirectory, "LIC-AI", "LicAi.exe");
                if (!File.Exists(licExe)) { MessageBox.Show("LIC AI nao foi encontrada nesta instalacao. Atualize o PDV e tente novamente.", "LIC AI", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = licExe, WorkingDirectory = Path.GetDirectoryName(licExe) ?? AppContext.BaseDirectory, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("Nao foi possivel abrir a LIC AI.\n\n" + ex.Message, "LIC AI", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };

        int beatFrame = 0;
        int[] beatHeights = {104,108,114,120,126,120,114,108,104,106,111,116,111,106,104,104,104,104,104,104};
        var licPulseTimer = new System.Windows.Forms.Timer { Interval = 62 };
        licPulseTimer.Tick += (_, _) =>
        {
            int h = beatHeights[beatFrame++ % beatHeights.Length];
            int w = (int)Math.Round(h * 1.125);
            licHeart.Size = new Size(w, h);
            PositionLicHeart();
            licHeart.Invalidate();
        };

        Controls.Add(licHeart);
        PositionLicHeart();
        Resize += (_, _) => PositionLicHeart();
        Shown += (_, _) => { PositionLicHeart(); licPulseTimer.Start(); };
        FormClosed += (_, _) => { licPulseTimer.Stop(); licPulseTimer.Dispose(); };
'@
$text = $text.Replace($anchor, $insert)
Set-Content $path $text -Encoding UTF8
Write-Host 'LIC AI V10.194: coracao corrigido, LIC AI dentro e batimento cardiaco.'
