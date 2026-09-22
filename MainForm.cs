using Microsoft.Data.Sqlite;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System.Net.Http;
using System.Text.Json;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace LealInfoPDV;

public sealed class MainForm : Form
{
    private const uint GwHwndNext = 2;
    [DllImport("user32.dll")]
    private static extern IntPtr GetTopWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    private PictureBox? mainScreenPicture;

    private readonly Color Blue = Color.FromArgb(10, 104, 157);
    private readonly Color DarkBlue = Color.FromArgb(4, 70, 112);
    private readonly StatusStrip status = new();
    private readonly Label lowStockLabel = new();
    private readonly CancellationTokenSource navigationListenerCts = new();
    private bool navigationListenerStarted;
    private string lastAiNavigationCommand = "";
    private DateTime lastAiNavigationUtc = DateTime.MinValue;
    private bool automaticBackupCompleted;
    private bool automaticBackupRunning;

    public MainForm()
    {
        Text = "LEAL INFO CONECTADO - SISTEMA PDV - V10.134";
        WindowState = FormWindowState.Maximized;
        // Mantem o PDV dentro da area visivel tambem em monitores menores.
        MinimumSize = new Size(900, 600);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10);
        BuildUi();
        RefreshDashboard();
        FormClosing += MainForm_FormClosing;

        Shown += (_, _) =>
        {
            MaximizedBounds = Screen.FromControl(this).WorkingArea;
            WindowState = FormWindowState.Maximized;
            StartNavigationListener();

            if (GetSetting("company_registered", "0") != "1")
            {
                if (!ShowCompanyRegistration(true))
                {
                    Close();
                    return;
                }
            }

            if (Auth.IsAdmin && GetSetting("security_setup_completed", "0") != "1")
                ShowInitialSecuritySetup();

            OpenFirstAccessTutorial(true);
            ShowPostLoginWelcome();
            _ = UpdateManager.CheckForUpdatesAsync(this, true);
        };

