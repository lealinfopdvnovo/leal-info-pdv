using LicAi.Core;
using LicAi.Security;
using System.IO.Pipes;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NAudio.Wave;

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
            if (!EnsureApiKey()) { Close(); return; }
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
            if (IsCreditError(message))
            {
                _offlineMode = true;
                _status.Text = "LIA GEMINI • preparando voz local...";
                Append("LIA", "O crédito da OpenAI acabou. Entrei no modo básico Gemini para continuar ajudando no PDV.");
                _ = SwitchToGeminiVoiceAsync();
                Interlocked.Exchange(ref _errorDialogVisible, 0);
                return;
            }
            _status.Text = "Falha na conexao de voz";
            _ = SendStatusAsync("ERROR");
            try { MessageBox.Show(this, message, "LIC ASSISTENTE AI", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { Interlocked.Exchange(ref _errorDialogVisible, 0); }
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
            await SendStatusAsync("ERROR");
            _voiceButton.Text = "🎙 FALAR";
            MessageBox.Show(this, "A LIA nao conseguiu iniciar.\n\n" + ex.Message, "LIC ASSISTENTE AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (_voiceOnly) Close();
        }
        finally { _voiceButton.Enabled = true; }
    }

    private async Task ToggleVoiceAsync()
    {
        try
        {
            if (_offlineMode)
            {
                if (_geminiMic != null) { StopOfflineVoice(); }
                else StartOfflineVoice();
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
        LiaLog("GEMINI_FALLBACK_START");
        try
        {
            if (_realtime != null)
            {
                await _realtime.StopMicrophoneAsync();
                await _realtime.DisconnectAsync();
            }
            StartOfflineVoice();
        }
        catch (Exception ex)
        {
            _status.Text = "LIA GEMINI • voz local indisponível";
            Append("LIA", "Não consegui iniciar a escuta local: " + ex.Message);
            await SendStatusAsync("ERROR");
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
        _geminiCaptureTimer = new System.Windows.Forms.Timer { Interval = 6000 };
        _geminiCaptureTimer.Tick += (_, _) =>
        {
            _geminiCaptureTimer?.Stop();
            LiaLog("CAPTURE_TIMEOUT_STOP", "6000ms");
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
            var answer = await SendGeminiAudioAsync(wav, _lifetime.Token);
            LiaLog("GEMINI_AUDIO_RESPONSE", answer);
            if (TryExtractNavigationCommand(answer, out var command))
            {
                LiaLog("PDV_COMMAND_SEND", command);
                var result = await SendNavigationCommandAsync(command, _lifetime.Token);
                LiaLog("PDV_COMMAND_RESULT", result);
                Ui(() => Append("LIA", result));
                _ = SpeakGeminiAsync(result, _lifetime.Token);
            }
            else
            {
                Ui(() => Append("LIA", answer));
                _ = SpeakGeminiAsync(answer, _lifetime.Token);
            }
            Ui(() => { _status.Text = "LIA GEMINI • modo básico"; _voiceButton.Text = "🎙 FALAR"; });
            await SendStatusAsync("IDLE");
        }
        catch (Exception ex)
        {
            LiaLog("VOICE_PIPELINE_ERROR", ex.ToString());
            Ui(() => { _status.Text = "LIA GEMINI • erro"; _voiceButton.Text = "🎙 FALAR"; });
            await SendStatusAsync("ERROR");
        }
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

    private async Task SpeakGeminiAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            LiaLog("TTS_REQUEST", $"chars={text.Length}");
            var key = EnsureGeminiApiKey();
            var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent";
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("x-goog-api-key", key);
            request.Content = JsonContent.Create(new
            {
                contents = new[] { new { parts = new[] { new { text = "Fale em português do Brasil, com voz natural, ritmo normal e tom acolhedor: " + text } } } },
                generationConfig = new
                {
                    responseModalities = new[] { "AUDIO" },
                    speechConfig = new
                    {
                        voiceConfig = new { prebuiltVoiceConfig = new { voiceName = "Kore" } },
                        languageCode = "pt-BR"
                    }
                }
            });
            using var response = await GeminiHttp.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Gemini TTS {(int)response.StatusCode}: {ExtractGeminiError(json)}");
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("inlineData").GetProperty("data").GetString();
            if (string.IsNullOrWhiteSpace(data)) throw new InvalidOperationException("Gemini TTS não retornou áudio.");
            var pcm = Convert.FromBase64String(data);
            LiaLog("TTS_AUDIO_READY", $"bytes={pcm.Length}");
            var provider = new BufferedWaveProvider(new WaveFormat(24000, 16, 1)) { DiscardOnBufferOverflow = false, BufferDuration = TimeSpan.FromSeconds(120) };
            provider.AddSamples(pcm, 0, pcm.Length);
            using var output = new WaveOutEvent();
            output.Init(provider);
            LiaLog("TTS_PLAY_START");
            await SendStatusAsync("SPEAKING");
            output.Play();
            while (output.PlaybackState == PlaybackState.Playing && !cancellationToken.IsCancellationRequested)
                await Task.Delay(50, cancellationToken);
            LiaLog("TTS_PLAY_END");
            await SendStatusAsync("IDLE");
        }
        catch (Exception ex) { LiaLog("TTS_ERROR", ex.ToString()); }
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
            _status.Text = "LIA GEMINI • pensando...";
            try
            {
                var answer = await SendGeminiAsync(text, _lifetime.Token);
                if (TryExtractNavigationCommand(answer, out var command))
                {
                    var result = await SendNavigationCommandAsync(command, _lifetime.Token);
                    Append("LIA", result);
                    _status.Text = "LIA GEMINI • comando executado";
                }
                else
                {
                    Append("LIA", answer);
                    _status.Text = "LIA GEMINI • modo básico";
                }
            }
            catch (Exception ex)
            {
                Append("LIA", "Não consegui usar o Gemini agora: " + ex.Message);
                _status.Text = "LIA GEMINI • indisponível";
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
        catch (Exception ex) { ShowFatalError(ex); }
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
        _keyButton.Click += (_, _) => ConfigureApiKey();
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
            "CONFIGURACOES","CADASTROS","AJUDA_CADASTRO","FECHAR_TELA"
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

    private const string GeminiSystemPrompt = """
Você é a LIA, assistente virtual do LEAL INFO PDV. Fale sempre em português do Brasil, de forma natural, curta e útil.
Você conhece as áreas reais do sistema: Produtos, Clientes, Fornecedores, Serviços, Ordens de Serviço, Orçamentos, Fluxo de Caixa, Histórico de Vendas, Tela de Vendas, Relatórios, Usuários, Configurações, Cadastros e Ajuda de Cadastro.
Produtos: código, código de barras, nome, categoria, custo, preço, estoque, estoque mínimo e foto.
Clientes/Fornecedores: nome, documento, telefone, e-mail e endereço.
Serviços: nome, preço e descrição.
OS: cliente, equipamento, defeito, serviço realizado, status, valor e observações.
Orçamentos: cliente, descrição, valor e status.
Tela de vendas: F5 consulta produto; F2 finaliza; F7 remove item; possui quantidade, cliente, pagamentos, desconto, troco e comprovante.
Se a pessoa perguntar ONDE, COMO, PARA QUE SERVE ou pedir explicação, explique e NÃO gere comando.
Somente quando houver pedido claro para abrir, ir, mostrar ou fechar uma tela, responda EXCLUSIVAMENTE com uma linha COMANDO: NOME.
Comandos permitidos: PRODUTOS, CLIENTES, FORNECEDORES, SERVICOS, ORDENS_SERVICO, ORCAMENTOS, FLUXO_CAIXA, HISTORICO_VENDAS, TELA_VENDAS, RELATORIOS, USUARIOS, CONFIGURACOES, CADASTROS, AJUDA_CADASTRO, FECHAR_TELA.
Exemplo: "onde vejo minhas vendas?" => explique Histórico de Vendas.
Exemplo: "abre minhas vendas" => COMANDO: HISTORICO_VENDAS
Nunca diga que executou antes da confirmação do PDV. Não contorne permissões. Não execute venda, exclusão, alteração financeira ou mudança de segurança.
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
