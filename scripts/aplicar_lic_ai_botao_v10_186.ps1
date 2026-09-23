$ErrorActionPreference = 'Stop'
$path = 'MainForm.cs'
$text = Get-Content $path -Raw
if ($text.Contains('btnAssistenteAI')) {
    Write-Host 'Botao LIC AI ja esta aplicado.'
    exit 0
}
$anchorMatch = [regex]::Match($text, 'Controls\.Add\(menu\);')
if (-not $anchorMatch.Success) { throw 'Ponto de insercao LIC AI nao encontrado' }
$anchor = $anchorMatch.Value.TrimEnd("`r", "`n")
$insert = @'
        Controls.Add(menu);

        // LIC ASSISTENTE AI: controle criado em runtime; o evento e vinculado explicitamente aqui.
        var licAiButton = new Control
        {
            Name = "btnAssistenteAI",
            Size = new Size(210, 210),
            Cursor = Cursors.Hand,
            TabStop = true,
            Anchor = AnchorStyles.Bottom,
            Enabled = true,
            Visible = true
        };
        var setStyle = licAiButton.GetType().GetMethod("SetStyle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        setStyle!.Invoke(licAiButton, new object[] { ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true });
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
            using var outerFill = new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(0, 0, licAiButton.Width, licAiButton.Height), Color.FromArgb(2, 13, 36), Color.FromArgb(0, 55, 105), 45f);
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
            g.DrawArc(arc, arcRect, spin, 82); g.DrawArc(arc, arcRect, spin + 180, 82);
            var core = new RectangleF(47, 47, licAiButton.Width - 95, licAiButton.Height - 95);
            using var coreGlow = new Pen(Color.FromArgb(70 + (int)(pulse * 75), 0, 245, 255), 15f); g.DrawEllipse(coreGlow, core);
            using var coreFill = new System.Drawing.Drawing2D.LinearGradientBrush(core, Color.FromArgb(8, 105, 210), Color.FromArgb(1, 22, 74), 90f); g.FillEllipse(coreFill, core);
            using var coreEdge = new Pen(Color.FromArgb(245, 40, 245, 255), 3f); g.DrawEllipse(coreEdge, core);
            using var textBrush = new SolidBrush(Color.White); using var subBrush = new SolidBrush(Color.FromArgb(135, 240, 255));
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var licFont = new Font("Segoe UI", 20f, FontStyle.Bold, GraphicsUnit.Point); g.DrawString("LIC", licFont, textBrush, new RectangleF(core.Left, core.Top + 9, core.Width, 34), sf);
            using var subFont = new Font("Segoe UI", 7.2f, FontStyle.Bold, GraphicsUnit.Point); g.DrawString("ASSISTENTE", subFont, subBrush, new RectangleF(core.Left, core.Top + 46, core.Width, 15), sf);
            using var aiFont = new Font("Segoe UI", 20f, FontStyle.Bold, GraphicsUnit.Point); g.DrawString("AI", aiFont, textBrush, new RectangleF(core.Left, core.Top + 65, core.Width, 34), sf);
        };

        int licAiInitializing = 0;

        async void btnAssistenteAI_Click(object? sender, EventArgs e)
        {
            if (System.Threading.Interlocked.Exchange(ref licAiInitializing, 1) == 1)
                return;

            var licExe = Path.Combine(AppContext.BaseDirectory, "LIC-AI", "LicAi.exe");
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LealInfoConectado",
                "Logs",
                "lic-ai-launcher.log");

            try
            {
                licAiButton.Enabled = false;
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                await File.AppendAllTextAsync(logPath,
                    $"{DateTime.Now:dd/MM/yyyy HH:mm:ss} - Clique recebido. Executavel: {licExe}{Environment.NewLine}");

                if (!File.Exists(licExe))
                {
                    throw new FileNotFoundException(
                        "O executavel da LIC AI nao foi encontrado nesta instalacao.",
                        licExe);
                }

                // Processos travados sao encerrados fora da thread visual para o PDV nao congelar.
                await Task.Run(() =>
                {
                    foreach (var stale in System.Diagnostics.Process.GetProcessesByName("LicAi"))
                    {
                        try
                        {
                            var runningPath = stale.MainModule?.FileName;
                            if (string.Equals(runningPath, licExe, StringComparison.OrdinalIgnoreCase))
                            {
                                stale.Kill(true);
                                stale.WaitForExit(1500);
                            }
                        }
                        catch (Exception staleError)
                        {
                            try
                            {
                                File.AppendAllText(logPath,
                                    $"{DateTime.Now:dd/MM/yyyy HH:mm:ss} - Falha ao encerrar instancia anterior: {staleError}{Environment.NewLine}");
                            }
                            catch { }
                        }
                        finally { stale.Dispose(); }
                    }
                });

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = licExe,
                    Arguments = "--voice",
                    WorkingDirectory = Path.GetDirectoryName(licExe) ?? AppContext.BaseDirectory,
                    UseShellExecute = true
                };
                var process = System.Diagnostics.Process.Start(psi);
                if (process == null) throw new InvalidOperationException("O Windows nao iniciou o processo LicAi.exe.");

                // Detecta executavel que abre e fecha imediatamente sem bloquear a interface.
                await Task.Delay(600);
                if (process.HasExited)
                {
                    var exitCode = process.ExitCode;
                    process.Dispose();
                    throw new InvalidOperationException(
                        $"A LIC AI abriu e encerrou imediatamente. Codigo de saida: {exitCode}.");
                }

                process.EnableRaisingEvents = true;
                process.Exited += (_, _) =>
                {
                    try
                    {
                        File.AppendAllText(logPath,
                            $"{DateTime.Now:dd/MM/yyyy HH:mm:ss} - LIC AI encerrada. Codigo: {process.ExitCode}{Environment.NewLine}");
                    }
                    catch { }
                    finally { process.Dispose(); }
                };

                await File.AppendAllTextAsync(logPath,
                    $"{DateTime.Now:dd/MM/yyyy HH:mm:ss} - LIC AI iniciada. PID: {process.Id}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                    await File.AppendAllTextAsync(logPath,
                        $"{DateTime.Now:dd/MM/yyyy HH:mm:ss} - ERRO: {ex}{Environment.NewLine}");
                }
                catch { }

                MessageBox.Show(
                    "Nao foi possivel abrir a LIC AI.\n\n" +
                    "Mensagem: " + ex.Message + "\n\n" +
                    "Tipo: " + ex.GetType().FullName + "\n\n" +
                    "Executavel esperado:\n" + licExe + "\n\n" +
                    "Log de diagnostico:\n" + logPath + "\n\n" +
                    "Detalhes tecnicos:\n" + ex,
                    "Erro da LIC ASSISTENTE AI",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                // O botao nunca permanece travado ou desabilitado depois de uma falha.
                licAiButton.Enabled = true;
                licAiButton.Focus();
                System.Threading.Interlocked.Exchange(ref licAiInitializing, 0);
            }
        }

        // Vinculacao explicita do EventHandler; nao depende de MainForm.Designer.cs.
        licAiButton.Click += btnAssistenteAI_Click;
        licAiButton.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                e.SuppressKeyPress = true;
                btnAssistenteAI_Click(licAiButton, EventArgs.Empty);
                await Task.Yield();
            }
        };

        var licPulseTimer = new System.Windows.Forms.Timer { Interval = 35 };
        licPulseTimer.Tick += (_, _) => { licPhase += 0.09; licAiButton.Invalidate(); };
        Controls.Add(licAiButton);
        PositionLicAiButton();
        Resize += (_, _) => PositionLicAiButton();
        Shown += (_, _) => { PositionLicAiButton(); licAiButton.BringToFront(); licPulseTimer.Start(); };
        FormClosed += (_, _) => { licPulseTimer.Stop(); licPulseTimer.Dispose(); };
'@
$insert = $insert.Replace('        Controls.Add(menu);', $anchor)
$text = $text.Replace($anchor, $insert)
Set-Content $path $text -Encoding UTF8
Write-Host 'LIC AI: clique vinculado explicitamente com diagnostico visual e inicializacao assincrona.'
