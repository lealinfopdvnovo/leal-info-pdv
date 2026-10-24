$ErrorActionPreference='Stop'
$p='LIC-AI/MainForm.cs'
$t=Get-Content $p -Raw -Encoding UTF8

# Aguarda o Windows encerrar a gravação antes de finalizar e enviar o WAV.
$t=$t.Replace('    private DateTime _lastVoiceUtc;', '    private DateTime _lastVoiceUtc;'+[Environment]::NewLine+'    private TaskCompletionSource<bool>? _recordingStopped;')
$t=$t.Replace('            _microphone.RecordingStopped += (_, e) =>'+[Environment]::NewLine+'            {'+[Environment]::NewLine+'                _recognizing = false;', '            _recordingStopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);'+[Environment]::NewLine+'            _microphone.RecordingStopped += (_, e) =>'+[Environment]::NewLine+'            {'+[Environment]::NewLine+'                _recognizing = false;'+[Environment]::NewLine+'                _recordingStopped?.TrySetResult(true);')
$t=$t.Replace('        StopRecognition(false);'+[Environment]::NewLine+'        await SendStatusAsync("SPEAKING");', '        await StopRecognitionAsync();'+[Environment]::NewLine+'        await SendStatusAsync("SPEAKING");')

$stopAnchor='    private void StopRecognition(bool turnOff)'
$stopMethod=@'
    private async Task StopRecognitionAsync()
    {
        var stopped = _recordingStopped;
        try { _microphone?.StopRecording(); }
        catch (Exception ex) { await WriteLogAsync("Erro ao parar microfone: " + ex.Message); }
        if (stopped != null)
            await Task.WhenAny(stopped.Task, Task.Delay(1500));
        _recognizing = false;
    }

'@
if(!$t.Contains($stopAnchor)){throw 'Metodo StopRecognition nao encontrado'}
$t=$t.Replace($stopAnchor,$stopMethod+$stopAnchor)

# Garante que o sintetizador use a saída de áudio padrão do computador.
$t=$t.Replace('            _speaker = new SpeechSynthesizer();', '            _speaker = new SpeechSynthesizer();'+[Environment]::NewLine+'            _speaker.SetOutputToDefaultAudioDevice();')
$t=$t.Replace('            if(_speaker!=null) await Task.Run(()=>_speaker.Speak(text));', '            if (_speaker == null)'+[Environment]::NewLine+'            {'+[Environment]::NewLine+'                _speaker = new SpeechSynthesizer();'+[Environment]::NewLine+'                _speaker.SetOutputToDefaultAudioDevice();'+[Environment]::NewLine+'                _speaker.Volume = 100;'+[Environment]::NewLine+'            }'+[Environment]::NewLine+'            await Task.Run(()=>_speaker.Speak(text));')

# Uma interação por clique: ouvir (azul), responder (vermelho), repousar.
$old=@'
            if(_voiceMode)
            {
                await Task.Delay(250);
                _processingVoice=false;
                await SendStatusAsync("LISTENING");
                StartRecognition();
            }
'@
$new=@'
            _processingVoice=false;
            _voiceMode=false;
            _voiceButton.Text="🎙 FALAR";
            _voiceButton.BackColor=Color.FromArgb(0,125,190);
            _status.Text="Pronta para conversar";
            await SendStatusAsync("IDLE");
            if (_voiceOnly) Close();
'@
if(!$t.Contains($old)){throw 'Final do ciclo de fala nao encontrado'}
$t=$t.Replace($old,$new)

Set-Content $p $t -Encoding UTF8
Write-Host 'Ciclo de voz V10.215 aplicado.'
