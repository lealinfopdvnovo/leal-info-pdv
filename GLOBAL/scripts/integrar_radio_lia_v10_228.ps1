$ErrorActionPreference='Stop'
$p='LIC-AI/MainForm.cs'
$t=Get-Content $p -Raw -Encoding UTF8

$fields=@'
    private readonly bool _voiceOnly;
    private readonly CancellationTokenSource _licControlCts = new();
    private bool _goodbyeRunning;
'@
$t=$t.Replace('    private readonly bool _voiceOnly;', $fields.TrimEnd())

$t=$t.Replace('        LoadHistory();', '        LoadHistory();'+[Environment]::NewLine+'        _ = ListenForLicControlAsync(_licControlCts.Token);')
$t=$t.Replace('        if (!_voiceMode || _recognizing) return;', '        if (!_voiceMode || _recognizing) return;'+[Environment]::NewLine+'        _ = SendRadioCommandAsync("PAUSE_FOR_LIA");')

$old=@'
            if(heard.Contains("encerrar conversa",StringComparison.OrdinalIgnoreCase) || heard.Contains("parar conversa",StringComparison.OrdinalIgnoreCase))
            {
                _voiceMode=false; await SendStatusAsync("IDLE"); Close(); return;
            }
'@
$new=@'
            if(heard.Contains("encerrar conversa",StringComparison.OrdinalIgnoreCase) || heard.Contains("parar conversa",StringComparison.OrdinalIgnoreCase))
            {
                await HandleGoodbyeAsync(); return;
            }
'@
if(-not $t.Contains($old)){throw 'Comando de encerramento por voz nao localizado'}
$t=$t.Replace($old,$new)

$anchor='    private static async Task SendNavigationCommandAsync('
if(-not $t.Contains($anchor)){throw 'Ponto de integracao da radio nao localizado'}
$methods=@'
    private async Task<string> SendRadioCommandAsync(string command)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", "LealInfoPDV.RadioControl", PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await pipe.ConnectAsync(timeout.Token);
            using var writer = new StreamWriter(pipe, Encoding.UTF8, 1024, true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, true, 1024, true);
            await writer.WriteLineAsync(command);
            return (await reader.ReadLineAsync(timeout.Token) ?? "STOPPED").Trim().ToUpperInvariant();
        }
        catch { return "STOPPED"; }
    }

    private async Task ListenForLicControlAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream("LealInfoPDV.LicAiControl", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(pipe);
                var command = (await reader.ReadLineAsync(cancellationToken) ?? "").Trim().ToUpperInvariant();
                if (command == "REQUEST_CLOSE" && !IsDisposed)
                    BeginInvoke(async () => await HandleGoodbyeAsync());
            }
            catch (OperationCanceledException) { break; }
            catch { if (!cancellationToken.IsCancellationRequested) await Task.Delay(200, cancellationToken); }
        }
    }

    private async Task HandleGoodbyeAsync()
    {
        if (_goodbyeRunning) return;
        _goodbyeRunning = true;
        try
        {
            _voiceMode = false;
            await StopRecognitionAsync();
            var radioState = await SendRadioCommandAsync("STATE");
            if (radioState == "STOPPED") { await SendStatusAsync("IDLE"); Close(); return; }

            await Task.Delay(1000);
            const string question = "Estou encerrando por aqui, chefe! Quer que eu desligue a rádio da loja também ou deixa o som rolando?";
            await SendStatusAsync("SPEAKING");
            await Task.Run(() =>
            {
                using var speaker = new SpeechSynthesizer();
                speaker.SetOutputToDefaultAudioDevice();
                speaker.Volume = 100;
                speaker.Rate = 1;
                var pt = speaker.GetInstalledVoices().FirstOrDefault(v => v.Enabled && v.VoiceInfo.Culture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase));
                if (pt != null) speaker.SelectVoice(pt.VoiceInfo.Name);
                speaker.Speak(question);
            });

            await SendStatusAsync("LISTENING");
            var answer = await CaptureFinalAnswerAsync();
            var normalized = (answer ?? "").Trim().ToLowerInvariant();
            var turnOff = normalized == "sim" || normalized.Contains("desliga") || normalized.Contains("pode desligar");
            await SendRadioCommandAsync(turnOff ? "STOP" : "RESUME_AFTER_LIA");
            await SendStatusAsync("IDLE");
            Close();
        }
        catch
        {
            await SendRadioCommandAsync("RESUME_AFTER_LIA");
            Close();
        }
    }

    private async Task<string> CaptureFinalAnswerAsync()
    {
        if (WaveInEvent.DeviceCount < 1) { await Task.Delay(4000); return ""; }
        using var buffer = new MemoryStream();
        using var microphone = new WaveInEvent { DeviceNumber=0, WaveFormat=new WaveFormat(16000,16,1), BufferMilliseconds=100 };
        using var writer = new WaveFileWriter(buffer, microphone.WaveFormat);
        var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        microphone.DataAvailable += (_,e) => writer.Write(e.Buffer,0,e.BytesRecorded);
        microphone.RecordingStopped += (_,_) => stopped.TrySetResult(true);
        microphone.StartRecording();
        await Task.Delay(4000);
        microphone.StopRecording();
        await Task.WhenAny(stopped.Task,Task.Delay(1000));
        writer.Flush();
        writer.Dispose();
        var audio = buffer.ToArray();
        if (audio.Length < 2000) return "";
        try { return await TranscribeAsync(audio,CancellationToken.None); } catch { return ""; }
    }

'@
$t=$t.Replace($anchor,$methods+$anchor)

$t=$t.Replace('        _=SendStatusAsync("IDLE");', '        _licControlCts.Cancel();'+[Environment]::NewLine+'        _=SendRadioCommandAsync("RESUME_AFTER_LIA");'+[Environment]::NewLine+'        _=SendStatusAsync("IDLE");')

Set-Content $p $t -Encoding UTF8
Write-Host 'Radio, despedida e controle final da LIA V10.228 aplicados.'
