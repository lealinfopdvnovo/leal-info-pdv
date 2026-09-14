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
        // V10.188: LIC AI em local fixo e sempre visivel na barra superior.
        var licAiMenu = new ToolStripMenuItem("\u2665  LIC AI  \u2665")
        {
            Alignment = ToolStripItemAlignment.Right,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(210, 0, 0),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            AutoSize = false,
            Width = 155,
            Height = 30,
            ToolTipText = "Abrir LIC AI"
        };

        licAiMenu.Click += (_, _) =>
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

        int licPulse = 0;
        bool licPulseUp = true;
        var licPulseTimer = new System.Windows.Forms.Timer { Interval = 80 };
        licPulseTimer.Tick += (_, _) =>
        {
            licPulse += licPulseUp ? 8 : -8;
            if (licPulse >= 72) { licPulse = 72; licPulseUp = false; }
            if (licPulse <= 0) { licPulse = 0; licPulseUp = true; }

            licAiMenu.BackColor = Color.FromArgb(180 + licPulse, 0, 0);
            licAiMenu.ForeColor = licPulse > 35 ? Color.White : Color.FromArgb(255, 235, 235);
        };

        menu.Items.Add(licAiMenu);
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
Write-Host 'LIC AI aplicada na barra superior, com coracao e vermelho pulsante.'
