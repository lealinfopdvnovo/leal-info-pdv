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
            MessageBox.Show(this,
                "A LIA gerou a resposta, mas não conseguiu reproduzir a voz.\n\n" + ex.Message,
                "LIA - Saída de áudio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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

        using var http=new HttpClient { Timeout=TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        var requestBody=JsonSerializer.Serialize(new
        {
            model="gpt-4o-mini-tts",
            voice="coral",
            input=text,
            instructions="Fale em português do Brasil, com voz feminina natural, acolhedora, clara e profissional.",
            response_format="wav"
        });
        using var content=new StringContent(requestBody,Encoding.UTF8,"application/json");
        using var response=await http.PostAsync("https://api.openai.com/v1/audio/speech",content,cancellationToken);
        var audioBytes=await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if(!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI TTS HTTP {(int)response.StatusCode}: {Encoding.UTF8.GetString(audioBytes)}");
        if(audioBytes.Length<44) throw new InvalidDataException("A OpenAI não retornou um áudio válido.");

        using var audioStream=new MemoryStream(audioBytes,false);
        using var reader=new WaveFileReader(audioStream);
        using var output=new WaveOutEvent { DesiredLatency=120 };
        var finished=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? playbackError=null;
        output.PlaybackStopped += (_,e) => { playbackError=e.Exception; finished.TrySetResult(true); };
        output.Init(reader);
        output.Play();
        using var registration=cancellationToken.Register(() => { try { output.Stop(); } catch { } });
        var completed=await Task.WhenAny(finished.Task,Task.Delay(TimeSpan.FromMinutes(2),cancellationToken));
        if(completed!=finished.Task) throw new TimeoutException("Tempo limite da reprodução de voz excedido.");
        if(playbackError!=null) throw new InvalidOperationException("Falha no dispositivo de áudio.",playbackError);
    }

'@
$t=$t.Substring(0,$start)+$new+$t.Substring($end)
Set-Content $p $t -Encoding UTF8
Write-Host 'TTS da OpenAI V10.217 aplicado.'
