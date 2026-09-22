using LicAi.Core;
using LicAi.Security;
using System.IO.Pipes;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NAudio.Wave;
using Microsoft.CognitiveServices.Speech;
using System.Speech.Synthesis;

namespace LicAi;

public sealed class MainForm : Form
{
    private readonly ConversationEngine _engine;
    private readonly LocalSecretStore _secrets;
    private readonly bool _voiceOnly;
    private readonly RichTextBox _chat = new();
    private readonly TextBox _input = new();
    private readonly Button _send = new();
    private readonly Button _keyButton = new();
    private readonly Button _voiceButton = new();
    private readonly Label _status = new();
    private OpenAiRealtimeConnection? _realtime;
    private CancellationTokenSource _lifetime = new();
    private bool _closing;
    private int _errorDialogVisible;
    private bool _offlineMode;
    private static readonly HttpClient GeminiHttp = new() { Timeout = TimeSpan.FromSeconds(90) };
    private const string GeminiModel = "gemini-3.5-flash-lite";
    private WaveInEvent? _geminiMic;
    private MemoryStream? _geminiAudio;
    private WaveFileWriter? _geminiWriter;
    private DateTime _geminiVoiceStarted;
    private System.Windows.Forms.Timer? _geminiCaptureTimer;
    private readonly SemaphoreSlim _voicePipelineLock = new(1, 1);
    private readonly SemaphoreSlim _ttsLock = new(1, 1);
    private CancellationTokenSource? _voiceRequestCts;
    private CancellationTokenSource? _ttsCts;
    private int _voiceFallbackStarting;
    private bool _continuousVoiceMode;
    private static readonly object LiaLogLock = new();
    private static string LiaLogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LealInfoPDV", "Logs", "lia-diagnostico.log");
    private static void LiaLog(string stage, string detail = "")
    {
        try { lock (LiaLogLock) { Directory.CreateDirectory(Path.GetDirectoryName(LiaLogPath)!); File.AppendAllText(LiaLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {stage} | {detail}{Environment.NewLine}"); } } catch { }
    }

    public MainForm(ConversationEngine engine, LocalSecretStore secrets, bool voiceOnly = false)
    {
        _engine = engine;
        _secrets = secrets;
        _voiceOnly = voiceOnly;
        Text = "LIC ASSISTENTE AI";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 560);
        Size = new Size(980, 720);
        BackColor = Color.FromArgb(12, 18, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        BuildUi();
        LoadHistory();

        if (_voiceOnly)
        {
            ShowInTaskbar = false;
            Opacity = 0;
            WindowState = FormWindowState.Minimized;
        }

        Shown += OnShownAsync;
        FormClosing += OnFormClosingAsync;
    }

    private async void OnShownAsync(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_secrets.GetApiKey()))
            {
                await ActivateVoiceFallbackAsync("OPENAI_KEY_MISSING");
                return;
            }
            CreateRealtimeConnection();
            if (_voiceOnly) await StartVoiceAsync();
        }
        catch (Exception ex) { ShowFatalError(ex); }
    }

    private async void OnFormClosingAsync(object? sender, FormClosingEventArgs e)
    {
        if (_closing) return;
        _closing = true;
        e.Cancel = true;
        try
        {
            _lifetime.Cancel();
            StopOfflineVoice();
            if (_realtime != null) await _realtime.DisposeAsync();
            await SendStatusAsync("IDLE");
        }
        catch { }
        finally
        {
            e.Cancel = false;
            FormClosing -= OnFormClosingAsync;
            Close();
        }
    }

