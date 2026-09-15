using LicAi.Core;
using LicAi.Security;
using System.Globalization;
using System.Speech.Recognition;
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
    private SpeechRecognitionEngine? _recognizer;
    private SpeechSynthesizer? _speaker;
    private bool _voiceMode;
    private bool _recognizing;

    public MainForm(ConversationEngine engine, LocalSecretStore secrets)
    {
        _engine = engine;
        _secrets = secrets;

        Text = "LIC AI";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 560);
        Size = new Size(980, 720);
        BackColor = Color.FromArgb(12, 18, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        BuildUi();
        LoadHistory();
        Shown += (_, _) => { EnsureApiKey(); InitializeVoice(); };
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
        _voiceButton.Click += (_, _) => ToggleVoice();

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
            var reply = await _engine.SendAsync(text, _cts.Token);
            Append("LIC", reply);
            if (speakReply) await SpeakAndContinueAsync(reply);
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
        try
        {
            _speaker = new SpeechSynthesizer();
            var preferred = _speaker.GetInstalledVoices()
                .FirstOrDefault(v => v.Enabled &&
                    (v.VoiceInfo.Culture.Name.Equals("pt-BR", StringComparison.OrdinalIgnoreCase) ||
                     v.VoiceInfo.Name.Contains("Francisca", StringComparison.OrdinalIgnoreCase) ||
                     v.VoiceInfo.Name.Contains("Maria", StringComparison.OrdinalIgnoreCase)));
            if (preferred != null) _speaker.SelectVoice(preferred.VoiceInfo.Name);
            _speaker.Rate = -1;
            _speaker.Volume = 100;

            try { _recognizer = new SpeechRecognitionEngine(CultureInfo.GetCultureInfo("pt-BR")); }
            catch { _recognizer = new SpeechRecognitionEngine(); }

            _recognizer.LoadGrammar(new DictationGrammar());
            _recognizer.SetInputToDefaultAudioDevice();
            _recognizer.SpeechRecognized += (_, e) =>
            {
                if (!_voiceMode || e.Result.Confidence < 0.45 || string.IsNullOrWhiteSpace(e.Result.Text)) return;
                var heard = e.Result.Text.Trim();
                BeginInvoke(async () =>
                {
                    StopRecognition(false);
                    _input.Text = heard;
                    _status.Text = "Você disse: " + heard;
                    await SendAsync(true);
                });
            };
            _recognizer.RecognizeCompleted += (_, _) => _recognizing = false;
            _voiceButton.Enabled = true;
            _status.Text = "Pronta para conversar por texto ou voz";
        }
        catch
        {
            _voiceButton.Enabled = false;
            _voiceButton.Text = "VOZ INDISPONÍVEL";
            _status.Text = "Instale o idioma Português (Brasil) no Windows para usar voz";
        }
    }

    private void ToggleVoice()
    {
        if (_recognizer == null || _speaker == null) return;
        _voiceMode = !_voiceMode;
        if (_voiceMode) StartRecognition();
        else StopRecognition(true);
    }

    private void StartRecognition()
    {
        if (!_voiceMode || _recognizer == null || _recognizing) return;
        try
        {
            _recognizing = true;
            _voiceButton.Text = "⏹ PARAR";
            _voiceButton.BackColor = Color.FromArgb(0, 175, 150);
            _status.Text = "LIA está ouvindo...";
            _recognizer.RecognizeAsync(RecognizeMode.Multiple);
        }
        catch
        {
            _recognizing = false;
            _voiceMode = false;
            _voiceButton.Text = "🎙 FALAR";
            _status.Text = "Não foi possível abrir o microfone";
        }
    }

    private void StopRecognition(bool turnOff)
    {
        if (turnOff) _voiceMode = false;
        if (_recognizer != null && _recognizing)
        {
            try { _recognizer.RecognizeAsyncCancel(); } catch { }
        }
        _recognizing = false;
        _voiceButton.Text = _voiceMode ? "⏳ RESPONDENDO" : "🎙 FALAR";
        _voiceButton.BackColor = _voiceMode ? Color.FromArgb(90, 80, 180) : Color.FromArgb(0, 125, 190);
        if (!_voiceMode) _status.Text = "Pronta para conversar por texto ou voz";
    }

    private async Task SpeakAndContinueAsync(string text)
    {
        if (_speaker == null) return;
        _status.Text = "LIA está falando...";
        _voiceButton.Text = "🔊 FALANDO";
        try { await Task.Run(() => _speaker.Speak(text)); }
        catch { }
        if (_voiceMode)
        {
            await Task.Delay(250);
            StartRecognition();
        }
    }

    private void DisposeVoice()
    {
        _voiceMode = false;
        try { _recognizer?.RecognizeAsyncCancel(); } catch { }
        _recognizer?.Dispose();
        _speaker?.Dispose();
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
