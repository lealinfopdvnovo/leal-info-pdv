$ErrorActionPreference='Stop'
$p='LIC-AI/MainForm.cs'
$t=Get-Content $p -Raw -Encoding UTF8
$t=$t.Replace('TimeSpan.FromMilliseconds(950)','TimeSpan.FromMilliseconds(550)')
$start=$t.IndexOf('    private async Task SpeakAndContinueAsync(string text)')
$end=$t.IndexOf('    private async Task SpeakWithOpenAiAsync(', $start)
if($start -lt 0 -or $end -lt 0){throw 'Bloco de fala continua da LIA nao localizado'}
$new=@'
    private async Task SpeakAndContinueAsync(string text)
    {
        _status.Text="LIA está falando...";
        _voiceButton.Text="LIA RESPONDENDO";
        _processingVoice=true;
        _pulseTimer.Start();
        await SendStatusAsync("SPEAKING");
        await WriteLogAsync("LIA respondeu: " + text);
        try
        {
            await Task.Run(() =>
            {
                using var speaker = new SpeechSynthesizer();
                speaker.SetOutputToDefaultAudioDevice();
                speaker.Volume = 100;
                speaker.Rate = 1;
                var portuguese = speaker.GetInstalledVoices().FirstOrDefault(v => v.Enabled && v.VoiceInfo.Culture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase));
                if (portuguese != null) speaker.SelectVoice(portuguese.VoiceInfo.Name);
                speaker.Speak(text);
            });
        }
        catch(Exception ex)
        {
            await WriteLogAsync("ERRO TTS LOCAL: " + ex);
            await SpeakWithOpenAiAsync(text, CancellationToken.None);
        }
        finally
        {
            _pulseTimer.Stop();
            _processingVoice=false;
            if (_voiceMode)
            {
                await Task.Delay(120);
                await SendStatusAsync("LISTENING");
                StartRecognition();
            }
            else
            {
                _voiceButton.Text="🎙 FALAR";
                _voiceButton.BackColor=Color.FromArgb(0,125,190);
                _status.Text="Pronta para conversar";
                await SendStatusAsync("IDLE");
                if (_voiceOnly) Close();
            }
        }
    }

'@
$t=$t.Substring(0,$start)+$new+$t.Substring($end)
Set-Content $p $t -Encoding UTF8
Write-Host 'Conversa continua e voz rapida da LIA V10.222 aplicadas.'
