using LicAi.Core;
using LicAi.Security;
using System.IO.Pipes;

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

    private async Task SendTextAsync()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0 || !_send.Enabled) return;
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

    private static async Task<string> SendNavigationCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", "LealInfoPDV.Navigation", PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1500, cancellationToken);
            await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(command.AsMemory(), cancellationToken);
            using var reader = new StreamReader(pipe, leaveOpen: true);
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
