$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$text = Get-Content $path -Raw

$anchor = @'
        Controls.Add(menu);
'@

if (-not $text.Contains($anchor)) {
    throw 'Ponto de insercao do acesso LIC AI nao encontrado em MainForm.cs'
}

$insert = @'
        // V10.189: icone LIC AI em formato de coracao, pulsante, no canto superior direito.
        var licHeart = new ToolStripMenuItem("\u2665")
        {
            Alignment = ToolStripItemAlignment.Right,
            ForeColor = Color.FromArgb(255, 35, 55),
            BackColor = menu.BackColor,
            Font = new Font("Segoe UI Symbol", 19, FontStyle.Bold),
            AutoSize = false,
            Width = 48,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter,
            ToolTipText = "LIC AI"
        };

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
        var licPulseTimer = new System.Windows.Forms.Timer { Interval = 90 };
        licPulseTimer.Tick += (_, _) =>
        {
            pulseStep += pulseGrowing ? 1 : -1;
            if (pulseStep >= 6) { pulseStep = 6; pulseGrowing = false; }
            if (pulseStep <= 0) { pulseStep = 0; pulseGrowing = true; }

            float fontSize = 18f + (pulseStep * 0.85f);
            licHeart.Font = new Font("Segoe UI Symbol", fontSize, FontStyle.Bold);
            int red = 205 + (pulseStep * 8);
            int green = 18 + (pulseStep * 3);
            int blue = 35 + (pulseStep * 3);
            licHeart.ForeColor = Color.FromArgb(Math.Min(255, red), green, blue);
        };

        menu.Items.Add(licHeart);
        Shown += (_, _) => licPulseTimer.Start();
        FormClosed += (_, _) =>
        {
            licPulseTimer.Stop();
            licPulseTimer.Dispose();
        };

        Controls.Add(menu);
'@

$text = $text.Replace($anchor, $insert)
Set-Content $path $text -Encoding UTF8
Write-Host 'LIC AI aplicada como icone de coracao pulsante.'
