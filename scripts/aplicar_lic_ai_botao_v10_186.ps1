$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$text = Get-Content $path -Raw

$anchor = @'
        Controls.Add(menu);
'@

if (-not $text.Contains($anchor)) { throw 'Ponto de insercao LIC AI nao encontrado' }

$insert = @'
        Controls.Add(menu);

        // V10.190: somente o coracao LIC AI, sem quadrado/moldura, no canto inferior esquerdo.
        var licHeart = new Label
        {
            Text = "\u2665",
            AutoSize = false,
            Size = new Size(58, 58),
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(235, 18, 45),
            Font = new Font("Segoe UI Symbol", 31, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };
        licHeart.Location = new Point(12, ClientSize.Height - licHeart.Height - 34);
        licHeart.BringToFront();
        licHeart.MouseEnter += (_, _) => licHeart.ForeColor = Color.FromArgb(255, 35, 60);
        licHeart.MouseLeave += (_, _) => licHeart.ForeColor = Color.FromArgb(235, 18, 45);
        licHeart.Click += (_, _) =>
        {
            try
            {
                var licExe = Path.Combine(AppContext.BaseDirectory, "LIC-AI", "LicAi.exe");
                if (!File.Exists(licExe))
                {
                    MessageBox.Show("LIC AI nao foi encontrada nesta instalacao. Atualize o PDV e tente novamente.", "LIC AI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = licExe,
                    WorkingDirectory = Path.GetDirectoryName(licExe) ?? AppContext.BaseDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nao foi possivel abrir a LIC AI.\n\n" + ex.Message, "LIC AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        int pulseStep = 0;
        bool pulseGrowing = true;
        var licPulseTimer = new System.Windows.Forms.Timer { Interval = 85 };
        licPulseTimer.Tick += (_, _) =>
        {
            pulseStep += pulseGrowing ? 1 : -1;
            if (pulseStep >= 7) { pulseStep = 7; pulseGrowing = false; }
            if (pulseStep <= 0) { pulseStep = 0; pulseGrowing = true; }
            float size = 28f + pulseStep * 1.05f;
            licHeart.Font = new Font("Segoe UI Symbol", size, FontStyle.Bold);
            licHeart.ForeColor = pulseStep >= 4 ? Color.FromArgb(255, 25, 52) : Color.FromArgb(220, 10, 35);
        };

        Controls.Add(licHeart);
        licHeart.BringToFront();
        Shown += (_, _) => { licHeart.BringToFront(); licPulseTimer.Start(); };
        FormClosed += (_, _) => { licPulseTimer.Stop(); licPulseTimer.Dispose(); };
'@

$text = $text.Replace($anchor, $insert)
Set-Content $path $text -Encoding UTF8
Write-Host 'LIC AI: coracao puro, sem moldura, inferior esquerdo.'
