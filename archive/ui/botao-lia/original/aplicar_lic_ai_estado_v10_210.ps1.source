$ErrorActionPreference='Stop'
$p='MainForm.cs'; $t=Get-Content $p -Raw
$replacement = "double licPhase = 0;`r`n        string licAiState = `"IDLE`";"
$t=$t.Replace('double licPhase = 0;',$replacement)
$t=$t.Replace('Color.FromArgb(70 + (int)(pulse * 75), 0, 245, 255)','licAiState == "SPEAKING" ? Color.FromArgb(80 + (int)(pulse * 120), 255, 30, 30) : licAiState == "THINKING" ? Color.FromArgb(80 + (int)(pulse * 120), 255, 190, 20) : licAiState == "LISTENING" ? Color.FromArgb(70 + (int)(pulse * 120), 0, 150, 255) : Color.FromArgb(70, 0, 125, 180)')
$t=$t.Replace('new System.Drawing.Drawing2D.LinearGradientBrush(core, Color.FromArgb(8, 105, 210), Color.FromArgb(1, 22, 74), 90f)','new System.Drawing.Drawing2D.LinearGradientBrush(core, licAiState == "SPEAKING" ? Color.FromArgb(210, 35, 35) : licAiState == "THINKING" ? Color.FromArgb(215, 145, 15) : licAiState == "LISTENING" ? Color.FromArgb(8, 105, 230) : Color.FromArgb(5, 55, 105), licAiState == "SPEAKING" ? Color.FromArgb(75, 5, 5) : licAiState == "THINKING" ? Color.FromArgb(85, 45, 3) : licAiState == "LISTENING" ? Color.FromArgb(1, 22, 90) : Color.FromArgb(1, 18, 48), 90f)')
$t=$t.Replace('g.DrawString("AI", aiFont, textBrush, new RectangleF(core.Left, core.Top + 65, core.Width, 34), sf);','g.DrawString(licAiState == "THINKING" ? "..." : "AI", aiFont, textBrush, new RectangleF(core.Left, core.Top + 65, core.Width, 34), sf);')
$anchor='        var licPulseTimer = new System.Windows.Forms.Timer { Interval = 35 };'
$insert=@'
        var licStatusCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!licStatusCts.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream("LealInfoPDV.LicAiStatus", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await pipe.WaitForConnectionAsync(licStatusCts.Token);
                    using var reader = new StreamReader(pipe);
                    var state = (await reader.ReadLineAsync(licStatusCts.Token) ?? "IDLE").Trim().ToUpperInvariant();
                    if (!IsDisposed) BeginInvoke(() => { licAiState = state; licAiButton.Invalidate(); });
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(150); }
            }
        });

        var licPulseTimer = new System.Windows.Forms.Timer { Interval = 35 };
'@
if(!$t.Contains($anchor)){throw 'Timer LIC AI nao encontrado'}
$t=$t.Replace($anchor,$insert)
$t=$t.Replace('FormClosed += (_, _) => { licPulseTimer.Stop(); licPulseTimer.Dispose(); };','FormClosed += (_, _) => { licStatusCts.Cancel(); licStatusCts.Dispose(); licPulseTimer.Stop(); licPulseTimer.Dispose(); };')
Set-Content $p $t -Encoding UTF8
