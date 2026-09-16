using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using NAudio.Wave;

namespace LicAi.Core;

/// <summary>Conexao persistente de voz-a-voz com a OpenAI Realtime API.</summary>
public sealed class OpenAiRealtimeConnection : IAsyncDisposable
{
    private const string Model = "gpt-realtime-2.1";
    private static readonly Uri Endpoint = new($"wss://api.openai.com/v1/realtime?model={Model}");
    private readonly Func<string?> _apiKeyProvider;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _lifecycleLock = new();
    private readonly Channel<byte[]> _microphoneQueue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(24)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _sessionCts;
    private WaveInEvent? _microphone;
    private WaveOutEvent? _speaker;
    private BufferedWaveProvider? _speakerBuffer;
    private Task? _receiveTask;
    private Task? _microphoneSendTask;
    private CancellationTokenSource? _playbackGuardCts;
    private bool _disposed;
    private int _responseActive;
    private int _suppressMicrophone;

    public event Action? Connected;
    public event Action? Listening;
    public event Action? Speaking;
    public event Action? Idle;
    public event Action<string>? Transcript;
    public event Action<string>? Error;
    public event Func<string, Task>? NavigationRequested;

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
                output_modalities = new[] { "audio" },
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
                    },
                    output = new
                    {
                        format = new { type = "audio/pcm", rate = 24000 },
                        voice = "marin"
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

            if (type is "response.output_audio.delta" or "response.audio.delta")
            {
                var base64 = FindAudioDelta(root);
                if (string.IsNullOrWhiteSpace(base64)) return;
                var pcm = Convert.FromBase64String(base64);
                SuppressMicrophoneDuringPlayback();
                _speakerBuffer?.AddSamples(pcm, 0, pcm.Length);
                Speaking?.Invoke();
                return;
            }

            switch (type)
            {
                case "response.created":
                    Interlocked.Exchange(ref _responseActive, 1);
                    SuppressMicrophoneDuringPlayback();
                    break;
                case "response.done":
                case "response.cancelled":
                    Interlocked.Exchange(ref _responseActive, 0);
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
                case "response.output_audio_transcript.done":
                case "response.audio_transcript.done":
                    var transcript = GetString(root, "transcript");
                    if (!string.IsNullOrWhiteSpace(transcript)) Transcript?.Invoke(transcript);
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

    private async Task HandleFunctionCallAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (!string.Equals(GetString(root, "name"), "abrir_tela", StringComparison.Ordinal)) return;
        var callId = GetString(root, "call_id");
        var arguments = GetString(root, "arguments");
        string? screen = null;
        try
        {
            using var args = JsonDocument.Parse(arguments ?? "{}");
            screen = GetString(args.RootElement, "tela")?.Trim().ToUpperInvariant();
        }
        catch { }
        var ok = screen != null && AllowedScreens.Contains(screen, StringComparer.OrdinalIgnoreCase);
        if (ok && NavigationRequested != null) await NavigationRequested(screen!).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(callId) && IsConnected)
        {
            await SendJsonAsync(new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "function_call_output",
                    call_id = callId,
                    output = ok ? "Tela aberta com sucesso." : "Tela nao reconhecida."
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
        _speakerBuffer = new BufferedWaveProvider(new WaveFormat(24000, 16, 1))
        {
            BufferDuration = TimeSpan.FromSeconds(6),
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };
        _speaker = new WaveOutEvent { DesiredLatency = 80, NumberOfBuffers = 3 };
        _speaker.Init(_speakerBuffer);
        _speaker.Play();
    }

    private void ClearLocalPlayback()
    {
        try { _speakerBuffer?.ClearBuffer(); } catch { }
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
            while ((_speakerBuffer?.BufferedBytes ?? 0) > 0 && DateTime.UtcNow < deadline)
                await Task.Delay(40, guard.Token).ConfigureAwait(false);

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
        catch (Exception ex) { Error?.Invoke("Protecao antieco: " + ex.Message); }
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
        _speaker?.Stop();
        _speaker?.Dispose();
        _speaker = null;
        _speakerBuffer = null;
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
Nunca execute venda, exclusao, alteracao financeira ou mudanca de seguranca. Seja leve, util e direta.
""";
}
