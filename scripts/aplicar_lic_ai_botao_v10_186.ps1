$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$text = Get-Content $path -Raw
$anchor = @'
        Controls.Add(menu);
'@
if (-not $text.Contains($anchor)) { throw 'Ponto de insercao LIC AI nao encontrado' }
$insert = @'
        Controls.Add(menu);

        // V10.192: coracao LIC AI organico, sem moldura, centralizado embaixo e com batimento real.
        var licHeart = new Label
        {
            Text = "LIC AI",
            AutoSize = false,
            Size = new Size(104, 92),
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom
        };

        void PositionLicHeart()
        {
            licHeart.Location = new Point((ClientSize.Width - licHeart.Width) / 2, ClientSize.Height - licHeart.Height - 30);
            licHeart.BringToFront();
        }

        licHeart.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = licHeart.ClientRectangle;
            float w = r.Width, h = r.Height;
            using var pathHeart = new System.Drawing.Drawing2D.GraphicsPath();
            pathHeart.StartFigure();
            pathHeart.AddBezier(w*0.50f,h*0.88f, w*0.43f,h*0.78f, w*0.08f,h*0.56f, w*0.08f,h*0.31f);
            pathHeart.AddBezier(w*0.08f,h*0.31f, w*0.08f,h*0.12f, w*0.30f,h*0.04f, w*0.50f,h*0.24f);
            pathHeart.AddBezier(w*0.50f,h*0.24f, w*0.70f,h*0.04f, w*0.92f,h*0.12f, w*0.92f,h*0.31f);
            pathHeart.AddBezier(w*0.92f,h*0.31f, w*0.92f,h*0.56f, w*0.57f,h*0.78f, w*0.50f,h*0.88f);
            pathHeart.CloseFigure();
            using var fill = new SolidBrush(Color.FromArgb(232,10,42));
            e.Graphics.FillPath(fill,pathHeart);
            TextRenderer.DrawText(e.Graphics,"LIC AI",licHeart.Font,r,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
        };

        licHeart.Click += (_, _) =>
        {
            try
            {
                var licExe = Path.Combine(AppContext.BaseDirectory,"LIC-AI","LicAi.exe");
                if (!File.Exists(licExe)) { MessageBox.Show("LIC AI nao foi encontrada nesta instalacao. Atualize o PDV e tente novamente.","LIC AI",MessageBoxButtons.OK,MessageBoxIcon.Warning); return; }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName=licExe, WorkingDirectory=Path.GetDirectoryName(licExe) ?? AppContext.BaseDirectory, UseShellExecute=true });
            }
            catch(Exception ex) { MessageBox.Show("Nao foi possivel abrir a LIC AI.\n\n"+ex.Message,"LIC AI",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        };

        int beat = 0;
        int[] beatSizes = { 92,96,102,108,102,96,92,92,92,96,103,98,92,92,92,92 };
        var licPulseTimer = new System.Windows.Forms.Timer { Interval = 70 };
        licPulseTimer.Tick += (_, _) =>
        {
            int h = beatSizes[beat++ % beatSizes.Length];
            int w = (int)(h * 1.13);
            licHeart.Size = new Size(w,h);
            licHeart.Font = new Font("Segoe UI", Math.Max(11f,h*0.14f), FontStyle.Bold);
            PositionLicHeart();
            licHeart.Invalidate();
        };

        Controls.Add(licHeart);
        PositionLicHeart();
        Resize += (_, _) => PositionLicHeart();
        Shown += (_, _) => { PositionLicHeart(); licPulseTimer.Start(); };
        FormClosed += (_, _) => { licPulseTimer.Stop(); licPulseTimer.Dispose(); };
'@
$text = $text.Replace($anchor,$insert)
Set-Content $path $text -Encoding UTF8
Write-Host 'LIC AI: coracao organico corrigido, com nome dentro, batimento duplo, central inferior.'
