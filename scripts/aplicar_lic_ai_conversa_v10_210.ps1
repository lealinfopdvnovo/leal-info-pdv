$ErrorActionPreference='Stop'
$p='LIC-AI/MainForm.cs'
$t=Get-Content $p -Raw
$t=$t.Replace('using Vosk;','')
$t=$t.Replace('    private Model? _voiceModel;','')
$t=$t.Replace('    private VoskRecognizer? _voiceRecognizer;','')
$fields=@'
    private WaveInEvent? _microphone;
    private WaveFileWriter? _waveWriter;
    private MemoryStream? _audioBuffer;
    private DateTime _lastVoiceUtc;
    private bool _speechDetected;
    private readonly System.Windows.Forms.Timer _pulseTimer = new() { Interval = 70 };
    private double _pulsePhase;
'@
$t=$t.Replace('    private WaveInEvent? _microphone;',$fields.TrimEnd())
$build=@'
        BuildUi();
        _pulseTimer.Tick += (_, _) => { if (_processingVoice) { _pulsePhase += .28; var v = 125 + (int)(Math.Sin(_pulsePhase) * 65); _voiceButton.BackColor = Color.FromArgb(Math.Clamp(v,60,230), 25, 25); } };
'@
$t=$t.Replace('        BuildUi();',$build.TrimEnd())
$start=$t.IndexOf('    private async Task EnsureVoiceModelAsync()')
$end=$t.IndexOf('    private static async Task SendNavigationCommandAsync(', $start)
if($start -lt 0 -or $end -lt 0){throw 'Bloco de voz nao localizado'}
$new=@'
    private Task EnsureVoiceModelAsync() => Task.CompletedTask;

    private void StartRecognition()
    {
        if (!_voiceMode || _recognizing) return;
        try
        {
            if (WaveInEvent.DeviceCount < 1) throw new InvalidOperationException("O Windows não encontrou nenhum microfone conectado.");
            _audioBuffer?.Dispose();
            _audioBuffer = new MemoryStream();
            _waveWriter = new WaveFileWriter(_audioBuffer, new WaveFormat(16000, 16, 1));
            _microphone?.Dispose();
            _microphone = new WaveInEvent { DeviceNumber = 0, WaveFormat = new WaveFormat(16000,16,1), BufferMilliseconds = 100 };
            _microphone.DataAvailable += MicrophoneDataAvailable;
            _microphone.RecordingStopped += (_, _) => _recognizing = false;
            _speechDetected = false; _lastVoiceUtc = DateTime.UtcNow; _processingVoice = false; _recognizing = true;
            _voiceButton.Enabled = true; _voiceButton.Text = "OUVINDO • CLIQUE PARA ENCERRAR"; _voiceButton.BackColor = Color.FromArgb(0,110,230);
            _status.Text = "LIC está ouvindo...";
            _microphone.StartRecording();
            _ = SendStatusAsync("LISTENING");
        }
        catch(Exception ex)
        {
            _voiceMode=false; _recognizing=false;
            MessageBox.Show(this,"Não foi possível ativar o microfone.\n\n"+ex.Message,"LIC ASSISTENTE AI",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }

    private void MicrophoneDataAvailable(object? sender, WaveInEventArgs e)
    {
        if(!_voiceMode || _processingVoice || _waveWriter==null) return;
        _waveWriter.Write(e.Buffer,0,e.BytesRecorded); _waveWriter.Flush();
        double sum=0; int samples=e.BytesRecorded/2;
        for(int i=0;i+1<e.BytesRecorded;i+=2){short s=(short)(e.Buffer[i]|(e.Buffer[i+1]<<8)); double n=s/32768.0; sum+=n*n;}
        var rms=samples>0?Math.Sqrt(sum/samples):0;
        if(rms>0.018){_speechDetected=true;_lastVoiceUtc=DateTime.UtcNow;}
        if(_speechDetected && DateTime.UtcNow-_lastVoiceUtc>TimeSpan.FromMilliseconds(950))
        {
            _processingVoice=true;
            BeginInvoke(async ()=>await ProcessCapturedSpeechAsync());
        }
    }

    private async Task ProcessCapturedSpeechAsync()
    {
        StopRecognition(false);
        await SendStatusAsync("SPEAKING");
        try
        {
            _waveWriter?.Flush();
            if(_audioBuffer == null){_processingVoice=false;StartRecognition();return;}
            var bytes=_audioBuffer.ToArray();
            _waveWriter?.Dispose(); _waveWriter=null; _audioBuffer=null;
            if(bytes.Length<2000){_processingVoice=false;StartRecognition();return;}
            _status.Text="Transcrevendo...";
            var heard=await TranscribeAsync(bytes, CancellationToken.None);
            if(string.IsNullOrWhiteSpace(heard)){_processingVoice=false;StartRecognition();return;}
            if(heard.Contains("encerrar conversa",StringComparison.OrdinalIgnoreCase)) { _voiceMode=false; await SendStatusAsync("IDLE"); Close(); return; }
            _input.Text=heard; _status.Text="Você disse: "+heard;
            await SendAsync(true);
        }
        catch(Exception ex)
        {
            MessageBox.Show(this,"Erro na conversa por voz.\n\n"+ex.Message,"LIC ASSISTENTE AI",MessageBoxButtons.OK,MessageBoxIcon.Error);
            _processingVoice=false; if(_voiceMode) StartRecognition();
        }
    }

    private async Task<string> TranscribeAsync(byte[] wav, CancellationToken ct)
    {
        var key=_secrets.GetApiKey(); if(string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Chave da OpenAI não configurada.");
        using var http=new HttpClient{Timeout=TimeSpan.FromMinutes(2)}; http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        using var form=new MultipartFormDataContent(); form.Add(new StringContent("whisper-1"),"model"); form.Add(new StringContent("pt"),"language");
        var audio=new ByteArrayContent(wav); audio.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav"); form.Add(audio,"file","fala.wav");
        using var r=await http.PostAsync("https://api.openai.com/v1/audio/transcriptions",form,ct); var body=await r.Content.ReadAsStringAsync(ct); if(!r.IsSuccessStatusCode) throw new InvalidOperationException("Whisper: "+body);
        using var doc=JsonDocument.Parse(body); return doc.RootElement.GetProperty("text").GetString()?.Trim() ?? "";
    }

    private void StopRecognition(bool turnOff)
    {
        if(turnOff)_voiceMode=false;
        try{_microphone?.StopRecording();}catch{}
        _recognizing=false;
        if(!_voiceMode){_voiceButton.Text="🎙 FALAR";_voiceButton.BackColor=Color.FromArgb(0,125,190);_status.Text="Pronta para conversar";_ = SendStatusAsync("IDLE");}
    }

    private async Task SpeakAndContinueAsync(string text)
    {
        _status.Text="LIC está falando..."; _voiceButton.Text="LIC RESPONDENDO"; _processingVoice=true; _pulseTimer.Start(); await SendStatusAsync("SPEAKING");
        if(_speaker!=null){try{await Task.Run(()=>_speaker.Speak(text));}catch{}}
        _pulseTimer.Stop();
        if(_voiceMode){await Task.Delay(250);_processingVoice=false;StartRecognition();}
    }

    private async Task SendStatusAsync(string state)
    {
        try{using var p=new NamedPipeClientStream(".","LealInfoPDV.LicAiStatus",PipeDirection.Out,PipeOptions.Asynchronous);await p.ConnectAsync(300);await using var w=new StreamWriter(p){AutoFlush=true};await w.WriteLineAsync(state);}catch{}
    }

    private void DisposeVoice()
    {
        _voiceMode=false; try{_microphone?.StopRecording();}catch{} _microphone?.Dispose(); _waveWriter?.Dispose(); _audioBuffer?.Dispose(); _speaker?.Dispose(); _pulseTimer.Stop(); _pulseTimer.Dispose(); _=SendStatusAsync("IDLE");
    }

'@
$t=$t.Substring(0,$start)+$new+$t.Substring($end)
Set-Content $p $t -Encoding UTF8