    private void CreateRealtimeConnection()
    {
        if (_realtime != null) return;
        _realtime = new OpenAiRealtimeConnection(() => _secrets.GetApiKey());
        _realtime.Listening += () => Ui(() => SetVoiceState("LISTENING", "LIA esta ouvindo...", "OUVINDO"));
        _realtime.Speaking += () => Ui(() => SetVoiceState("SPEAKING", "LIA esta respondendo...", "FALANDO"));
        _realtime.Idle += () => Ui(() => SetVoiceState(_realtime?.IsCapturing == true ? "LISTENING" : "IDLE", _realtime?.IsCapturing == true ? "LIA esta ouvindo..." : "Pronta para conversar", _realtime?.IsCapturing == true ? "OUVINDO" : "🎙 FALAR"));
        _realtime.Transcript += text => Ui(() => Append("LIA", text));
        _realtime.Error += message => Ui(() =>
        {
            if (Interlocked.Exchange(ref _errorDialogVisible, 1) == 1) return;
            _ = ActivateVoiceFallbackAsync("OPENAI_CONNECTION_FAILED");
            Interlocked.Exchange(ref _errorDialogVisible, 0);
        });
        _realtime.NavigationRequested += async command => await SendNavigationCommandAsync(command, _lifetime.Token);
    }

    private async Task StartVoiceAsync()
    {
        if (_realtime == null) CreateRealtimeConnection();
        _voiceButton.Enabled = false;
        _voiceButton.Text = "CONECTANDO...";
        _status.Text = "Conectando a LIA em tempo real...";
        try
        {
            await _realtime!.StartMicrophoneAsync(_lifetime.Token);
            await SendStatusAsync("LISTENING");
        }
        catch (Exception ex)
        {
            await ActivateVoiceFallbackAsync("OPENAI_START_FAILED");
        }
        finally { _voiceButton.Enabled = true; }
    }

    private async Task ToggleVoiceAsync()
    {
        try
        {
            if (_offlineMode)
            {
                if (_continuousVoiceMode)
                {
                    _continuousVoiceMode = false;
                    StopOfflineVoice();
                    _voiceRequestCts?.Cancel();
                    _ttsCts?.Cancel();
                    _status.Text = "LIA VOZ • conversa encerrada";
                    _voiceButton.Text = "🎙 FALAR";
                    await SendStatusAsync("IDLE");
                }
                else
                {
                    _continuousVoiceMode = true;
                    StartOfflineVoice();
                }
                return;
            }
            if (_realtime?.IsCapturing == true)
            {
                await _realtime.StopMicrophoneAsync();
                await _realtime.DisconnectAsync();
                await SendStatusAsync("IDLE");
                if (_voiceOnly) Close();
                return;
            }
            await StartVoiceAsync();
        }
        catch (Exception ex) { ShowFatalError(ex); }
    }

    private async Task SwitchToGeminiVoiceAsync()
    {
        if (!await _voicePipelineLock.WaitAsync(0))
        {
            LiaLog("VOICE_DUPLICATE_IGNORED");
            return;
        }
        LiaLog("GEMINI_FALLBACK_START");
        try
        {
            if (_realtime != null)
            {
                await _realtime.StopMicrophoneAsync();
                await _realtime.DisconnectAsync();
            }
            _continuousVoiceMode = true;
            StartOfflineVoice();
        }
        catch (Exception ex)
        {
            _status.Text = "LIA GEMINI • voz local indisponível";
            Append("LIA", "Não consegui iniciar a escuta local: " + ex.Message);
            await SendStatusAsync("ERROR");
            _voicePipelineLock.Release();
        }
    }

