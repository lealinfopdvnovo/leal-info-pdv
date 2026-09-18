using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;
using System.Threading.Channels;
using NAudio.Wave;

namespace LicAi.Core;

/// <summary>Conexao persistente de voz-a-voz com a OpenAI Realtime API.</summary>
public sealed class OpenAiRealtimeConnection : IAsyncDisposable
{
    private const string Model = "gpt-realtime-2.1";
    private const int OutputSampleRate = 24000;
    private const int PcmBytesPerSecond = OutputSampleRate * 2; // PCM16 mono
    private const int StartupBufferBytes = PcmBytesPerSecond * 300 / 1000;
    private static readonly Uri Endpoint = new($"wss://api.openai.com/v1/realtime?model={Model}");
    private readonly Func<string?> _apiKeyProvider;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _lifecycleLock = new();
    private readonly object _playbackClockLock = new();
    private readonly Channel<byte[]> _microphoneQueue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(24)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });
    // Saida de audio: FIFO sem limite e sem politica de descarte. Cada delta recebido
    // precisa ser reproduzido integralmente e na ordem em que chegou.
    private readonly Channel<byte[]> _speakerQueue = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _sessionCts;
    private WaveInEvent? _microphone;
    private WaveOutEvent? _speaker;
    private BufferedWaveProvider? _speakerBuffer;
    private Task? _receiveTask;
    private Task? _microphoneSendTask;
    private Task? _speakerPlaybackTask;
    private CancellationTokenSource? _playbackGuardCts;
    private bool _disposed;
    private int _responseActive;
    private int _suppressMicrophone;
    private DateTime _playbackEndsUtc = DateTime.MinValue;

    public event Action? Connected;
    public event Action? Listening;
    public event Action? Speaking;
    public event Action? Idle;
    public event Action<string>? Transcript;
    public event Action<string>? Error;
    public event Func<string, Task<string>>? NavigationRequested;

    public bool IsConnected => _socket?.State == WebSocketState.Open;
    public bool IsCapturing => _microphone != null;

    public OpenAiRealtimeConnection(Func<string?> apiKeyProvider) =>
        _apiKeyProvider = apiKeyProvider;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsConnected) return;

        var key = NormalizeKey(_apiKeyProvider());
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("A chave da OpenAI ainda nao foi configurada.");

        CancellationTokenSource sessionCts;
        ClientWebSocket socket;
        lock (_lifecycleLock)
        {
            if (IsConnected) return;
            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sessionCts = _sessionCts;
            socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Authorization", $"Bearer {key}");
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            _socket = socket;
        }

        await socket.ConnectAsync(Endpoint, sessionCts.Token).ConfigureAwait(false);
        InitializeSpeaker();
        while (_speakerQueue.Reader.TryRead(out _)) { }
        _speakerPlaybackTask = SpeakerPlaybackLoopAsync(sessionCts.Token);
        _receiveTask = ReceiveLoopAsync(sessionCts.Token);
        _microphoneSendTask = MicrophoneSendLoopAsync(sessionCts.Token);
        await SendSessionUpdateAsync(sessionCts.Token).ConfigureAwait(false);
        Connected?.Invoke();
        Idle?.Invoke();
    }

    private async Task SendSessionUpdateAsync(CancellationToken cancellationToken)
    {
        var session = new
        {
            type = "session.update",
            session = new
            {
                type = "realtime",
                model = Model,
                output_modalities = new[] { "text" },
                instructions = SystemPrompt,
                audio = new
                {
                    input = new
                    {
                        format = new { type = "audio/pcm", rate = 24000 },
                        turn_detection = new
                        {
                            type = "server_vad",
                            threshold = 0.45,
                            prefix_padding_ms = 250,
                            silence_duration_ms = 420,
                            create_response = true,
                            interrupt_response = true
                        }
                    }
                },
                tools = new object[]
                {
                    new
                    {
                        type = "function",
                        name = "abrir_tela",
                        description = "Abre uma tela do LEAL INFO PDV somente quando o operador pedir explicitamente.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                tela = new
                                {
                                    type = "string",
                                    @enum = AllowedScreens
                                }
                            },
                            required = new[] { "tela" },
                            additionalProperties = false
                        }
                    },
                    new
                    {
                        type = "function",
                        name = "fechar_tela",
                        description = "Fecha somente a janela ou modal que esta em primeiro plano. Use quando o operador disser fechar tela ou fechar janela.",
                        parameters = new
                        {
                            type = "object",
                            properties = new { },
                            additionalProperties = false
                        }
                    }
                },
                tool_choice = "auto"
            }
        };
        await SendJsonAsync(session, cancellationToken).ConfigureAwait(false);
    }

    public async Task StartMicrophoneAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        if (_microphone != null) return;

        await BeginOperatorSpeechAsync(cancellationToken).ConfigureAwait(false);
        var waveIn = new WaveInEvent
        {
            DeviceNumber = 0,
            WaveFormat = new WaveFormat(24000, 16, 1),
            BufferMilliseconds = 100,
            NumberOfBuffers = 3
        };
        waveIn.DataAvailable += OnMicrophoneDataAvailable;
        waveIn.RecordingStopped += OnMicrophoneStopped;
        _microphone = waveIn;
        waveIn.StartRecording();
        Listening?.Invoke();
    }

    public Task StopMicrophoneAsync()
    {
        var waveIn = Interlocked.Exchange(ref _microphone, null);
        if (waveIn == null) return Task.CompletedTask;
        waveIn.DataAvailable -= OnMicrophoneDataAvailable;
        waveIn.RecordingStopped -= OnMicrophoneStopped;
        try { waveIn.StopRecording(); } catch { }
        waveIn.Dispose();
        Idle?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>Corta a fala atual e prepara imediatamente um novo turno do operador.</summary>
    public async Task BeginOperatorSpeechAsync(CancellationToken cancellationToken = default)
    {
        CancelPlaybackGuard();
        Interlocked.Exchange(ref _suppressMicrophone, 0);
        ClearLocalPlayback();
        if (!IsConnected) return;
        if (Interlocked.Exchange(ref _responseActive, 0) == 1)
            await SendJsonAsync(new { type = "response.cancel" }, cancellationToken).ConfigureAwait(false);
        await SendJsonAsync(new { type = "input_audio_buffer.clear" }, cancellationToken).ConfigureAwait(false);
        Listening?.Invoke();
    }

    public async Task SendTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        await SendJsonAsync(new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "message",
                role = "user",
                content = new[] { new { type = "input_text", text = text.Trim() } }
            }
        }, cancellationToken).ConfigureAwait(false);
        await SendJsonAsync(new { type = "response.create" }, cancellationToken).ConfigureAwait(false);
    }

    private void OnMicrophoneDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Impede que a LIA escute a propria voz pelo alto-falante e responda em ciclo.
        if (!IsConnected || e.BytesRecorded <= 0 || Volatile.Read(ref _suppressMicrophone) == 1) return;
        var pcm = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, pcm, 0, e.BytesRecorded);
        _microphoneQueue.Writer.TryWrite(pcm);
    }

    private void OnMicrophoneStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null) Error?.Invoke("Microfone: " + e.Exception.Message);
    }

    private async Task MicrophoneSendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var pcm in _microphoneQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                await SendJsonAsync(new
                {
                    type = "input_audio_buffer.append",
                    audio = Convert.ToBase64String(pcm)
                }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Error?.Invoke("Envio do microfone: " + ex.Message); }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var chunk = new byte[16 * 1024];
        using var message = new MemoryStream();
        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                message.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket!.ReceiveAsync(new ArraySegment<byte>(chunk), cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Idle?.Invoke();
                        return;
                    }
                    message.Write(chunk, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text) continue;
                ProcessServerEvent(message.ToArray());
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Error?.Invoke("Conexao Realtime: " + ex.Message); }
    }

    private void ProcessServerEvent(byte[] utf8Json)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8Json);
            var root = document.RootElement;
            var type = GetString(root, "type");

            if (type is "response.output_audio.delta" or "response.audio.delta") return;

            switch (type)
            {
                case "response.created":
                    Interlocked.Exchange(ref _responseActive, 1);
                    SuppressMicrophoneDuringPlayback();
                    break;
                case "response.done":
                case "response.cancelled":
                    Interlocked.Exchange(ref _responseActive, 0);
                    // Marcador de fim: libera falas menores que os 300 ms do prebuffer.
                    _speakerQueue.Writer.TryWrite(Array.Empty<byte>());
                    ScheduleMicrophoneResume();
                    break;
                case "input_audio_buffer.speech_started":
                    // Eventos recebidos enquanto a LIA fala sao retorno acustico, nao um novo turno.
                    if (Volatile.Read(ref _suppressMicrophone) == 1) break;
                    ClearLocalPlayback();
                    Listening?.Invoke();
                    break;
                case "input_audio_buffer.speech_stopped":
                    break;
                case "response.output_text.done":
                    var transcript = GetString(root, "text");
                    if (!string.IsNullOrWhiteSpace(transcript))
                    {
                        Transcript?.Invoke(transcript);
                        _ = SpeakFranciscaAsync(transcript);
                    }
                    break;
                case "response.output_audio_transcript.done":
                case "response.audio_transcript.done":
                    var audioTranscript = GetString(root, "transcript");
                    if (!string.IsNullOrWhiteSpace(audioTranscript))
                    {
                        Transcript?.Invoke(audioTranscript);
                        _ = SpeakFranciscaAsync(audioTranscript);
                    }
                    break;
                case "response.function_call_arguments.done":
                    _ = HandleFunctionCallAsync(root.Clone(), _sessionCts?.Token ?? CancellationToken.None);
                    break;
                case "error":
                    var error = root.TryGetProperty("error", out var errorObject)
                        ? GetString(errorObject, "message")
                        : "Erro desconhecido da Realtime API.";
                    Error?.Invoke(error ?? "Erro desconhecido da Realtime API.");
                    break;
            }
        }
        catch (Exception ex) { Error?.Invoke("Evento Realtime invalido: " + ex.Message); }
    }

    private async Task SpeakFranciscaAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        SuppressMicrophoneDuringPlayback();
        Speaking?.Invoke();
        try
        {
            using var synthesizer = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
            var francisca = Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices
                .FirstOrDefault(v => v.DisplayName.Contains("Francisca", StringComparison.OrdinalIgnoreCase)
                                  || v.Id.Contains("Francisca", StringComparison.OrdinalIgnoreCase));
            if (francisca == null)
                throw new InvalidOperationException("Microsoft Francisca Natural nao foi exposta pela API WinRT.");
            synthesizer.Voice = francisca;
            using var stream = await synthesizer.SynthesizeTextToStreamAsync(text);
            var bytes = new byte[stream.Size];
            using (var reader = new DataReader(stream.GetInputStreamAt(0)))
            {
                await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(bytes);
            }
            using var memory = new MemoryStream(bytes, false);
            using var wave = new WaveFileReader(memory);
            using var output = new WaveOutEvent();
            var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            output.PlaybackStopped += (_, args) =>
            {
                if (args.Exception != null) finished.TrySetException(args.Exception);
                else finished.TrySetResult(true);
            };
            output.Init(wave);
            output.Play();
            await finished.Task.ConfigureAwait(false);
        }
        catch (Exception ex) { Error?.Invoke("Voz Francisca WinRT: " + ex.Message); }
        finally { ScheduleMicrophoneResume(); }
    }

    private async Task HandleFunctionCallAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var functionName = GetString(root, "name");
        if (functionName is not ("abrir_tela" or "fechar_tela")) return;
        var callId = GetString(root, "call_id");
        var arguments = GetString(root, "arguments");
        string? screen = null;
        if (functionName == "abrir_tela") try
        {
            using var args = JsonDocument.Parse(arguments ?? "{}");
            screen = GetString(args.RootElement, "tela")?.Trim().ToUpperInvariant();
        }
        catch { }
        var ok = functionName == "fechar_tela" ||
                 screen != null && AllowedScreens.Contains(screen, StringComparer.OrdinalIgnoreCase);
        var result = ok
            ? functionName == "fechar_tela" ? "Fechando a janela em primeiro plano."
            : "Tela aberta com sucesso."
            : "Tela nao reconhecida.";
        if (ok && NavigationRequested != null)
            result = await NavigationRequested(functionName == "fechar_tela" ? "FECHAR_TELA" : screen!).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(callId) && IsConnected)
        {
            await SendJsonAsync(new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "function_call_output",
                    call_id = callId,
                    output = result
                }
            }, cancellationToken).ConfigureAwait(false);
            await SendJsonAsync(new { type = "response.create" }, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendJsonAsync(object payload, CancellationToken cancellationToken)
    {
        var socket = _socket;
        if (socket?.State != WebSocketState.Open) return;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        finally { _sendLock.Release(); }
    }

    private void InitializeSpeaker()
    {
        // PCM16 mono a 24 kHz determina fisicamente a velocidade nativa 1.0x.
        // Nao ha resampling, time-stretch ou qualquer alteracao de playback rate.
        _speakerBuffer = new BufferedWaveProvider(new WaveFormat(OutputSampleRate, 16, 1))
        {
            BufferDuration = TimeSpan.FromSeconds(30),
            DiscardOnBufferOverflow = false,
            ReadFully = true
        };
        _speaker = new WaveOutEvent { DesiredLatency = 80, NumberOfBuffers = 3 };
        _speaker.Init(_speakerBuffer);
    }

    private async Task SpeakerPlaybackLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _speakerQueue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var pcm = await _speakerQueue.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (pcm.Length == 0) continue;
                var buffer = _speakerBuffer;
                var speaker = _speaker;
                if (buffer == null || speaker == null) continue;

                if (buffer.BufferedBytes == 0)
                {
                    // Jitter buffer: acumula 300 ms antes de iniciar. Se a resposta
                    // terminar antes disso, o marcador vazio libera a fala curta.
                    speaker.Pause();
                    buffer.AddSamples(pcm, 0, pcm.Length);
                    var accumulated = pcm.Length;
                    while (accumulated < StartupBufferBytes)
                    {
                        var next = await _speakerQueue.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                        if (next.Length == 0) break;
                        buffer.AddSamples(next, 0, next.Length);
                        accumulated += next.Length;
                    }
                    EnsureSpeakerAwake();
                    continue;
                }

                // Backpressure mantem aproximadamente 300-600 ms reservados, sem
                // descartar blocos e sem acelerar a reproducao para alcancar a rede.
                while (buffer.BufferedBytes >= StartupBufferBytes * 2)
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                EnsureSpeakerAwake();
                buffer.AddSamples(pcm, 0, pcm.Length);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Error?.Invoke("Reproducao de audio: " + ex.Message); }
    }

    private void EnsureSpeakerAwake()
    {
        // Equivalente NAudio ao audioContext.resume(): dispositivos do Windows podem
        // entrar em Paused/Stopped apos ociosidade ou troca temporaria da saida.
        // Acordamos o WaveOut imediatamente antes de liberar cada bloco PCM.
        var speaker = _speaker;
        if (speaker != null && speaker.PlaybackState != PlaybackState.Playing)
            speaker.Play();
    }

    private void ClearLocalPlayback()
    {
        while (_speakerQueue.Reader.TryRead(out _)) { }
        try { _speaker?.Pause(); } catch { }
        try { _speakerBuffer?.ClearBuffer(); } catch { }
        lock (_playbackClockLock) _playbackEndsUtc = DateTime.UtcNow;
    }

    private void RegisterPlaybackBytes(int byteCount)
    {
        // PCM16 mono 24 kHz = 48.000 bytes por segundo.
        var duration = TimeSpan.FromSeconds(byteCount / (double)PcmBytesPerSecond);
        lock (_playbackClockLock)
        {
            var now = DateTime.UtcNow;
            if (_playbackEndsUtc < now) _playbackEndsUtc = now;
            _playbackEndsUtc = _playbackEndsUtc.Add(duration);
        }
    }

    private void SuppressMicrophoneDuringPlayback()
    {
        Interlocked.Exchange(ref _suppressMicrophone, 1);
        CancelPlaybackGuard();
        while (_microphoneQueue.Reader.TryRead(out _)) { }
    }

    private void ScheduleMicrophoneResume()
    {
        var sessionToken = _sessionCts?.Token ?? CancellationToken.None;
        var guard = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
        CancellationTokenSource? previous;
        lock (_lifecycleLock)
        {
            previous = _playbackGuardCts;
            _playbackGuardCts = guard;
        }
        previous?.Cancel();
        previous?.Dispose();
        _ = ResumeMicrophoneAfterPlaybackAsync(guard);
    }

    private async Task ResumeMicrophoneAfterPlaybackAsync(CancellationTokenSource guard)
    {
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                TimeSpan remaining;
                lock (_playbackClockLock) remaining = _playbackEndsUtc - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) break;
                await Task.Delay(Math.Min(80, Math.Max(10, (int)remaining.TotalMilliseconds)), guard.Token).ConfigureAwait(false);
            }

            // Pequena margem para o som fisico do alto-falante desaparecer do ambiente.
            await Task.Delay(300, guard.Token).ConfigureAwait(false);
            if (IsConnected)
                await SendJsonAsync(new { type = "input_audio_buffer.clear" }, guard.Token).ConfigureAwait(false);

            lock (_lifecycleLock)
            {
                if (!ReferenceEquals(_playbackGuardCts, guard)) return;
                _playbackGuardCts = null;
            }
            Interlocked.Exchange(ref _suppressMicrophone, 0);
            if (IsCapturing) Listening?.Invoke(); else Idle?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch
        {
            // A protecao antieco nunca pode bloquear a LIA nem abrir varias janelas de erro.
            var ownsGuard = false;
            lock (_lifecycleLock)
            {
                if (ReferenceEquals(_playbackGuardCts, guard))
                {
                    _playbackGuardCts = null;
                    ownsGuard = true;
                }
            }
            if (ownsGuard)
            {
                Interlocked.Exchange(ref _suppressMicrophone, 0);
                if (IsCapturing) Listening?.Invoke(); else Idle?.Invoke();
            }
        }
        finally { guard.Dispose(); }
    }

    private void CancelPlaybackGuard()
    {
        CancellationTokenSource? guard;
        lock (_lifecycleLock)
        {
            guard = _playbackGuardCts;
            _playbackGuardCts = null;
        }
        guard?.Cancel();
    }

    public async Task DisconnectAsync()
    {
        CancelPlaybackGuard();
        Interlocked.Exchange(ref _suppressMicrophone, 0);
        await StopMicrophoneAsync().ConfigureAwait(false);
        CancellationTokenSource? cts;
        ClientWebSocket? socket;
        lock (_lifecycleLock)
        {
            cts = _sessionCts;
            socket = _socket;
            _sessionCts = null;
            _socket = null;
        }
        cts?.Cancel();
        if (socket?.State == WebSocketState.Open)
        {
            try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "LIC AI encerrada", CancellationToken.None).ConfigureAwait(false); }
            catch { socket.Abort(); }
        }
        try { if (_receiveTask != null) await _receiveTask.ConfigureAwait(false); } catch { }
        try { if (_microphoneSendTask != null) await _microphoneSendTask.ConfigureAwait(false); } catch { }
        try { if (_speakerPlaybackTask != null) await _speakerPlaybackTask.ConfigureAwait(false); } catch { }
        while (_speakerQueue.Reader.TryRead(out _)) { }
        _speaker?.Stop();
        _speaker?.Dispose();
        _speaker = null;
        _speakerBuffer = null;
        _speakerPlaybackTask = null;
        socket?.Dispose();
        cts?.Dispose();
        Idle?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
        _sendLock.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OpenAiRealtimeConnection));
    }

    private static string NormalizeKey(string? value) =>
        (value ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? FindAudioDelta(JsonElement root)
    {
        var direct = GetString(root, "delta");
        if (!string.IsNullOrWhiteSpace(direct)) return direct;
        if (root.TryGetProperty("audio", out var audio))
        {
            var nested = GetString(audio, "delta") ?? GetString(audio, "data");
            if (!string.IsNullOrWhiteSpace(nested)) return nested;
        }
        return GetString(root, "audio_delta");
    }

    private static readonly string[] AllowedScreens =
    {
        "PRODUTOS", "CLIENTES", "FORNECEDORES", "SERVICOS", "ORDENS_SERVICO",
        "ORCAMENTOS", "FLUXO_CAIXA", "HISTORICO_VENDAS", "TELA_VENDAS",
        "RELATORIOS", "USUARIOS", "CONFIGURACOES", "CADASTROS", "AJUDA_CADASTRO"
    };

    private const string SystemPrompt = """
Voce e a LIA, parceira de trabalho simpatica, bem-humorada, informal e muito rapida do LEAL INFO PDV.
Fale sempre em portugues do Brasil, naturalmente, em no maximo duas frases curtas. Converse livremente quando o operador quiser.
Conheca as telas: Produtos, Clientes, Fornecedores, Servicos, Ordens de Servico, Orcamentos, Fluxo de Caixa, Historico de Vendas, Tela de Vendas, Relatorios, Usuarios, Configuracoes, Cadastros e Ajuda.
Para mudar nome da empresa, telefone, CNPJ ou qualquer ajuste tecnico, use abrir_tela com CONFIGURACOES.
So chame abrir_tela na primeira solicitacao explicita para abrir; se a pessoa apenas continuar uma duvida ou conversa sobre ajuda, nao chame novamente.
Quando o operador disser "fechar tela" ou "fechar janela", chame fechar_tela exatamente uma vez. Use o resultado da ferramenta para confirmar em voz qual janela foi fechada.
Nunca execute venda, exclusao, alteracao financeira ou mudanca de seguranca. Seja leve, util e direta.
""";
}
