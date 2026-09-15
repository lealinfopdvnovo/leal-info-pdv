$ErrorActionPreference='Stop'
$p='MainForm.cs'; $t=Get-Content $p -Raw
$t=$t.Replace('double licPhase = 0;','double licPhase = 0;`r`n        string licAiState = "IDLE";')
$t=$t.Replace('Color.FromArgb(70 + (int)(pulse * 75), 0, 245, 255)','licAiState == "SPEAKING" ? Color.FromArgb(80 + (int)(pulse * 120), 255, 30, 30) : Color.FromArgb(70 + (int)(pulse * 75), 0, 245, 255)')
$t=$t.Replace('new System.Drawing.Drawing2D.LinearGradientBrush(core, Color.FromArgb(8, 105, 210), Color.FromArgb(1, 22, 74), 90f)','new System.Drawing.Drawing2D.LinearGradientBrush(core, licAiState == "SPEAKING" ? Color.FromArgb(210, 35, 35) : Color.FromArgb(8, 105, 210), licAiState == "SPEAKING" ? Color.FromArgb(75, 5, 5) : Color.FromArgb(1, 22, 74), 90f)')
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