    private void StartOfflineVoice()
    {
        if (_geminiMic != null) return;
        LiaLog("GEMINI_AUDIO_CAPTURE_START");
        _geminiAudio = new MemoryStream();
        _geminiWriter = new WaveFileWriter(_geminiAudio, new WaveFormat(16000, 16, 1));
        var mic = new WaveInEvent { DeviceNumber = 0, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
        mic.DataAvailable += GeminiMicDataAvailable;
        mic.RecordingStopped += GeminiMicStopped;
        _geminiMic = mic;
        _geminiVoiceStarted = DateTime.UtcNow;
        mic.StartRecording();
        _geminiCaptureTimer?.Stop();
        _geminiCaptureTimer?.Dispose();
        _geminiCaptureTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _geminiCaptureTimer.Tick += (_, _) =>
        {
            _geminiCaptureTimer?.Stop();
            LiaLog("CAPTURE_TIMEOUT_STOP", "3000ms");
            StopOfflineVoice();
        };
        _geminiCaptureTimer.Start();
        LiaLog("MIC_INPUT_READY", "NAudio 16000Hz 16-bit mono device=0");
        _status.Text = "LIA GEMINI • ouvindo";
        _voiceButton.Text = "OUVINDO";
        _voiceButton.BackColor = Color.FromArgb(0, 125, 210);
        _ = SendStatusAsync("LISTENING");
    }

    private void GeminiMicDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            _geminiWriter?.Write(e.Buffer, 0, e.BytesRecorded);
            _geminiWriter?.Flush();

        }
        catch (Exception ex) { LiaLog("AUDIO_CAPTURE_ERROR", ex.Message); }
    }

    private void StopOfflineVoice()
    {
        var mic = Interlocked.Exchange(ref _geminiMic, null);
        if (mic == null) return;
        _geminiCaptureTimer?.Stop();
        _geminiCaptureTimer?.Dispose();
        _geminiCaptureTimer = null;
        LiaLog("MIC_STOP_REQUEST", $"elapsed={(DateTime.UtcNow - _geminiVoiceStarted).TotalMilliseconds:0}ms");
        try { mic.StopRecording(); } catch (Exception ex) { LiaLog("MIC_STOP_ERROR", ex.Message); }
        _voiceButton.Text = "🎙 FALAR";
        _status.Text = "LIA GEMINI • processando...";
        _ = SendStatusAsync("THINKING");
    }

    private async void GeminiMicStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            var mic = sender as WaveInEvent;
            if (mic != null)
            {
                mic.DataAvailable -= GeminiMicDataAvailable;
                mic.RecordingStopped -= GeminiMicStopped;
                mic.Dispose();
            }
            _geminiWriter?.Dispose();
            _geminiWriter = null;
            var wav = _geminiAudio?.ToArray() ?? Array.Empty<byte>();
            _geminiAudio?.Dispose();
            _geminiAudio = null;
            LiaLog("AUDIO_CAPTURED", $"bytes={wav.Length}; error={e.Exception?.Message}");
            if (e.Exception != null) throw e.Exception;
            if (wav.Length < 2000) throw new InvalidOperationException("Nenhum áudio útil foi capturado.");

            LiaLog("GEMINI_AUDIO_SEND", $"bytes={wav.Length}");
            _voiceRequestCts?.Cancel();
            _voiceRequestCts?.Dispose();
            _voiceRequestCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _voiceRequestCts.CancelAfter(TimeSpan.FromSeconds(12));
            var answer = await SendGeminiAudioAsync(wav, _voiceRequestCts.Token);
            LiaLog("GEMINI_AUDIO_RESPONSE", answer);
            if (TryExtractNavigationCommand(answer, out var command))
            {
                if (command.Equals("ENCERRAR_VOZ", StringComparison.OrdinalIgnoreCase))
                {
                    _continuousVoiceMode = false;
                    Ui(() => Append("LIA", "Conversa por voz encerrada."));
                    await SpeakGeminiAsync("Conversa por voz encerrada.", _lifetime.Token);
                    Ui(() => { _status.Text = "LIA VOZ • conversa encerrada"; _voiceButton.Text = "🎙 FALAR"; });
                    await SendStatusAsync("IDLE");
                    return;
                }
                LiaLog("PDV_COMMAND_SEND", command);
                await SendNavigationCommandAndLogAsync(command, _lifetime.Token);
            }
            else
            {
                Ui(() => Append("LIA", answer));
                await SpeakGeminiAsync(answer, _lifetime.Token);
            }
            if (_continuousVoiceMode && !_lifetime.IsCancellationRequested)
            {
                Ui(() =>
                {
                    _status.Text = "LIA VOZ • ouvindo novamente";
                    StartOfflineVoice();
                });
            }
            else
            {
                Ui(() => { _status.Text = "LIA VOZ • pronta"; _voiceButton.Text = "🎙 FALAR"; });
                await SendStatusAsync("IDLE");
            }
        }
        catch (Exception ex)
        {
            LiaLog("VOICE_PIPELINE_ERROR", ex.ToString());
            Ui(() => { _status.Text = "LIA GEMINI • erro"; _voiceButton.Text = "🎙 FALAR"; });
            await SendStatusAsync("ERROR");
        }
    }

    private async Task SendNavigationCommandAndLogAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var result = await SendNavigationCommandAsync(command, timeout.Token);
            LiaLog("PDV_COMMAND_RESULT", result);
            Ui(() => Append("LIA", result));
            await SpeakGeminiAsync(result, cancellationToken);
        }
        catch (Exception ex) { LiaLog("PDV_COMMAND_ERROR", ex.Message); }
    }

    private async Task<string> SendGeminiAudioAsync(byte[] wav, CancellationToken cancellationToken)
    {
        var key = EnsureGeminiApiKey();
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{GeminiModel}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", key);
        request.Content = JsonContent.Create(new
        {
            system_instruction = new { parts = new[] { new { text = GeminiSystemPrompt } } },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = "Ouça este áudio em português do Brasil. Entenda o pedido falado e responda seguindo rigorosamente as instruções do sistema. Se for comando de navegação, devolva somente COMANDO: NOME." },
                        new { inline_data = new { mime_type = "audio/wav", data = Convert.ToBase64String(wav) } }
                    }
                }
            }
        });
        using var response = await GeminiHttp.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini {(int)response.StatusCode}: {ExtractGeminiError(json)}");
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            var texts = parts.EnumerateArray().Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString()).Where(x => !string.IsNullOrWhiteSpace(x));
            var answer = string.Join("\n", texts!);
            if (!string.IsNullOrWhiteSpace(answer)) return answer.Trim();
        }
        throw new InvalidOperationException("O Gemini não retornou resposta para o áudio.");
    }

    private const string AzureVoiceName = "pt-BR-FranciscaNeural";
    private const string GeminiTtsModel = "gemini-3.1-flash-tts-preview";
    private static string TtsCacheDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LealInfoPdv", "voz-cache");

    private static string TtsCachePath(string text)
    {
        Directory.CreateDirectory(TtsCacheDir);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AzureVoiceName + "\n" + text))).ToLowerInvariant();
        return Path.Combine(TtsCacheDir, hash + ".wav");
    }

    private static bool IsFixedTtsPhrase(string text)
    {
        var t = text.Trim();
        return t.Equals("Tela aberta com sucesso.", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Estou com lentidão, tente de novo.", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Bom dia", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Boa tarde", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Boa noite", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task PlayCachedWavAsync(string path, CancellationToken cancellationToken)
    {
        using var reader = new AudioFileReader(path);
        using var output = new WaveOutEvent();
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, e) => { if (e.Exception != null) done.TrySetException(e.Exception); else done.TrySetResult(true); };
        output.Init(reader);
        output.Play();
        await done.Task.WaitAsync(cancellationToken);
    }

    private static Task SpeakWindowsLocalAsync(string text, CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool TryGetGeminiAudioChunk(string json, out byte[] audio)
    {
        audio = Array.Empty<byte>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("event_type", out var eventType) || eventType.GetString() != "step.delta") return false;
            if (!root.TryGetProperty("delta", out var delta) ||
                !delta.TryGetProperty("type", out var type) || type.GetString() != "audio" ||
                !delta.TryGetProperty("data", out var data)) return false;
            var encoded = data.GetString();
            if (string.IsNullOrWhiteSpace(encoded)) return false;
            audio = Convert.FromBase64String(encoded);
            return audio.Length > 0;
        }
        catch { return false; }
    }

    private async Task SpeakGeminiNaturalAsync(string text, string key, CancellationToken cancellationToken)
    {
        var endpoint = "https://generativelanguage.googleapis.com/v1beta/interactions";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", key);
        request.Headers.Add("Api-Revision", "2026-05-20");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Content = JsonContent.Create(new
        {
            model = GeminiTtsModel,
            input = "Fale em português brasileiro, com voz feminina natural, acolhedora, clara e ritmo ágil de atendente de PDV. Diga somente: " + text,
            response_format = new { type = "audio" },
            generation_config = new { speech_config = new[] { new { voice = "Sulafat" } } },
            stream = true
        });

        using var response = await GeminiHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Gemini TTS {(int)response.StatusCode}: {ExtractGeminiError(error)}");
        }

        var provider = new BufferedWaveProvider(new WaveFormat(24000, 16, 1))
        {
            BufferDuration = TimeSpan.FromSeconds(20),
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };
        using var output = new WaveOutEvent { DesiredLatency = 80 };
        output.Init(provider);
        var started = false;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
            var payload = line[5..].Trim();
            if (payload == "[DONE]") break;
            if (!TryGetGeminiAudioChunk(payload, out var chunk)) continue;
            provider.AddSamples(chunk, 0, chunk.Length);
            if (!started)
            {
                output.Play();
                started = true;
                await SendStatusAsync("SPEAKING");
            }
        }
        if (!started) throw new InvalidOperationException("O serviço de voz não retornou áudio.");
        while (provider.BufferedDuration > TimeSpan.FromMilliseconds(60))
            await Task.Delay(30, cancellationToken);
        output.Stop();
    }

    private async Task SpeakGeminiAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        await _ttsLock.WaitAsync(cancellationToken);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        LiaLog("TTS_REQUEST", $"chars={text.Length}");
        try
        {
            var key = EnsureGeminiApiKey();
            LiaLog("TTS_GEMINI_STREAM_REQUEST", $"ms={sw.ElapsedMilliseconds}");
            await SpeakGeminiNaturalAsync(text, key, cancellationToken);
            LiaLog("TTS_PLAY_END", $"ms={sw.ElapsedMilliseconds}; engine=gemini-natural");
            await SendStatusAsync("IDLE");
        }
        catch (OperationCanceledException)
        {
            LiaLog("TTS_CANCELLED", $"ms={sw.ElapsedMilliseconds}; reason=request_cancelled");
            await SendStatusAsync("IDLE");
        }
        catch (Exception ex)
        {
            LiaLog("TTS_ERROR", $"ms={sw.ElapsedMilliseconds}; reason={ex.GetType().Name}:{ex.Message.Replace(Environment.NewLine, " ")}");
            await SendStatusAsync("IDLE");
        }
        finally { _ttsLock.Release(); }
    }

    private async Task SendTextAsync()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0 || !_send.Enabled) return;
        if (_offlineMode)
        {
            _input.Clear();
            Append("Voce", text);
            _send.Enabled = false;
            _status.Text = "LIA LOCAL • respondendo...";
            try
            {
                var answer = LocalPdvAssistant.Answer(text);
                if (TryExtractNavigationCommand(answer, out var command))
                {
                    var result = await SendNavigationCommandAsync(command, _lifetime.Token);
                    Append("LIA", result);
                    _status.Text = "LIA LOCAL • comando executado";
                }
                else
                {
                    Append("LIA", answer);
                    _status.Text = "LIA LOCAL • pronta";
                }
            }
            catch (Exception ex)
            {
                Append("LIA", "Não consegui concluir esse comando: " + ex.Message);
                _status.Text = "LIA LOCAL • pronta";
            }
            finally { _send.Enabled = true; }
            return;
        }
        if (!EnsureApiKey()) return;
        CreateRealtimeConnection();
        _input.Clear();
        Append("Voce", text);
        _send.Enabled = false;
        _status.Text = "LIA esta pensando...";
        await SendStatusAsync("THINKING");
        try { await _realtime!.SendTextAsync(text, _lifetime.Token); }
        catch (Exception ex)
        {
            await ActivateVoiceFallbackAsync("TEXT_CONNECTION_FAILED");
            var answer = LocalPdvAssistant.Answer(text);
            if (TryExtractNavigationCommand(answer, out var command))
                Append("LIA", await SendNavigationCommandAsync(command, _lifetime.Token));
            else Append("LIA", answer);
        }
        finally { _send.Enabled = true; }
    }

    private void SetVoiceState(string state, string status, string button)
    {
        _status.Text = status;
        _voiceButton.Text = button;
        _voiceButton.BackColor = state == "SPEAKING" ? Color.FromArgb(190, 35, 35) : state == "LISTENING" ? Color.FromArgb(0, 125, 210) : Color.FromArgb(0, 100, 150);
        _ = SendStatusAsync(state);
    }

    private void BuildUi()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(18, 12, 18, 8) };
        top.Controls.Add(new Label { Text = "LIC AI REALTIME", AutoSize = true, Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold), ForeColor = Color.White, Location = new Point(18, 10) });
        _status.Text = "Pronta para conversar";
        _status.AutoSize = true;
        _status.ForeColor = Color.FromArgb(165, 185, 220);
        _status.Location = new Point(21, 48);
        _keyButton.Text = "Configurar chave";
        _keyButton.Size = new Size(140, 34);
        _keyButton.Location = new Point(Width - 185, 20);
        _keyButton.Click += (_, _) =>
        {
            if (_offlineMode)
            {
                var value = Microsoft.VisualBasic.Interaction.InputBox(
                    "Cole sua chave gratuita do Google AI Studio. Ela ficará protegida neste computador.",
                    "Configurar voz da LIA", "").Trim();
                if (value.Length >= 20)
                {
                    SaveGeminiApiKey(value);
                    _status.Text = "Chave de voz salva";
                }
            }
            else ConfigureApiKey();
        };
        top.Controls.AddRange(new Control[] { _status, _keyButton });
        top.Resize += (_, _) => _keyButton.Left = top.ClientSize.Width - _keyButton.Width - 18;

        _chat.Dock = DockStyle.Fill;
        _chat.ReadOnly = true;
        _chat.BorderStyle = BorderStyle.None;
        _chat.BackColor = Color.FromArgb(17, 24, 39);
        _chat.ForeColor = Color.FromArgb(235, 240, 250);
        _chat.Font = new Font("Segoe UI", 11F);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 96, Padding = new Padding(16, 14, 16, 14) };
        _send.Text = "Enviar";
        _send.Dock = DockStyle.Right;
        _send.Width = 110;
        _send.Click += async (_, _) => await SendTextAsync();
        _voiceButton.Text = "🎙 FALAR";
        _voiceButton.Dock = DockStyle.Right;
        _voiceButton.Width = 125;
        _voiceButton.BackColor = Color.FromArgb(0, 100, 150);
        _voiceButton.ForeColor = Color.White;
        _voiceButton.FlatStyle = FlatStyle.Flat;
        _voiceButton.Click += async (_, _) => await ToggleVoiceAsync();
        _input.Multiline = true;
        _input.Dock = DockStyle.Fill;
        _input.BackColor = Color.FromArgb(28, 37, 54);
        _input.ForeColor = Color.White;
        _input.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; await SendTextAsync(); } };
        bottom.Controls.AddRange(new Control[] { _input, _voiceButton, _send });
        Controls.AddRange(new Control[] { _chat, bottom, top });
    }

    private void LoadHistory()
    {
        foreach (var item in _engine.Recent()) Append(item.Role == "assistant" ? "LIA" : "Voce", item.Content);
        if (_chat.TextLength == 0) Append("LIA", "Oi, chefe! Estou pronta.");
    }

    private void Append(string who, string text)
    {
        if (_chat.TextLength > 0) _chat.AppendText(Environment.NewLine + Environment.NewLine);
        _chat.SelectionFont = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        _chat.AppendText(who + Environment.NewLine);
        _chat.SelectionFont = new Font("Segoe UI", 11F);
        _chat.AppendText(text.Trim());
        _chat.SelectionStart = _chat.TextLength;
        _chat.ScrollToCaret();
    }

    private void Ui(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired) BeginInvoke(action); else action();
    }

    private async Task ActivateVoiceFallbackAsync(string reason)
    {
        if (Interlocked.Exchange(ref _voiceFallbackStarting, 1) == 1) return;
        _offlineMode = true;
        LiaLog("VOICE_FALLBACK", reason);
        _status.Text = "LIA VOZ • preparando microfone";
        _voiceButton.Text = "PREPARANDO...";
        try
        {
            // Quando a LIA e aberta pelo botao do PDV, o formulario nasce oculto.
            // Exiba-o antes de pedir a chave para o dialogo nao ficar atras do PDV.
            if (string.IsNullOrWhiteSpace(GetGeminiApiKey()))
            {
                RevealLocalChat();
                TopMost = true;
                Activate();
                BringToFront();
                await Task.Delay(180);
            }
            _ = EnsureGeminiApiKey();
            TopMost = false;
            if (_realtime != null)
            {
                await _realtime.StopMicrophoneAsync();
                await _realtime.DisconnectAsync();
            }
            _continuousVoiceMode = true;
            StartOfflineVoice();
        }
        catch (Exception ex)
        {
            TopMost = false;
            LiaLog("VOICE_FALLBACK_ERROR", ex.Message);
            RevealLocalChat();
            _status.Text = "Configure a chave gratuita para usar a voz";
            _voiceButton.Text = "🎙 FALAR";
            Append("LIA", "Para conversar por voz, configure uma chave gratuita do Google AI Studio no botão Configurar.");
            await SendStatusAsync("ERROR");
        }
        finally { Interlocked.Exchange(ref _voiceFallbackStarting, 0); }
    }

    private void RevealLocalChat()
    {
        if (!_voiceOnly) return;
        Opacity = 1;
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Show();
        Activate();
        BringToFront();
        _input.Focus();
    }

    private bool EnsureApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_secrets.GetApiKey())) return true;
        ConfigureApiKey();
        return !string.IsNullOrWhiteSpace(_secrets.GetApiKey());
    }

    private void ConfigureApiKey()
    {
        var value = Microsoft.VisualBasic.Interaction.InputBox("Cole sua chave da OpenAI:", "Configurar OpenAI", "").Trim();
        if (value.Length < 20) return;
        _secrets.SaveApiKey(value);
        _status.Text = "Chave salva com protecao do Windows";
    }

    private void ShowFatalError(Exception ex)
    {
        _status.Text = "A LIA encontrou um erro";
        _ = SendStatusAsync("ERROR");
        MessageBox.Show(this, ex.Message, "LIC ASSISTENTE AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }


    private static bool IsCreditError(string message)
    {
        var m=(message??"").ToLowerInvariant();
        return m.Contains("no credits") || m.Contains("insufficient_quota") || m.Contains("quota") || m.Contains("billing");
    }

    private static bool TryExtractNavigationCommand(string answer, out string command)
    {
        command = "";
        if (string.IsNullOrWhiteSpace(answer)) return false;
        var line = answer.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(x => x.TrimStart().StartsWith("COMANDO:", StringComparison.OrdinalIgnoreCase));
        if (line == null) return false;
        var candidate = line[(line.IndexOf(':') + 1)..].Trim().ToUpperInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PRODUTOS","CLIENTES","FORNECEDORES","SERVICOS","ORDENS_SERVICO","ORCAMENTOS",
            "FLUXO_CAIXA","HISTORICO_VENDAS","TELA_VENDAS","RELATORIOS","USUARIOS",
            "CONFIGURACOES","CADASTROS","AJUDA_CADASTRO","FECHAR_TELA","ENCERRAR_VOZ"
        };
        if (!allowed.Contains(candidate)) return false;
        command = candidate;
        return true;
    }

    private static string GeminiKeyPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LealInfoPDV", "gemini.key");

    private static string? GetGeminiApiKey()
    {
        try
        {
            if (!File.Exists(GeminiKeyPath)) return null;
            var encrypted = File.ReadAllBytes(GeminiKeyPath);
            var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain).Trim();
        }
        catch { return null; }
    }

    private static void SaveGeminiApiKey(string key)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GeminiKeyPath)!);
        var plain = Encoding.UTF8.GetBytes(key.Trim());
        var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(GeminiKeyPath, encrypted);
    }

    private string EnsureGeminiApiKey()
    {
        var key = GetGeminiApiKey();
        if (!string.IsNullOrWhiteSpace(key)) return key;
        var value = Microsoft.VisualBasic.Interaction.InputBox(
            "Cole sua chave gratuita do Google AI Studio. Ela ficará protegida neste computador.",
            "Configurar Gemini • LIA modo básico", "").Trim();
        if (value.Length < 20) throw new InvalidOperationException("Chave do Gemini não configurada.");
        SaveGeminiApiKey(value);
        return value;
    }

    private async Task<string> SendGeminiAsync(string userText, CancellationToken cancellationToken)
    {
        var key = EnsureGeminiApiKey();
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{GeminiModel}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", key);
        request.Content = JsonContent.Create(new
        {
            system_instruction = new
            {
                parts = new[] { new { text = GeminiSystemPrompt } }
            },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userText } } }
            }
        });
        using var response = await GeminiHttp.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini {(int)response.StatusCode}: {ExtractGeminiError(json)}");
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var content = candidates[0].GetProperty("content");
            if (content.TryGetProperty("parts", out var parts))
            {
                var texts = parts.EnumerateArray()
                    .Where(p => p.TryGetProperty("text", out _))
                    .Select(p => p.GetProperty("text").GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x));
                var answer = string.Join("\n", texts!);
                if (!string.IsNullOrWhiteSpace(answer)) return answer.Trim();
            }
        }
        throw new InvalidOperationException("O Gemini não retornou uma resposta de texto.");
    }

    private static string ExtractGeminiError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "erro desconhecido";
        }
        catch { return "erro desconhecido"; }
    }

    private static readonly string GeminiSystemPrompt = PdvKnowledge.SystemPrompt + """

REGRAS DO MODO TEXTO/GEMINI
Se a pessoa perguntar ONDE, COMO, PARA QUE SERVE ou pedir explicação, explique e NÃO gere comando.
Somente quando houver pedido claro para abrir, ir, mostrar ou fechar uma tela, responda EXCLUSIVAMENTE com uma linha COMANDO: NOME.
Comandos permitidos: PRODUTOS, CLIENTES, FORNECEDORES, SERVICOS, ORDENS_SERVICO, ORCAMENTOS, FLUXO_CAIXA, HISTORICO_VENDAS, TELA_VENDAS, RELATORIOS, USUARIOS, CONFIGURACOES, CADASTROS, AJUDA_CADASTRO, FECHAR_TELA.
Quando a pessoa disser "encerrar voz", "parar conversa", "pode parar de ouvir" ou equivalente, responda EXCLUSIVAMENTE: COMANDO: ENCERRAR_VOZ
Exemplo: "onde vejo minhas vendas?" => explique Histórico de Vendas.
Exemplo: "abre minhas vendas" => COMANDO: HISTORICO_VENDAS
Nunca diga que executou antes da confirmação do PDV.
""";

    private static async Task<string> SendNavigationCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", "LealInfoPDV.Navigation", PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1500, cancellationToken);
            await using var writer = new StreamWriter(pipe, System.Text.Encoding.UTF8, 1024, true) { AutoFlush = true };
            await writer.WriteLineAsync(command.AsMemory(), cancellationToken);
            using var reader = new StreamReader(pipe, System.Text.Encoding.UTF8, true, 1024, true);
            return await reader.ReadLineAsync(cancellationToken) ?? "Comando concluido.";
        }
        catch { return "Nao consegui controlar essa janela agora."; }
    }

    private static async Task SendStatusAsync(string state)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", "LealInfoPDV.LicAiStatus", PipeDirection.Out, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(400);
            await pipe.ConnectAsync(timeout.Token);
            await using var writer = new StreamWriter(pipe) { AutoFlush = true };
            await writer.WriteLineAsync(state);
        }
        catch { }
    }
}
