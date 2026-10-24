$ErrorActionPreference='Stop'
$p='LIC-AI/MainForm.cs'
$t=Get-Content $p -Raw -Encoding UTF8

$start=$t.IndexOf('    private async Task SpeakAndContinueAsync(string text)')
$end=$t.IndexOf('    private async Task SendStatusAsync(string state)', $start)
if($start -lt 0 -or $end -lt 0){throw 'Bloco de fala da LIC AI nao localizado'}

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
            await SpeakWithOpenAiAsync(text, CancellationToken.None);
        }
        catch(Exception ex)
        {
            await WriteLogAsync("ERRO TTS OPENAI: " + ex);
            try
            {
                if (_speaker != null)
                {
                    _speaker.Rate=3;
                    _speaker.Volume=100;
                    await Task.Run(() => _speaker.Speak(text));
                }
                else throw;
            }
            catch
            {
                MessageBox.Show(this,
                    "A LIA gerou a resposta, mas não conseguiu reproduzir a voz.\n\n" + ex.Message,
                    "LIA - Saída de áudio",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            _pulseTimer.Stop();
            _processingVoice=false;
            _voiceMode=false;
            _voiceButton.Text="🎙 FALAR";
            _voiceButton.BackColor=Color.FromArgb(0,125,190);
            _status.Text="Pronta para conversar";
            await SendStatusAsync("IDLE");
            if (_voiceOnly) Close();
        }
    }

    private async Task SpeakWithOpenAiAsync(string text, CancellationToken cancellationToken)
    {
        var key=(_secrets.GetApiKey() ?? string.Empty).Replace("\r",string.Empty).Replace("\n",string.Empty).Trim();
        if(string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Chave da OpenAI não configurada.");

        using var http=new HttpClient { Timeout=TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        var requestBody=JsonSerializer.Serialize(new
        {
            model="tts-1",
            voice="nova",
            input=text,
            response_format="mp3",
            speed=1.15
        });
        using var content=new StringContent(requestBody,Encoding.UTF8,"application/json");
        using var response=await http.PostAsync("https://api.openai.com/v1/audio/speech",content,cancellationToken);
        var audioBytes=await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if(!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI TTS HTTP {(int)response.StatusCode}: {Encoding.UTF8.GetString(audioBytes)}");
        if(audioBytes.Length<128) throw new InvalidDataException("A OpenAI não retornou um áudio MP3 válido.");

        using var mp3Stream=new MemoryStream(audioBytes,false);
        using var reader=new Mp3FileReader(mp3Stream);
        using var output=new WaveOutEvent();
        var finished=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_,e) =>
        {
            if(e.Exception != null) finished.TrySetException(e.Exception);
            else finished.TrySetResult(true);
        };
        output.Init(reader);
        output.Play();
        using var registration=cancellationToken.Register(() =>
        {
            try { output.Stop(); } catch { }
            finished.TrySetCanceled(cancellationToken);
        });
        await finished.Task;
    }

'@
$t=$t.Substring(0,$start)+$new+$t.Substring($end)
Set-Content $p $t -Encoding UTF8
Write-Host 'TTS OpenAI tts-1/nova com NAudio assincrono aplicado.'
