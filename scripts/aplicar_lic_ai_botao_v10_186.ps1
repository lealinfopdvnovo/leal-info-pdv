$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$text = Get-Content $path -Raw

$anchor = @'
        body.Controls.Add(mainScreenPicture);
        mainScreenPicture.SendToBack();
'@

if (-not $text.Contains($anchor)) {
    throw 'Ponto de insercao do botao LIC AI nao encontrado em MainForm.cs'
}

$insert = @'
        body.Controls.Add(mainScreenPicture);
        mainScreenPicture.SendToBack();

        // V10.187: acesso visual da LIC AI. Aplicativo continua isolado do PDV.
        var licAiButton = new Button
        {
            Text = "\u2764  LIC AI  \u2764",
            Width = 200,
            Height = 64,
            BackColor = Color.FromArgb(220, 18, 18),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TabStop = false
        };
        licAiButton.FlatAppearance.BorderSize = 3;
        licAiButton.FlatAppearance.BorderColor = Color.FromArgb(255, 120, 0);
        licAiButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 35, 10);
        licAiButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(170, 0, 0);

        void PositionLicAiButton()
        {
            licAiButton.Left = Math.Max(12, body.ClientSize.Width - licAiButton.Width - 24);
            licAiButton.Top = 22;
        }

        int licPulse = 0;
        bool licPulseUp = true;
        var licPulseTimer = new System.Windows.Forms.Timer { Interval = 75 };
        licPulseTimer.Tick += (_, _) =>
        {
            licPulse += licPulseUp ? 7 : -7;
            if (licPulse >= 70) { licPulse = 70; licPulseUp = false; }
            if (licPulse <= 0) { licPulse = 0; licPulseUp = true; }

            int red = Math.Min(255, 185 + licPulse);
            int green = 8 + licPulse / 3;
            licAiButton.BackColor = Color.FromArgb(red, green, 0);
            licAiButton.FlatAppearance.BorderColor = Color.FromArgb(255, Math.Min(190, 75 + licPulse), 0);
        };

        licAiButton.Click += (_, _) =>
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

        body.Controls.Add(licAiButton);
        licAiButton.BringToFront();
        body.Resize += (_, _) => PositionLicAiButton();
        Shown += (_, _) =>
        {
            PositionLicAiButton();
            licPulseTimer.Start();
        };
        FormClosed += (_, _) =>
        {
            licPulseTimer.Stop();
            licPulseTimer.Dispose();
        };
'@

$text = $text.Replace($anchor, $insert)
Set-Content $path $text -Encoding UTF8
Write-Host 'Botao LIC AI com coracao, vermelho pulsante, aplicado no canto superior direito.'