        FormClosed += (_, _) =>
        {
            navigationListenerCts.Cancel();
            navigationListenerCts.Dispose();
        };
    }

    private void ShowPostLoginWelcome()
    {
        try
        {
            var bubble = new Panel
            {
                Width = 720,
                Height = 210,
                BackColor = Color.FromArgb(10, 35, 62)
            };
            bubble.Left = Math.Max(20, (ClientSize.Width - bubble.Width) / 2);
            bubble.Top = Math.Max(90, (ClientSize.Height - bubble.Height) / 2 - 30);
            bubble.Anchor = AnchorStyles.None;

            bubble.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = new Rectangle(2, 2, bubble.Width - 5, bubble.Height - 5);
                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                const int radius = 30;
                path.AddArc(rect.Left, rect.Top, radius, radius, 180, 90);
                path.AddArc(rect.Right-radius, rect.Top, radius, radius, 270, 90);
                path.AddArc(rect.Right-radius, rect.Bottom-radius, radius, radius, 0, 90);
                path.AddArc(rect.Left, rect.Bottom-radius, radius, radius, 90, 90);
                path.CloseFigure();
                using var glow = new Pen(Color.FromArgb(190, 80, 220, 255), 3.5f);
                e.Graphics.DrawPath(glow, path);
            };

            var title = new Label
            {
                Text = "BEM-VINDO AO FUTURO DO SEU NEGÓCIO",
                Dock = DockStyle.Top,
                Height = 78,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            var message = new Label
            {
                Text = "TECNOLOGIA E INTELIGÊNCIA TRABALHANDO COM VOCÊ.",
                Dock = DockStyle.Top,
                Height = 52,
                ForeColor = Color.FromArgb(115, 220, 255),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            var brand = new Label
            {
                Text = "LEAL INFO PDV PRO  •  Inteligência que simplifica. Tecnologia que conecta.",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10.5f),
                TextAlign = ContentAlignment.MiddleCenter
            };
            bubble.Controls.Add(brand);
            bubble.Controls.Add(message);
            bubble.Controls.Add(title);
            Controls.Add(bubble);
            bubble.BringToFront();

            var timer = new System.Windows.Forms.Timer { Interval = 5000 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                if (!bubble.IsDisposed)
                {
                    Controls.Remove(bubble);
                    bubble.Dispose();
                }
            };
            bubble.Disposed += (_, _) => { if (timer.Enabled) timer.Stop(); timer.Dispose(); };
            timer.Start();
        }
        catch
        {
            // O balão é puramente visual e nunca pode impedir a abertura do PDV.
        }
    }

    private void StartNavigationListener()
    {
        if (navigationListenerStarted) return;
        navigationListenerStarted = true;
        _ = ListenForNavigationCommandsAsync(navigationListenerCts.Token);
    }

    private async Task ListenForNavigationCommandsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    "LealInfoPDV.Navigation",
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(pipe, System.Text.Encoding.UTF8, true, 1024, true);
                var command = await reader.ReadLineAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(command) && !IsDisposed)
                {
                    var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                    BeginInvoke(() =>
                    {
                        try { completion.TrySetResult(ExecuteAiWindowCommand(command)); }
                        catch (Exception ex) { completion.TrySetResult("Nao consegui fechar essa janela: " + ex.Message); }
                    });
                    var response = await completion.Task.WaitAsync(cancellationToken);
                    await using var writer = new StreamWriter(pipe, System.Text.Encoding.UTF8, 1024, true) { AutoFlush = true };
                    await writer.WriteLineAsync(response.AsMemory(), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (!cancellationToken.IsCancellationRequested)
                    await Task.Delay(400, cancellationToken);
            }
        }
    }

    private string ExecuteAiWindowCommand(string command)
    {
        var normalized = (command ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized == "FECHAR_TELA") return CloseTopmostWindowFromAi();
        OpenScreenFromAi(normalized);
        return "Tela aberta com sucesso.";
    }

    private string CloseTopmostWindowFromAi()
    {
        var formsByHandle = Application.OpenForms.Cast<Form>()
            .Where(form => !form.IsDisposed && form.Visible && form.IsHandleCreated)
            .ToDictionary(form => form.Handle);
        Form? top = null;
        for (var handle = GetTopWindow(IntPtr.Zero); handle != IntPtr.Zero; handle = GetWindow(handle, GwHwndNext))
        {
            if (formsByHandle.TryGetValue(handle, out top)) break;
        }

        top ??= Form.ActiveForm;
        if (top != null && !ReferenceEquals(top, this))
        {
            var title = string.IsNullOrWhiteSpace(top.Text) ? "aviso" : top.Text.Trim();
            top.Close();
            return $"Fechando janela {title}.";
        }

        BeginInvoke(Close);
        return "Fechando tela principal.";
    }

    private void OpenScreenFromAi(string command)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OpenScreenFromAi(command));
            return;
        }

        command = (command ?? string.Empty).Trim().ToUpperInvariant();

        // Descarta comandos repetidos enviados em sequência pela mesma resposta/conversa.
        if (command == lastAiNavigationCommand && DateTime.UtcNow - lastAiNavigationUtc < TimeSpan.FromSeconds(2))
            return;
        lastAiNavigationCommand = command;
        lastAiNavigationUtc = DateTime.UtcNow;

        WindowState = FormWindowState.Maximized;
        Show();
        Activate();
        BringToFront();

        var screenTitles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["PRODUTOS"] = new[] { "PRODUTOS" },
            ["CLIENTES"] = new[] { "CLIENTES" },
            ["FORNECEDORES"] = new[] { "FORNECEDORES" },
            ["SERVICOS"] = new[] { "SERVIÇOS" },
            ["ORDENS_SERVICO"] = new[] { "ORDENS DE SERVIÇO" },
            ["ORCAMENTOS"] = new[] { "ORÇAMENTOS" },
            ["FLUXO_CAIXA"] = new[] { "FLUXO DE CAIXA" },
            ["HISTORICO_VENDAS"] = new[] { "HISTÓRICO DE VENDAS" },
            ["TELA_VENDAS"] = new[] { "LEAL INFO CONECTADO - TELA DE VENDAS" },
            ["USUARIOS"] = new[] { "Usuários e Níveis de Acesso" },
            ["CADASTROS"] = new[] { "Cadastros" },
            ["AJUDA_CADASTRO"] = new[] { "Central de Ajuda • Cadastro" },
            ["CONFIGURACOES"] = new[] { "Configurações do Sistema" }
        };

        if (screenTitles.TryGetValue(command, out var titles))
        {
            var open = Application.OpenForms.Cast<Form>().FirstOrDefault(form =>
                !ReferenceEquals(form, this) && titles.Any(title =>
                    form.Text.Equals(title, StringComparison.OrdinalIgnoreCase) ||
                    form.Text.StartsWith(title, StringComparison.OrdinalIgnoreCase)));
            if (open != null)
            {
                if (open.WindowState == FormWindowState.Minimized) open.WindowState = FormWindowState.Normal;
                open.BringToFront();
                open.Activate();
                open.Focus();
                return;
            }
        }

        switch (command)
        {
            case "PRODUTOS": OpenProducts(); break;
            case "CLIENTES": OpenCustomers(); break;
            case "FORNECEDORES": OpenSuppliers(); break;
            case "SERVICOS": OpenServices(); break;
            case "ORDENS_SERVICO": OpenOrders(); break;
            case "ORCAMENTOS": OpenQuotes(); break;
            case "FLUXO_CAIXA":
                if (Auth.CanViewFinance) OpenFinance();
                else Info("Acesso não permitido para seu nível.");
                break;
            case "HISTORICO_VENDAS": OpenHistory(); break;
            case "TELA_VENDAS": OpenSales(); break;
            case "RELATORIOS": OpenReports(); break;
            case "USUARIOS":
                if (Auth.CanManageUsers) OpenUsers();
                else Info("Acesso não permitido para seu nível.");
                break;
            case "CONFIGURACOES": OpenSettings(); break;
            case "CADASTROS": OpenCadastroCentral(); break;
            case "AJUDA_CADASTRO": ShowCadastroHelp(); break;
        }
    }

    private Form? firstAccessTutorial;

    private void OpenFirstAccessTutorial(bool automatic = false)
    {
        // V10.130: guia lateral de primeiro acesso. É modeless: o PDV continua clicável.
        // Fechar antes do fim não conclui o tutorial. O botão de Produtos só libera ao fim do vídeo.
        if (automatic && GetSetting("first_access_tutorial_completed", "0") == "1") return;
        if (firstAccessTutorial != null && !firstAccessTutorial.IsDisposed)
        {
            firstAccessTutorial.Activate();
            return;
        }

        var videoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "tutorial_primeiro_acesso.mp4");
        if (!File.Exists(videoPath))
        {
            if (!automatic) MessageBox.Show("Vídeo do tutorial não encontrado.", "Tutorial de Primeiro Acesso");
            return;
        }

        var f = new Form
        {
            Text = "LEAL INFO • Tutorial de Primeiro Acesso",
            StartPosition = FormStartPosition.Manual,
            Width = 520,
            Height = 700,
            MinimumSize = new Size(430, 560),
            FormBorderStyle = FormBorderStyle.SizableToolWindow,
            BackColor = Color.FromArgb(3, 18, 36),
            TopMost = true,
            ShowInTaskbar = false,
            Font = new Font("Segoe UI", 10)
        };
        firstAccessTutorial = f;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(14), BackColor = f.BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        f.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "CONHEÇA SEU LEAL INFO PDV\nAssista, pause e faça cada etapa no sistema.",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 0);

        var web = new WebView2 { Dock = DockStyle.Fill, BackColor = Color.Black };
        root.Controls.Add(web, 0, 1);

        var action = new Button
        {
            Text = "▶ TERMINE O VÍDEO PARA LIBERAR ESTA ETAPA",
            Dock = DockStyle.Fill,
            Enabled = false,
            BackColor = Color.FromArgb(4, 70, 112),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold)
        };
        action.FlatAppearance.BorderSize = 0;
        root.Controls.Add(action, 0, 2);

        root.Controls.Add(new Label
        {
            Text = "Fechou sem querer? AJUDA → Tutorial de Primeiro Acesso",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(120, 200, 235),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        }, 0, 3);

        action.Click += (_, _) =>
        {
            SetSetting("first_access_tutorial_completed", "1");
            f.Close();
            OpenProducts();
        };

        f.FormClosed += (_, _) => firstAccessTutorial = null;

        void PlaceAtRight()
        {
            var area = Screen.FromControl(this).WorkingArea;
            f.Height = Math.Min(720, Math.Max(560, area.Height - 80));
            f.Left = area.Right - f.Width - 18;
            f.Top = area.Top + Math.Max(18, (area.Height - f.Height) / 2);
        }
        PlaceAtRight();
        f.Shown += async (_, _) =>
        {
            try
            {
                await web.EnsureCoreWebView2Async();
                web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                web.CoreWebView2.Settings.AreDevToolsEnabled = false;
                web.CoreWebView2.WebMessageReceived += (_, e) =>
                {
                    if (e.TryGetWebMessageAsString() == "video-ended")
                    {
                        action.Enabled = true;
                        action.Text = "CADASTRAR MEU PRIMEIRO PRODUTO";
                        action.BackColor = Color.FromArgb(0, 163, 224);
                    }
                };
                web.CoreWebView2.SetVirtualHostNameToFolderMapping("appassets.local", Path.Combine(AppContext.BaseDirectory, "Assets"), CoreWebView2HostResourceAccessKind.Allow);
                var uri = "https://appassets.local/tutorial_primeiro_acesso.mp4";
                var html = $@"<!doctype html><html><body style='margin:0;background:#020a16;display:flex;height:100vh;align-items:center;justify-content:center;overflow:hidden'><video id='v' controls autoplay style='width:100%;height:100%;object-fit:contain;background:black'><source src='{uri}' type='video/mp4'></video><script>document.getElementById('v').addEventListener('ended',()=>chrome.webview.postMessage('video-ended'));</script></body></html>";
                web.NavigateToString(html);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível iniciar o vídeo do tutorial.\n\n" + ex.Message, "Tutorial");
            }
        };
        f.Show(this);
    }


    private static string GetSetting(string key, string fallback = "")
    {
        try
        {
            using var cn = Database.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key=$k";
            cmd.Parameters.AddWithValue("$k", key);
            return Convert.ToString(cmd.ExecuteScalar()) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static void SetSetting(string key, string value)
    {
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings(key,value) VALUES($k,$v)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value ?? "");
        cmd.ExecuteNonQuery();
    }

    private bool ShowCompanyRegistration(bool firstRun)
    {
        using var f = new Form
        {
            Text = firstRun ? "Cadastro Inicial da Empresa" : "Dados da Empresa",
            StartPosition = FormStartPosition.CenterParent,
            Width = 780,
            Height = 760,
            MinimumSize = new Size(760, 720),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(224, 239, 248),
            Font = new Font("Segoe UI", 10),
            KeyPreview = true
        };

        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.FromArgb(224, 239, 248),
            Padding = new Padding(0)
        };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        f.Controls.Add(page);

        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DarkBlue,
            Margin = new Padding(0)
        };
        header.Controls.Add(new Label
        {
            Text = firstRun ? "CADASTRO DA EMPRESA" : "EDITAR DADOS DA EMPRESA",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });
        page.Controls.Add(header, 0, 0);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 14,
            Padding = new Padding(34, 18, 34, 12),
            Margin = new Padding(0),
            BackColor = Color.FromArgb(224, 239, 248)
        };

        Label L(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(4, 55, 94),
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0)
        };

        TextBox T(string value = "") => new()
        {
            Text = value,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(8, 38, 68),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 2, 0, 7)
        };

        var companyName = T(GetSetting("company_name"));
        var tradeName = T(GetSetting("company_trade_name"));
        var document = T(GetSetting("company_document"));
        var phone = T(GetSetting("company_phone"));
        var address = T(GetSetting("company_address"));
        var cityState = T(GetSetting("company_city_state"));
        var footer = T(GetSetting("company_footer", "Obrigado pela preferência!"));

        var fields = new (string, TextBox)[]
        {
            ("Razão Social / Nome da Empresa", companyName),
            ("Nome Fantasia", tradeName),
            ("CNPJ / CPF", document),
            ("Telefone / WhatsApp", phone),
            ("Endereço", address),
            ("Cidade / UF", cityState),
            ("Mensagem no rodapé do cupom", footer)
        };

        int row = 0;
        foreach (var item in fields)
        {
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 7.142857f));
            body.Controls.Add(L(item.Item1), 0, row++);
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 7.142857f));
            body.Controls.Add(item.Item2, 0, row++);
        }
        page.Controls.Add(body, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(28, 12, 28, 10),
            Margin = new Padding(0),
            BackColor = Color.FromArgb(224, 239, 248)
        };

        var save = new Button
        {
            Text = "SALVAR",
            Width = 150,
            Height = 44,
            BackColor = Color.FromArgb(0, 163, 224),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold)
        };
        save.FlatAppearance.BorderSize = 0;

        var cancel = new Button
        {
            Text = firstRun ? "FECHAR PROGRAMA" : "CANCELAR",
            Width = 160,
            Height = 44,
            BackColor = Color.FromArgb(55, 88, 115),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        cancel.FlatAppearance.BorderSize = 0;

        actions.Controls.Add(save);
        actions.Controls.Add(cancel);
        page.Controls.Add(actions, 0, 2);

        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(companyName.Text))
            {
                MessageBox.Show(
                    "Informe o nome da empresa.",
                    "LEAL INFO PDV",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                companyName.Focus();
                return;
            }

            SetSetting("company_name", companyName.Text.Trim());
            SetSetting("company_trade_name", tradeName.Text.Trim());
            SetSetting("company_document", document.Text.Trim());
            SetSetting("company_phone", phone.Text.Trim());
            SetSetting("company_address", address.Text.Trim());
            SetSetting("company_city_state", cityState.Text.Trim());
            SetSetting("company_footer", footer.Text.Trim());
            SetSetting("company_registered", "1");

            f.DialogResult = DialogResult.OK;
            f.Close();
        };

        cancel.Click += (_, _) =>
        {
            f.DialogResult = DialogResult.Cancel;
            f.Close();
        };

        ApplyFloatingTheme(f);
        f.AcceptButton = save;
        f.CancelButton = cancel;

        return f.ShowDialog(this) == DialogResult.OK;
    }

    private sealed class LealMenuColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(0, 118, 178);
        public override Color MenuItemBorder => Color.FromArgb(65, 205, 255);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(0, 118, 178);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(0, 118, 178);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(0, 95, 150);
        public override Color MenuItemPressedGradientMiddle => Color.FromArgb(0, 105, 165);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(0, 95, 150);
        public override Color ToolStripDropDownBackground => Color.White;
        public override Color ImageMarginGradientBegin => Color.White;
        public override Color ImageMarginGradientMiddle => Color.White;
        public override Color ImageMarginGradientEnd => Color.White;
    }

    private void BuildUi()
    {
        var menu = new MenuStrip
        {
            BackColor = Blue,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Renderer = new ToolStripProfessionalRenderer(new LealMenuColors())
        };
        foreach (var title in new[] { "Cadastro", "Consulta", "Movimentação", "Financeiro", "Tela de Vendas", "Utilitários", "Relatórios", "Ajuda", "Sair" })
        {
            var item = new ToolStripMenuItem(title)
            {
                ForeColor = Color.White,
                BackColor = Blue
            };
            item.DropDownOpening += (_, _) =>
            {
                item.ForeColor = Color.White;
                item.BackColor = Color.FromArgb(0, 95, 150);
                item.Invalidate();
            };
            item.DropDownClosed += (_, _) =>
            {
                item.ForeColor = Color.White;
                item.BackColor = Blue;
                item.Invalidate();
            };

            void AddMenu(string text, Action action)
            {
                var sub = new ToolStripMenuItem(text)
                {
                    AutoSize = false,
                    Width = 245,
                    Height = 34,
                    ForeColor = Color.FromArgb(4,55,94)
                };
                sub.Click += (_,_) => action();
                item.DropDownItems.Add(sub);
            }

            if (title == "Cadastro")
            {
                // Botao direto: abre a central completa de cadastros com um clique.
                item.Click += (_, _) => OpenCadastroCentral();
            }
            else if (title == "Consulta")
            {
                AddMenu("Produtos", OpenProducts);
                AddMenu("Clientes", OpenCustomers);
                AddMenu("Histórico de vendas", OpenHistory);
                AddMenu("Ordens / OS", OpenOrders);
                AddMenu("Orçamentos", OpenQuotes);
            }
            else if (title == "Movimentação")
            {
                AddMenu("Tela de Vendas", OpenSales);
                AddMenu("Histórico de vendas", OpenHistory);
                AddMenu("Ordens / OS", OpenOrders);
            }
            else if (title == "Financeiro")
            {
                AddMenu("Fluxo de Caixa", () => { if (Auth.CanViewFinance) OpenFinance(); else MessageBox.Show("Acesso não permitido para seu nível."); });
            }
            else if (title == "Tela de Vendas")
            {
                // Botao direto: abre a tela de vendas com um clique.
                item.Click += (_, _) => OpenSales();
            }
            else if (title == "Utilitários")
            {
                AddMenu("Fazer Backup", () => _ = BackupAsync());
                AddMenu("Restaurar Backup", () => _ = RestoreBackupAsync());
                AddMenu("Configurações", OpenSettings);
            }
            else if (title == "Relatórios")
            {
                AddMenu("Abrir Relatórios", () => { if (Auth.CanViewReports) OpenReports(); else MessageBox.Show("Acesso não permitido para seu nível."); });
            }
            else if (title == "Ajuda")
            {
                AddMenu("Conheça o menu Cadastro", ShowCadastroHelp);
                AddMenu("Tutorial de Primeiro Acesso", () => OpenFirstAccessTutorial(false));
                AddMenu("Atalhos do PDV", () => MessageBox.Show("F2  Finalizar venda\nF5  Código do produto\nF7  Remover item\nESC  Fechar janela", "Atalhos do LEAL INFO PDV"));
                AddMenu("Atualizações do sistema", () => _ = UpdateManager.ShowUpdateCenterAsync(this));
                AddMenu("Sobre o sistema", () => MessageBox.Show($"LEAL INFO PDV PRO\nVersão V{UpdateManager.CurrentVersion}\nTecnologia que conecta.", "Sobre"));
            }
            else if (title == "Sair")
            {
                AddMenu("Sair do sistema", ConfirmExit);
            }

            menu.Items.Add(item);
        }
        Controls.Add(menu);

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 112,
            BackColor = Color.FromArgb(4, 55, 94),
            Padding = new Padding(3, 3, 3, 2),
            WrapContents = false,
            AutoScroll = false
        };
        Controls.Add(bar);
        bar.BringToFront();

        AddTool(bar, "PRODUTOS", "products.png", OpenProducts);
        AddTool(bar, "CLIENTES", "customers.png", OpenCustomers);
        AddTool(bar, "FORNECEDORES", "suppliers.png", OpenSuppliers);
        AddTool(bar, "SERVIÇOS", "services.png", OpenServices);
        AddTool(bar, "HISTÓRICO\nVENDAS", "history.png", OpenHistory);
        AddTool(bar, "FLUXO DE\nCAIXA", "finance.png", OpenFinance);
        AddTool(bar, "ORDENS /\nOS", "orders.png", OpenOrders);
        AddTool(bar, "ORÇAMENTOS", "quotes.png", OpenQuotes);
        AddTool(bar, "TELA DE\nVENDAS", "sales.png", OpenSales);
        AddTool(bar, "RELATÓRIOS", "reports.png", OpenReports);
        AddTool(bar, "FAZER\nBACKUP", "backup.png", () => _ = BackupAsync());
        AddTool(bar, "RESTAURAR\nBACKUP", "restore.png", () => _ = RestoreBackupAsync());
        AddTool(bar, "CONFIGURAÇÕES", "settings.png", OpenSettings);
        AddTool(bar, "SAIR", "exit.png", ConfirmExit);

        // Distribui todos os atalhos pela largura disponível.
        // Assim não existe barra de rolagem horizontal, independentemente
        // da resolução da tela.
        void ResizeShortcutBar()
        {
            if (bar.Controls.Count == 0) return;

            int usable = Math.Max(560, bar.ClientSize.Width - bar.Padding.Horizontal - 4);
            int each = Math.Max(58, usable / bar.Controls.Count);

            foreach (Control shortcut in bar.Controls)
            {
                shortcut.Width = Math.Max(56, each - shortcut.Margin.Horizontal);

                // Recentraliza ícone e texto conforme a largura real do card.
                if (shortcut.Controls.Count >= 2)
                {
                    var pic = shortcut.Controls.OfType<PictureBox>().FirstOrDefault();
                    var cap = shortcut.Controls.OfType<Label>().FirstOrDefault();
                    if (pic != null) pic.Left = (shortcut.Width - pic.Width) / 2;
                    if (cap != null)
                    {
                        cap.Width = shortcut.Width;
                        cap.Left = 0;
                    }
                }
            }
        }

        bar.SizeChanged += (_, _) => ResizeShortcutBar();
        Shown += (_, _) => ResizeShortcutBar();

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        Controls.Add(body);

        mainScreenPicture = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0),
            TabStop = false
        };

        var homeImage = LoadMainScreenImage();
        if (homeImage == null)
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
            if (File.Exists(logoPath))
            {
                using var fallback = Image.FromFile(logoPath);
                homeImage = new Bitmap(fallback);
            }
        }

        mainScreenPicture.Image = homeImage;
        body.Controls.Add(mainScreenPicture);
        mainScreenPicture.SendToBack();

        var monitor = new Panel
        {
            Width = 275,
            Height = 185,
            BackColor = Blue,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        var mt = new Label
        {
            Text = "MONITOR DE ESTOQUE",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            AutoSize = true,
            Left = 16,
            Top = 15
        };
        lowStockLabel.ForeColor = Color.White;
        lowStockLabel.Font = new Font("Segoe UI", 10);
        lowStockLabel.Left = 16;
        lowStockLabel.Top = 55;
        lowStockLabel.Width = 265;
        lowStockLabel.Height = 130;
        monitor.Controls.Add(mt);
        monitor.Controls.Add(lowStockLabel);
        // Monitor antigo removido da tela principal.
        // Monitor antigo nao e mais exibido.
        body.Resize += (_, _) =>
        {
            monitor.Left = Math.Max(10, body.ClientSize.Width - monitor.Width - 20);
            monitor.Top = 18;
        };

        status.BackColor = Blue;
        status.ForeColor = Color.White;
        status.Items.Add(new ToolStripStatusLabel("LEAL INFO CONECTADO"));
        status.Items.Add(new ToolStripStatusLabel { Spring = true, Text = $"Operador: {Auth.OperatorName} • {Auth.Current?.Role}" });
        status.Items.Add(new ToolStripStatusLabel($"Data: {DateTime.Now:dd/MM/yyyy}"));
        status.Items.Add(new ToolStripStatusLabel($"Serial: {Database.DeviceSerial()}"));
        status.Items.Add(new ToolStripStatusLabel($"V{UpdateManager.CurrentVersion}"));
        Controls.Add(status);
    }

    private void ShowCadastroHelp()
    {
        void RoundHelp(Control c, int radius)
        {
            void ApplyRoundHelp()
            {
                if (c.Width < 4 || c.Height < 4) return;

                var rect = new Rectangle(0, 0, c.Width - 1, c.Height - 1);
                int d = Math.Max(6, radius * 2);
                var gp = new System.Drawing.Drawing2D.GraphicsPath();

                gp.AddArc(rect.X, rect.Y, d, d, 180, 90);
                gp.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                gp.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                gp.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                gp.CloseFigure();

                c.Region?.Dispose();
                c.Region = new Region(gp);
                gp.Dispose();
            }

            c.HandleCreated += (_, _) => ApplyRoundHelp();
            c.Resize += (_, _) => ApplyRoundHelp();
            if (c.IsHandleCreated) ApplyRoundHelp();
        }

        using var f = new Form
        {
            Text = "Central de Ajuda • Cadastro",
            StartPosition = FormStartPosition.CenterScreen,
            Width = 1100,
            Height = 760,
            BackColor = Color.FromArgb(7,31,54),
            FormBorderStyle = FormBorderStyle.Sizable,
            MaximizeBox = true,
            MinimizeBox = false,
            AutoScaleMode = AutoScaleMode.None,
            KeyPreview = true
        };

        var header = new Label
        {
            Text = "GUIA VISUAL • CADASTRO",
            Left = 0,
            Top = 0,
            Width = 964,
            Height = 70,
            BackColor = Color.FromArgb(4,55,94),
            ForeColor = Color.White,
            Font = new Font("Segoe UI",20,FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        f.Controls.Add(header);

        var stepTitle = new Label
        {
            Left = 30,
            Top = 86,
            Width = 904,
            Height = 48,
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI",18,FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        f.Controls.Add(stepTitle);

        var mock = new Panel
        {
            Left = 48,
            Top = 145,
            Width = 868,
            Height = 330,
            BackColor = Color.FromArgb(238,248,255),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        f.Controls.Add(mock);
        RoundHelp(mock,22);

        var instruction = new Label
        {
            Left = 48,
            Top = 490,
            Width = 868,
            Height = 72,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(205,235,250),
            Font = new Font("Segoe UI",11.5f,FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        f.Controls.Add(instruction);

        var prev = new Button
        {
            Text = "◀  ANTERIOR",
            Left = 48,
            Top = 585,
            Width = 180,
            Height = 48,
            BackColor = Color.FromArgb(55,88,115),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI",10.5f,FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        prev.FlatAppearance.BorderSize=0;
        RoundHelp(prev,14);
        f.Controls.Add(prev);

        var counter = new Label
        {
            Left = 392,
            Top = 585,
            Width = 180,
            Height = 48,
            ForeColor = Color.FromArgb(185,230,250),
            Font = new Font("Segoe UI",11,FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Bottom
        };
        f.Controls.Add(counter);

        var next = new Button
        {
            Text = "PRÓXIMO  ▶",
            Left = 736,
            Top = 585,
            Width = 180,
            Height = 48,
            BackColor = Color.FromArgb(0,163,224),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI",10.5f,FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        next.FlatAppearance.BorderSize=0;
        RoundHelp(next,14);
        f.Controls.Add(next);

        Label BoxLabel(string text,int x,int y,int w,int h,Color bg,Color fg,float size=10)
        {
            var c=new Label
            {
                Text=text,
                Left=x,
                Top=y,
                Width=w,
                Height=h,
                BackColor=bg,
                ForeColor=fg,
                Font=new Font("Segoe UI",size,FontStyle.Bold),
                TextAlign=ContentAlignment.MiddleCenter
            };
            mock.Controls.Add(c);
            RoundHelp(c,12);
            return c;
        }

        void Glow(Control c)
        {
            var glow=new Panel
            {
                Left=c.Left-5,
                Top=c.Top-5,
                Width=c.Width+10,
                Height=c.Height+10,
                BackColor=Color.FromArgb(0,210,255)
            };
            mock.Controls.Add(glow);
            glow.SendToBack();
            RoundHelp(glow,15);
        }

        int step=0;
        const int totalSteps = 7;

        Label InfoCard(string title, string body, int x, int y, int w, int h, Color accent)
        {
            var card = new Label
            {
                Text = title + "\n\n" + body,
                Left = x,
                Top = y,
                Width = w,
                Height = h,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(4,55,94),
                Font = new Font("Segoe UI",9.8f,FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18,12,18,12)
            };
            mock.Controls.Add(card);
            RoundHelp(card,14);

            var stripe = new Panel
            {
                Left = x,
                Top = y,
                Width = 7,
                Height = h,
                BackColor = accent
            };
            mock.Controls.Add(stripe);
            stripe.BringToFront();
            return card;
        }

        void AddProgress()
        {
            int dot = 24;
            int gap = 12;
            int total = totalSteps * dot + (totalSteps - 1) * gap;
            int x = Math.Max(20, (mock.Width - total) / 2);
            int y = Math.Max(8, mock.Height - 40);
            for (int i = 0; i < totalSteps; i++)
            {
                var d = new Label
                {
                    Text = (i + 1).ToString(),
                    Left = x + i * (dot + gap),
                    Top = y,
                    Width = dot,
                    Height = dot,
                    BackColor = i == step ? Color.FromArgb(0,163,224) : Color.FromArgb(196,216,230),
                    ForeColor = i == step ? Color.White : Color.FromArgb(4,55,94),
                    Font = new Font("Segoe UI",8.5f,FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                mock.Controls.Add(d);
                RoundHelp(d,12);
            }
        }

        void Render()
        {
            mock.Controls.Clear();
            counter.Text=$"{step+1} de {totalSteps}";
            prev.Enabled=step>0;
            prev.BackColor = step>0 ? Color.FromArgb(55,88,115) : Color.FromArgb(42,65,84);
            next.Text=step==totalSteps-1 ? "CONCLUIR  ✓" : "PRÓXIMO  ▶";

            if(step==0)
            {
                stepTitle.Text="PASSO 1 • CONHEÇA O MENU CADASTRO";
                instruction.Text="O menu CADASTRO reúne quatro áreas: Produtos, Clientes, Fornecedores e Serviços.";

                BoxLabel("CADASTRO",30,28,180,42,Color.FromArgb(0,118,178),Color.White,12);
                BoxLabel("1",42,92,34,34,Color.FromArgb(0,163,224),Color.White,10);
                BoxLabel("PRODUTOS",88,88,220,42,Color.FromArgb(8,59,98),Color.White,11);
                BoxLabel("2",42,143,34,34,Color.FromArgb(0,163,224),Color.White,10);
                BoxLabel("CLIENTES",88,139,220,42,Color.FromArgb(8,59,98),Color.White,11);
                BoxLabel("3",42,194,34,34,Color.FromArgb(0,163,224),Color.White,10);
                BoxLabel("FORNECEDORES",88,190,220,42,Color.FromArgb(8,59,98),Color.White,11);
                BoxLabel("4",42,245,34,34,Color.FromArgb(0,163,224),Color.White,10);
                BoxLabel("SERVIÇOS",88,241,220,42,Color.FromArgb(8,59,98),Color.White,11);

                InfoCard("PARA QUE SERVE?",
                    "Use este menu para criar e manter os cadastros que serão usados nas vendas, consultas e ordens de serviço.",
                    365,68,450,170,Color.FromArgb(0,163,224));
                BoxLabel("Nas próximas telas, cada opção será explicada separadamente.",365,252,450,44,
                    Color.FromArgb(225,242,252),Color.FromArgb(4,55,94),9.5f);
            }
            else if(step==1)
            {
                stepTitle.Text="PASSO 2 • CADASTRO > PRODUTOS";
                instruction.Text="Cadastre os itens vendidos e controle preço, estoque mínimo e foto do produto.";

                BoxLabel("PRODUTOS / ESTOQUE",28,20,500,42,Color.FromArgb(4,55,94),Color.White,13);
                BoxLabel("Código de barras",35,72,180,26,Color.Transparent,Color.FromArgb(4,55,94),9);
                BoxLabel("7890000000000",35,100,250,36,Color.White,Color.FromArgb(4,55,94),9.5f);
                BoxLabel("Nome do produto",310,72,180,26,Color.Transparent,Color.FromArgb(4,55,94),9);
                BoxLabel("PRODUTO EXEMPLO",310,100,250,36,Color.White,Color.FromArgb(4,55,94),9.5f);
                BoxLabel("Categoria",35,146,120,26,Color.Transparent,Color.FromArgb(4,55,94),9);
                BoxLabel("INFORMÁTICA",35,174,180,36,Color.White,Color.FromArgb(4,55,94),9.5f);
                BoxLabel("Custo",235,146,100,26,Color.Transparent,Color.FromArgb(4,55,94),9);
                BoxLabel("R$ 7,00",235,174,135,36,Color.White,Color.FromArgb(4,55,94),9.5f);
                BoxLabel("Venda",390,146,100,26,Color.Transparent,Color.FromArgb(4,55,94),9);
                BoxLabel("R$ 10,00",390,174,135,36,Color.White,Color.FromArgb(4,55,94),9.5f);
                BoxLabel("Estoque",35,220,100,26,Color.Transparent,Color.FromArgb(4,55,94),9);
                BoxLabel("25,000",35,248,135,36,Color.White,Color.FromArgb(4,55,94),9.5f);
                BoxLabel("Estoque mínimo",190,220,150,26,Color.Transparent,Color.FromArgb(4,55,94),9);
                BoxLabel("5,000",190,248,135,36,Color.White,Color.FromArgb(4,55,94),9.5f);
                var photo=BoxLabel("📷  FOTO",390,226,135,58,Color.FromArgb(0,118,178),Color.White,10); Glow(photo);

                InfoCard("O QUE VOCÊ FAZ AQUI",
                    "• Código de barras identifica o item.\n• Nome e categoria organizam a busca.\n• Custo e venda registram os valores.\n• Estoque e mínimo ajudam no controle.\n• Foto facilita reconhecer o produto.",
                    555,48,290,238,Color.FromArgb(0,163,224));
            }
            else if(step==2)
            {
                stepTitle.Text="PASSO 3 • CADASTRO > CLIENTES";
                instruction.Text="Guarde os dados dos clientes para consultas, vendas e ordens de serviço.";

                BoxLabel("CADASTRO DE CLIENTE",28,20,500,42,Color.FromArgb(4,55,94),Color.White,13);
                string[] labs={"Nome","CPF/CNPJ","Telefone","E-mail","Endereço"};
                string[] vals={"CLIENTE EXEMPLO","000.000.000-00","(24) 99999-9999","cliente@email.com","Rua / Bairro / Cidade"};
                int y=78;
                for(int i=0;i<labs.Length;i++)
                {
                    BoxLabel(labs[i],38,y,115,34,Color.Transparent,Color.FromArgb(4,55,94),9);
                    var fld=BoxLabel(vals[i],165,y,355,36,Color.White,Color.FromArgb(4,55,94),9.5f);
                    if(i==0) Glow(fld);
                    y+=47;
                }
                InfoCard("QUANDO USAR",
                    "Cadastre o cliente quando quiser manter nome e contato disponíveis no sistema. O NOME é obrigatório; os demais dados podem ser preenchidos conforme a necessidade.",
                    570,70,260,205,Color.FromArgb(0,163,224));
            }
            else if(step==3)
            {
                stepTitle.Text="PASSO 4 • CADASTRO > FORNECEDORES";
                instruction.Text="Cadastre empresas e parceiros que fornecem produtos ou serviços para sua loja.";

                BoxLabel("CADASTRO DE FORNECEDOR",28,20,500,42,Color.FromArgb(4,55,94),Color.White,13);
                string[] labs={"Nome / Empresa","CPF/CNPJ","Telefone","E-mail","Endereço"};
                string[] vals={"FORNECEDOR EXEMPLO","00.000.000/0001-00","(24) 99999-9999","contato@empresa.com","Rua / Bairro / Cidade"};
                int y=78;
                for(int i=0;i<labs.Length;i++)
                {
                    BoxLabel(labs[i],38,y,125,34,Color.Transparent,Color.FromArgb(4,55,94),9);
                    var fld=BoxLabel(vals[i],175,y,345,36,Color.White,Color.FromArgb(4,55,94),9.3f);
                    if(i==0) Glow(fld);
                    y+=47;
                }
                InfoCard("PARA QUE SERVE",
                    "Use este cadastro para registrar fornecedores e deixar os contatos centralizados. Isso facilita localizar rapidamente empresa, documento, telefone, e-mail e endereço.",
                    570,70,260,205,Color.FromArgb(0,163,224));
            }
            else if(step==4)
            {
                stepTitle.Text="PASSO 5 • CADASTRO > SERVIÇOS";
                instruction.Text="Cadastre os serviços prestados e deixe o valor pronto para reutilizar no atendimento.";

                BoxLabel("CADASTRO DE SERVIÇO",28,20,500,42,Color.FromArgb(4,55,94),Color.White,13);
                BoxLabel("Serviço",40,88,110,30,Color.Transparent,Color.FromArgb(4,55,94),9);
                var serv=BoxLabel("FORMATAÇÃO DE COMPUTADOR",40,120,490,42,Color.White,Color.FromArgb(4,55,94),9.5f); Glow(serv);
                BoxLabel("Valor",40,180,110,30,Color.Transparent,Color.FromArgb(4,55,94),9);
                BoxLabel("R$ 120,00",40,212,190,42,Color.White,Color.FromArgb(4,55,94),9.5f);
                BoxLabel("Descrição",260,180,120,30,Color.Transparent,Color.FromArgb(4,55,94),9);
                BoxLabel("Descrição do serviço executado",260,212,270,74,Color.White,Color.FromArgb(4,55,94),9.2f);
                InfoCard("COMO FUNCIONA",
                    "Informe o nome do serviço, o valor cobrado e uma descrição. Depois ele fica disponível no cadastro para consulta e reutilização.",
                    570,82,260,180,Color.FromArgb(0,163,224));
            }
            else if(step==5)
            {
                stepTitle.Text="PASSO 6 • NOVO, EDITAR E EXCLUIR";
                instruction.Text="Nas listas de cadastro, use os botões de ação para manter seus registros atualizados.";

                BoxLabel("AÇÕES DO CADASTRO",28,25,802,42,Color.FromArgb(4,55,94),Color.White,13);
                var novo=BoxLabel("＋  NOVO",45,95,215,62,Color.FromArgb(0,163,224),Color.White,12); Glow(novo);
                var editar=BoxLabel("✎  EDITAR",325,95,215,62,Color.FromArgb(4,105,160),Color.White,12); Glow(editar);
                var excluir=BoxLabel("🗑  EXCLUIR",605,95,190,62,Color.FromArgb(180,66,66),Color.White,12); Glow(excluir);
                InfoCard("NOVO","Cria um novo registro e abre os campos para preenchimento.",45,190,215,105,Color.FromArgb(0,163,224));
                InfoCard("EDITAR","Selecione um registro da lista e altere os dados já cadastrados.",325,190,215,105,Color.FromArgb(4,105,160));
                InfoCard("EXCLUIR","Remove o cadastro selecionado. Confirme somente quando tiver certeza.",605,190,190,105,Color.FromArgb(180,66,66));
            }
            else
            {
                stepTitle.Text="CADASTRO • GUIA CONCLUÍDO";
                instruction.Text="Você já conhece as quatro áreas do Cadastro e as principais ações. Clique em CONCLUIR para voltar ao PDV.";
                BoxLabel("✓",330,38,210,118,Color.FromArgb(0,170,105),Color.White,42);
                BoxLabel("PRODUTOS  •  CLIENTES  •  FORNECEDORES  •  SERVIÇOS",105,180,660,52,Color.FromArgb(9,52,88),Color.White,11.5f);
                InfoCard("PRONTO PARA USAR",
                    "Entre em CADASTRO, escolha a área desejada e use NOVO para começar. Revise os dados antes de salvar.",
                    205,238,460,54,Color.FromArgb(0,170,105));
            }

            AddProgress();
        }

        prev.Click += (_,_) => { if(step>0){step--;Render();} };
        next.Click += (_,_) => { if(step<totalSteps-1){step++;Render();} else f.Close(); };

        f.KeyDown += (_,e) =>
        {
            if(e.KeyCode==Keys.Right && step<totalSteps-1){step++;Render();}
            else if(e.KeyCode==Keys.Left && step>0){step--;Render();}
            else if(e.KeyCode==Keys.Escape) f.Close();
        };

        f.Load += (_,_) =>
        {
            var area = Screen.FromControl(this).WorkingArea;

            // Abre grande de verdade, respeitando apenas a área útil do monitor.
            int w = Math.Min(1100, area.Width - 40);
            int h = Math.Min(760, area.Height - 40);
            f.Bounds = new Rectangle(
                area.Left + (area.Width - w) / 2,
                area.Top + (area.Height - h) / 2,
                w,
                h);

            // Reposiciona a estrutura principal com base no tamanho REAL da janela.
            header.Width = f.ClientSize.Width;
            stepTitle.Width = f.ClientSize.Width - 60;
            mock.Width = f.ClientSize.Width - 96;
            mock.Height = Math.Max(340, f.ClientSize.Height - 330);

            instruction.Top = f.ClientSize.Height - 180;
            instruction.Width = f.ClientSize.Width - 96;

            prev.Top = f.ClientSize.Height - 75;
            next.Top = f.ClientSize.Height - 75;
            next.Left = f.ClientSize.Width - next.Width - 48;
            counter.Top = f.ClientSize.Height - 75;
            counter.Left = (f.ClientSize.Width - counter.Width) / 2;

            // Renderiza somente depois que o tamanho real da janela estiver definido.
            // Evita cartões/progresso calculados com a altura inicial e textos cortados.
            Render();
        };

        f.Resize += (_,_) =>
        {
            if (!f.IsHandleCreated) return;
            header.Width = f.ClientSize.Width;
            stepTitle.Width = Math.Max(300, f.ClientSize.Width - 60);
            mock.Width = Math.Max(500, f.ClientSize.Width - 96);
            mock.Height = Math.Max(340, f.ClientSize.Height - 330);
            instruction.Top = f.ClientSize.Height - 180;
            instruction.Width = Math.Max(500, f.ClientSize.Width - 96);
            prev.Top = f.ClientSize.Height - 75;
            next.Top = f.ClientSize.Height - 75;
            next.Left = f.ClientSize.Width - next.Width - 48;
            counter.Top = f.ClientSize.Height - 75;
            counter.Left = (f.ClientSize.Width - counter.Width) / 2;

            // Recalcula os elementos internos para nenhuma etapa ficar cortada ao redimensionar.
            Render();
        };

        f.ShowDialog(this);
    }


    private void ConfirmExit()
    {
        var r = MessageBox.Show(
            "Deseja realmente sair do LEAL INFO PDV?",
            "Confirmar saída",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (r == DialogResult.Yes)
            Close();
    }

    private void AddTool(Control parent, string text, string iconFile, Action action)
    {
        const int cardW = 92;
        const int cardH = 104;
        var card = new Panel
        {
            Width = cardW,
            Height = cardH,
            Margin = new Padding(1),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        bool hover = false;
        int pulse = 0;
        bool pulseUp = true;
        string normalizedText = text.Trim();
        // Todos os atalhos ficam estaveis e usam apenas o destaque suave ao passar o mouse.
        bool shouldPulse = false;
        var pulseTimer = new System.Windows.Forms.Timer { Interval = 70 };

        card.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int visualPulse = shouldPulse ? pulse : 0;
            int inset = Math.Max(1, 3 - visualPulse / 4);
            var rect = new Rectangle(inset, inset, card.Width - inset * 2 - 1, card.Height - inset * 2 - 1);
            const int radius = 20;
            using var gp = new System.Drawing.Drawing2D.GraphicsPath();
            gp.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            gp.AddArc(rect.Right-radius, rect.Y, radius, radius, 270, 90);
            gp.AddArc(rect.Right-radius, rect.Bottom-radius, radius, radius, 0, 90);
            gp.AddArc(rect.X, rect.Bottom-radius, radius, radius, 90, 90);
            gp.CloseFigure();

            int lift = visualPulse * 5;
            using var bg = new System.Drawing.Drawing2D.LinearGradientBrush(rect,
                hover ? Color.FromArgb(22, 170, 235) : Color.FromArgb(8, 115 + lift, 180 + lift),
                Color.FromArgb(2, 28, 66), 90f);
            e.Graphics.FillPath(bg, gp);

            int alpha = Math.Min(255, 105 + visualPulse * 18 + (hover ? 45 : 0));
            using var glow = new Pen(Color.FromArgb(alpha, 80, 225, 255), hover ? 4.5f : 3.2f + visualPulse * 0.12f);
            e.Graphics.DrawPath(glow, gp);

            var innerRect = Rectangle.Inflate(rect, -4, -4);
            using var innerPath = new System.Drawing.Drawing2D.GraphicsPath();
            innerPath.AddArc(innerRect.X, innerRect.Y, radius - 4, radius - 4, 180, 90);
            innerPath.AddArc(innerRect.Right-(radius-4), innerRect.Y, radius - 4, radius - 4, 270, 90);
            innerPath.AddArc(innerRect.Right-(radius-4), innerRect.Bottom-(radius-4), radius - 4, radius - 4, 0, 90);
            innerPath.AddArc(innerRect.X, innerRect.Bottom-(radius-4), radius - 4, radius - 4, 90, 90);
            innerPath.CloseFigure();
            using var innerGlow = new Pen(Color.FromArgb(70 + visualPulse * 10, 210, 250, 255), 1.2f);
            e.Graphics.DrawPath(innerGlow, innerPath);
        };

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", iconFile);
        PictureBox? icon = null;
        if (File.Exists(iconPath))
        {
            using var source = Image.FromFile(iconPath);
            icon = new PictureBox
            {
                Width = 58,
                Height = 58,
                Left = (cardW - 58) / 2,
                Top = 5,
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = new Bitmap(source),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            card.Controls.Add(icon);
        }

        var caption = new Label
        {
            Text = normalizedText,
            Dock = DockStyle.Bottom,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            AutoEllipsis = false,
            Cursor = Cursors.Hand,
            Padding = new Padding(3)
        };
        card.Controls.Add(caption);

        if (shouldPulse)
        {
            pulseTimer.Tick += (_, _) =>
            {
                pulse += pulseUp ? 1 : -1;
                if (pulse >= 7) { pulse = 7; pulseUp = false; }
                if (pulse <= 0) { pulse = 0; pulseUp = true; }
                card.Invalidate();
            };
            pulseTimer.Start();
        }

        void SetHover(bool on)
        {
            hover = on;
            caption.Font = new Font("Segoe UI", on ? 9.7f : 9.2f, FontStyle.Bold);
            card.Invalidate();
        }
        void Enter(object? s, EventArgs e) => SetHover(true);
        void Leave(object? s, EventArgs e)
        {
            var pt = card.PointToClient(Cursor.Position);
            if (!card.ClientRectangle.Contains(pt)) SetHover(false);
        }
        card.MouseEnter += Enter;
        card.MouseLeave += Leave;
        caption.MouseEnter += Enter;
        caption.MouseLeave += Leave;
        if (icon != null)
        {
            icon.MouseEnter += Enter;
            icon.MouseLeave += Leave;
        }
        void Run(object? s, EventArgs e) => action();
        card.Click += Run;
        caption.Click += Run;
        if (icon != null) icon.Click += Run;
        card.Disposed += (_, _) =>
        {
            pulseTimer.Dispose();
            icon?.Image?.Dispose();
        };
        parent.Controls.Add(card);
    }
private void ApplyFloatingTheme(Form f)
    {
        f.BackColor = Color.FromArgb(224, 239, 248);
        f.Font = new Font("Segoe UI", 10);

        void RoundControl(Control c, int radius)
        {
            void Apply()
            {
                if (c.Width < 4 || c.Height < 4) return;
                var rect = new Rectangle(0, 0, c.Width, c.Height);
                var gp = new System.Drawing.Drawing2D.GraphicsPath();
                int d = Math.Max(6, radius * 2);
                gp.AddArc(rect.X, rect.Y, d, d, 180, 90);
                gp.AddArc(rect.Right - d - 1, rect.Y, d, d, 270, 90);
                gp.AddArc(rect.Right - d - 1, rect.Bottom - d - 1, d, d, 0, 90);
                gp.AddArc(rect.X, rect.Bottom - d - 1, d, d, 90, 90);
                gp.CloseFigure();
                c.Region?.Dispose();
                c.Region = new Region(gp);
                gp.Dispose();
            }
            c.HandleCreated += (_, _) => Apply();
            c.Resize += (_, _) => Apply();
            if (c.IsHandleCreated) Apply();
        }

        void StyleRecursive(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is TextBox tb)
                {
                    tb.BackColor = Color.White;
                    tb.ForeColor = Color.FromArgb(8, 38, 68);
                    tb.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    RoundControl(tb, 10);
                }
                else if (c is ComboBox cb)
                {
                    cb.BackColor = Color.White;
                    cb.ForeColor = Color.FromArgb(8, 38, 68);
                    cb.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                    RoundControl(cb, 10);
                }
                else if (c is NumericUpDown nud)
                {
                    nud.BackColor = Color.White;
                    nud.ForeColor = Color.FromArgb(8, 38, 68);
                    nud.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
                    RoundControl(nud, 10);
                }
                else if (c is Button b)
                {
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderSize = 0;
                    b.Cursor = Cursors.Hand;
                    if (b.BackColor == SystemColors.Control || b.BackColor == Color.Empty)
                        b.BackColor = Color.FromArgb(0, 145, 210);
                    if (b.ForeColor == SystemColors.ControlText || b.ForeColor == Color.Empty)
                        b.ForeColor = Color.White;
                    b.Font = new Font("Segoe UI", Math.Max(9f, b.Font.Size), FontStyle.Bold);
                    RoundControl(b, 12);
                }
                else if (c is DataGridView dg)
                {
                    dg.BorderStyle = BorderStyle.None;
                    dg.BackgroundColor = Color.White;
                    dg.EnableHeadersVisualStyles = false;
                    dg.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(205, 232, 247);
                    dg.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(4, 55, 94);
                    dg.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                    dg.DefaultCellStyle.SelectionBackColor = Color.FromArgb(190, 232, 250);
                    dg.DefaultCellStyle.SelectionForeColor = Color.FromArgb(4, 45, 82);
                    RoundControl(dg, 12);
                }
                else if (c is Label lbl)
                {
                    if (lbl.BackColor == Color.Transparent || lbl.BackColor == SystemColors.Control)
                        lbl.ForeColor = Color.FromArgb(4, 55, 94);
                }
                else if (c is Panel pnl && pnl.BackColor == Color.White)
                {
                    RoundControl(pnl, 18);
                }

                if (c.HasChildren)
                    StyleRecursive(c);
            }
        }

        StyleRecursive(f);
    }

    private void RefreshDashboard()
    {
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
        SELECT
          (SELECT COUNT(*) FROM products WHERE active=1),
          (SELECT COUNT(*) FROM products WHERE active=1 AND stock <= min_stock),
          (SELECT COUNT(*) FROM sales);
        """;
        using var rd = cmd.ExecuteReader();
        if (rd.Read())
        {
            lowStockLabel.Text =
                $"Produtos abaixo do mínimo\n{rd.GetInt32(1)} produto(s)\n\n" +
                $"Produtos cadastrados\n{rd.GetInt32(0)} produto(s)\n\n" +
                $"Vendas realizadas\n{rd.GetInt32(2)} venda(s)";
        }
    }

    private void OpenProducts() => ShowCrud(
        "PRODUTOS / ESTOQUE",
        "SELECT id AS ID, barcode AS Código, name AS Produto, category AS Categoria, printf('R$ %.2f',price) AS Venda, stock AS Estoque, min_stock AS Mínimo FROM products WHERE active=1 ORDER BY name",
        () => EditProduct(null),
        id => EditProduct(id),
        id =>
        {
            if (Confirm("Excluir este produto?"))
            {
                Exec("UPDATE products SET active=0 WHERE id=$id", ("$id", id));
                RefreshDashboard();
            }
        });

    private void EditProduct(long? id)
    {
        using var f = new Form
        {
            Text = id.HasValue ? "Editar Produto" : "Novo Produto",
            StartPosition = FormStartPosition.CenterParent,
            Width = 900,
            Height = 650,
            MinimumSize = new Size(860, 620),
            BackColor = Color.FromArgb(238, 246, 252),
            Font = new Font("Segoe UI", 10)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(18),
            BackColor = Color.FromArgb(238, 246, 252)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        f.Controls.Add(root);

        void RoundProductControl(Control c, int radius)
        {
            void Apply()
            {
                if (c.Width <= 1 || c.Height <= 1) return;
                var r = new Rectangle(0, 0, c.Width, c.Height);
                var gp = new System.Drawing.Drawing2D.GraphicsPath();
                int d = Math.Max(4, radius * 2);
                gp.AddArc(r.X, r.Y, d, d, 180, 90);
                gp.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
                gp.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
                gp.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
                gp.CloseFigure();
                c.Region?.Dispose();
                c.Region = new Region(gp);
                gp.Dispose();
            }
            c.Resize += (_, _) => Apply();
            c.HandleCreated += (_, _) => Apply();
        }

        var fieldsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(224, 239, 248),
            Padding = new Padding(22)
        };
        root.Controls.Add(fieldsPanel, 0, 0);
        RoundProductControl(fieldsPanel, 24);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 15,
            BackColor = Color.FromArgb(224, 239, 248)
        };
        fieldsPanel.Controls.Add(fields);

        TextBox Field(string placeholder = "")
        {
            var box = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 19, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(8, 38, 68),
                PlaceholderText = placeholder,
                Margin = new Padding(0, 3, 0, 7),
                Padding = new Padding(10, 8, 10, 8)
            };
            RoundProductControl(box, 12);
            return box;
        }

        Label Lbl(string s) => new()
        {
            Text = s,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(4, 55, 94),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        };

        var barcode = Field();
        var name = Field();
        var category = Field();
        var cost = Field("0,00");
        var price = Field("0,00");
        var stock = Field("0");
        var minStock = Field("0");

        // Cadastro inteligente refinado: confirmação discreta e foco em Custo.
        // Primeiro consulta Open Food Facts; se não houver produto, consulta Open Products Facts.
        // Funciona com leitores que enviam ENTER e com leitores que apenas digitam o EAN/GTIN.
        var barcodeLookupTimer = new System.Windows.Forms.Timer { Interval = 650 };
        var barcodeLookupRunning = false;
        string lastBarcodeLookup = "";
        var lookupStatus = new Label
        {
            Text = "Aguardando leitura do código de barras...",
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = Color.FromArgb(4, 105, 165),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 4, 0)
        };

        void SetLookupStatus(string text, bool error = false)
        {
            if (f.IsDisposed) return;
            lookupStatus.Text = text;
            lookupStatus.ForeColor = error ? Color.FromArgb(190, 45, 45) : Color.FromArgb(4, 105, 165);
        }

        bool IsBarcodeLengthValid(string code) => code.Length is 8 or 12 or 13 or 14;

        async Task<JsonElement?> TryFindProductAsync(HttpClient http, string baseUrl, string code)
        {
            var url = $"{baseUrl}/api/v2/product/{Uri.EscapeDataString(code)}.json?fields=product_name,product_name_pt,brands,categories_tags";
            using var response = await http.GetAsync(url);

            // Produto ausente nesta base: não é erro; apenas tenta a próxima.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var rootJson = doc.RootElement;

            if (rootJson.TryGetProperty("status", out var statusJson) && statusJson.ValueKind == JsonValueKind.Number && statusJson.GetInt32() == 0)
                return null;

            if (!rootJson.TryGetProperty("product", out var product) || product.ValueKind != JsonValueKind.Object)
                return null;

            return product.Clone();
        }

        async Task LookupBarcodeOnlineAsync()
        {
            var code = new string(barcode.Text.Where(char.IsDigit).ToArray());
            if (!IsBarcodeLengthValid(code))
            {
                SetLookupStatus($"Código com {code.Length} dígitos — aguardando 8, 12, 13 ou 14.");
                return;
            }
            if (barcodeLookupRunning)
            {
                SetLookupStatus("Consulta já está em andamento...");
                return;
            }
            if (code == lastBarcodeLookup)
            {
                SetLookupStatus($"Código {code} já consultado nesta tentativa.");
                return;
            }

            // Primeiro respeita o cadastro local: nunca sobrescreve produto existente no PDV.
            using (var local = Database.Open())
            using (var cmd = local.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM products WHERE barcode=$b AND active=1 LIMIT 1";
                cmd.Parameters.AddWithValue("$b", code);
                var existing = cmd.ExecuteScalar()?.ToString();
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    SetLookupStatus($"Código já cadastrado: {existing}");
                    MessageBox.Show(f, $"Este código já está cadastrado como:\n\n{existing}", "Produto já cadastrado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            barcodeLookupRunning = true;
            lastBarcodeLookup = code;
            var oldCursor = f.Cursor;
            f.Cursor = Cursors.WaitCursor;
            barcode.Enabled = false;

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("LEAL-INFO-PDV/10.130 (cadastro-inteligente)");

                SetLookupStatus($"Consultando {code} em Open Food Facts...");
                var product = await TryFindProductAsync(http, "https://world.openfoodfacts.org", code);
                var source = "Open Food Facts";

                if (product is null)
                {
                    SetLookupStatus($"Não encontrado em alimentos. Consultando produtos gerais...");
                    product = await TryFindProductAsync(http, "https://world.openproductsfacts.org", code);
                    source = "Open Products Facts";
                }

                if (product is null)
                {
                    SetLookupStatus($"Produto {code} não encontrado online. Preencha manualmente.", true);
                    name.Focus();
                    return;
                }

                var pjson = product.Value;
                string ReadString(string prop) => pjson.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";
                var productName = ReadString("product_name_pt");
                if (string.IsNullOrWhiteSpace(productName)) productName = ReadString("product_name");
                var brand = ReadString("brands");

                if (!string.IsNullOrWhiteSpace(productName))
                    name.Text = string.IsNullOrWhiteSpace(brand) || productName.Contains(brand, StringComparison.OrdinalIgnoreCase)
                        ? productName
                        : $"{productName} - {brand}";

                if (string.IsNullOrWhiteSpace(category.Text) && pjson.TryGetProperty("categories_tags", out var cats) && cats.ValueKind == JsonValueKind.Array)
                {
                    string fallbackCategory = "";
                    foreach (var c in cats.EnumerateArray())
                    {
                        var raw = c.GetString() ?? "";
                        if (string.IsNullOrWhiteSpace(raw)) continue;
                        if (raw.StartsWith("pt:", StringComparison.OrdinalIgnoreCase))
                        {
                            fallbackCategory = raw[3..].Replace('-', ' ');
                            break;
                        }
                        if (string.IsNullOrWhiteSpace(fallbackCategory))
                            fallbackCategory = raw.Contains(':') ? raw[(raw.IndexOf(':') + 1)..].Replace('-', ' ') : raw.Replace('-', ' ');
                    }
                    if (!string.IsNullOrWhiteSpace(fallbackCategory))
                        category.Text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(fallbackCategory);
                }

                if (!string.IsNullOrWhiteSpace(name.Text))
                {
                    SetLookupStatus($"ENCONTRADO em {source}: {name.Text}");
                    SetLookupStatus($"✓ Produto encontrado online — {name.Text}");
                    cost.Focus();
                }
                else
                {
                    SetLookupStatus($"Código encontrado em {source}, porém sem nome. Preencha manualmente.", true);
                    name.Focus();
                }
            }
            catch (HttpRequestException ex)
            {
                SetLookupStatus($"Falha de comunicação: {ex.Message}", true);
                MessageBox.Show(f, $"Não foi possível consultar as bases online agora.\n\n{ex.Message}\n\nO cadastro manual continua disponível.", "Cadastro inteligente", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                name.Focus();
            }
            catch (TaskCanceledException)
            {
                SetLookupStatus("Consulta online excedeu o tempo limite. Preencha manualmente.", true);
                name.Focus();
            }
            catch (Exception ex)
            {
                SetLookupStatus($"Erro na consulta: {ex.Message}", true);
                MessageBox.Show(f, $"Ocorreu um erro durante a consulta online.\n\n{ex.Message}\n\nO cadastro manual continua disponível.", "Cadastro inteligente", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                name.Focus();
            }
            finally
            {
                barcode.Enabled = true;
                f.Cursor = oldCursor;
                barcodeLookupRunning = false;
            }
        }

        barcode.TextChanged += (_, _) =>
        {
            barcodeLookupTimer.Stop();
            var code = new string(barcode.Text.Where(char.IsDigit).ToArray());
            if (code != lastBarcodeLookup) lastBarcodeLookup = "";

            if (IsBarcodeLengthValid(code))
            {
                SetLookupStatus($"Código detectado: {code}. Consultando automaticamente...");
                barcodeLookupTimer.Start();
            }
            else if (code.Length > 0)
            {
                SetLookupStatus($"Lendo código... {code.Length} dígitos recebidos.");
            }
        };

        barcodeLookupTimer.Tick += async (_, _) =>
        {
            barcodeLookupTimer.Stop();
            await LookupBarcodeOnlineAsync();
        };

        barcode.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            barcodeLookupTimer.Stop();
            await LookupBarcodeOnlineAsync();
        };

        var controls = new (string label, Control input)[]
        {
            ("Código de barras", barcode),
            ("Nome", name),
            ("Categoria", category),
            ("Custo", cost),
            ("Preço de venda", price),
            ("Estoque", stock),
            ("Estoque mínimo", minStock)
        };

        int row = 0;
        foreach (var x in controls)
        {
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            fields.Controls.Add(Lbl(x.label), 0, row++);
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            fields.Controls.Add(x.input, 0, row++);

            if (ReferenceEquals(x.input, barcode))
            {
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
                fields.Controls.Add(lookupStatus, 0, row++);
            }
        }
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var photoSide = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(8, 59, 98),
            Padding = new Padding(20)
        };
        root.Controls.Add(photoSide, 1, 0);
        RoundProductControl(photoSide, 24);

        var photoLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Color.Transparent
        };
        photoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        photoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        photoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        photoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        photoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        photoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        photoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        photoSide.Controls.Add(photoLayout);

        photoLayout.Controls.Add(new Label
        {
            Text = "FOTO DO PRODUTO",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 0);

        var preview = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(8)
        };
        photoLayout.Controls.Add(preview, 0, 1);
        RoundProductControl(preview, 18);

        string? selectedPhoto = null;

        void LoadPreview(string? path)
        {
            preview.Image?.Dispose();
            preview.Image = null;

            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                using var img = Image.FromFile(path);
                preview.Image = new Bitmap(img);
                return;
            }

            var logo = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
            if (File.Exists(logo))
            {
                using var img = Image.FromFile(logo);
                preview.Image = new Bitmap(img);
            }
        }

        var chooseLocal = new Button
        {
            Text = "SELECIONAR FOTO DO COMPUTADOR",
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 4, 8, 4),
            BackColor = Color.FromArgb(0, 145, 210),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        chooseLocal.FlatAppearance.BorderSize = 0;
        RoundProductControl(chooseLocal, 14);
        photoLayout.Controls.Add(chooseLocal, 0, 2);

        var webCheck = new CheckBox
        {
            Text = "Buscar foto na Web",
            Dock = DockStyle.Fill,
            Margin = new Padding(12, 4, 8, 4),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Checked = false
        };
        var webCheckHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 3, 8, 3),
            BackColor = Color.FromArgb(18, 82, 128),
            Padding = new Padding(10, 0, 0, 0)
        };
        webCheck.Dock = DockStyle.Fill;
        webCheck.Margin = new Padding(0);
        webCheckHost.Controls.Add(webCheck);
        photoLayout.Controls.Add(webCheckHost, 0, 3);
        RoundProductControl(webCheckHost, 13);

        var webButton = new Button
        {
            Text = "PESQUISAR IMAGENS NA WEB",
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 4, 8, 4),
            BackColor = Color.FromArgb(28, 96, 135),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Enabled = false
        };
        webButton.FlatAppearance.BorderSize = 0;
        RoundProductControl(webButton, 14);
        photoLayout.Controls.Add(webButton, 0, 4);

        var useDownloaded = new Button
        {
            Text = "USAR IMAGEM BAIXADA",
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 4, 8, 4),
            BackColor = Color.FromArgb(28, 96, 135),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Enabled = false
        };
        useDownloaded.FlatAppearance.BorderSize = 0;
        RoundProductControl(useDownloaded, 14);
        photoLayout.Controls.Add(useDownloaded, 0, 5);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6)
        };
        var save = new Button
        {
            Text = "SALVAR",
            Width = 115,
            Height = 40,
            BackColor = Color.FromArgb(0, 170, 220),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        var cancel = new Button
        {
            Text = "CANCELAR",
            Width = 115,
            Height = 40,
            BackColor = Color.FromArgb(55, 88, 115),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        save.FlatAppearance.BorderSize = 0;
        RoundProductControl(save, 14);
        cancel.FlatAppearance.BorderSize = 0;
        RoundProductControl(cancel, 14);
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        photoLayout.Controls.Add(buttons, 0, 6);

        chooseLocal.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Selecionar foto do produto",
                Filter = "Imagens|*.jpg;*.jpeg;*.png;*.webp;*.bmp"
            };
            if (dlg.ShowDialog(f) == DialogResult.OK)
            {
                selectedPhoto = dlg.FileName;
                LoadPreview(selectedPhoto);
            }
        };

        webCheck.CheckedChanged += (_, _) =>
        {
            webButton.Enabled = webCheck.Checked;
            useDownloaded.Enabled = webCheck.Checked;
        };

        webButton.Click += (_, _) =>
        {
            var term = string.IsNullOrWhiteSpace(name.Text)
                ? "produto"
                : name.Text.Trim();

            var url = "https://www.bing.com/images/search?q=" +
                      Uri.EscapeDataString(term + " produto");
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                MessageBox.Show(
                    "Escolha uma imagem no navegador e salve no computador.\n\nDepois volte ao cadastro e clique em \"USAR IMAGEM BAIXADA\".",
                    "Buscar foto na Web",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível abrir a busca na Web.\n\n" + ex.Message);
            }
        };

        useDownloaded.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Selecionar a imagem baixada da Web",
                Filter = "Imagens|*.jpg;*.jpeg;*.png;*.webp;*.bmp"
            };
            if (dlg.ShowDialog(f) == DialogResult.OK)
            {
                selectedPhoto = dlg.FileName;
                LoadPreview(selectedPhoto);
            }
        };

        if (id.HasValue)
        {
            using var cn = Database.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                SELECT COALESCE(barcode,''), name, COALESCE(category,''),
                       cost, price, stock, min_stock, COALESCE(photo_path,'')
                FROM products WHERE id=$id
                """;
            cmd.Parameters.AddWithValue("$id", id.Value);
            using var rd = cmd.ExecuteReader();
            if (rd.Read())
            {
                barcode.Text = rd.GetString(0);
                name.Text = rd.GetString(1);
                category.Text = rd.GetString(2);
                cost.Text = rd.GetDouble(3).ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
                price.Text = rd.GetDouble(4).ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
                stock.Text = rd.GetDouble(5).ToString("N3", CultureInfo.GetCultureInfo("pt-BR"));
                minStock.Text = rd.GetDouble(6).ToString("N3", CultureInfo.GetCultureInfo("pt-BR"));
                selectedPhoto = rd.GetString(7);
            }
        }

        LoadPreview(selectedPhoto);

        cancel.Click += (_, _) => f.Close();

        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text))
            {
                Info("Informe o nome do produto.");
                name.Focus();
                return;
            }

            string? finalPhotoPath = selectedPhoto;
            if (!string.IsNullOrWhiteSpace(selectedPhoto) && File.Exists(selectedPhoto))
            {
                var productPhotos = Path.Combine(Database.AppFolder, "ProductImages");
                Directory.CreateDirectory(productPhotos);

                var ext = Path.GetExtension(selectedPhoto);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

                var dest = Path.Combine(
                    productPhotos,
                    $"produto_{(id?.ToString() ?? Guid.NewGuid().ToString("N"))}{ext.ToLowerInvariant()}");

                if (!Path.GetFullPath(selectedPhoto).Equals(Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                    File.Copy(selectedPhoto, dest, true);

                finalPhotoPath = dest;
            }

            using var cn = Database.Open();
            using var cmd = cn.CreateCommand();

            if (id.HasValue)
            {
                cmd.CommandText = """
                    UPDATE products
                    SET barcode=$b,name=$n,category=$c,cost=$co,price=$p,
                        stock=$s,min_stock=$m,photo_path=$photo
                    WHERE id=$id
                    """;
                cmd.Parameters.AddWithValue("$id", id.Value);
            }
            else
            {
                cmd.CommandText = """
                    INSERT INTO products(barcode,name,category,cost,price,stock,min_stock,photo_path)
                    VALUES($b,$n,$c,$co,$p,$s,$m,$photo)
                    """;
            }

            cmd.Parameters.AddWithValue("$b", barcode.Text.Trim());
            cmd.Parameters.AddWithValue("$n", name.Text.Trim());
            cmd.Parameters.AddWithValue("$c", category.Text.Trim());
            cmd.Parameters.AddWithValue("$co", Num(cost.Text));
            cmd.Parameters.AddWithValue("$p", Num(price.Text));
            cmd.Parameters.AddWithValue("$s", Num(stock.Text));
            cmd.Parameters.AddWithValue("$m", Num(minStock.Text));
            cmd.Parameters.AddWithValue("$photo", (object?)finalPhotoPath ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            RefreshDashboard();
            f.DialogResult = DialogResult.OK;
            f.Close();
        };

        ApplyFloatingTheme(f);


        f.ShowDialog(this);
    }

    private void OpenCustomers() => ShowPersonCrud("CLIENTES", "customers");
    private void OpenSuppliers() => ShowPersonCrud("FORNECEDORES", "suppliers");

    private void ShowPersonCrud(string title, string table)
    {
        ShowCrud(title,
            $"SELECT id AS ID,name AS Nome,document AS Documento,phone AS Telefone,email AS Email,address AS Endereço FROM {table} ORDER BY name",
            () => EditPerson(table, null, title[..^1]),
            id => EditPerson(table, id, title[..^1]),
            id => { if (Confirm("Excluir este cadastro?")) Exec($"DELETE FROM {table} WHERE id=$id", ("$id",id)); });
    }

    private void EditPerson(string table, long? id, string title)
    {
        var f = Editor(title, new[] { "Nome", "CPF/CNPJ", "Telefone", "E-mail", "Endereço" });
        if (id.HasValue)
        {
            using var cn = Database.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT name,document,phone,email,address FROM {table} WHERE id=$id";
            cmd.Parameters.AddWithValue("$id",id.Value);
            using var rd=cmd.ExecuteReader();
            if(rd.Read()) FillEditor(f, rd.GetString(0),rd.GetString(1),rd.GetString(2),rd.GetString(3),rd.GetString(4));
        }
        ApplyFloatingTheme(f);

        if(f.ShowDialog(this)==DialogResult.OK)
        {
            var v=EditorValues(f);
            if(string.IsNullOrWhiteSpace(v[0])) { Info("Informe o nome."); return; }
            if(id.HasValue)
                Exec($"UPDATE {table} SET name=$n,document=$d,phone=$p,email=$e,address=$a WHERE id=$id",
                    ("$n",v[0]),("$d",v[1]),("$p",v[2]),("$e",v[3]),("$a",v[4]),("$id",id.Value));
            else
                Exec($"INSERT INTO {table}(name,document,phone,email,address) VALUES($n,$d,$p,$e,$a)",
                    ("$n",v[0]),("$d",v[1]),("$p",v[2]),("$e",v[3]),("$a",v[4]));
        }
    }

    private void OpenServices() => ShowCrud("SERVIÇOS",
        "SELECT id AS ID,name AS Serviço,printf('R$ %.2f',price) AS Valor,description AS Descrição FROM services ORDER BY name",
        () => EditService(null),
        id => EditService(id),
        id => { if(Confirm("Excluir este serviço?")) Exec("DELETE FROM services WHERE id=$id",("$id",id)); });

    private void EditService(long? id)
    {
        var f=Editor("Serviço",new[]{"Serviço","Valor","Descrição"});
        if(id.HasValue)
        {
            using var cn=Database.Open(); using var cmd=cn.CreateCommand();
            cmd.CommandText="SELECT name,price,description FROM services WHERE id=$id"; cmd.Parameters.AddWithValue("$id",id.Value);
            using var rd=cmd.ExecuteReader(); if(rd.Read()) FillEditor(f,rd.GetString(0),rd.GetDouble(1),rd.GetString(2));
        }
        ApplyFloatingTheme(f);

        if(f.ShowDialog(this)==DialogResult.OK)
        {
            var v=EditorValues(f);
            if(id.HasValue) Exec("UPDATE services SET name=$n,price=$p,description=$d WHERE id=$id",("$n",v[0]),("$p",Num(v[1])),("$d",v[2]),("$id",id.Value));
            else Exec("INSERT INTO services(name,price,description) VALUES($n,$p,$d)",("$n",v[0]),("$p",Num(v[1])),("$d",v[2]));
        }
    }

    private void OpenOrders() => ShowCrud("ORDENS DE SERVIÇO",
        "SELECT id AS ID,opened_at AS Data,customer_name AS Cliente,equipment AS Equipamento,defect AS Defeito,status AS Status,printf('R$ %.2f',amount) AS Valor FROM service_orders ORDER BY id DESC",
        () => EditOrder(null),
        id => EditOrder(id),
        id => { if(Confirm("Excluir esta OS?")) Exec("DELETE FROM service_orders WHERE id=$id",("$id",id)); });

    private void EditOrder(long? id)
    {
        var f=Editor("Ordem de Serviço",new[]{"Cliente","Equipamento","Defeito / Reclamação","Serviço realizado","Status","Valor","Observações"});
        if(id.HasValue)
        {
            using var cn=Database.Open(); using var cmd=cn.CreateCommand();
            cmd.CommandText="SELECT customer_name,equipment,defect,service_done,status,amount,notes FROM service_orders WHERE id=$id"; cmd.Parameters.AddWithValue("$id",id.Value);
            using var rd=cmd.ExecuteReader(); if(rd.Read()) FillEditor(f,rd.GetString(0),rd.GetString(1),rd.GetString(2),rd.GetString(3),rd.GetString(4),rd.GetDouble(5),rd.GetString(6));
        }
        ApplyFloatingTheme(f);

        if(f.ShowDialog(this)==DialogResult.OK)
        {
            var v=EditorValues(f);
            if(id.HasValue) Exec("""UPDATE service_orders SET customer_name=$c,equipment=$e,defect=$d,service_done=$s,status=$st,amount=$a,notes=$n WHERE id=$id""",
                ("$c",v[0]),("$e",v[1]),("$d",v[2]),("$s",v[3]),("$st",v[4]),("$a",Num(v[5])),("$n",v[6]),("$id",id.Value));
            else Exec("""INSERT INTO service_orders(opened_at,customer_name,equipment,defect,service_done,status,amount,notes) VALUES($dt,$c,$e,$d,$s,$st,$a,$n)""",
                ("$dt",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),("$c",v[0]),("$e",v[1]),("$d",v[2]),("$s",v[3]),("$st",string.IsNullOrWhiteSpace(v[4])?"ABERTA":v[4]),("$a",Num(v[5])),("$n",v[6]));
        }
    }

    private void OpenQuotes() => ShowCrud("ORÇAMENTOS",
        "SELECT id AS ID,created_at AS Data,customer_name AS Cliente,description AS Descrição,printf('R$ %.2f',amount) AS Valor,status AS Status FROM quotes ORDER BY id DESC",
        () => EditQuote(null),
        id => EditQuote(id),
        id => { if(Confirm("Excluir este orçamento?")) Exec("DELETE FROM quotes WHERE id=$id",("$id",id)); });

    private void EditQuote(long? id)
    {
        var f=Editor("Orçamento",new[]{"Cliente","Descrição","Valor","Status"});
        if(id.HasValue)
        {
            using var cn=Database.Open(); using var cmd=cn.CreateCommand();
            cmd.CommandText="SELECT customer_name,description,amount,status FROM quotes WHERE id=$id";cmd.Parameters.AddWithValue("$id",id.Value);
            using var rd=cmd.ExecuteReader();if(rd.Read())FillEditor(f,rd.GetString(0),rd.GetString(1),rd.GetDouble(2),rd.GetString(3));
        }
        ApplyFloatingTheme(f);

        if(f.ShowDialog(this)==DialogResult.OK)
        {
            var v=EditorValues(f);
            if(id.HasValue) Exec("UPDATE quotes SET customer_name=$c,description=$d,amount=$a,status=$s WHERE id=$id",("$c",v[0]),("$d",v[1]),("$a",Num(v[2])),("$s",v[3]),("$id",id.Value));
            else Exec("INSERT INTO quotes(created_at,customer_name,description,amount,status) VALUES($dt,$c,$d,$a,$s)",("$dt",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),("$c",v[0]),("$d",v[1]),("$a",Num(v[2])),("$s",string.IsNullOrWhiteSpace(v[3])?"PENDENTE":v[3]));
        }
    }

    private void OpenFinance() => ShowCrud("FLUXO DE CAIXA",
        "SELECT id AS ID,occurred_at AS Data,type AS Tipo,description AS Descrição,printf('R$ %.2f',amount) AS Valor FROM cash_movements ORDER BY id DESC",
        () =>
        {
            var f=Editor("Lançamento Financeiro",new[]{"Tipo (ENTRADA/SAÍDA)","Descrição","Valor"});
            if(f.ShowDialog(this)==DialogResult.OK){var v=EditorValues(f);Exec("INSERT INTO cash_movements(occurred_at,type,description,amount) VALUES($d,$t,$x,$a)",("$d",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),("$t",v[0]),("$x",v[1]),("$a",Num(v[2])));}
        }, null,
        id=>{if(Confirm("Excluir este lançamento?"))Exec("DELETE FROM cash_movements WHERE id=$id",("$id",id));});

    private void OpenHistory() => ShowReadOnly("HISTÓRICO DE VENDAS",
        "SELECT id AS Venda,sold_at AS Data,payment AS Pagamento,printf('R$ %.2f',subtotal) AS Subtotal,printf('R$ %.2f',discount) AS Desconto,printf('R$ %.2f',total) AS Total,operator AS Operador FROM sales ORDER BY id DESC");

    private void OpenReports()
    {
        using var cn=Database.Open();
        long products=ScalarLong(cn,"SELECT COUNT(*) FROM products WHERE active=1");
        long clients=ScalarLong(cn,"SELECT COUNT(*) FROM customers");
        long sales=ScalarLong(cn,"SELECT COUNT(*) FROM sales");
        double total=ScalarDouble(cn,"SELECT COALESCE(SUM(total),0) FROM sales");
        double entries=ScalarDouble(cn,"SELECT COALESCE(SUM(amount),0) FROM cash_movements WHERE upper(type) NOT LIKE '%SAÍDA%'");
        double exits=ScalarDouble(cn,"SELECT COALESCE(SUM(amount),0) FROM cash_movements WHERE upper(type) LIKE '%SAÍDA%'");
        long low=ScalarLong(cn,"SELECT COUNT(*) FROM products WHERE active=1 AND stock<=min_stock");
        MessageBox.Show(
            $"RELATÓRIO GERAL\n\nProdutos: {products}\nClientes: {clients}\nVendas: {sales}\nTotal vendido: {Money(total)}\n\nEntradas: {Money(entries)}\nSaídas: {Money(exits)}\nSaldo: {Money(entries-exits)}\n\nEstoque baixo: {low} produto(s)",
            "LEAL INFO PDV - Relatórios",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

    private void OpenUsers()
    {
        using var f=new Form{Text="Usuários e Níveis de Acesso",StartPosition=FormStartPosition.CenterParent,
            Width=980,Height=650,BackColor=Color.FromArgb(224,239,248),Font=new Font("Segoe UI",10)};
        var grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,
            AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
        var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=64,Padding=new Padding(10),FlowDirection=FlowDirection.LeftToRight};
        var add=new Button{Text="NOVO USUÁRIO",Width=150,Height=42};
        var reset=new Button{Text="REDEFINIR SENHA",Width=160,Height=42};
        var toggle=new Button{Text="ATIVAR / INATIVAR",Width=160,Height=42};
        var access=new Button{Text="ALTERAR NÍVEL",Width=160,Height=42};
        bar.Controls.Add(add);bar.Controls.Add(reset);bar.Controls.Add(toggle);bar.Controls.Add(access);
        f.Controls.Add(grid);f.Controls.Add(bar);

        void LoadUsers()
        {
            using var cn=Database.Open();
            using var cmd=cn.CreateCommand();
            cmd.CommandText="SELECT id AS ID,full_name AS Nome,username AS Usuario,role AS Nivel,email AS Email,phone AS Telefone,CASE active WHEN 1 THEN 'ATIVO' ELSE 'INATIVO' END AS Status,CASE can_discount WHEN 1 THEN 'SIM' ELSE 'NÃO' END AS Desconto FROM users ORDER BY full_name";
            using var rd=cmd.ExecuteReader();
            var dt=new System.Data.DataTable();
            dt.Load(rd);
            grid.DataSource=dt;
        }

        add.Click+=(_,_)=>{
            using var uf=new Form{Text="Novo usuário",StartPosition=FormStartPosition.CenterParent,Width=560,Height=570,
                FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(224,239,248)};
            var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,Padding=new Padding(30)};
            uf.Controls.Add(p);
            TextBox B(bool pw=false)=>new(){Dock=DockStyle.Top,Height=38,UseSystemPasswordChar=pw,Font=new Font("Segoe UI",11)};
            Label L(string s)=>new(){Text=s,Dock=DockStyle.Top,Height=26,Font=new Font("Segoe UI",10,FontStyle.Bold)};
            var n=B();var u=B();var e=B();var ph=B();var pw=B(true);
            var role=new ComboBox{Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,Height=38};
            role.Items.AddRange(new[]{"PATRÃO","GERENTE","FUNCIONÁRIO","CAIXA"});role.SelectedIndex=2;
            var discount=new CheckBox{Text="Pode conceder desconto",Dock=DockStyle.Top,Height=35};
            foreach(var x in new (string,Control)[]{("Nome completo",n),("Usuário",u),("E-mail",e),("Telefone",ph),("Senha inicial",pw),("Nível de acesso",role)})
            {p.Controls.Add(L(x.Item1));p.Controls.Add(x.Item2);}
            p.Controls.Add(discount);
            var save=new Button{Text="SALVAR USUÁRIO",Dock=DockStyle.Top,Height=46,BackColor=Color.FromArgb(0,163,224),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
            p.Controls.Add(save);
            save.Click+=(_,_)=>{
                if(string.IsNullOrWhiteSpace(n.Text)||string.IsNullOrWhiteSpace(u.Text)||pw.Text.Length<6){MessageBox.Show("Nome, usuário e senha de no mínimo 6 caracteres são obrigatórios.");return;}
                try{Auth.CreateUser(n.Text,u.Text,pw.Text,role.Text,e.Text,ph.Text,discount.Checked);uf.DialogResult=DialogResult.OK;uf.Close();}
                catch(Exception ex){MessageBox.Show("Erro ao salvar usuário:\\n"+ex.Message);}
            };
            if(uf.ShowDialog(f)==DialogResult.OK)LoadUsers();
        };

        reset.Click+=(_,_)=>{
            if(grid.CurrentRow==null)return;
            long id=Convert.ToInt64(grid.CurrentRow.Cells["ID"].Value);
            string name=Convert.ToString(grid.CurrentRow.Cells["Nome"].Value)??"";
            using var rf=new Form{Text="Redefinir senha",StartPosition=FormStartPosition.CenterParent,Width=480,Height=240,FormBorderStyle=FormBorderStyle.FixedDialog};
            var tb=new TextBox{Left=35,Top=70,Width=390,UseSystemPasswordChar=true,Font=new Font("Segoe UI",12)};
            var lab=new Label{Left=35,Top=25,Width=390,Text="Nova senha para "+name+" (mínimo 6 caracteres):"};
            var ok=new Button{Left=275,Top=125,Width=150,Height=38,Text="REDEFINIR"};
            rf.Controls.AddRange(new Control[]{lab,tb,ok});
            ok.Click+=(_,_)=>{if(tb.Text.Length<6){MessageBox.Show("Use pelo menos 6 caracteres.");return;}Auth.ResetPassword(id,tb.Text);rf.DialogResult=DialogResult.OK;rf.Close();};
            if(rf.ShowDialog(f)==DialogResult.OK)MessageBox.Show("Senha redefinida com sucesso.");
        };

        toggle.Click+=(_,_)=>{
            if(grid.CurrentRow==null)return;
            long id=Convert.ToInt64(grid.CurrentRow.Cells["ID"].Value);
            if(Auth.Current?.Id==id){MessageBox.Show("Você não pode inativar seu próprio usuário durante a sessão.");return;}
            using var cn=Database.Open();using var cmd=cn.CreateCommand();
            cmd.CommandText="UPDATE users SET active=CASE active WHEN 1 THEN 0 ELSE 1 END WHERE id=$id";
            cmd.Parameters.AddWithValue("$id",id);cmd.ExecuteNonQuery();LoadUsers();
        };
        access.Click+=(_,_)=>{
            if(grid.CurrentRow==null)return;
            long id=Convert.ToInt64(grid.CurrentRow.Cells["ID"].Value);
            string currentRole=Convert.ToString(grid.CurrentRow.Cells["Nivel"].Value)??"";
            if(Auth.Current?.Id==id){MessageBox.Show("Seu próprio nível não pode ser alterado durante a sessão.");return;}
            using var af=new Form{Text="Alterar nível de acesso",StartPosition=FormStartPosition.CenterParent,Width=440,Height=220,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
            var cb=new ComboBox{Left=35,Top=45,Width=350,DropDownStyle=ComboBoxStyle.DropDownList};
            cb.Items.AddRange(new[]{"PATRÃO","GERENTE","FUNCIONÁRIO","CAIXA"}); cb.SelectedItem=currentRole; if(cb.SelectedIndex<0)cb.SelectedIndex=2;
            var ok=new Button{Left=235,Top=105,Width=150,Height=40,Text="SALVAR"};
            af.Controls.AddRange(new Control[]{cb,ok});
            ok.Click+=(_,_)=>{using var cn=Database.Open();using var cmd=cn.CreateCommand();cmd.CommandText="UPDATE users SET role=$r WHERE id=$id";cmd.Parameters.AddWithValue("$r",cb.Text);cmd.Parameters.AddWithValue("$id",id);cmd.ExecuteNonQuery();af.DialogResult=DialogResult.OK;af.Close();};
            if(af.ShowDialog(f)==DialogResult.OK)LoadUsers();
        };

        LoadUsers();
        ApplyFloatingTheme(f);
        f.ShowDialog(this);
    }

    private void ShowInitialSecuritySetup()
    {
        if (Auth.Current == null) return;

        using var f = new Form
        {
            Text = "Proteja sua conta de Administrador",
            StartPosition = FormStartPosition.CenterParent,
            Width = 650,
            Height = 430,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(224,239,248),
            Font = new Font("Segoe UI",10)
        };

        var root = new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(28)};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,80));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));
        f.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text="PROTEÇÃO DA CONTA",
            Dock=DockStyle.Fill,
            ForeColor=DarkBlue,
            Font=new Font("Segoe UI",20,FontStyle.Bold),
            TextAlign=ContentAlignment.MiddleCenter
        },0,0);

        root.Controls.Add(new Label
        {
            Text="Antes de continuar, gere códigos de recuperação de emergência.\\n\\nEles permitem recuperar a senha mesmo se o e-mail ainda não estiver configurado.\\n\\nGuarde esses códigos em local seguro. Cada código funciona apenas uma vez.",
            Dock=DockStyle.Fill,
            ForeColor=Color.FromArgb(4,55,94),
            Font=new Font("Segoe UI",11,FontStyle.Bold),
            TextAlign=ContentAlignment.MiddleCenter
        },0,1);

        var generate = new Button
        {
            Text="GERAR CÓDIGOS DE EMERGÊNCIA",
            Dock=DockStyle.Fill,
            BackColor=Color.FromArgb(185,22,38),
            ForeColor=Color.White,
            FlatStyle=FlatStyle.Flat,
            Font=new Font("Segoe UI",11,FontStyle.Bold)
        };
        generate.FlatAppearance.BorderSize=0;
        root.Controls.Add(generate,0,2);

        var later = new Button
        {
            Text="CONTINUAR",
            Dock=DockStyle.Fill,
            BackColor=Color.FromArgb(0,163,224),
            ForeColor=Color.White,
            FlatStyle=FlatStyle.Flat,
            Font=new Font("Segoe UI",11,FontStyle.Bold),
            Enabled=false
        };
        later.FlatAppearance.BorderSize=0;
        root.Controls.Add(later,0,3);

        generate.Click += (_,_) =>
        {
            ShowEmergencyCodes(Auth.Current.Id, true);
            if (Auth.RemainingEmergencyCodes(Auth.Current.Id) > 0)
                later.Enabled = true;
        };

        later.Click += (_,_) =>
        {
            SetSetting("security_setup_completed","1");
            f.Close();
        };

        f.ShowDialog(this);
    }

    private void ShowEmergencyCodes(long userId, bool initialSetup)
    {
        var codes = Auth.GenerateEmergencyCodes(userId, 8);
        string recoveryIdentity = Auth.Current?.Username ?? "";
        if (Auth.Current != null && !string.IsNullOrWhiteSpace(Auth.Current.Email))
            recoveryIdentity = Auth.Current.Email;
        Auth.SaveLocalRecoveryKey(userId, recoveryIdentity, codes);

        using var f = new Form
        {
            Text = "Códigos de Recuperação de Emergência",
            StartPosition = FormStartPosition.CenterParent,
            Width = 620,
            Height = 620,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(224,239,248),
            Font = new Font("Segoe UI",10)
        };

        var tb = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas",16,FontStyle.Bold),
            TextAlign = HorizontalAlignment.Center,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(8,38,68),
            Text = string.Join(Environment.NewLine + Environment.NewLine, codes)
        };

        var info = new Label
        {
            Dock = DockStyle.Top,
            Height = 85,
            Text = "GUARDE ESTES CÓDIGOS EM LOCAL SEGURO\\nCada código funciona somente uma vez.",
            ForeColor = Color.FromArgb(185,22,38),
            Font = new Font("Segoe UI",11,FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12)
        };

        var copy = new Button{Text="COPIAR CÓDIGOS",Width=160,Height=42};
        var close = new Button{Text="JÁ GUARDEI",Width=150,Height=42};

        copy.Click += (_,_) =>
        {
            Clipboard.SetText(tb.Text);
            MessageBox.Show("Códigos copiados.");
        };
        close.Click += (_,_) => f.Close();

        bottom.Controls.Add(close);
        bottom.Controls.Add(copy);

        f.Controls.Add(tb);
        f.Controls.Add(info);
        f.Controls.Add(bottom);

        ApplyFloatingTheme(f);
        f.ShowDialog(this);
    }

    private void OpenEmailSettings()
    {
        var s=EmailRecovery.GetSmtp();
        using var f=new Form{Text="Configuração de Recuperação por E-mail",StartPosition=FormStartPosition.CenterParent,Width=680,Height=600,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(224,239,248),Font=new Font("Segoe UI",10)};
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=13,Padding=new Padding(34,20,34,20)};f.Controls.Add(p);
        Label L(string x)=>new(){Text=x,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(4,55,94),Font=new Font("Segoe UI",10,FontStyle.Bold),TextAlign=ContentAlignment.BottomLeft};
        TextBox B(string x="",bool pw=false)=>new(){Text=x,Dock=DockStyle.Fill,Font=new Font("Segoe UI",11,FontStyle.Bold),UseSystemPasswordChar=pw};
        var host=B(s.host);var port=B(s.port.ToString());var user=B(s.user);var password=B("",true);var from=B(s.fromName);
        var ssl=new CheckBox{Text="Usar SSL/TLS",Checked=s.ssl,Dock=DockStyle.Fill,Font=new Font("Segoe UI",10,FontStyle.Bold)};
        var hint=new Label{Text=s.hasPassword?"Senha SMTP já cadastrada. Deixe em branco para manter.":"Informe a senha SMTP.",Dock=DockStyle.Fill,ForeColor=Color.FromArgb(90,90,90)};
        var fields=new (string,Control)[]{("Servidor SMTP",host),("Porta",port),("E-mail/Usuário SMTP",user),("Senha SMTP",password),("Nome do remetente",from)};
        int r=0;foreach(var x in fields){p.RowStyles.Add(new RowStyle(SizeType.Absolute,28));p.Controls.Add(L(x.Item1),0,r++);p.RowStyles.Add(new RowStyle(SizeType.Absolute,45));p.Controls.Add(x.Item2,0,r++);}
        p.RowStyles.Add(new RowStyle(SizeType.Absolute,36));p.Controls.Add(ssl,0,r++);p.RowStyles.Add(new RowStyle(SizeType.Absolute,34));p.Controls.Add(hint,0,r++);
        var save=new Button{Text="SALVAR CONFIGURAÇÃO",Dock=DockStyle.Fill,BackColor=Color.FromArgb(0,163,224),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",11,FontStyle.Bold)};save.FlatAppearance.BorderSize=0;p.RowStyles.Add(new RowStyle(SizeType.Percent,100));p.Controls.Add(save,0,r++);
        save.Click+=(_,_)=>{if(string.IsNullOrWhiteSpace(host.Text)||string.IsNullOrWhiteSpace(user.Text)){MessageBox.Show("Servidor SMTP e usuário/e-mail são obrigatórios.");return;}if(!int.TryParse(port.Text,out var po)){MessageBox.Show("Porta inválida.");return;}EmailRecovery.SaveSmtp(host.Text,po,user.Text,password.Text,ssl.Checked,from.Text);MessageBox.Show("Configuração de e-mail salva.");f.DialogResult=DialogResult.OK;f.Close();};
        ApplyFloatingTheme(f);f.ShowDialog(this);
    }

    private void OpenSettings()
    {
        var alreadyOpen = Application.OpenForms.Cast<Form>()
            .FirstOrDefault(x => x.Text == "Configurações do Sistema");
        if (alreadyOpen != null)
        {
            alreadyOpen.BringToFront();
            alreadyOpen.Activate();
            alreadyOpen.Focus();
            return;
        }

        var f = new Form
        {
            Text = "Configurações do Sistema",
            StartPosition = FormStartPosition.CenterParent,
            Width = 900,
            Height = 760,
            MinimumSize = new Size(820, 680),
            BackColor = Color.FromArgb(224,239,248),
            Font = new Font("Segoe UI",10)
        };

        var header = new Label
        {
            Text = "CENTRAL DE CONFIGURAÇÕES",
            Dock = DockStyle.Top,
            Height = 78,
            BackColor = DarkBlue,
            ForeColor = Color.White,
            Font = new Font("Segoe UI",20,FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        f.Controls.Add(header);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI",11,FontStyle.Bold), Padding = new Point(18,8) };
        var companyTab = new TabPage("DADOS DA EMPRESA") { BackColor = Color.FromArgb(224,239,248), Padding = new Padding(26) };
        var pixTab = new TabPage("PIX") { BackColor = Color.FromArgb(224,239,248), Padding = new Padding(26) };
        var systemTab = new TabPage("SISTEMA E SEGURANÇA") { BackColor = Color.FromArgb(224,239,248), Padding = new Padding(26) };
        var equipmentTab = new TabPage("EQUIPAMENTOS") { BackColor = Color.FromArgb(224,239,248), Padding = new Padding(26) };
        tabs.TabPages.Add(companyTab);
        tabs.TabPages.Add(pixTab);
        tabs.TabPages.Add(systemTab);
        tabs.TabPages.Add(equipmentTab);
        f.Controls.Add(tabs);
        tabs.BringToFront();

        var equipmentPanel = new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(45) };
        equipmentPanel.RowStyles.Add(new RowStyle(SizeType.Percent,30));
        equipmentPanel.RowStyles.Add(new RowStyle(SizeType.Percent,40));
        equipmentPanel.RowStyles.Add(new RowStyle(SizeType.Percent,30));
        var equipmentText = new Label { Text="CONFIGURE BALANÇAS, IMPRESSORAS TÉRMICAS E MODELOS DE BOBINA",Dock=DockStyle.Fill,ForeColor=DarkBlue,Font=new Font("Segoe UI",16,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter };
        var equipmentButton = new Button { Text="ABRIR CENTRAL DE EQUIPAMENTOS",Dock=DockStyle.Fill,Margin=new Padding(60,20,60,20),BackColor=Color.FromArgb(0,145,85),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",14,FontStyle.Bold) };
        equipmentButton.FlatAppearance.BorderSize=0;
        equipmentButton.Click += (_,_) => EquipmentSettingsForm.Open(f);
        var equipmentHint = new Label { Text="Impressoras instaladas no Windows • Bobinas 58, 76, 80 mm ou personalizada\nBalanças COM/RS-232 • TCP/IP • Teclado/HID • Etiqueta com código de barras",Dock=DockStyle.Fill,ForeColor=DarkBlue,Font=new Font("Segoe UI",10),TextAlign=ContentAlignment.MiddleCenter };
        equipmentPanel.Controls.Add(equipmentText,0,0);equipmentPanel.Controls.Add(equipmentButton,0,1);equipmentPanel.Controls.Add(equipmentHint,0,2);
        equipmentTab.Controls.Add(equipmentPanel);

        var company = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8, Padding = new Padding(12) };
        company.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,230));
        company.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        for (int i=0;i<7;i++) company.RowStyles.Add(new RowStyle(SizeType.Percent,12.5f));
        company.RowStyles.Add(new RowStyle(SizeType.Percent,12.5f));
        companyTab.Controls.Add(company);

        TextBox SettingBox(string value) => new() { Text=value, Dock=DockStyle.Fill, Font=new Font("Segoe UI",11,FontStyle.Bold), Margin=new Padding(4,8,4,8) };
        Label SettingLabel(string value) => new() { Text=value, Dock=DockStyle.Fill, ForeColor=DarkBlue, Font=new Font("Segoe UI",10,FontStyle.Bold), TextAlign=ContentAlignment.MiddleLeft };

        var companyName = SettingBox(GetSetting("company_name"));
        var tradeName = SettingBox(GetSetting("company_trade_name"));
        var document = SettingBox(GetSetting("company_document"));
        var phone = SettingBox(GetSetting("company_phone"));
        var address = SettingBox(GetSetting("company_address"));
        var cityState = SettingBox(GetSetting("company_city_state"));
        var receiptFooter = SettingBox(GetSetting("company_footer","Obrigado pela preferência!"));
        var fields = new (string label, TextBox box)[]
        {
            ("Razão Social / Nome da Empresa",companyName), ("Nome Fantasia",tradeName),
            ("CNPJ / CPF",document), ("Telefone / WhatsApp",phone), ("Endereço",address),
            ("Cidade / UF",cityState), ("Mensagem no rodapé do cupom",receiptFooter)
        };
        for(int i=0;i<fields.Length;i++) { company.Controls.Add(SettingLabel(fields[i].label),0,i); company.Controls.Add(fields[i].box,1,i); }

        var saveCompany = new Button { Text="SALVAR DADOS DA EMPRESA",Dock=DockStyle.Right,Width=260,Height=46,BackColor=Color.FromArgb(0,163,224),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10,FontStyle.Bold),Margin=new Padding(4,10,4,4) };
        saveCompany.FlatAppearance.BorderSize=0;
        company.SetColumnSpan(saveCompany,2); company.Controls.Add(saveCompany,0,7);
        saveCompany.Click += (_,_) =>
        {
            if(string.IsNullOrWhiteSpace(companyName.Text)) { Info("Informe o nome da empresa."); companyName.Focus(); return; }
            SetSetting("company_name",companyName.Text.Trim()); SetSetting("company_trade_name",tradeName.Text.Trim());
            SetSetting("company_document",document.Text.Trim()); SetSetting("company_phone",phone.Text.Trim());
            SetSetting("company_address",address.Text.Trim()); SetSetting("company_city_state",cityState.Text.Trim());
            SetSetting("company_footer",receiptFooter.Text.Trim()); SetSetting("company_registered","1");
            Info("Dados da empresa salvos com sucesso.");
        };

        // PIX: simples por chave (qualquer banco) + Mercado Pago automático opcional
        var pixPanel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=10,Padding=new Padding(18)};
        pixPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,240));pixPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        for(int i=0;i<10;i++)pixPanel.RowStyles.Add(new RowStyle(SizeType.Percent,10));pixTab.Controls.Add(pixPanel);
        var pixKey=SettingBox(GetSetting("pix_key"));
        var pixType=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=new Font("Segoe UI",11,FontStyle.Bold),Margin=new Padding(4,8,4,8)};
        pixType.Items.AddRange(new[]{"CPF","CNPJ","E-MAIL","TELEFONE","ALEATÓRIA"});var savedType=GetSetting("pix_key_type","ALEATÓRIA");pixType.SelectedItem=pixType.Items.Cast<object>().FirstOrDefault(x=>string.Equals(x.ToString(),savedType,StringComparison.OrdinalIgnoreCase))??"ALEATÓRIA";
        var mpEnabled=new CheckBox{Text="ATIVAR PIX AUTOMÁTICO PELO MERCADO PAGO",Checked=GetSetting("pix_mp_enabled")=="1",AutoSize=true,ForeColor=DarkBlue,Font=new Font("Segoe UI",9.5f,FontStyle.Bold),Anchor=AnchorStyles.Left};
        var mpToken=SettingBox(PixSecureSettings.Unprotect(GetSetting("pix_mp_access_token")));mpToken.UseSystemPasswordChar=true;
        var mpShow=new CheckBox{Text="Mostrar Access Token",AutoSize=true,ForeColor=DarkBlue,Anchor=AnchorStyles.Left};mpShow.CheckedChanged+=(_,_)=>mpToken.UseSystemPasswordChar=!mpShow.Checked;
        var statusPanel=new Panel{Dock=DockStyle.Fill,Margin=new Padding(4,8,4,8),BackColor=Color.FromArgb(230,145,20)};
        var statusText=new Label{Text="PIX SIMPLES • AGUARDANDO SALVAR",Dock=DockStyle.Fill,ForeColor=Color.White,Font=new Font("Segoe UI",10.5f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter};statusPanel.Controls.Add(statusText);
        void PixStatus(string t,Color col){statusPanel.BackColor=col;statusText.Text=t;}
        pixPanel.Controls.Add(SettingLabel("Tipo da chave PIX"),0,0);pixPanel.Controls.Add(pixType,1,0);
        pixPanel.Controls.Add(SettingLabel("Chave PIX (qualquer banco)"),0,1);pixPanel.Controls.Add(pixKey,1,1);
        pixPanel.Controls.Add(SettingLabel("Modo automático opcional"),0,2);pixPanel.Controls.Add(mpEnabled,1,2);
        pixPanel.Controls.Add(SettingLabel("Mercado Pago • Access Token"),0,3);pixPanel.Controls.Add(mpToken,1,3);
        pixPanel.Controls.Add(SettingLabel("Exibição da credencial"),0,4);pixPanel.Controls.Add(mpShow,1,4);
        pixPanel.Controls.Add(SettingLabel("Status"),0,5);pixPanel.Controls.Add(statusPanel,1,5);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false};
        var savePix=new Button{Text="SALVAR PIX",Width=180,Height=44,BackColor=Color.FromArgb(0,163,224),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10,FontStyle.Bold)};
        var testMp=new Button{Text="TESTAR MERCADO PAGO",Width=220,Height=44,BackColor=Color.FromArgb(0,145,85),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",9.5f,FontStyle.Bold)};savePix.FlatAppearance.BorderSize=0;testMp.FlatAppearance.BorderSize=0;buttons.Controls.Add(savePix);buttons.Controls.Add(testMp);pixPanel.SetColumnSpan(buttons,2);pixPanel.Controls.Add(buttons,0,6);
        var help=new Label{Text="PIX SIMPLES: informe somente sua chave de qualquer banco. O PDV gera QR Code e Copia e Cola com o valor da venda; você confirma o recebimento manualmente.\nPIX AUTOMÁTICO: opcional, usa Mercado Pago e confirma o pagamento automaticamente.",Dock=DockStyle.Fill,ForeColor=DarkBlue,Font=new Font("Segoe UI",9.5f),TextAlign=ContentAlignment.MiddleCenter};pixPanel.SetColumnSpan(help,2);pixPanel.Controls.Add(help,0,7);
        savePix.Click+=(_,_)=>{if(!Auth.IsAdmin){Info("Somente ADMINISTRADOR pode alterar o PIX.");return;}if(string.IsNullOrWhiteSpace(pixKey.Text)){PixStatus("INFORME SUA CHAVE PIX",Color.FromArgb(180,55,55));return;}SetSetting("pix_key",pixKey.Text.Trim());SetSetting("pix_key_type",pixType.SelectedItem?.ToString()??"ALEATÓRIA");SetSetting("pix_mp_enabled",mpEnabled.Checked?"1":"0");SetSetting("pix_mp_access_token",PixSecureSettings.Protect(mpToken.Text));PixStatus(mpEnabled.Checked?"PIX SALVO • MERCADO PAGO OPCIONAL ATIVO":"PIX POR CHAVE • PRONTO PARA RECEBER",Color.FromArgb(0,145,85));};
        testMp.Click+=async(_,_)=>{if(!mpEnabled.Checked){Info("Ative o PIX automático do Mercado Pago primeiro.");return;}if(string.IsNullOrWhiteSpace(mpToken.Text)){PixStatus("INFORME O ACCESS TOKEN DO MERCADO PAGO",Color.FromArgb(180,55,55));return;}try{testMp.Enabled=false;PixStatus("TESTANDO MERCADO PAGO...",Color.FromArgb(230,145,20));await new MercadoPagoPixService(mpToken.Text).TestarConexaoAsync();SetSetting("pix_mp_access_token",PixSecureSettings.Protect(mpToken.Text));SetSetting("pix_mp_enabled","1");PixStatus("MERCADO PAGO CONECTADO • AUTOMÁTICO",Color.FromArgb(0,145,85));}catch(Exception ex){PixStatus("MERCADO PAGO DESCONECTADO",Color.FromArgb(180,55,55));MessageBox.Show(f,ex.Message,"PIX Mercado Pago",MessageBoxButtons.OK,MessageBoxIcon.Error);}finally{testMp.Enabled=true;}};

        var system = new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=5,Padding=new Padding(14) };
        system.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); system.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        for(int i=0;i<4;i++) system.RowStyles.Add(new RowStyle(SizeType.Percent,20));
        system.RowStyles.Add(new RowStyle(SizeType.Percent,20)); systemTab.Controls.Add(system);
        Button ConfigButton(string text, Action action)
        {
            var b=new Button{Text=text,Dock=DockStyle.Fill,Margin=new Padding(10),BackColor=Color.FromArgb(4,70,112),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10,FontStyle.Bold)};
            b.FlatAppearance.BorderSize=0;b.Click+=(_,_)=>action();return b;
        }
        system.Controls.Add(ConfigButton("ALTERAR IMAGEM DA TELA PRINCIPAL",()=>{if(mainScreenPicture!=null)ChangeMainScreenImage(mainScreenPicture);}),0,0);
        system.Controls.Add(ConfigButton("RECUPERAÇÃO POR E-MAIL",()=>{if(Auth.IsAdmin)OpenEmailSettings();else Info("Somente ADMINISTRADOR pode configurar o e-mail.");}),1,0);
        system.Controls.Add(ConfigButton("USUÁRIOS E ACESSOS",()=>{if(Auth.IsAdmin)OpenUsers();else Info("Somente ADMINISTRADOR pode gerenciar usuários.");}),0,1);
        system.Controls.Add(ConfigButton("CÓDIGOS DE EMERGÊNCIA",()=>{if(Auth.IsAdmin&&Auth.Current!=null)ShowEmergencyCodes(Auth.Current.Id,false);else Info("Somente ADMINISTRADOR pode gerar códigos de emergência.");}),1,1);
        system.Controls.Add(ConfigButton("FAZER BACKUP",()=>_ = BackupAsync()),0,2);
        system.Controls.Add(ConfigButton("RESTAURAR BACKUP",()=>_ = RestoreBackupAsync()),1,2);
        system.Controls.Add(ConfigButton("ATUALIZAÇÕES DO SISTEMA",()=>UpdateManager.ShowUpdateCenter(f)),0,3);
        system.Controls.Add(ConfigButton("TUTORIAL DE PRIMEIRO ACESSO",()=>OpenFirstAccessTutorial(false)),1,3);
        var systemInfo=new Label{Text=$"Sistema: LEAL INFO PDV   •   Versão: V{UpdateManager.CurrentVersion}\nSerial: {Database.DeviceSerial()}\nBanco local: {Database.DbPath}",Dock=DockStyle.Fill,ForeColor=DarkBlue,Font=new Font("Segoe UI",9.5f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter};
        system.SetColumnSpan(systemInfo,2);system.Controls.Add(systemInfo,0,4);

        ApplyFloatingTheme(f);
        f.Show(this);
    }

    private async Task BackupAsync()
    {
        if (!Auth.IsAdmin) { MessageBox.Show("Somente ADMINISTRADOR pode enviar backups.", "Acesso negado", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        try
        {
            UseWaitCursor = true;
            var zip = await DatabaseBackupService.CreateAndSendAsync();
            MessageBox.Show("Backup enviado com sucesso para o e-mail configurado.\n\nCópia local:\n" + zip,
                "Backup concluído", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch(Exception ex)
        {
            MessageBox.Show("Não foi possível enviar o backup.\n\n" + ex.Message,
                "Erro no backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private async Task RestoreBackupAsync()
    {
        if (!Auth.IsAdmin) { MessageBox.Show("Somente ADMINISTRADOR pode restaurar backups.", "Acesso negado", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        using var dialog = new OpenFileDialog
        {
            Title = "Selecionar backup do LEAL INFO PDV",
            Filter = "Backup do PDV (*.zip;*.db;*.sqlite)|*.zip;*.db;*.sqlite|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (MessageBox.Show("A restauração substituirá os dados atuais pelos dados do backup selecionado.\n\nUma cópia de segurança do banco atual será guardada antes da troca. Deseja continuar?",
            "Confirmar restauração", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

        try
        {
            UseWaitCursor = true;
            await DatabaseBackupService.RestoreAsync(dialog.FileName);
            automaticBackupCompleted = true;
            MessageBox.Show("Backup restaurado e validado com sucesso.\n\nO PDV será reiniciado agora para carregar os dados recuperados.",
                "Restauração concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Restart();
        }
        catch(Exception ex)
        {
            MessageBox.Show("O banco atual não foi substituído.\n\n" + ex.Message,
                "Falha na restauração", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { UseWaitCursor = false; }
    }

    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (automaticBackupCompleted || automaticBackupRunning) return;
        automaticBackupRunning = true;
        e.Cancel = true;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await DatabaseBackupService.CreateAndSendAsync(timeout.Token);
        }
        catch(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Database.BackupFolder);
                await File.AppendAllTextAsync(Path.Combine(Database.BackupFolder, "backup-errors.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n");
            }
            catch { }
        }
        finally
        {
            automaticBackupCompleted = true;
            automaticBackupRunning = false;
            BeginInvoke(Close);
        }
    }




    private string MainScreenImagePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "LEAL INFO CONECTADO", "PDV", "tela_principal.png");

    private string DefaultMainScreenImagePath =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "tela_principal.png");

    private Image? LoadMainScreenImage()
    {
        try
        {
            var custom = MainScreenImagePath;
            var source = File.Exists(custom) ? custom : DefaultMainScreenImagePath;
            if (!File.Exists(source)) return null;

            using var temp = Image.FromFile(source);
            return new Bitmap(temp);
        }
        catch
        {
            return null;
        }
    }

    private void ChangeMainScreenImage(PictureBox picture)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Escolher imagem da tela principal",
            Filter = "Imagens (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            Multiselect = false
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var folder = Path.GetDirectoryName(MainScreenImagePath)!;
            Directory.CreateDirectory(folder);

            using var original = Image.FromFile(dlg.FileName);
            using var bmp = new Bitmap(original);

            // Salva sempre em PNG para o sistema usar um formato previsível.
            bmp.Save(MainScreenImagePath, System.Drawing.Imaging.ImageFormat.Png);

            picture.Image?.Dispose();
            picture.Image = new Bitmap(bmp);
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            picture.Refresh();
            picture.Invalidate();
            picture.Update();
            Application.DoEvents();

            MessageBox.Show(
                "Tela principal alterada com sucesso.\n\n" +
                "Dica: para preencher melhor a tela, use uma imagem horizontal 16:9, " +
                "por exemplo 1920 x 1080.",
                "LEAL INFO CONECTADO",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível trocar a imagem.\n\n" + ex.Message,
                "LEAL INFO CONECTADO", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }


    private void OpenCadastroCentral()
    {
        using var f = new Form
        {
            Text = "Cadastros",
            StartPosition = FormStartPosition.CenterParent,
            Width = 600,
            Height = 730,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(5, 24, 43),
            Font = new Font("Segoe UI", 10),
            KeyPreview = true
        };

        // Layout estrutural: cabeçalho / conteúdo / rodapé.
        // Evita qualquer sobreposição ou corte.
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.FromArgb(5, 24, 43),
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        f.Controls.Add(root);

        var list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(18, 14, 18, 8),
            Margin = new Padding(0),
            BackColor = Color.FromArgb(5, 24, 43)
        };
        root.Controls.Add(list, 0, 0);

        void CentralizarCadastro()
        {
            int larguraItem = 500;
            int margem = Math.Max(0, (list.ClientSize.Width - larguraItem) / 2);
            list.Padding = new Padding(margem, 8, margem, 0);
        }
        list.SizeChanged += (_, _) => CentralizarCadastro();
        f.Shown += (_, _) => CentralizarCadastro();






        Control MakeCadastroButton(string text, string description, string iconFile, Action action)
        {
            const int hostW = 500;
            const int hostH = 104;
            const int normalW = 468;
            const int normalH = 88;
            const int hoverW = 486;
            const int hoverH = 102;

            var host = new Panel
            {
                Width = hostW,
                Height = hostH,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };

            var b = new Button
            {
                Width = normalW,
                Height = normalH,
                Left = (hostW - normalW) / 2,
                Top = (hostH - normalH) / 2,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(8, 59, 98),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                ImageAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                Padding = new Padding(22, 8, 16, 8),
                Cursor = Cursors.Hand,
                TabStop = true,
                UseVisualStyleBackColor = false,
                Text = "        " + text + "\n        " + description
            };

            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(0, 150, 210);
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 118, 178);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 98, 155);

            host.Controls.Add(b);

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", iconFile);
            Image? baseIcon = null;

            if (File.Exists(iconPath))
            {
                using var srcIcon = Image.FromFile(iconPath);
                baseIcon = new Bitmap(srcIcon);
                b.Image = new Bitmap(baseIcon, new Size(52, 52));
            }

            var hover = false;
            var timer = new System.Windows.Forms.Timer { Interval = 15 };

            void ApplyState()
            {
                if (hover)
                {
                    b.BackColor = Color.FromArgb(0, 118, 178);
                    b.FlatAppearance.BorderSize = 3;
                    b.FlatAppearance.BorderColor = Color.FromArgb(90, 225, 255);
                    b.Font = new Font("Segoe UI", 14, FontStyle.Bold);
                    b.Padding = new Padding(18, 8, 14, 8);

                    if (baseIcon != null)
                    {
                        b.Image?.Dispose();
                        b.Image = new Bitmap(baseIcon, new Size(70, 70));
                    }
                }
                else
                {
                    b.BackColor = Color.FromArgb(8, 59, 98);
                    b.FlatAppearance.BorderSize = 1;
                    b.FlatAppearance.BorderColor = Color.FromArgb(0, 150, 210);
                    b.Font = new Font("Segoe UI", 13, FontStyle.Bold);
                    b.Padding = new Padding(22, 8, 16, 8);

                    if (baseIcon != null)
                    {
                        b.Image?.Dispose();
                        b.Image = new Bitmap(baseIcon, new Size(52, 52));
                    }
                }

                b.Invalidate();
                b.Update();
            }

            timer.Tick += (_, _) =>
            {
                var targetW = hover ? hoverW : normalW;
                var targetH = hover ? hoverH : normalH;

                var dw = targetW - b.Width;
                var dh = targetH - b.Height;

                if (Math.Abs(dw) <= 2 && Math.Abs(dh) <= 2)
                {
                    b.Width = targetW;
                    b.Height = targetH;
                    b.Left = (hostW - b.Width) / 2;
                    b.Top = (hostH - b.Height) / 2;
                    timer.Stop();
                    return;
                }

                b.Width += Math.Sign(dw) * Math.Max(2, Math.Abs(dw) / 4);
                b.Height += Math.Sign(dh) * Math.Max(2, Math.Abs(dh) / 4);

                // O host fica fixo. Só o botão cresce dentro dele.
                b.Left = (hostW - b.Width) / 2;
                b.Top = (hostH - b.Height) / 2;
                b.BringToFront();
            };

            b.MouseEnter += (_, _) =>
            {
                hover = true;
                ApplyState();
                timer.Start();
            };

            b.MouseLeave += (_, _) =>
            {
                var local = b.PointToClient(Cursor.Position);
                if (b.ClientRectangle.Contains(local))
                    return;

                hover = false;
                ApplyState();
                timer.Start();
            };

            b.Click += (_, _) =>
            {
                f.Hide();
                action();
                f.Show();
                f.Activate();
            };

            b.Disposed += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                b.Image?.Dispose();
                baseIcon?.Dispose();
            };

            return host;
        }

        list.Controls.Add(MakeCadastroButton(
            "PRODUTOS",
            "Cadastro, preços e controle de estoque",
            "products.png",
            OpenProducts));

        list.Controls.Add(MakeCadastroButton(
            "CLIENTES",
            "Dados, contato e histórico do cliente",
            "customers.png",
            OpenCustomers));

        list.Controls.Add(MakeCadastroButton(
            "FORNECEDORES",
            "Cadastro e dados de fornecedores",
            "suppliers.png",
            OpenSuppliers));

        list.Controls.Add(MakeCadastroButton(
            "SERVIÇOS",
            "Serviços, valores e descrições",
            "services.png",
            OpenServices));


        var escolhaLabel = new Label
        {
            Text = "ESCOLHA UMA DAS OPÇÕES ACIMA",
            Width = 500,
            Height = 32,
            Margin = new Padding(0, 2, 0, 0),
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(120, 220, 255),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        list.Controls.Add(escolhaLabel);





        var footer = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(4, 70, 112),
            ForeColor = Color.FromArgb(185, 230, 250),
            Text = "Passe o mouse sobre uma opção para ampliar • ESC fecha",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9),
            Margin = new Padding(0)
        };
        root.Controls.Add(footer, 0, 1);

        f.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                f.Close();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        ApplyFloatingTheme(f);


        f.ShowDialog(this);
    }

    private sealed class RemoveConfirmForm : Form
    {
        public Button YesButton { get; }
        public Button NoButton { get; }

        private bool yesSelected = true;
        private readonly Color darkBlue;

        public RemoveConfirmForm(string productName, double qty, string totalText, Color darkBlue)
        {
            this.darkBlue = darkBlue;

            Text = "Remover item da venda";
            StartPosition = FormStartPosition.CenterParent;
            Width = 520;
            Height = 265;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(245, 249, 252);
            Font = new Font("Segoe UI", 10);
            KeyPreview = true;

            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = darkBlue
            };
            top.Controls.Add(new Label
            {
                Text = "CONFIRMAR REMOÇÃO",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 15, FontStyle.Bold)
            });
            Controls.Add(top);

            var msg = new Label
            {
                Text = $"Deseja realmente remover este item da venda?\n\n{productName}\nQtd.: {qty:N3}   •   {totalText}",
                Left = 30,
                Top = 78,
                Width = 445,
                Height = 82,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(40, 55, 70),
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            Controls.Add(msg);

            YesButton = new Button
            {
                Text = "SIM, REMOVER",
                Left = 95,
                Top = 175,
                Width = 150,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                DialogResult = DialogResult.Yes,
                TabStop = false
            };
            YesButton.FlatAppearance.BorderSize = 0;

            NoButton = new Button
            {
                Text = "CANCELAR",
                Left = 265,
                Top = 175,
                Width = 150,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                DialogResult = DialogResult.No,
                TabStop = false
            };
            NoButton.FlatAppearance.BorderSize = 0;

            Controls.Add(YesButton);
            Controls.Add(NoButton);

            UpdateSelectionVisual();

            YesButton.Click += (_, _) =>
            {
                yesSelected = true;
                DialogResult = DialogResult.Yes;
                Close();
            };

            NoButton.Click += (_, _) =>
            {
                yesSelected = false;
                DialogResult = DialogResult.No;
                Close();
            };
        }

        private void UpdateSelectionVisual()
        {
            if (yesSelected)
            {
                YesButton.BackColor = Color.FromArgb(190, 45, 45);
                YesButton.ForeColor = Color.White;
                YesButton.FlatAppearance.BorderSize = 3;
                YesButton.FlatAppearance.BorderColor = Color.FromArgb(255, 215, 70);

                NoButton.BackColor = darkBlue;
                NoButton.ForeColor = Color.White;
                NoButton.FlatAppearance.BorderSize = 0;
            }
            else
            {
                YesButton.BackColor = Color.FromArgb(125, 125, 125);
                YesButton.ForeColor = Color.White;
                YesButton.FlatAppearance.BorderSize = 0;

                NoButton.BackColor = Color.FromArgb(0, 150, 210);
                NoButton.ForeColor = Color.White;
                NoButton.FlatAppearance.BorderSize = 3;
                NoButton.FlatAppearance.BorderColor = Color.FromArgb(255, 215, 70);
            }

            Invalidate();
            Update();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var key = keyData & Keys.KeyCode;

            if (key == Keys.Left || key == Keys.Right || key == Keys.Tab)
            {
                yesSelected = !yesSelected;
                UpdateSelectionVisual();
                return true;
            }

            if (key == Keys.Enter)
            {
                DialogResult = yesSelected ? DialogResult.Yes : DialogResult.No;
                Close();
                return true;
            }

            if (key == Keys.Escape)
            {
                DialogResult = DialogResult.No;
                Close();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    private sealed class CartItem
    {
        public long ProductId { get; set; }
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
        public double Qty { get; set; }
        public double UnitPrice { get; set; }
        public double Total => Qty * UnitPrice;
    }


    private sealed class PaymentPart
    {
        public string Method { get; set; } = "";
        public double Amount { get; set; }
    }


    private (long id, string code, string name, double price, double stock)? SelectProductFromCatalog()
    {
        using var f = new Form
        {
            Text = "Consultar Produto - F5",
            StartPosition = FormStartPosition.CenterParent,
            Width = 980,
            Height = 650,
            BackColor = Color.FromArgb(245, 249, 252),
            Font = new Font("Segoe UI", 10)
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = DarkBlue
        };
        header.Controls.Add(new Label
        {
            Text = "CONSULTA DE PRODUTOS",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            AutoSize = true,
            Left = 22,
            Top = 16
        });
        f.Controls.Add(header);

        var search = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 36,
            Font = new Font("Segoe UI", 12),
            PlaceholderText = "Digite código, código de barras ou nome do produto..."
        };
        f.Controls.Add(search);
        search.BringToFront();

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            ColumnHeadersHeight = 40
        };
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(218, 239, 251);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = DarkBlue;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grid.DataError += (_, e) => { e.ThrowException = false; e.Cancel = true; };

        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ID", HeaderText = "ID", Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Código", HeaderText = "Código", Width = 140 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Produto", HeaderText = "Produto", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Preço", HeaderText = "Preço", Width = 120 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Estoque", HeaderText = "Estoque", Width = 120 });

        f.Controls.Add(grid);
        grid.BringToFront();

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10)
        };
        var choose = ActionButton("SELECIONAR", () => { f.DialogResult = DialogResult.OK; f.Close(); });
        var cancel = ActionButton("CANCELAR", f.Close);
        bottom.Controls.Add(choose);
        bottom.Controls.Add(cancel);
        f.Controls.Add(bottom);

        void LoadProducts(string term)
        {
            grid.Rows.Clear();
            using var cn = Database.Open();
            using var cmd = cn.CreateCommand();

            if (string.IsNullOrWhiteSpace(term))
            {
                cmd.CommandText = """
                    SELECT id, COALESCE(barcode,''), name, price, stock
                    FROM products
                    WHERE active=1
                    ORDER BY name
                    """;
            }
            else
            {
                cmd.CommandText = """
                    SELECT id, COALESCE(barcode,''), name, price, stock
                    FROM products
                    WHERE active=1
                      AND (
                          CAST(id AS TEXT) LIKE $term
                          OR barcode LIKE $term
                          OR lower(name) LIKE lower($term)
                      )
                    ORDER BY name
                    """;
                cmd.Parameters.AddWithValue("$term", "%" + term.Trim() + "%");
            }

            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                grid.Rows.Add(
                    rd.GetInt64(0),
                    rd.GetString(1),
                    rd.GetString(2),
                    Money(rd.GetDouble(3)),
                    rd.GetDouble(4).ToString("N3", CultureInfo.GetCultureInfo("pt-BR"))
                );
            }
        }

        search.TextChanged += (_, _) => LoadProducts(search.Text);
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                f.DialogResult = DialogResult.OK;
                f.Close();
            }
        };
        search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && grid.Rows.Count > 0)
            {
                grid.Rows[0].Selected = true;
                grid.CurrentCell = grid.Rows[0].Cells[0];
                f.DialogResult = DialogResult.OK;
                f.Close();
                e.SuppressKeyPress = true;
            }
        };

        LoadProducts("");
        f.Shown += (_, _) => search.Focus();

        ApplyFloatingTheme(f);


        if (f.ShowDialog(this) != DialogResult.OK || grid.CurrentRow == null)
            return null;

        var id = Convert.ToInt64(grid.CurrentRow.Cells["ID"].Value);

        using var cn2 = Database.Open();
        using var cmd2 = cn2.CreateCommand();
        cmd2.CommandText = """
            SELECT id, COALESCE(barcode,''), name, price, stock
            FROM products
            WHERE id=$id AND active=1
            """;
        cmd2.Parameters.AddWithValue("$id", id);
        using var rd2 = cmd2.ExecuteReader();
        if (!rd2.Read())
            return null;

        return (
            rd2.GetInt64(0),
            rd2.GetString(1),
            rd2.GetString(2),
            rd2.GetDouble(3),
            rd2.GetDouble(4)
        );
    }



    private List<PaymentPart>? SelectPayment(double total)
    {
        using var f = new Form
        {
            Text = "Finalizar Venda",
            StartPosition = FormStartPosition.CenterParent,
            Width = 760,
            Height = 610,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(240, 246, 251),
            Font = new Font("Segoe UI", 10),
            KeyPreview = true
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 82,
            BackColor = DarkBlue
        };

        var title = new Label
        {
            Text = "FINALIZAR VENDA",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            AutoSize = true,
            Left = 24,
            Top = 14
        };

        var totalLabel = new Label
        {
            Text = "TOTAL: " + Money(total),
            ForeColor = Color.FromArgb(115, 220, 255),
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        header.Controls.Add(title);
        header.Controls.Add(totalLabel);
        header.Resize += (_, _) =>
        {
            totalLabel.Left = Math.Max(350, header.ClientSize.Width - totalLabel.Width - 24);
            totalLabel.Top = 22;
        };
        f.Controls.Add(header);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Padding = new Point(24, 10)
        };
        f.Controls.Add(tabs);
        tabs.BringToFront();

        var tabCash = new TabPage("DINHEIRO") { BackColor = Color.White };
        var tabPix = new TabPage("PIX") { BackColor = Color.White };
        var tabCard = new TabPage("CARTÃO") { BackColor = Color.White };
        var tabMulti = new TabPage("MÚLTIPLO") { BackColor = Color.White };
        tabs.TabPages.Add(tabCash);
        tabs.TabPages.Add(tabPix);
        tabs.TabPages.Add(tabCard);
        tabs.TabPages.Add(tabMulti);

        Button BigConfirm(string text)
        {
            var b = new Button
            {
                Text = text,
                Width = 300,
                Height = 58,
                BackColor = Color.FromArgb(0, 163, 224),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        Label CenterInfo(string text, int top, int size = 13)
        {
            return new Label
            {
                Text = text,
                Left = 40,
                Top = top,
                Width = 640,
                Height = 52,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", size, FontStyle.Bold),
                ForeColor = DarkBlue
            };
        }

        // DINHEIRO
        tabCash.Controls.Add(CenterInfo("PAGAMENTO EM DINHEIRO", 45, 16));
        tabCash.Controls.Add(CenterInfo("Valor da venda: " + Money(total), 115, 14));

        var receivedLabel = new Label
        {
            Text = "Valor recebido:",
            Left = 135,
            Top = 200,
            Width = 180,
            Height = 32,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = DarkBlue
        };
        var received = new NumericUpDown
        {
            Left = 320,
            Top = 195,
            Width = 230,
            Height = 38,
            DecimalPlaces = 2,
            Maximum = 9999999,
            Minimum = 0,
            Value = (decimal)total,
            ThousandsSeparator = true,
            TextAlign = HorizontalAlignment.Right,
            Font = new Font("Segoe UI", 14, FontStyle.Bold)
        };
        var change = CenterInfo("TROCO: R$ 0,00", 250, 16);
        change.ForeColor = Color.FromArgb(0, 130, 78);
        received.ValueChanged += (_, _) =>
        {
            var troco = Math.Max(0, (double)received.Value - total);
            change.Text = "TROCO: " + Money(troco);
        };

        var cashConfirm = BigConfirm("CONFIRMAR DINHEIRO");
        cashConfirm.Left = 210;
        cashConfirm.Top = 340;

        tabCash.Controls.Add(receivedLabel);
        tabCash.Controls.Add(received);
        tabCash.Controls.Add(change);
        tabCash.Controls.Add(cashConfirm);

        // PIX
        tabPix.Controls.Add(CenterInfo("PAGAMENTO VIA PIX", 55, 16));
        tabPix.Controls.Add(CenterInfo("Valor a receber: " + Money(total), 125, 15));
        var pixInfo = CenterInfo("Clique abaixo para gerar o QR Code PIX com o valor exato da venda.", 205, 12);
        pixInfo.Font = new Font("Segoe UI", 11);
        tabPix.Controls.Add(pixInfo);

        var pixConfirm = BigConfirm("GERAR QR CODE PIX");
        pixConfirm.Left = 210;
        pixConfirm.Top = 320;
        tabPix.Controls.Add(pixConfirm);

        // CARTÃO
        tabCard.Controls.Add(CenterInfo("PAGAMENTO NO CARTÃO", 45, 16));
        tabCard.Controls.Add(CenterInfo("Valor: " + Money(total), 110, 14));

        var cardTypeLabel = new Label
        {
            Text = "Tipo:",
            Left = 190,
            Top = 205,
            Width = 100,
            Height = 32,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = DarkBlue
        };
        var cardType = new ComboBox
        {
            Left = 290,
            Top = 200,
            Width = 250,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 11)
        };
        cardType.Items.AddRange(new[] { "Débito", "Crédito" });
        cardType.SelectedIndex = 0;

        var cardConfirm = BigConfirm("CONFIRMAR CARTÃO");
        cardConfirm.Left = 210;
        cardConfirm.Top = 320;
        tabCard.Controls.Add(cardTypeLabel);
        tabCard.Controls.Add(cardType);
        tabCard.Controls.Add(cardConfirm);

        // MÚLTIPLO
        tabMulti.Controls.Add(CenterInfo("DIVIDIR PAGAMENTO", 20, 16));
        var multiInfo = CenterInfo("Informe os valores de cada forma. Use duas ou três formas.", 70, 11);
        multiInfo.Font = new Font("Segoe UI", 10);
        tabMulti.Controls.Add(multiInfo);

        NumericUpDown PayBox(int top)
        {
            return new NumericUpDown
            {
                Left = 325,
                Top = top,
                Width = 230,
                Height = 35,
                DecimalPlaces = 2,
                Maximum = 9999999,
                Minimum = 0,
                ThousandsSeparator = true,
                TextAlign = HorizontalAlignment.Right,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
        }

        void PayLabel(Control parent, string txt, int top)
        {
            parent.Controls.Add(new Label
            {
                Text = txt,
                Left = 150,
                Top = top + 4,
                Width = 160,
                Height = 30,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = DarkBlue
            });
        }

        PayLabel(tabMulti, "Dinheiro", 130);
        PayLabel(tabMulti, "PIX", 180);
        PayLabel(tabMulti, "Cartão", 230);

        var multiCash = PayBox(126);
        var multiPix = PayBox(176);
        var multiCard = PayBox(226);

        tabMulti.Controls.Add(multiCash);
        tabMulti.Controls.Add(multiPix);
        tabMulti.Controls.Add(multiCard);

        var multiStatus = CenterInfo("", 285, 13);
        tabMulti.Controls.Add(multiStatus);

        void UpdateMulti()
        {
            var sum = (double)multiCash.Value + (double)multiPix.Value + (double)multiCard.Value;
            var diff = total - sum;

            if (Math.Abs(diff) <= 0.01)
            {
                multiStatus.Text = "VALORES CONFEREM • " + Money(sum);
                multiStatus.ForeColor = Color.FromArgb(0, 130, 78);
            }
            else if (diff > 0)
            {
                multiStatus.Text = "FALTA: " + Money(diff);
                multiStatus.ForeColor = Color.FromArgb(190, 45, 45);
            }
            else
            {
                multiStatus.Text = "EXCEDE: " + Money(Math.Abs(diff));
                multiStatus.ForeColor = Color.FromArgb(190, 45, 45);
            }
        }

        multiCash.ValueChanged += (_, _) => UpdateMulti();
        multiPix.ValueChanged += (_, _) => UpdateMulti();
        multiCard.ValueChanged += (_, _) => UpdateMulti();

        var multiConfirm = BigConfirm("CONFIRMAR MÚLTIPLO");
        multiConfirm.Left = 210;
        multiConfirm.Top = 355;
        tabMulti.Controls.Add(multiConfirm);

        // Footer
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            BackColor = Color.FromArgb(225, 236, 245)
        };
        var cancel = new Button
        {
            Text = "CANCELAR",
            Width = 150,
            Height = 38,
            Left = 565,
            Top = 10,
            BackColor = Color.FromArgb(90, 100, 110),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        cancel.FlatAppearance.BorderSize = 0;
        cancel.Click += (_, _) => f.Close();
        footer.Controls.Add(cancel);
        f.Controls.Add(footer);
        footer.BringToFront();

        var result = new List<PaymentPart>();

        cashConfirm.Click += (_, _) =>
        {
            if ((double)received.Value + 0.01 < total)
            {
                Info("O valor recebido é menor que o total da venda.");
                return;
            }

            result.Add(new PaymentPart { Method = "Dinheiro", Amount = total });
            f.DialogResult = DialogResult.OK;
            f.Close();
        };

        pixConfirm.Click += (_, _) =>
        {
            var key=GetSetting("pix_key");var keyType=GetSetting("pix_key_type","ALEATÓRIA");
            if(string.IsNullOrWhiteSpace(key)){Info("Configure sua chave em CONFIGURAÇÕES > PIX.");return;}
            try
            {
                if(GetSetting("pix_mp_enabled")=="1")
                {
                    var token=PixSecureSettings.Unprotect(GetSetting("pix_mp_access_token"));
                    if(string.IsNullOrWhiteSpace(token)){Info("Mercado Pago automático está ativo, mas falta o Access Token.");return;}
                    var payer=Microsoft.VisualBasic.Interaction.InputBox("E-mail do cliente para gerar o PIX automático:","PIX Mercado Pago","");
                    if(string.IsNullOrWhiteSpace(payer))return;
                    using var pix=new MercadoPagoPixForm((decimal)total,payer,new MercadoPagoPixService(token));
                    if(pix.ShowDialog(f)!=DialogResult.OK)return;
                }
                else
                {
                    var merchant=GetSetting("company_trade_name",GetSetting("company_name","LEAL INFO"));
                    var cityState=GetSetting("company_city_state","BRASIL");var city=cityState.Split('/')[0].Trim();
                    using var pix=new SimplePixPaymentForm((decimal)total,key,keyType,merchant,city);
                    if(pix.ShowDialog(f)!=DialogResult.OK)return;
                }
                result.Add(new PaymentPart{Method="PIX",Amount=total});f.DialogResult=DialogResult.OK;f.Close();
            }
            catch(Exception ex){MessageBox.Show(f,"Não foi possível iniciar o PIX:\n\n"+ex.Message,"PIX - LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        };

        cardConfirm.Click += (_, _) =>
        {
            result.Add(new PaymentPart
            {
                Method = "Cartão - " + (cardType.SelectedItem?.ToString() ?? "Débito"),
                Amount = total
            });
            f.DialogResult = DialogResult.OK;
            f.Close();
        };

        multiConfirm.Click += (_, _) =>
        {
            result.Clear();

            if ((double)multiCash.Value > 0.004)
                result.Add(new PaymentPart { Method = "Dinheiro", Amount = (double)multiCash.Value });
            if ((double)multiPix.Value > 0.004)
                result.Add(new PaymentPart { Method = "PIX", Amount = (double)multiPix.Value });
            if ((double)multiCard.Value > 0.004)
                result.Add(new PaymentPart { Method = "Cartão", Amount = (double)multiCard.Value });

            if (result.Count < 2)
            {
                Info("No pagamento múltiplo, informe pelo menos duas formas.");
                return;
            }

            var sum = result.Sum(x => x.Amount);
            if (Math.Abs(sum - total) > 0.01)
            {
                Info($"A soma precisa fechar o total da venda.\n\nTotal: {Money(total)}\nInformado: {Money(sum)}");
                return;
            }

            var pixPart=result.FirstOrDefault(x=>x.Method=="PIX");
            if(pixPart!=null&&pixPart.Amount>0.004)
            {
                var key=GetSetting("pix_key");var keyType=GetSetting("pix_key_type","ALEATÓRIA");
                if(string.IsNullOrWhiteSpace(key)){Info("Configure sua chave em CONFIGURAÇÕES > PIX.");result.Clear();return;}
                try
                {
                    if(GetSetting("pix_mp_enabled")=="1")
                    {
                        var token=PixSecureSettings.Unprotect(GetSetting("pix_mp_access_token"));if(string.IsNullOrWhiteSpace(token)){Info("Falta o Access Token do Mercado Pago.");result.Clear();return;}
                        var payer=Microsoft.VisualBasic.Interaction.InputBox("E-mail do cliente para o PIX automático:","PIX Mercado Pago","");if(string.IsNullOrWhiteSpace(payer)){result.Clear();return;}
                        using var pix=new MercadoPagoPixForm((decimal)pixPart.Amount,payer,new MercadoPagoPixService(token));if(pix.ShowDialog(f)!=DialogResult.OK){result.Clear();return;}
                    }
                    else
                    {
                        var merchant=GetSetting("company_trade_name",GetSetting("company_name","LEAL INFO"));var city=GetSetting("company_city_state","BRASIL").Split('/')[0].Trim();
                        using var pix=new SimplePixPaymentForm((decimal)pixPart.Amount,key,keyType,merchant,city);if(pix.ShowDialog(f)!=DialogResult.OK){result.Clear();return;}
                    }
                }catch(Exception ex){result.Clear();MessageBox.Show(f,"Não foi possível iniciar o PIX:\n\n"+ex.Message,"PIX - LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Error);return;}
            }

            f.DialogResult = DialogResult.OK;
            f.Close();
        };

        f.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                f.Close();
        };

        UpdateMulti();

        ApplyFloatingTheme(f);
        return f.ShowDialog(this) == DialogResult.OK ? result : null;
    }

    private string BuildReceipt(long saleId, DateTime soldAt, IEnumerable<CartItem> items, IEnumerable<PaymentPart> payments, double total)
    {
        var sb = new System.Text.StringBuilder();
        var companyName = GetSetting("company_name", "LEAL INFO CONECTADO");
        var tradeName = GetSetting("company_trade_name");
        var document = GetSetting("company_document");
        var phone = GetSetting("company_phone");
        var address = GetSetting("company_address");
        var cityState = GetSetting("company_city_state");
        var footer = GetSetting("company_footer", "Obrigado pela preferência!");

        sb.AppendLine(string.IsNullOrWhiteSpace(tradeName) ? companyName : tradeName);
        if (!string.IsNullOrWhiteSpace(companyName) && companyName != tradeName)
            sb.AppendLine(companyName);
        if (!string.IsNullOrWhiteSpace(document))
            sb.AppendLine("CNPJ/CPF: " + document);
        if (!string.IsNullOrWhiteSpace(phone))
            sb.AppendLine("Telefone: " + phone);
        if (!string.IsNullOrWhiteSpace(address))
            sb.AppendLine(address);
        if (!string.IsNullOrWhiteSpace(cityState))
            sb.AppendLine(cityState);

        sb.AppendLine("COMPROVANTE DE VENDA - NÃO FISCAL");
        sb.AppendLine(new string('-', 46));
        sb.AppendLine($"Venda: #{saleId}");
        sb.AppendLine($"Data: {soldAt:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Atendente: {Auth.OperatorName}");
        sb.AppendLine(new string('-', 46));

        foreach (var item in items)
        {
            sb.AppendLine(item.Description);
            sb.AppendLine($"{item.Qty:N3} x {Money(item.UnitPrice)}   =   {Money(item.Total)}");
        }

        sb.AppendLine(new string('-', 46));
        sb.AppendLine($"TOTAL: {Money(total)}");
        sb.AppendLine();
        sb.AppendLine("PAGAMENTO:");

        foreach (var p in payments)
            sb.AppendLine($"{p.Method}: {Money(p.Amount)}");

        sb.AppendLine(new string('-', 46));
        sb.AppendLine(string.IsNullOrWhiteSpace(footer) ? "Obrigado pela preferência!" : footer);
        sb.AppendLine(string.IsNullOrWhiteSpace(tradeName) ? companyName : tradeName);
        return sb.ToString();
    }

    private void ShowReceipt(string receipt)
    {
        using var f = new Form
        {
            Text = "Comprovante da Venda",
            StartPosition = FormStartPosition.CenterParent,
            Width = 650,
            Height = 720,
            BackColor = Color.FromArgb(245, 249, 252)
        };

        var title = new Label
        {
            Text = "VENDA FINALIZADA COM SUCESSO",
            Dock = DockStyle.Top,
            Height = 58,
            BackColor = DarkBlue,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        f.Controls.Add(title);

        var box = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 11),
            BackColor = Color.White,
            Text = receipt,
            Dock = DockStyle.Fill
        };
        f.Controls.Add(box);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 66,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10)
        };

        var close = ActionButton("FECHAR", f.Close);
        var print = ActionButton("IMPRIMIR", () => PrintReceipt(receipt));
        var save = ActionButton("SALVAR TXT", () =>
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "Arquivo de texto (*.txt)|*.txt",
                FileName = $"Comprovante_LEAL_INFO_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };
            if (dlg.ShowDialog(f) == DialogResult.OK)
            {
                File.WriteAllText(dlg.FileName, receipt, System.Text.Encoding.UTF8);
                Info("Comprovante salvo com sucesso.");
            }
        });

        buttons.Controls.Add(close);
        buttons.Controls.Add(print);
        buttons.Controls.Add(save);
        f.Controls.Add(buttons);
        buttons.BringToFront();

        ApplyFloatingTheme(f);


        f.ShowDialog(this);
    }

    private void PrintReceipt(string receipt)
    {
        using var doc = new PrintDocument();
        doc.DocumentName = "LEAL INFO CONECTADO - Comprovante de Venda";

        doc.PrintPage += (_, e) =>
        {
            using var font = new Font("Consolas", 9);
            e.Graphics.DrawString(
                receipt,
                font,
                Brushes.Black,
                e.MarginBounds.Left,
                e.MarginBounds.Top);
        };

        using var dlg = new PrintDialog
        {
            Document = doc,
            UseEXDialog = true
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try { doc.Print(); }
            catch (Exception ex) { Info("Não foi possível imprimir:\n" + ex.Message); }
        }
    }

    private void OpenSales()
    {
        var f = new Form
        {
            Text = "LEAL INFO CONECTADO - TELA DE VENDAS • V10.130",
            WindowState = FormWindowState.Maximized,
            MinimumSize = new Size(1180, 720),
            BackColor = Color.FromArgb(7, 24, 43),
            Font = new Font("Segoe UI", 10),
            KeyPreview = true
        };

        var cartItems = new List<CartItem>();
        var cartSource = new BindingSource { DataSource = cartItems };

        // Visual V10.130: cantos arredondados, temas e acabamento moderno,
        // sem alterar a lógica de venda.
        void Round(Control c, int radius)
        {
            void Apply()
            {
                if (c.Width <= 1 || c.Height <= 1) return;
                var r = new Rectangle(0, 0, c.Width, c.Height);
                var gp = new System.Drawing.Drawing2D.GraphicsPath();
                int d = Math.Max(4, radius * 2);
                gp.AddArc(r.X, r.Y, d, d, 180, 90);
                gp.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
                gp.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
                gp.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
                gp.CloseFigure();
                c.Region?.Dispose();
                c.Region = new Region(gp);
                gp.Dispose();
            }
            c.Resize += (_, _) => Apply();
            c.HandleCreated += (_, _) => Apply();
        }

        void ModernButton(Button b, Color normal, Color hover)
        {
            b.BackColor = normal;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Cursor = Cursors.Hand;
            Round(b, 14);
            b.MouseEnter += (_, _) =>
            {
                b.BackColor = hover;
                b.Font = new Font(b.Font.FontFamily, b.Font.Size + 0.6f, FontStyle.Bold);
            };
            b.MouseLeave += (_, _) =>
            {
                b.BackColor = normal;
                b.Font = new Font(b.Font.FontFamily, Math.Max(8f, b.Font.Size - 0.6f), FontStyle.Bold);
            };
        }

        // ===== CABEÇALHO =====
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 92,
            BackColor = Color.FromArgb(4, 45, 82)
        };

        var headerTitle = new Label
        {
            Text = "LEAL INFO CONECTADO  •  CAIXA / PDV",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            AutoSize = true,
            Left = 26,
            Top = 18
        };

        var headerInfo = new Label
        {
            Text = $"TECNOLOGIA QUE CONECTA  •  Atendente: ADMIN  •  {DateTime.Now:dd/MM/yyyy HH:mm}",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        header.Controls.Add(headerTitle);
        header.Controls.Add(headerInfo);

        var headerLine = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 4,
            BackColor = Color.FromArgb(0, 183, 255)
        };
        header.Controls.Add(headerLine);
        header.Resize += (_, _) =>
        {
            headerInfo.Left = Math.Max(20, header.ClientSize.Width - headerInfo.Width - 28);
            headerInfo.Top = 32;
        };
        f.Controls.Add(header);

        // ===== CONTEÚDO RESPONSIVO =====
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(18),
            BackColor = Color.FromArgb(7, 24, 43)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        f.Controls.Add(body);
        body.BringToFront();

        // ===== VITRINE GRANDE DO PRODUTO =====
        var photoShowcase = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(9, 52, 88),
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 12, 0)
        };
        body.Controls.Add(photoShowcase, 0, 0);
        Round(photoShowcase, 24);

        var photoLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        photoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        photoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        photoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        photoShowcase.Controls.Add(photoLayout);

        var photoTitle = new Label
        {
            Text = "PRODUTO",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        photoLayout.Controls.Add(photoTitle, 0, 0);

        var brandPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(238, 248, 255),
            Padding = new Padding(10),
            Margin = new Padding(0, 4, 0, 10)
        };

        var productPicture = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        brandPanel.Controls.Add(productPicture);
        photoLayout.Controls.Add(brandPanel, 0, 1);
        Round(brandPanel, 22);

        var photoProductName = new Label
        {
            Text = "Selecione um produto",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(4, 45, 82),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(8),
            Margin = new Padding(0)
        };
        photoLayout.Controls.Add(photoProductName, 0, 2);
        Round(photoProductName, 16);

        void ShowProductPhoto(long? productId)
        {
            productPicture.Image?.Dispose();
            productPicture.Image = null;
            photoProductName.Text = "Selecione um produto";

            string? path = null;
            string? productName = null;
            if (productId.HasValue)
            {
                using var cn = Database.Open();
                using var cmd = cn.CreateCommand();
                cmd.CommandText = "SELECT COALESCE(photo_path,''), name FROM products WHERE id=$id";
                cmd.Parameters.AddWithValue("$id", productId.Value);
                using var rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    path = rd.GetString(0);
                    productName = rd.GetString(1);
                }
            }

            if (!string.IsNullOrWhiteSpace(productName))
                photoProductName.Text = productName;

            if (!string.IsNullOrWhiteSpace(path))
            {
                string resolved = path;

                if (!Path.IsPathRooted(resolved))
                {
                    var appRelative = Path.Combine(AppContext.BaseDirectory, resolved);
                    var assetsRelative = Path.Combine(AppContext.BaseDirectory, "Assets", resolved);

                    if (File.Exists(appRelative))
                        resolved = appRelative;
                    else if (File.Exists(assetsRelative))
                        resolved = assetsRelative;
                }

                if (File.Exists(resolved))
                {
                    using var img = Image.FromFile(resolved);
                    productPicture.Image = new Bitmap(img);
                    productPicture.Refresh();
                    return;
                }
            }

            // Estado vazio: mantém a logomarca na vitrine.
            // Produto selecionado sem foto: não confundir a logo com a foto do produto.
            if (!productId.HasValue)
            {
                var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
                if (File.Exists(logoPath))
                {
                    using var img = Image.FromFile(logoPath);
                    productPicture.Image = new Bitmap(img);
                }
                photoProductName.Text = "Selecione um produto";
            }
            else
            {
                productPicture.Image = null;
                photoProductName.Text = string.IsNullOrWhiteSpace(productName)
                    ? "SEM FOTO CADASTRADA"
                    : productName + " • SEM FOTO CADASTRADA";
            }
        }

        ShowProductPhoto(null);

        // ===== COLUNA CENTRAL / LANÇAMENTO =====
        var left = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(9, 52, 88),
            Padding = new Padding(24)
        };
        body.Controls.Add(left, 1, 0);
        Round(left, 24);

        var leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 11,
            Padding = new Padding(24, 14, 24, 14),
            BackColor = Color.Transparent
        };
        // Reserva espaço REAL para o status no rodapé. Antes as 10 primeiras linhas
        // consumiam praticamente toda a altura útil e o "CAIXA LIVRE" era cortado.
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(leftLayout);

        Label SaleLabel(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        };

        var searchLabel = SaleLabel("Código de barras / Produto  [F5]");
        searchLabel.Cursor = Cursors.Hand;
        var search = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(8, 38, 68),
            BorderStyle = BorderStyle.FixedSingle
        };
        var qty = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            DecimalPlaces = 3,
            Minimum = 0.001M,
            Maximum = 999999,
            Value = 1,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Right,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(8, 38, 68)
        };
        var unit = new TextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Right,
            BackColor = Color.White, ForeColor = Color.FromArgb(8, 38, 68),
            BorderStyle = BorderStyle.FixedSingle, Text = "R$ 0,00"
        };
        var itemTotal = new TextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Right,
            BackColor = Color.White, ForeColor = Color.FromArgb(8, 38, 68),
            BorderStyle = BorderStyle.FixedSingle, Text = "R$ 0,00"
        };
        var add = new Button
        {
            Text = "ADICIONAR ITEM  [ENTER]",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0, 183, 255), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Cursor = Cursors.Hand, Margin = new Padding(0, 4, 0, 2)
        };
        add.FlatAppearance.BorderSize = 0;
        ModernButton(add, Color.FromArgb(0, 183, 255), Color.FromArgb(35, 205, 255));

        var clear = new Button
        {
            Text = "LIMPAR",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(28, 96, 135),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 4, 0, 2)
        };
        clear.FlatAppearance.BorderSize = 0;
        ModernButton(clear, Color.FromArgb(28, 96, 135), Color.FromArgb(45, 130, 175));

        // Moldura cinematográfica do status: o Label continua sendo o mesmo componente
        // usado pela lógica da venda. A moldura é apenas visual, evitando regressões.
        var statusFrame = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0, 190, 245),
            Margin = new Padding(0, 6, 0, 0),
            Padding = new Padding(2)
        };
        var statusInner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DarkBlue,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        var statusBox = new Label
        {
            Text = "CAIXA LIVRE",
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            UseCompatibleTextRendering = false
        };
        statusInner.Controls.Add(statusBox);
        statusFrame.Controls.Add(statusInner);

        Round(search, 12);
        Round(qty, 12);
        Round(unit, 12);
        Round(itemTotal, 12);
        Round(statusFrame, 18);
        Round(statusInner, 16);

        // Pulso cinematográfico estável: geometria e fonte nunca mudam.
        // Somente a luz da moldura e o fundo variam suavemente.
        bool pulseUp = true;
        int pulseStep = 0;
        var freePulse = new System.Windows.Forms.Timer { Interval = 90 };
        freePulse.Tick += (_,_) =>
        {
            if(statusBox.Text != "CAIXA LIVRE")
            {
                statusFrame.BackColor = Color.FromArgb(0, 150, 205);
                statusInner.BackColor = DarkBlue;
                statusBox.ForeColor = Color.White;
                return;
            }

            pulseStep += pulseUp ? 1 : -1;
            if(pulseStep >= 6) { pulseStep = 6; pulseUp = false; }
            if(pulseStep <= 0) { pulseStep = 0; pulseUp = true; }

            statusFrame.BackColor = Color.FromArgb(
                0,
                165 + pulseStep * 8,
                215 + pulseStep * 6);
            statusInner.BackColor = Color.FromArgb(
                0,
                92 + pulseStep * 5,
                142 + pulseStep * 7);
            statusBox.ForeColor = Color.White;
        };
        freePulse.Start();
        f.FormClosed += (_,_) => freePulse.Dispose();

        leftLayout.Controls.Add(searchLabel, 0, 0);
        leftLayout.Controls.Add(search, 0, 1);
        leftLayout.Controls.Add(SaleLabel("Quantidade"), 0, 2);
        leftLayout.Controls.Add(qty, 0, 3);
        leftLayout.Controls.Add(SaleLabel("Valor Unitário"), 0, 4);
        leftLayout.Controls.Add(unit, 0, 5);
        leftLayout.Controls.Add(SaleLabel("Valor Total do Item"), 0, 6);
        leftLayout.Controls.Add(itemTotal, 0, 7);
        leftLayout.Controls.Add(add, 0, 8);
        leftLayout.Controls.Add(clear, 0, 9);
        leftLayout.Controls.Add(statusFrame, 0, 10);

        // ===== COLUNA DIREITA / CUPOM =====
        var right = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(240, 247, 252),
            Padding = new Padding(14)
        };
        body.Controls.Add(right, 2, 0);
        Round(right, 24);

        // Estrutura profissional:
        // 1) Total compacto e totalmente visível
        // 2) Dica F2 logo abaixo
        // 3) Título + tabela ocupando o maior espaço
        // 4) Cliente
        // 5) Ações no rodapé
        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(0),
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126)); // total
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // F2
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));  // título itens
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // tabela
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));  // cliente
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));  // botões
        right.Controls.Add(rightLayout);

        // ===== TOTAL DA VENDA =====
        var subtotalPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DarkBlue,
            Padding = new Padding(18, 10, 18, 10),
            Margin = new Padding(0, 0, 0, 7)
        };
        Round(subtotalPanel, 18);

        var subtotalCaption = new Label
        {
            Text = "TOTAL DA VENDA",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtotalValue = new Label
        {
            Text = "R$ 0,00",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 4, 0)
        };

        subtotalPanel.Controls.Add(subtotalValue);
        subtotalPanel.Controls.Add(subtotalCaption);
        rightLayout.Controls.Add(subtotalPanel, 0, 0);

        // ===== DICA F2 =====
        var paymentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        var paymentText = new Label
        {
            Text = "Pressione F2 para escolher a forma de pagamento",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = DarkBlue,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 0, 0, 0)
        };
        paymentPanel.Controls.Add(paymentText);
        rightLayout.Controls.Add(paymentPanel, 0, 1);

        // Mantido por compatibilidade com a lógica existente.
        var payment = new ComboBox
        {
            Width = 180,
            Height = 36,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 11),
            Visible = false
        };
        payment.Items.AddRange(new[] { "Dinheiro", "PIX", "Cartão", "Múltiplo" });
        payment.SelectedIndex = 0;
        paymentPanel.Controls.Add(payment);

        // ===== TÍTULO DOS ITENS =====
        var cupomTitle = new Label
        {
            Text = "LEAL INFO • ITENS DA VENDA",
            Dock = DockStyle.Fill,
            BackColor = DarkBlue,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 4, 0, 5)
        };
        rightLayout.Controls.Add(cupomTitle, 0, 2);
        Round(cupomTitle, 16);

        // ===== TABELA GRANDE =====
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ColumnHeadersHeight = 40,
            Margin = new Padding(0, 0, 0, 5)
        };
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(215, 239, 252);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = DarkBlue;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 10);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(20, 135, 210);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 251, 255);
        grid.DataError += (_, e) => { e.ThrowException = false; e.Cancel = true; };

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Código",
            DataPropertyName = nameof(CartItem.Code),
            Width = 95
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Descrição",
            DataPropertyName = nameof(CartItem.Description),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Qtd.",
            DataPropertyName = nameof(CartItem.Qty),
            Width = 68,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N3" }
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Vlr Unit.",
            DataPropertyName = nameof(CartItem.UnitPrice),
            Width = 95,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" }
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Total",
            DataPropertyName = nameof(CartItem.Total),
            Width = 105,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" }
        });
        grid.DataSource = cartSource;
        rightLayout.Controls.Add(grid, 0, 3);
        Round(grid, 12);

        // ===== CLIENTE =====
        var clientLabel = new Label
        {
            Text = "CLIENTE: CONSUMIDOR FINAL",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(222, 239, 250),
            ForeColor = DarkBlue,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Margin = new Padding(0, 0, 0, 5)
        };
        rightLayout.Controls.Add(clientLabel, 0, 4);
        Round(clientLabel, 12);

        // ===== AÇÕES NO RODAPÉ =====
        var actionPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0, 5, 0, 0)
        };
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));

        var remove = new Button
        {
            Text = "REMOVER ITEM [F7]",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 5, 0),
            BackColor = Color.FromArgb(165, 48, 62),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        remove.FlatAppearance.BorderSize = 0;
        ModernButton(remove, Color.FromArgb(165,48,62), Color.FromArgb(215,65,82));
        Round(remove, 12);

        var styleButton = new Button
        {
            Text = "🎨 ESTILO",
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 0, 5, 0),
            BackColor = Color.FromArgb(0, 145, 210),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        styleButton.FlatAppearance.BorderSize = 0;
        ModernButton(styleButton, Color.FromArgb(112,72,190), Color.FromArgb(155,105,235));
        Round(styleButton, 12);

        var finish = new Button
        {
            Text = "FINALIZAR VENDA  [F2]",
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 0, 5, 0),
            BackColor = Color.FromArgb(0, 163, 224),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold)
        };
        finish.FlatAppearance.BorderSize = 0;
        finish.Cursor = Cursors.Hand;
        ModernButton(finish, Color.FromArgb(0,170,105), Color.FromArgb(25,220,145));
        Round(finish, 12);

        var close = new Button
        {
            Text = "FECHAR",
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 0, 0, 0),
            BackColor = DarkBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        close.FlatAppearance.BorderSize = 0;
        close.Cursor = Cursors.Hand;
        ModernButton(close, Color.FromArgb(55,68,82), Color.FromArgb(88,105,122));
        Round(close, 12);

        actionPanel.Controls.Add(remove, 0, 0);
        actionPanel.Controls.Add(styleButton, 1, 0);
        actionPanel.Controls.Add(finish, 2, 0);
        actionPanel.Controls.Add(close, 3, 0);
        rightLayout.Controls.Add(actionPanel, 0, 5);

        Bitmap CreatePdvTexture(int width, int height, Color c1, Color c2, Color glow, bool light, int seed)
        {
            width = Math.Max(96, width);
            height = Math.Max(96, height);
            var bmp = new Bitmap(width, height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (var baseBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Point(0, 0), new Point(width, height), c1, c2))
            {
                g.FillRectangle(baseBrush, 0, 0, width, height);
            }

            // Luz perolada difusa: dá volume sem virar um fundo chamativo demais.
            using (var glowPath = new System.Drawing.Drawing2D.GraphicsPath())
            {
                glowPath.AddEllipse(-width / 5, -height / 3, width, height);
                using var pgb = new System.Drawing.Drawing2D.PathGradientBrush(glowPath);
                pgb.CenterColor = Color.FromArgb(light ? 72 : 54, glow);
                pgb.SurroundColors = new[] { Color.FromArgb(0, glow) };
                g.FillPath(pgb, glowPath);
            }

            // Faixas acetinadas diagonais, quase transparentes.
            using (var satin = new Pen(Color.FromArgb(light ? 22 : 18, Color.White), 1.2f))
            {
                for (int x = -height; x < width + height; x += 26)
                    g.DrawLine(satin, x, 0, x + height, height);
            }

            // Microtextura determinística para quebrar qualquer sensação de cor chapada.
            var rnd = new Random(seed);
            int dots = Math.Max(800, width * height / 550);
            for (int i = 0; i < dots; i++)
            {
                int x = rnd.Next(width);
                int y = rnd.Next(height);
                int a = rnd.Next(light ? 4 : 5, light ? 15 : 17);
                int v = rnd.Next(170, 256);
                using var dot = new SolidBrush(Color.FromArgb(a, v, v, v));
                g.FillRectangle(dot, x, y, 1, 1);
            }

            // Veios suaves rosé/metálicos.
            using (var vein = new Pen(Color.FromArgb(light ? 18 : 22, glow), 1.0f))
            {
                for (int y = 18; y < height; y += 42)
                    g.DrawBezier(vein, 0, y, width / 3, y - 14, width * 2 / 3, y + 16, width, y - 4);
            }

            return bmp;
        }

        void ClearTexture(Control c)
        {
            var old = c.BackgroundImage;
            c.BackgroundImage = null;
            old?.Dispose();
        }

        void SetTexture(Control c, Color c1, Color c2, Color glow, bool light, int seed)
        {
            ClearTexture(c);
            c.BackgroundImage = CreatePdvTexture(720, 520, c1, c2, glow, light, seed);
            c.BackgroundImageLayout = ImageLayout.Stretch;
        }

        void ApplySalesTheme(string theme)
        {
            Color bg, headerBg, accent, accentHover, leftBg, rightBg, fieldBg, textDark, soft, secondary;

            switch (theme)
            {
                case "Dark Premium":
                    bg = Color.FromArgb(10, 12, 18);
                    headerBg = Color.FromArgb(18, 21, 29);
                    accent = Color.FromArgb(0, 170, 235);
                    accentHover = Color.FromArgb(40, 210, 255);
                    leftBg = Color.FromArgb(24, 28, 38);
                    rightBg = Color.FromArgb(30, 34, 44);
                    fieldBg = Color.FromArgb(245, 247, 250);
                    textDark = Color.FromArgb(20, 28, 38);
                    soft = Color.FromArgb(210, 220, 230);
                    secondary = Color.FromArgb(52, 61, 75);
                    break;

                case "Clean Pro":
                    bg = Color.FromArgb(225, 235, 242);
                    headerBg = Color.FromArgb(35, 68, 92);
                    accent = Color.FromArgb(45, 135, 180);
                    accentHover = Color.FromArgb(70, 165, 205);
                    leftBg = Color.FromArgb(245, 249, 252);
                    rightBg = Color.White;
                    fieldBg = Color.White;
                    textDark = Color.FromArgb(35, 58, 72);
                    soft = Color.FromArgb(224, 235, 242);
                    secondary = Color.FromArgb(90, 115, 130);
                    break;

                case "Blue Red Racing":
                    bg = Color.FromArgb(8, 22, 42);
                    headerBg = Color.FromArgb(185, 22, 38);
                    accent = Color.FromArgb(235, 30, 48);
                    accentHover = Color.FromArgb(255, 65, 78);
                    leftBg = Color.FromArgb(18, 54, 92);
                    rightBg = Color.FromArgb(245, 246, 248);
                    fieldBg = Color.White;
                    textDark = Color.FromArgb(12, 38, 68);
                    soft = Color.FromArgb(238, 218, 222);
                    secondary = Color.FromArgb(25, 72, 125);
                    break;

                case "Verde Texturizado":
                    bg = Color.FromArgb(8, 45, 34);
                    headerBg = Color.FromArgb(10, 92, 63);
                    accent = Color.FromArgb(32, 190, 118);
                    accentHover = Color.FromArgb(72, 225, 150);
                    leftBg = Color.FromArgb(18, 105, 72);
                    rightBg = Color.FromArgb(232, 248, 239);
                    fieldBg = Color.FromArgb(250, 255, 252);
                    textDark = Color.FromArgb(12, 65, 45);
                    soft = Color.FromArgb(205, 238, 220);
                    secondary = Color.FromArgb(38, 125, 86);
                    break;
                case "PDV Rosa":
                    // Rosa Elegance: rosé, framboesa e vinho com acabamento acetinado/perolado.
                    bg = Color.FromArgb(65, 10, 43);
                    headerBg = Color.FromArgb(118, 13, 78);
                    accent = Color.FromArgb(244, 67, 151);
                    accentHover = Color.FromArgb(255, 126, 190);
                    leftBg = Color.FromArgb(92, 18, 67);
                    rightBg = Color.FromArgb(255, 242, 249);
                    fieldBg = Color.FromArgb(255, 252, 254);
                    textDark = Color.FromArgb(91, 20, 66);
                    soft = Color.FromArgb(255, 220, 238);
                    secondary = Color.FromArgb(176, 53, 120);
                    break;

                default:
                    theme = "Futurista Azul";
                    bg = Color.FromArgb(7, 24, 43);
                    headerBg = Color.FromArgb(4, 45, 82);
                    accent = Color.FromArgb(0, 183, 255);
                    accentHover = Color.FromArgb(25, 205, 255);
                    leftBg = Color.FromArgb(9, 52, 88);
                    rightBg = Color.FromArgb(240, 247, 252);
                    fieldBg = Color.White;
                    textDark = Color.FromArgb(4, 55, 94);
                    soft = Color.FromArgb(222, 239, 250);
                    secondary = Color.FromArgb(28, 96, 135);
                    break;
            }

            // Remove qualquer textura do tema anterior antes de aplicar a próxima.
            foreach (var c in new Control[] { f, body, header, left, right, photoShowcase })
                ClearTexture(c);

            f.BackColor = bg;
            body.BackColor = bg;
            header.BackColor = headerBg;

            if (theme == "Verde Texturizado")
            {
                SetTexture(f, Color.FromArgb(8, 58, 42), Color.FromArgb(18, 118, 78), Color.FromArgb(70, 235, 155), false, 1711);
                SetTexture(body, Color.FromArgb(10, 62, 44), Color.FromArgb(20, 112, 76), Color.FromArgb(70, 235, 155), false, 1712);
                SetTexture(left, Color.FromArgb(16, 92, 62), Color.FromArgb(9, 58, 42), Color.FromArgb(90, 245, 170), false, 1713);
            }
            if (theme == "PDV Rosa")
            {
                // Aquarela rosa clara escolhida pelo usuário para o tema PDV Rosa.
                const string rosaAquarelaBase64 = "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";
                using var rosaBytes = new MemoryStream(Convert.FromBase64String(rosaAquarelaBase64));
                using var rosaOriginal = Image.FromStream(rosaBytes);
                foreach (var c in new Control[] { f, body, header, photoShowcase, left, right })
                {
                    c.BackgroundImage?.Dispose();
                    c.BackgroundImage = new Bitmap(rosaOriginal);
                    c.BackgroundImageLayout = ImageLayout.Stretch;
                }
            }
            headerLine.BackColor = theme == "Blue Red Racing"
                ? Color.FromArgb(35, 125, 210)
                : accent;

            left.BackColor = leftBg;
            right.BackColor = rightBg;
            photoShowcase.BackColor = leftBg;
            photoTitle.ForeColor = theme == "Clean Pro" ? textDark : Color.White;
            photoProductName.BackColor = theme == "Blue Red Racing" ? accent : headerBg;
            brandPanel.BackColor = soft;
            productPicture.BackColor = fieldBg;

            search.BackColor = fieldBg;
            search.ForeColor = textDark;
            qty.BackColor = fieldBg;
            qty.ForeColor = textDark;
            unit.BackColor = fieldBg;
            unit.ForeColor = textDark;
            itemTotal.BackColor = fieldBg;
            itemTotal.ForeColor = textDark;

            statusFrame.BackColor = (theme == "Blue Red Racing" || theme == "PDV Rosa") ? accent : Color.FromArgb(0, 150, 205);
            statusInner.BackColor = headerBg;
            statusBox.BackColor = Color.Transparent;
            cupomTitle.BackColor = (theme == "Blue Red Racing" || theme == "PDV Rosa") ? accent : headerBg;
            subtotalPanel.BackColor = (theme == "Blue Red Racing" || theme == "PDV Rosa") ? accent : headerBg;
            clientLabel.BackColor = soft;
            clientLabel.ForeColor = textDark;
            paymentText.ForeColor = theme == "Dark Premium" ? Color.White : textDark;

            grid.BackgroundColor = fieldBg;
            grid.ColumnHeadersDefaultCellStyle.BackColor = soft;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = textDark;
            grid.AlternatingRowsDefaultCellStyle.BackColor =
                theme == "Dark Premium" ? Color.FromArgb(235, 240, 245) : Color.FromArgb(246, 250, 252);

            add.BackColor = accent;
            clear.BackColor = secondary;
            styleButton.BackColor = Color.FromArgb(112,72,190);
            finish.BackColor = Color.FromArgb(0,170,105);
            remove.BackColor = Color.FromArgb(165,48,62);
            close.BackColor = Color.FromArgb(55,68,82);

            foreach (Control c in leftLayout.Controls)
            {
                if (c is Label lbl && lbl != statusBox)
                    lbl.ForeColor = (theme == "Clean Pro" || theme == "PDV Rosa") ? (theme == "PDV Rosa" ? Color.Black : textDark) : Color.White;
            }

            SetSetting("sales_theme", theme);
            f.Invalidate(true);
        }

        void ShowThemeChooser()
        {
            using var tf = new Form
            {
                Text = "Estilo da Tela de Vendas",
                StartPosition = FormStartPosition.CenterParent,
                Width = 690,
                Height = 610,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(224, 239, 248),
                Font = new Font("Segoe UI", 10)
            };

            var title = new Label
            {
                Text = "ESCOLHA O ESTILO DO SEU PDV",
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = DarkBlue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            tf.Controls.Add(title);

            var options = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(18),
                BackColor = Color.FromArgb(224, 239, 248)
            };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            options.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));
            options.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            options.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            tf.Controls.Add(options);
            options.BringToFront();

            Button ThemeCard(string name, string description, Color c1, Color c2)
            {
                var b = new Button
                {
                    Text = name.ToUpperInvariant() + "\n\n" + description,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(10),
                    BackColor = c1,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    Tag = name
                };
                b.FlatAppearance.BorderColor = c2;
                b.FlatAppearance.BorderSize = 3;
                Round(b, 18);
                b.Click += (_, _) =>
                {
                    ApplySalesTheme(name);
                    tf.Close();
                };
                return b;
            }

            options.Controls.Add(ThemeCard("Futurista Azul", "Azul + ciano tecnológico", Color.FromArgb(7, 55, 95), Color.FromArgb(0, 183, 255)), 0, 0);
            options.Controls.Add(ThemeCard("Dark Premium", "Grafite + azul elétrico", Color.FromArgb(25, 28, 38), Color.FromArgb(0, 180, 240)), 1, 0);
            options.Controls.Add(ThemeCard("Clean Pro", "Claro + elegante", Color.FromArgb(75, 115, 140), Color.White), 0, 1);
            options.Controls.Add(ThemeCard("Blue Red Racing", "Azul + vermelho em destaque", Color.FromArgb(185, 22, 38), Color.FromArgb(25, 100, 180)), 1, 1);
            options.Controls.Add(ThemeCard("PDV Rosa", "Rosé texturizado + vinho acetinado", Color.FromArgb(125, 20, 86), Color.FromArgb(255, 72, 165)), 0, 2);

            tf.ShowDialog(f);
        }

        styleButton.Click += (_, _) => ShowThemeChooser();
        ApplySalesTheme(GetSetting("sales_theme", "Futurista Azul"));

        void OpenCatalogF5()
        {
            var selected = SelectProductFromCatalog();
            if (selected == null)
                return;

            // Prefer barcode as the key; if product has no barcode, use its exact name.
            search.Text = !string.IsNullOrWhiteSpace(selected.Value.code)
                ? selected.Value.code
                : selected.Value.name;

            unit.Text = Money(selected.Value.price);
            itemTotal.Text = Money(selected.Value.price * (double)qty.Value);
            statusBox.Text = $"{selected.Value.name}\nESTOQUE: {selected.Value.stock:N3}";
            ShowProductPhoto(selected.Value.id);
            search.Focus();
            search.SelectAll();
            AddCurrent();
        }

        void OpenLooseSaleF10()
        {
            using var vf = new Form
            {
                Text = "Venda Avulsa - F10",
                StartPosition = FormStartPosition.CenterParent,
                Width = 560,
                Height = 350,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(224, 239, 248),
                Font = new Font("Segoe UI", 10),
                KeyPreview = true
            };

            var title = new Label { Text = "VENDA AVULSA", Dock = DockStyle.Top, Height = 62, BackColor = DarkBlue, ForeColor = Color.White, Font = new Font("Segoe UI", 18, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            var description = new TextBox { Left = 34, Top = 105, Width = 475, Height = 36, Font = new Font("Segoe UI", 13), PlaceholderText = "Descrição do serviço" };
            var value = new NumericUpDown { Left = 34, Top = 185, Width = 475, Height = 40, DecimalPlaces = 2, Minimum = 0.01M, Maximum = 999999.99M, ThousandsSeparator = true, Font = new Font("Segoe UI", 16, FontStyle.Bold), TextAlign = HorizontalAlignment.Right };
            var addLoose = new Button { Text = "ADICIONAR À VENDA", Left = 274, Top = 248, Width = 235, Height = 48, BackColor = Color.FromArgb(0, 163, 224), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11, FontStyle.Bold), DialogResult = DialogResult.OK };
            var cancelLoose = new Button { Text = "CANCELAR", Left = 34, Top = 248, Width = 220, Height = 48, BackColor = Color.FromArgb(55, 88, 115), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11, FontStyle.Bold), DialogResult = DialogResult.Cancel };
            vf.Controls.AddRange(new Control[] { title, new Label { Text = "DESCRIÇÃO DO SERVIÇO", Left = 34, Top = 82, Width = 300, ForeColor = DarkBlue, Font = new Font("Segoe UI", 9, FontStyle.Bold) }, description, new Label { Text = "VALOR", Left = 34, Top = 162, Width = 150, ForeColor = DarkBlue, Font = new Font("Segoe UI", 9, FontStyle.Bold) }, value, cancelLoose, addLoose });
            vf.AcceptButton = addLoose; vf.CancelButton = cancelLoose; ApplyFloatingTheme(vf);
            vf.Shown += (_, _) => description.Focus();

            if (vf.ShowDialog(f) != DialogResult.OK) return;
            var desc = description.Text.Trim();
            if (string.IsNullOrWhiteSpace(desc)) { Info("Informe a descrição do serviço."); return; }
            if (value.Value <= 0) { Info("Informe o valor da venda avulsa."); return; }

            cartItems.Add(new CartItem { ProductId = 0, Code = "AVULSO", Description = desc, Qty = 1, UnitPrice = (double)value.Value });
            RefreshCart();
            statusBox.Text = $"{desc}\nVENDA AVULSA ADICIONADA";
            search.Focus();
        }

        searchLabel.Click += (_, _) => OpenCatalogF5();

        void RefreshCart()
        {
            cartSource.ResetBindings(false);
            subtotalValue.Text = Money(cartItems.Sum(x => x.Total));
        }

        CartItem? LoadProduct(string key)
        {
            var term = key.Trim();
            if (string.IsNullOrWhiteSpace(term))
                return null;

            using var cn = Database.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                SELECT id, COALESCE(barcode,''), name, price, stock
                FROM products
                WHERE active=1
                  AND (barcode=$exact OR lower(name) LIKE lower($name))
                ORDER BY CASE WHEN barcode=$exact THEN 0 ELSE 1 END, name
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("$exact", term);
            cmd.Parameters.AddWithValue("$name", "%" + term + "%");

            using var rd = cmd.ExecuteReader();
            if (!rd.Read())
                return null;

            var requestedQty = (double)qty.Value;
            var stock = rd.GetDouble(4);
            if (stock < requestedQty)
            {
                Info($"Estoque insuficiente.\nDisponível: {stock:N3}");
                return null;
            }

            return new CartItem
            {
                ProductId = rd.GetInt64(0),
                Code = rd.GetString(1),
                Description = rd.GetString(2),
                Qty = requestedQty,
                UnitPrice = rd.GetDouble(3)
            };
        }


        decimal? SelectQuantity(CartItem product)
        {
            using var qf = new Form
            {
                Text = "Quantidade do Produto",
                StartPosition = FormStartPosition.CenterParent,
                Width = 520,
                Height = 430,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(224, 239, 248),
                Font = new Font("Segoe UI", 10),
                KeyPreview = true
            };

            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = DarkBlue
            };
            top.Controls.Add(new Label
            {
                Text = "QUANTIDADE DO PRODUTO",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            });
            qf.Controls.Add(top);

            var bodyQ = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(24),
                BackColor = Color.FromArgb(224, 239, 248)
            };
            bodyQ.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            bodyQ.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            bodyQ.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            bodyQ.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            bodyQ.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            bodyQ.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            qf.Controls.Add(bodyQ);

            bodyQ.Controls.Add(new Label
            {
                Text = product.Description,
                Dock = DockStyle.Fill,
                ForeColor = DarkBlue,
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            }, 0, 0);

            double stockAvailable = 0;
            using (var cn = Database.Open())
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "SELECT stock FROM products WHERE id=$id";
                cmd.Parameters.AddWithValue("$id", product.ProductId);
                stockAvailable = Convert.ToDouble(cmd.ExecuteScalar() ?? 0);
            }

            bodyQ.Controls.Add(new Label
            {
                Text = $"Estoque disponível: {stockAvailable:N3}",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(4, 70, 112),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            }, 0, 0);

            var qInput = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                DecimalPlaces = 3,
                Minimum = 0.001M,
                Maximum = (decimal)Math.Max(stockAvailable, 0.001),
                Value = 1,
                TextAlign = HorizontalAlignment.Center,
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(8, 38, 68),
                Margin = new Padding(18, 5, 18, 5)
            };
            bodyQ.Controls.Add(qInput, 0, 0);

            bodyQ.Controls.Add(new Label
            {
                Text = $"Valor unitário: {Money(product.UnitPrice)}",
                Dock = DockStyle.Fill,
                ForeColor = DarkBlue,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            }, 0, 0);

            var totalPreview = new Label
            {
                Text = $"Total: {Money(product.UnitPrice)}",
                Dock = DockStyle.Fill,
                BackColor = DarkBlue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(18, 2, 18, 2)
            };
            bodyQ.Controls.Add(totalPreview, 0, 0);

            qInput.ValueChanged += (_, _) =>
                totalPreview.Text = $"Total: {Money(product.UnitPrice * (double)qInput.Value)}";

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(18, 8, 18, 0)
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var cancelQ = new Button
            {
                Text = "CANCELAR",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 0),
                BackColor = Color.FromArgb(55, 88, 115),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                DialogResult = DialogResult.Cancel
            };
            cancelQ.FlatAppearance.BorderSize = 0;

            var addQ = new Button
            {
                Text = "ADICIONAR",
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 0, 0),
                BackColor = Color.FromArgb(0, 163, 224),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            addQ.FlatAppearance.BorderSize = 0;

            actions.Controls.Add(cancelQ, 0, 0);
            actions.Controls.Add(addQ, 1, 0);
            bodyQ.Controls.Add(actions, 0, 0);

            qf.AcceptButton = addQ;
            qf.CancelButton = cancelQ;
            ApplyFloatingTheme(qf);

            qf.Shown += (_, _) =>
            {
                qInput.Focus();
                qInput.Select(0, qInput.Text.Length);
            };

            return qf.ShowDialog(f) == DialogResult.OK ? qInput.Value : null;
        }

        void AddCurrent()
        {
            if (string.IsNullOrWhiteSpace(search.Text))
            {
                Info("Digite o código de barras ou parte do nome do produto.");
                search.Focus();
                return;
            }

            var oldQty = qty.Value;
            qty.Value = 1;
            var product = LoadProduct(search.Text);
            qty.Value = oldQty;

            if (product == null)
            {
                Info("Produto não encontrado.");
                search.SelectAll();
                search.Focus();
                return;
            }

            var selectedQty = SelectQuantity(product);
            if (selectedQty == null)
            {
                search.SelectAll();
                search.Focus();
                return;
            }

            product.Qty = (double)selectedQty.Value;

            var existing = cartItems.FirstOrDefault(x => x.ProductId == product.ProductId);
            if (existing != null)
            {
                // Revalidar estoque considerando o que já está no carrinho.
                using var cn = Database.Open();
                using var cmd = cn.CreateCommand();
                cmd.CommandText = "SELECT stock FROM products WHERE id=$id";
                cmd.Parameters.AddWithValue("$id", product.ProductId);
                var stock = Convert.ToDouble(cmd.ExecuteScalar() ?? 0);
                if (existing.Qty + product.Qty > stock)
                {
                    Info($"Estoque insuficiente.\nDisponível: {stock:N3}");
                    return;
                }
                existing.Qty += product.Qty;
            }
            else
            {
                cartItems.Add(product);
            }

            unit.Text = Money(product.UnitPrice);
            itemTotal.Text = Money(product.Total);
            statusBox.Text = $"{product.Description}\nADICIONADO À VENDA";
            ShowProductPhoto(product.ProductId);
            productPicture.BringToFront();
            productPicture.Refresh();
            search.Clear();
            qty.Value = 1;
            RefreshCart();
            search.Focus();
        }

        void ClearEntry()
        {
            search.Clear();
            qty.Value = 1;
            unit.Text = "R$ 0,00";
            itemTotal.Text = "R$ 0,00";
            statusBox.Text = "CAIXA LIVRE";
            ShowProductPhoto(null);
            search.Focus();
        }

        add.Click += (_, _) => AddCurrent();
        clear.Click += (_, _) => ClearEntry();

        search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                AddCurrent();
                e.SuppressKeyPress = true;
            }
        };

        void RemoveSelectedItem()
        {
            if (grid.CurrentRow?.DataBoundItem is not CartItem item)
            {
                Info("Selecione um item da venda para remover.");
                return;
            }

            using var confirm = new RemoveConfirmForm(
                item.Description,
                item.Qty,
                Money(item.Total),
                DarkBlue);

            if (confirm.ShowDialog(f) != DialogResult.Yes)
                return;

            cartItems.Remove(item);
            RefreshCart();
            statusBox.Text = $"{item.Description}\nREMOVIDO DA VENDA";
            search.Focus();
        }

        remove.Click += (_, _) => RemoveSelectedItem();

        close.Click += (_, _) => f.Close();

        void FinalizeSale()
        {
            if (cartItems.Count == 0)
            {
                Info("A venda não possui produtos.");
                search.Focus();
                return;
            }

            var subtotal = cartItems.Sum(x => x.Total);

            // F2 sempre abre a janela flutuante de fechamento.
            var payments = SelectPayment(subtotal);
            if (payments == null || payments.Count == 0)
                return;

            var soldAt = DateTime.Now;

            using var cn = Database.Open();
            using var tx = cn.BeginTransaction();

            try
            {
                foreach (var item in cartItems)
                {
                    if (item.ProductId <= 0) continue;
                    using var chk = cn.CreateCommand();
                    chk.Transaction = tx;
                    chk.CommandText = "SELECT stock FROM products WHERE id=$id";
                    chk.Parameters.AddWithValue("$id", item.ProductId);
                    var stock = Convert.ToDouble(chk.ExecuteScalar() ?? 0);
                    if (stock < item.Qty)
                        throw new Exception($"Estoque insuficiente para {item.Description}. Disponível: {stock:N3}");
                }

                var paymentDescription = payments.Count == 1
                    ? payments[0].Method
                    : "Múltiplo: " + string.Join(" + ", payments.Select(x => x.Method));

                using var sale = cn.CreateCommand();
                sale.Transaction = tx;
                sale.CommandText = """
                    INSERT INTO sales(sold_at,payment,subtotal,discount,total,operator)
                    VALUES($date,$payment,$subtotal,0,$total,$operator);
                    SELECT last_insert_rowid();
                    """;
                sale.Parameters.AddWithValue("$date", soldAt.ToString("yyyy-MM-dd HH:mm:ss"));
                sale.Parameters.AddWithValue("$payment", paymentDescription);
                sale.Parameters.AddWithValue("$operator", Auth.OperatorName);
                sale.Parameters.AddWithValue("$subtotal", subtotal);
                sale.Parameters.AddWithValue("$total", subtotal);
                var saleId = Convert.ToInt64(sale.ExecuteScalar());

                foreach (var item in cartItems)
                {
                    using var itemCmd = cn.CreateCommand();
                    itemCmd.Transaction = tx;
                    itemCmd.CommandText = item.ProductId > 0
                        ? """
                            INSERT INTO sale_items(sale_id,product_id,description,qty,unit_price,total)
                            VALUES($sale,$product,$description,$qty,$unit,$total);
                            UPDATE products SET stock=stock-$qty WHERE id=$product;
                            """
                        : """
                            INSERT INTO sale_items(sale_id,product_id,description,qty,unit_price,total)
                            VALUES($sale,NULL,$description,$qty,$unit,$total);
                            """;
                    itemCmd.Parameters.AddWithValue("$sale", saleId);
                    if (item.ProductId > 0)
                        itemCmd.Parameters.AddWithValue("$product", item.ProductId);
                    itemCmd.Parameters.AddWithValue("$description", item.Description);
                    itemCmd.Parameters.AddWithValue("$qty", item.Qty);
                    itemCmd.Parameters.AddWithValue("$unit", item.UnitPrice);
                    itemCmd.Parameters.AddWithValue("$total", item.Total);
                    itemCmd.ExecuteNonQuery();
                }

                foreach (var part in payments)
                {
                    using var payCmd = cn.CreateCommand();
                    payCmd.Transaction = tx;
                    payCmd.CommandText = """
                        INSERT INTO sale_payments(sale_id,method,amount)
                        VALUES($sale,$method,$amount);
                        """;
                    payCmd.Parameters.AddWithValue("$sale", saleId);
                    payCmd.Parameters.AddWithValue("$method", part.Method);
                    payCmd.Parameters.AddWithValue("$amount", part.Amount);
                    payCmd.ExecuteNonQuery();

                    using var movement = cn.CreateCommand();
                    movement.Transaction = tx;
                    movement.CommandText = """
                        INSERT INTO cash_movements(occurred_at,type,description,amount,sale_id)
                        VALUES($date,'ENTRADA',$description,$amount,$sale)
                        """;
                    movement.Parameters.AddWithValue("$date", soldAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    movement.Parameters.AddWithValue("$description", $"Venda #{saleId} - {part.Method}");
                    movement.Parameters.AddWithValue("$amount", part.Amount);
                    movement.Parameters.AddWithValue("$sale", saleId);
                    movement.ExecuteNonQuery();
                }

                tx.Commit();

                var receipt = BuildReceipt(saleId, soldAt, cartItems.ToList(), payments, subtotal);

                cartItems.Clear();
                RefreshCart();
                ClearEntry();
                RefreshDashboard();

                ShowReceipt(receipt);
                ThermalPrinterService.PrintAutomaticallyIfEnabled(receipt, this);
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                MessageBox.Show(
                    "Não foi possível finalizar a venda:\n\n" + ex.Message,
                    "LEAL INFO PDV",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        finish.Click += (_, _) => FinalizeSale();

        f.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F2)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                FinalizeSale();
                return;
            }
            if (e.KeyCode == Keys.F10)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                OpenLooseSaleF10();
                return;
            }
            if (e.KeyCode == Keys.F5)
            {
                OpenCatalogF5();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F2)
            {
                return;
            }
            else if (e.KeyCode == Keys.F7)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                RemoveSelectedItem();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                f.Close();
            }
        };


        var footer = new StatusStrip
        {
            BackColor = Color.FromArgb(4, 70, 112),
            ForeColor = Color.White,
            SizingGrip = false
        };
        footer.Items.Add(new ToolStripStatusLabel("LEAL INFO CONECTADO"));
        footer.Items.Add(new ToolStripStatusLabel { Spring = true, Text = "PDV Desktop • Windows 11 • V5.1" });
        footer.Items.Add(new ToolStripStatusLabel("Serial: " + Database.DeviceSerial()));
        f.Controls.Add(footer);

        f.Shown += (_, _) => search.Focus();
        f.ShowDialog(this);
    }

    private void AddSaleLabel(Control c,string text,int x,int y)
    {
        c.Controls.Add(new Label{Text=text,Left=x,Top=y,AutoSize=true,ForeColor=Color.White,Font=new Font("Segoe UI",18,FontStyle.Bold)});
    }

    private void ShowCrud(string title,string sql,Action add,Action<long>? edit,Action<long>? delete)
    {
        var f=GridForm(title,sql,out var grid);
        var p=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=65,Padding=new Padding(15)};
        var b1=ActionButton("NOVO",()=>{add();ReloadGrid(grid,sql);});
        p.Controls.Add(b1);
        if(edit!=null)p.Controls.Add(ActionButton("EDITAR",()=>{var id=SelectedId(grid);if(id.HasValue){edit(id.Value);ReloadGrid(grid,sql);}}));
        if(delete!=null)p.Controls.Add(ActionButton("EXCLUIR",()=>{var id=SelectedId(grid);if(id.HasValue){delete(id.Value);ReloadGrid(grid,sql);}}));
        p.Controls.Add(ActionButton("FECHAR",f.Close));
        f.Controls.Add(p);
        ApplyFloatingTheme(f);

        f.ShowDialog(this);
    }

    private void ShowReadOnly(string title,string sql)
    {
        var f=GridForm(title,sql,out _);ApplyFloatingTheme(f);
f.ShowDialog(this);
    }

    private Form GridForm(string title,string sql,out DataGridView grid)
    {
        var f=new Form
        {
            Text=title,
            StartPosition=FormStartPosition.CenterParent,
            Width=1180,
            Height=720,
            BackColor=Color.FromArgb(245,248,252),
            Font=new Font("Segoe UI",10)
        };

        var header=new Panel{Dock=DockStyle.Top,Height=62,BackColor=DarkBlue};
        var titleLabel=new Label
        {
            Text=title,
            ForeColor=Color.White,
            Font=new Font("Segoe UI",18,FontStyle.Bold),
            AutoSize=true,
            Left=22,
            Top=16
        };
        header.Controls.Add(titleLabel);
        f.Controls.Add(header);

        grid=new DataGridView
        {
            Dock=DockStyle.Fill,
            ReadOnly=true,
            AllowUserToAddRows=false,
            AllowUserToDeleteRows=false,
            RowHeadersVisible=false,
            BackgroundColor=Color.White,
            BorderStyle=BorderStyle.None,
            SelectionMode=DataGridViewSelectionMode.FullRowSelect,
            MultiSelect=false,
            AutoGenerateColumns=false,
            ColumnHeadersHeight=42,
            RowTemplate={Height=34}
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(225,235,245);
        grid.ColumnHeadersDefaultCellStyle.ForeColor=DarkBlue;
        grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);
        grid.EnableHeadersVisualStyles=false;
        grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(248,250,253);
        grid.DataError += (_, e) => { e.ThrowException = false; e.Cancel = true; };

        f.Controls.Add(grid);
        grid.BringToFront();
        ReloadGrid(grid,sql);
        return f;
    }

    private void ReloadGrid(DataGridView grid,string sql)
    {
        using var cn=Database.Open();
        using var cmd=cn.CreateCommand();
        cmd.CommandText=sql;
        using var rd=cmd.ExecuteReader();

        grid.DataSource = null;
        grid.Rows.Clear();
        grid.Columns.Clear();
        grid.AutoGenerateColumns = false;

        for (int i = 0; i < rd.FieldCount; i++)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = rd.GetName(i),
                HeaderText = rd.GetName(i),
                AutoSizeMode = i == 0 ? DataGridViewAutoSizeColumnMode.AllCells : DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.Automatic
            });
        }

        while (rd.Read())
        {
            var values = new object[rd.FieldCount];
            for (int i = 0; i < rd.FieldCount; i++)
            {
                var v = rd.IsDBNull(i) ? "" : Convert.ToString(rd.GetValue(i), CultureInfo.GetCultureInfo("pt-BR")) ?? "";
                values[i] = v;
            }
            grid.Rows.Add(values);
        }
    }

    private Button ActionButton(string text,Action action)
    {
        var b=new Button{Text=text,Width=160,Height=42,BackColor=DarkBlue,ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10,FontStyle.Bold),Margin=new Padding(6)};
        b.Click+=(_,_)=>action();return b;
    }

    private static long? SelectedId(DataGridView grid)
    {
        if(grid.CurrentRow==null||grid.Columns["ID"]==null)return null;
        return Convert.ToInt64(grid.CurrentRow.Cells["ID"].Value);
    }

    private Form Editor(string title,string[] labels)
    {
        var f=new Form{Text=title,StartPosition=FormStartPosition.CenterParent,Width=620,Height=145+labels.Length*62,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,BackColor=Color.White,Tag=new List<TextBox>()};
        var list=(List<TextBox>)f.Tag;
        for(int i=0;i<labels.Length;i++){
            f.Controls.Add(new Label{Text=labels[i],Left=25,Top=25+i*55,Width=160,Height=25});
            var tb=new TextBox{Left=195,Top=22+i*55,Width=370,Height=28};list.Add(tb);f.Controls.Add(tb);
        }
        var save=ActionButton("SALVAR",()=>{f.DialogResult=DialogResult.OK;f.Close();});save.Left=245;save.Top=40+labels.Length*55;f.Controls.Add(save);
        var cancel=ActionButton("CANCELAR",()=>f.Close());cancel.Left=415;cancel.Top=40+labels.Length*55;f.Controls.Add(cancel);
        ApplyFloatingTheme(f);return f;
    }

    private static string[] EditorValues(Form f)=>((List<TextBox>)f.Tag!).Select(x=>x.Text.Trim()).ToArray();
    private static void FillEditor(Form f,params object[] values){var t=(List<TextBox>)f.Tag!;for(int i=0;i<Math.Min(t.Count,values.Length);i++)t[i].Text=Convert.ToString(values[i],CultureInfo.InvariantCulture)??"";}

    private static double Num(string s)
    {
        s=s.Trim().Replace("R$","").Replace(" ","");
        if(double.TryParse(s,NumberStyles.Any,CultureInfo.GetCultureInfo("pt-BR"),out var br))return br;
        if(double.TryParse(s.Replace(",","."),NumberStyles.Any,CultureInfo.InvariantCulture,out var inv))return inv;
        return 0;
    }
    private static string Money(double n)=>n.ToString("C2",CultureInfo.GetCultureInfo("pt-BR"));
    private static bool Confirm(string text)=>MessageBox.Show(text,"Confirmar",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;
    private static void Info(string text)=>MessageBox.Show(text,"LEAL INFO PDV",MessageBoxButtons.OK,MessageBoxIcon.Information);

    private static void Exec(string sql,params (string name,object value)[] pars)
    {
        using var cn=Database.Open();using var cmd=cn.CreateCommand();cmd.CommandText=sql;
        foreach(var p in pars)cmd.Parameters.AddWithValue(p.name,p.value??DBNull.Value);cmd.ExecuteNonQuery();
    }

    private static long ScalarLong(SqliteConnection cn,string sql){using var c=cn.CreateCommand();c.CommandText=sql;return Convert.ToInt64(c.ExecuteScalar()??0);}
    private static double ScalarDouble(SqliteConnection cn,string sql){using var c=cn.CreateCommand();c.CommandText=sql;return Convert.ToDouble(c.ExecuteScalar()??0);}

    private static string? PromptChoice(string title,string[] values)
    {
        using var f=new Form{Text=title,Width=420,Height=220,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(224,239,248),Font=new Font("Segoe UI",10)};
        var cb=new ComboBox{Left=35,Top=45,Width=330,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Color.White,ForeColor=Color.FromArgb(8,38,68),Font=new Font("Segoe UI",11,FontStyle.Bold)};cb.Items.AddRange(values);cb.SelectedIndex=0;
        var ok=new Button{Text="CONFIRMAR",Left=205,Top=100,Width=160,Height=40,DialogResult=DialogResult.OK,BackColor=Color.FromArgb(0,145,210),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10,FontStyle.Bold)};ok.FlatAppearance.BorderSize=0;
        f.Controls.Add(cb);f.Controls.Add(ok);f.AcceptButton=ok;
        return f.ShowDialog()==DialogResult.OK?cb.SelectedItem?.ToString():null;
    }
}
