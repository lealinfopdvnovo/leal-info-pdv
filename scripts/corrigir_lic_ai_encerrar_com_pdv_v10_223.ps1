$ErrorActionPreference='Stop'
$p='MainForm.cs'
$t=Get-Content $p -Raw -Encoding UTF8
$old='        FormClosed += (_, _) => { licStatusCts.Cancel(); licStatusCts.Dispose(); licPulseTimer.Stop(); licPulseTimer.Dispose(); };'
$new=@'
        void EncerrarLicAiComPdv()
        {
            var licExePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "LIC-AI", "LicAi.exe"));
            foreach (var liaProcess in System.Diagnostics.Process.GetProcessesByName("LicAi"))
            {
                try
                {
                    var runningPath = liaProcess.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(runningPath) && string.Equals(Path.GetFullPath(runningPath), licExePath, StringComparison.OrdinalIgnoreCase))
                    {
                        liaProcess.Kill(true);
                        liaProcess.WaitForExit(2000);
                    }
                }
                catch { }
                finally { liaProcess.Dispose(); }
            }
        }
        FormClosing += (_, _) => EncerrarLicAiComPdv();
        FormClosed += (_, _) => { EncerrarLicAiComPdv(); licStatusCts.Cancel(); licStatusCts.Dispose(); licPulseTimer.Stop(); licPulseTimer.Dispose(); };
'@
if(-not $t.Contains($old)){throw 'Evento final da LIA nao localizado no MainForm'}
$t=$t.Replace($old,$new.TrimEnd())
Set-Content $p $t -Encoding UTF8
Write-Host 'Encerramento da LIA junto com o PDV V10.223 aplicado.'
