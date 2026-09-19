using LicAi.Core;
using LicAi.Security;
using System.IO.Compression;
using System.IO.Pipes;
using System.Text.Json;
using NAudio.Wave;
using Vosk;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Speech.Synthesis;

namespace LicAi;

public sealed class MainForm : Form
{
    private readonly ConversationEngine _engine;
    private readonly LocalSecretStore _secrets;
    private readonly RichTextBox _chat = new();
    private readonly TextBox _input = new();
    private readonly Button _send = new();
    private readonly Button _keyButton = new();
    private readonly Button _voiceButton = new();
    private readonly Label _status = new();
    private CancellationTokenSource? _cts;
    private Model? _voiceModel;
    private VoskRecognizer? _voiceRecognizer;
    private WaveInEvent? _microphone;
    private bool _voiceMode;
    private bool _recognizing;
    private bool _processingVoice;
    private readonly bool _voiceOnly;

    public MainForm(ConversationEngine engine, LocalSecretStore secrets, bool voiceOnly = false)
    {
        _engine = engine;
        _secrets = secrets;
        _voiceOnly = voiceOnly;

        Text = "LIC AI";
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
        Shown += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_secrets.GetApiKey()))
            {
                if (_voiceOnly) { Opacity = 1; ShowInTaskbar = true; WindowState = FormWindowState.Normal; }
                EnsureApiKey();
                if (string.IsNullOrWhiteSpace(_secrets.GetApiKey())) { Close(); return; }
                if (_voiceOnly) { Opacity = 0; ShowInTaskbar = false; WindowState = FormWindowState.Minimized; }
            }
            InitializeVoice();
            if (_voiceOnly) await ToggleVoiceAsync();
        };
        FormClosed += (_, _) => DisposeVoice();
    }

    private void BuildUi()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(18, 12, 18, 8) };
        var title = new Label
        {
            Text = "LIC AI",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(18, 10)
        };
        _status.Text = "Pronta para conversar";
        _status.AutoSize = true;
        _status.ForeColor = Color.FromArgb(165, 185, 220);
        _status.Location = new Point(21, 48);

        _keyButton.Text = "Configurar chave";
        _keyButton.Width = 140;
        _keyButton.Height = 34;
        _keyButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _keyButton.Location = new Point(Width - 185, 20);
        _keyButton.Click += (_, _) => ConfigureApiKey();
        top.Controls.Add(title);
        top.Controls.Add(_status);
        top.Controls.Add(_keyButton);
        top.Resize += (_, _) => _keyButton.Left = top.ClientSize.Width - _keyButton.Width - 18;

        _chat.Dock = DockStyle.Fill;
        _chat.ReadOnly = true;
        _chat.BorderStyle = BorderStyle.None;
        _chat.BackColor = Color.FromArgb(17, 24, 39);
        _chat.ForeColor = Color.FromArgb(235, 240, 250);
        _chat.Font = new Font("Segoe UI", 11F);
        _chat.DetectUrls = true;

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 96, Padding = new Padding(16, 14, 16, 14) };
        _send.Text = "Enviar";
        _send.Dock = DockStyle.Right;
        _send.Width = 110;
        _send.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        _send.Click += async (_, _) => await SendAsync();

        _voiceButton.Text = "🎙 FALAR";
        _voiceButton.Dock = DockStyle.Right;
        _voiceButton.Width = 125;
        _voiceButton.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        _voiceButton.BackColor = Color.FromArgb(0, 125, 190);
        _voiceButton.ForeColor = Color.White;
        _voiceButton.FlatStyle = FlatStyle.Flat;
        _voiceButton.FlatAppearance.BorderSize = 0;
        _voiceButton.Click += async (_, _) => await ToggleVoiceAsync();

        _input.Multiline = true;
        _input.AcceptsReturn = true;
        _input.ScrollBars = ScrollBars.Vertical;
        _input.Dock = DockStyle.Fill;
        _input.Font = new Font("Segoe UI", 11F);
        _input.BackColor = Color.FromArgb(28, 37, 54);
        _input.ForeColor = Color.White;
        _input.BorderStyle = BorderStyle.FixedSingle;
        _input.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await SendAsync();
            }
        };

        bottom.Controls.Add(_input);
        bottom.Controls.Add(_voiceButton);
        bottom.Controls.Add(_send);
        Controls.Add(_chat);
        Controls.Add(bottom);
        Controls.Add(top);
    }

    private void LoadHistory()
    {
        _chat.Clear();
        var recent = _engine.Recent();
        if (recent.Count == 0)
        {
            Append("LIC", "Oi. Eu sou a LIC AI. Pode me chamar de LIC. O que vamos conversar?");
            return;
        }

        foreach (var item in recent)
            Append(item.Role == "assistant" ? "LIC" : "Você", item.Content);
    }

    private async Task SendAsync(bool speakReply = false)
    {
        if (!_send.Enabled) return;
        var text = _input.Text.Trim();
        if (text.Length == 0) return;

        if (string.IsNullOrWhiteSpace(_secrets.GetApiKey()))
        {
            ConfigureApiKey();
            if (string.IsNullOrWhiteSpace(_secrets.GetApiKey())) return;
        }

        _input.Clear();
        Append("Você", text);
        SetBusy(true);
        _cts = new CancellationTokenSource();

        try
        {
            var reply = await _engine.SendNavigationAsync(text, _cts.Token);
            Append("LIC", reply.Mensagem);
            if (!string.IsNullOrWhiteSpace(reply.ComandoAbrirTela))
                await SendNavigationCommandAsync(reply.ComandoAbrirTela, _cts.Token);
            if (speakReply) await SpeakAndContinueAsync(reply.Mensagem);
        }
        catch (OperationCanceledException)
        {
            Append("LIC", "Resposta cancelada.");
        }
        catch (Exception ex)
        {
            Append("LIC", "Não consegui responder agora. " + ex.Message);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            SetBusy(false);
            _input.Focus();
        }
    }

    private void InitializeVoice()
    {
        _voiceButton.Enabled = true;
        _voiceButton.Text = "🎙 FALAR";
        _status.Text = "Pronta para conversar por texto ou voz";
    }

    private async Task ToggleVoiceAsync()
    {
        if (_voiceMode)
        {
            StopRecognition(true);
            return;
        }

        _voiceMode = true;
        _voiceButton.Enabled = false;
        _voiceButton.Text = "PREPARANDO...";
        try
        {
            await EnsureVoiceModelAsync();
            StartRecognition();
        }
        catch (Exception ex)
        {
            _voiceMode = false;
            _voiceButton.Enabled = true;
            _voiceButton.Text = "🎙 FALAR";
            _status.Text = "Não foi possível preparar a voz";
            MessageBox.Show(this, "Não foi possível ativar o reconhecimento de voz.\n\n" + ex.Message,
                "LIC AI por voz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task EnsureVoiceModelAsync()
    {
        if (_voiceModel != null && _voiceRecognizer != null) return;

        var voiceRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LEAL INFO CONECTADO", "LIC AI", "voz");
        var modelFolder = Path.Combine(voiceRoot, "vosk-model-small-pt-0.3");
        Directory.CreateDirectory(voiceRoot);

        if (!Directory.Exists(modelFolder))
        {
            _status.Text = "Baixando reconhecimento de voz gratuito (uma única vez)...";
            var zipPath = Path.Combine(voiceRoot, "portugues.zip");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = await http.GetAsync(
                "https://alphacephei.com/vosk/models/vosk-model-small-pt-0.3.zip",
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync())
            await using (var target = File.Create(zipPath))
                await source.CopyToAsync(target);
            ZipFile.ExtractToDirectory(zipPath, voiceRoot, true);
            File.Delete(zipPath);
        }

        Vosk.Vosk.SetLogLevel(-1);
        _voiceModel = new Model(modelFolder);
        _voiceRecognizer = new VoskRecognizer(_voiceModel, 16000f);
    }

    private void StartRecognition()
    {
        if (!_voiceMode || _voiceRecognizer == null || _recognizing) return;

        _microphone?.Dispose();
        _microphone = new WaveInEvent
        {
            DeviceNumber = 0,
            WaveFormat = new WaveFormat(16000, 1),
            BufferMilliseconds = 250
        };
        _microphone.DataAvailable += MicrophoneDataAvailable;
        _microphone.RecordingStopped += (_, _) => _recognizing = false;
        _processingVoice = false;
        _recognizing = true;
        _voiceButton.Enabled = true;
        _voiceButton.Text = "⏹ PARAR";
        _voiceButton.BackColor = Color.FromArgb(0, 175, 150);
        _status.Text = "LIA está ouvindo...";
        _microphone.StartRecording();
    }

    private void MicrophoneDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_voiceMode || _processingVoice || _voiceRecognizer == null) return;
        if (!_voiceRecognizer.AcceptWaveform(e.Buffer, e.BytesRecorded)) return;

        try
        {
            using var doc = JsonDocument.Parse(_voiceRecognizer.Result());
            var heard = doc.RootElement.GetProperty("text").GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(heard)) return;
            if (heard.Contains("encerrar conversa", StringComparison.OrdinalIgnoreCase) ||
                heard.Contains("parar conversa", StringComparison.OrdinalIgnoreCase) ||
                heard.Contains("desligar lia", StringComparison.OrdinalIgnoreCase))
            {
                BeginInvoke(() => { _voiceMode = false; DisposeVoice(); Close(); });
                return;
            }
            _processingVoice = true;
            BeginInvoke(async () =>
            {
                StopRecognition(false);
                _input.Text = heard;
                _status.Text = "Você disse: " + heard;
                await SendAsync(true);
            });
        }
        catch { _processingVoice = false; }
    }

    private void StopRecognition(bool turnOff)
    {
        if (turnOff) _voiceMode = false;
        try { _microphone?.StopRecording(); } catch { }
        _recognizing = false;
        _voiceButton.Enabled = true;
        _voiceButton.Text = _voiceMode ? "⏳ RESPONDENDO" : "🎙 FALAR";
        _voiceButton.BackColor = _voiceMode ? Color.FromArgb(90, 80, 180) : Color.FromArgb(0, 125, 190);
        if (!_voiceMode) _status.Text = "Pronta para conversar por texto ou voz";
    }

    private static readonly SemaphoreSlim TtsGate = new(1, 1);
    private const string AzureVoice = "pt-BR-FranciscaNeural";

    private static string VoiceCacheFolder
    {
        get
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LealInfoPdv", "voz-cache");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private static void TtsLog(string evt, Stopwatch sw, string? detail = null)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LealInfoPdv");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "lia-voz.log"),
                $"{DateTime.Now:O} {evt} ms={sw.ElapsedMilliseconds}{(string.IsNullOrWhiteSpace(detail) ? "" : " " + detail)}{Environment.NewLine}");
        }
        catch { }
    }

    private static string CachePath(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(AzureVoice + "\n" + text));
        return Path.Combine(VoiceCacheFolder, Convert.ToHexString(bytes).ToLowerInvariant() + ".wav");
    }

    private static async Task PlayWavAsync(string path, Stopwatch sw)
    {
        TtsLog("TTS_PLAY_START", sw);
        using var audio = new AudioFileReader(path);
        using var player = new WaveOutEvent();
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        player.PlaybackStopped += (_, e) =>
        {
            if (e.Exception != null) finished.TrySetException(e.Exception);
            else finished.TrySetResult(true);
        };
        player.Init(audio);
        player.Play();
        await finished.Task;
        TtsLog("TTS_PLAY_END", sw);
    }

    private static async Task SpeakLocalAsync(string text, Stopwatch sw, string reason)
    {
        TtsLog("TTS_FALLBACK_LOCAL", sw, "reason=" + reason.Replace(Environment.NewLine, " "));
        await Task.Run(() =>
        {
            using var synth = new SpeechSynthesizer();
            try
            {
                var pt = synth.GetInstalledVoices()
                    .FirstOrDefault(v => v.Enabled && v.VoiceInfo.Culture.Name.Equals("pt-BR", StringComparison.OrdinalIgnoreCase));
                if (pt != null) synth.SelectVoice(pt.VoiceInfo.Name);
            }
            catch { }
            synth.Speak(text);
        });
        TtsLog("TTS_PLAY_END", sw, "engine=local");
    }

    private async Task SpeakAndContinueAsync(string text)
    {
        var sw = Stopwatch.StartNew();
        await TtsGate.WaitAsync();
        try
        {
            _status.Text = "LIA está falando...";
            _voiceButton.Text = "🔊 FALANDO";
            var cache = CachePath(text);
            if (File.Exists(cache))
            {
                TtsLog("TTS_CACHE_HIT", sw);
                await PlayWavAsync(cache, sw);
                return;
            }

            var key = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
            var region = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(region))
            {
                await SpeakLocalAsync(text, sw, "AZURE_ENV_MISSING");
                return;
            }

            TtsLog("TTS_AZURE_REQUEST", sw);
            try
            {
                var config = SpeechConfig.FromSubscription(key, region);
                config.SpeechSynthesisVoiceName = AzureVoice;
                using var synthesizer = new SpeechSynthesizer(config, null);
                var azureTask = synthesizer.SpeakTextAsync(text);
                var completed = await Task.WhenAny(azureTask, Task.Delay(TimeSpan.FromSeconds(3)));
                if (completed != azureTask)
                {
                    TtsLog("TTS_CANCELLED", sw, "reason=AZURE_TIMEOUT_3S");
                    await SpeakLocalAsync(text, sw, "AZURE_TIMEOUT_3S");
                    return;
                }

                var result = await azureTask;
                if (result.Reason != ResultReason.SynthesizingAudioCompleted || result.AudioData == null || result.AudioData.Length == 0)
                {
                    var reason = result.Reason.ToString();
                    if (result.Reason == ResultReason.Canceled)
                    {
                        var details = SpeechSynthesisCancellationDetails.FromResult(result);
                        reason = $"{details.Reason}:{details.ErrorCode}:{details.ErrorDetails}";
                    }
                    await SpeakLocalAsync(text, sw, reason);
                    return;
                }

                await File.WriteAllBytesAsync(cache, result.AudioData);
                TtsLog("TTS_AZURE_READY", sw);
                await PlayWavAsync(cache, sw);
            }
            catch (Exception ex)
            {
                await SpeakLocalAsync(text, sw, ex.GetType().Name + ":" + ex.Message);
            }
        }
        catch (Exception ex)
        {
            TtsLog("TTS_FALLBACK_LOCAL", sw, "reason=PIPELINE:" + ex.GetType().Name);
            try { await SpeakLocalAsync(text, sw, "PIPELINE:" + ex.Message); } catch { TtsLog("TTS_CANCELLED", sw, "reason=LOCAL_TTS_FAILED"); }
        }
        finally
        {
            TtsGate.Release();
            if (_voiceMode)
            {
                await Task.Delay(300);
                _processingVoice = false;
                StartRecognition();
            }
        }
    }

    private void DisposeVoice()
    {
        _voiceMode = false;
        try { _microphone?.StopRecording(); } catch { }
        _microphone?.Dispose();
        _voiceRecognizer?.Dispose();
        _voiceModel?.Dispose();
    }

    private static async Task SendNavigationCommandAsync(
        string command,
        CancellationToken cancellationToken)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PRODUTOS", "CLIENTES", "FORNECEDORES", "SERVICOS",
            "ORDENS_SERVICO", "ORCAMENTOS", "FLUXO_CAIXA",
            "HISTORICO_VENDAS", "TELA_VENDAS", "RELATORIOS",
            "USUARIOS", "CONFIGURACOES", "CADASTROS", "AJUDA_CADASTRO"
        };

        command = command.Trim().ToUpperInvariant();
        if (!allowed.Contains(command)) return;

        try
        {
            using var pipe = new NamedPipeClientStream(
                ".", "LealInfoPDV.Navigation", PipeDirection.Out, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1500, cancellationToken);
            await using var writer = new StreamWriter(pipe) { AutoFlush = true };
            await writer.WriteLineAsync(command.AsMemory(), cancellationToken);
        }
        catch
        {
            // A resposta continua visível/audível mesmo se o PDV não estiver aberto.
        }
    }

    private void SetBusy(bool busy)
    {
        _send.Enabled = !busy;
        _input.Enabled = !busy;
        _status.Text = busy ? "LIC está pensando..." : "Pronta para conversar";
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

    private void EnsureApiKey()
    {
        if (string.IsNullOrWhiteSpace(_secrets.GetApiKey())) ConfigureApiKey();
        _input.Focus();
    }

    private void ConfigureApiKey()
    {
        using var dialog = new ApiKeyDialog(!string.IsNullOrWhiteSpace(_secrets.GetApiKey()));
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (dialog.ClearRequested)
        {
            _secrets.Clear();
            _status.Text = "Chave removida";
            return;
        }
        _secrets.SaveApiKey(dialog.ApiKey);
        _status.Text = "Chave salva com proteção do Windows";
    }

    private sealed class ApiKeyDialog : Form
    {
        private readonly TextBox _key = new();
        public string ApiKey => _key.Text.Trim();
        public bool ClearRequested { get; private set; }

        public ApiKeyDialog(bool hasKey)
        {
            Text = "Configurar OpenAI";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 180);
            BackColor = Color.FromArgb(20, 27, 42);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);

            var label = new Label
            {
                Text = hasKey ? "Já existe uma chave salva. Digite outra para substituir:" : "Cole sua chave da OpenAI:",
                Left = 18, Top = 18, Width = 480, Height = 24
            };
            _key.Left = 18; _key.Top = 52; _key.Width = 480; _key.Height = 30;
            _key.UseSystemPasswordChar = true;

            var ok = new Button { Text = "Salvar", Left = 286, Top = 108, Width = 100, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancelar", Left = 398, Top = 108, Width = 100, DialogResult = DialogResult.Cancel };
            var clear = new Button { Text = "Remover chave", Left = 18, Top = 108, Width = 130, Enabled = hasKey };
            clear.Click += (_, _) => { ClearRequested = true; DialogResult = DialogResult.OK; Close(); };
            ok.Click += (_, e) =>
            {
                if (_key.Text.Trim().Length < 20)
                {
                    MessageBox.Show(this, "Cole uma chave válida.", "LIC AI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                }
            };

            Controls.AddRange(new Control[] { label, _key, clear, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
