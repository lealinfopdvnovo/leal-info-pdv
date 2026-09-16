$ErrorActionPreference='Stop'
$p='LIC-AI/MainForm.cs'
$t=Get-Content $p -Raw -Encoding UTF8
$t=$t.Replace('using Vosk;','')
if($t -notmatch 'using System.Text;'){ $t=$t.Replace('using System.Speech.Synthesis;','using System.Speech.Synthesis;'+[Environment]::NewLine+'using System.Text;') }
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
    private static readonly string VoiceLogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LealInfoConectado", "Logs");
    private static readonly string VoiceLogFile = Path.Combine(VoiceLogDirectory, "lic-ai.log");
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

    private async Task WriteLogAsync(string message)
    {
        try
        {
            Directory.CreateDirectory(VoiceLogDirectory);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(VoiceLogFile, line, new UTF8Encoding(false));
        }
        catch { }
    }

    private void StartRecognition()
    {
        if (!_voiceMode || _recognizing) return;
        try
        {
            if (WaveInEvent.DeviceCount < 1)
                throw new InvalidOperationException("O Windows não encontrou nenhum microfone conectado ou autorizado.");

            _waveWriter?.Dispose(); _waveWriter = null;
            _audioBuffer?.Dispose();
            _audioBuffer = new MemoryStream();
            _microphone?.Dispose();
            _microphone = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 100
            };
            _waveWriter = new WaveFileWriter(_audioBuffer, _microphone.WaveFormat);
            _microphone.DataAvailable += MicrophoneDataAvailable;
            _microphone.RecordingStopped += (_, e) =>
            {
                _recognizing = false;
                if (e.Exception != null)
                {
                    _ = WriteLogAsync("NAudio RecordingStopped: " + e.Exception);
                    BeginInvoke(() => MessageBox.Show(this, e.Exception.Message, "LIC ASSISTENTE AI - Microfone", MessageBoxButtons.OK, MessageBoxIcon.Error));
                }
            };
            _speechDetected = false;
            _lastVoiceUtc = DateTime.UtcNow;
            _processingVoice = false;
            _recognizing = true;
            _voiceButton.Enabled = true;
            _voiceButton.Text = "OUVINDO • CLIQUE PARA ENCERRAR";
            _voiceButton.BackColor = Color.FromArgb(0,110,230);
            _status.Text = "LIC está ouvindo...";
            _microphone.StartRecording();
            _ = WriteLogAsync("Microfone NAudio iniciado: 16000 Hz, 16-bit, mono, dispositivo 0.");
            _ = SendStatusAsync("LISTENING");
        }
        catch(Exception ex)
        {
            _voiceMode=false; _recognizing=false;
            _ = WriteLogAsync("ERRO AO INICIAR MICROFONE: " + ex);
            _ = SendStatusAsync("ERROR");
            MessageBox.Show(this, ex.Message, "LIC ASSISTENTE AI - Microfone", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void MicrophoneDataAvailable(object? sender, WaveInEventArgs e)
    {
        if(!_voiceMode || _processingVoice || _waveWriter==null) return;
        try
        {
            _waveWriter.Write(e.Buffer,0,e.BytesRecorded);
            double sum=0; int samples=e.BytesRecorded/2;
            for(int i=0;i+1<e.BytesRecorded;i+=2)
            {
                short s=(short)(e.Buffer[i]|(e.Buffer[i+1]<<8));
                double n=s/32768.0; sum+=n*n;
            }
            var rms=samples>0?Math.Sqrt(sum/samples):0;
            if(rms>0.018){_speechDetected=true;_lastVoiceUtc=DateTime.UtcNow;}
            if(_speechDetected && DateTime.UtcNow-_lastVoiceUtc>TimeSpan.FromMilliseconds(950))
            {
                _processingVoice=true;
                BeginInvoke(async ()=>await ProcessCapturedSpeechAsync());
            }
        }
        catch(Exception ex)
        {
            _processingVoice=true;
            _ = WriteLogAsync("ERRO DURANTE CAPTURA NAudio: " + ex);
            BeginInvoke(() => MessageBox.Show(this, ex.Message, "LIC ASSISTENTE AI - Áudio", MessageBoxButtons.OK, MessageBoxIcon.Error));
        }
    }

    private async Task ProcessCapturedSpeechAsync()
    {
        StopRecognition(false);
        await SendStatusAsync("SPEAKING");
        try
        {
            if(_audioBuffer == null){_processingVoice=false;if(_voiceMode)StartRecognition();return;}
            _waveWriter?.Dispose(); _waveWriter=null;
            var bytes=_audioBuffer.ToArray();
            _audioBuffer.Dispose(); _audioBuffer=null;
            if(bytes.Length<2000){_processingVoice=false;if(_voiceMode)StartRecognition();return;}

            _status.Text="Transcrevendo...";
            await WriteLogAsync($"Áudio capturado: {bytes.Length} bytes. Enviando ao Whisper.");
            var heard=await TranscribeAsync(bytes, CancellationToken.None);
            await WriteLogAsync("Whisper reconheceu: " + heard);
            if(string.IsNullOrWhiteSpace(heard)){_processingVoice=false;if(_voiceMode)StartRecognition();return;}
            if(heard.Contains("encerrar conversa",StringComparison.OrdinalIgnoreCase) || heard.Contains("parar conversa",StringComparison.OrdinalIgnoreCase))
            {
                _voiceMode=false; await SendStatusAsync("IDLE"); Close(); return;
            }

            _input.Text=heard;
            _status.Text="Você disse: "+heard;
            await SendAsync(true);
        }
        catch(Exception ex)
        {
            await WriteLogAsync("ERRO NO CICLO DE VOZ: " + ex);
            await SendStatusAsync("ERROR");
            MessageBox.Show(this, ex.Message, "LIC ASSISTENTE AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _processingVoice=false;
            if(_voiceMode) StartRecognition();
        }
    }

    private async Task<string> TranscribeAsync(byte[] wav, CancellationToken ct)
    {
        var key=(_secrets.GetApiKey() ?? string.Empty).Replace("\r",string.Empty).Replace("\n",string.Empty).Trim();
        if(string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Chave da OpenAI não configurada.");
        using var http=new HttpClient{Timeout=TimeSpan.FromMinutes(2)};
        http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        using var form=new MultipartFormDataContent();
        form.Add(new StringContent("whisper-1",Encoding.UTF8),"model");
        form.Add(new StringContent("pt",Encoding.UTF8),"language");
        using var audio=new ByteArrayContent(wav);
        audio.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        form.Add(audio,"file","fala.wav");
        using var r=await http.PostAsync("https://api.openai.com/v1/audio/transcriptions",form,ct);
        var responseBytes=await r.Content.ReadAsByteArrayAsync(ct);
        var body=Encoding.UTF8.GetString(responseBytes);
        if(!r.IsSuccessStatusCode) throw new InvalidOperationException($"Whisper HTTP {(int)r.StatusCode}: {body}");
        using var doc=JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("text").GetString()?.Trim() ?? "";
    }

    private void StopRecognition(bool turnOff)
    {
        if(turnOff)_voiceMode=false;
        try{_microphone?.StopRecording();}catch(Exception ex){_ = WriteLogAsync("Erro ao parar microfone: "+ex.Message);}
        _recognizing=false;
        if(!_voiceMode)
        {
            _voiceButton.Text="🎙 FALAR";
            _voiceButton.BackColor=Color.FromArgb(0,125,190);
            _status.Text="Pronta para conversar";
            _ = SendStatusAsync("IDLE");
        }
    }

    private async Task SpeakAndContinueAsync(string text)
    {
        _status.Text="LIC está falando...";
        _voiceButton.Text="LIC RESPONDENDO";
        _processingVoice=true;
        _pulseTimer.Start();
        await SendStatusAsync("SPEAKING");
        await WriteLogAsync("LIC respondeu: " + text);
        try
        {
            if(_speaker!=null) await Task.Run(()=>_speaker.Speak(text));
        }
        catch(Exception ex)
        {
            await WriteLogAsync("ERRO TTS: " + ex);
            MessageBox.Show(this, ex.Message, "LIC ASSISTENTE AI - Voz", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _pulseTimer.Stop();
            if(_voiceMode)
            {
                await Task.Delay(250);
                _processingVoice=false;
                await SendStatusAsync("LISTENING");
                StartRecognition();
            }
        }
    }

    private async Task SendStatusAsync(string state)
    {
        try
        {
            using var pipe=new NamedPipeClientStream(".","LealInfoPDV.LicAiStatus",PipeDirection.Out,PipeOptions.Asynchronous);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await pipe.ConnectAsync(timeout.Token);
            var data=Encoding.UTF8.GetBytes(state+"\n");
            await pipe.WriteAsync(data.AsMemory(0,data.Length),timeout.Token);
            await pipe.FlushAsync(timeout.Token);
        }
        catch(Exception ex)
        {
            await WriteLogAsync($"Falha ao enviar estado '{state}' ao PDV: {ex.Message}");
        }
    }

    private void DisposeVoice()
    {
        _voiceMode=false;
        try{_microphone?.StopRecording();}catch{}
        _microphone?.Dispose();
        _waveWriter?.Dispose();
        _audioBuffer?.Dispose();
        _speaker?.Dispose();
        _pulseTimer.Stop();
        _pulseTimer.Dispose();
        _=SendStatusAsync("IDLE");
    }

'@
$t=$t.Substring(0,$start)+$new+$t.Substring($end)
Set-Content $p $t -Encoding UTF8
