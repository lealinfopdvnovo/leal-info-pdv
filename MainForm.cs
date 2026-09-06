using Microsoft.Data.Sqlite;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System.Net.Http;
using System.Text.Json;

namespace LealInfoPDV;

public sealed class MainForm : Form
{
    private PictureBox? mainScreenPicture;

    private readonly Color Blue = Color.FromArgb(10, 104, 157);
    private readonly Color DarkBlue = Color.FromArgb(4, 70, 112);
    private readonly StatusStrip status = new();
    private readonly Label lowStockLabel = new();

    public MainForm()
    {
        Text = "LEAL INFO CONECTADO - SISTEMA PDV - V10.130";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1200, 720);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10);
        BuildUi();
        RefreshDashboard();

        Shown += (_, _) =>
        {
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
            _ = UpdateManager.CheckForUpdatesAsync(this, true);
        };
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
        foreach (var title in new[] { "Cadastro", "Consulta", "Movimentação", "Financeiro", "Tela de Vendas", "Utilitários", "Relatórios", "LIA", "Ajuda", "Sair" })
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
                AddMenu("Produtos", OpenProducts);
                AddMenu("Clientes", OpenCustomers);
                AddMenu("Fornecedores", OpenSuppliers);
                AddMenu("Serviços", OpenServices);
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
                AddMenu("Fluxo de Caixa", () => { if (Auth.IsManager) OpenFinance(); else MessageBox.Show("Seu nível de acesso não permite abrir o Financeiro."); });
            }
            else if (title == "Tela de Vendas")
            {
                AddMenu("Abrir Tela de Vendas", OpenSales);
            }
            else if (title == "LIA")
            {
                AddMenu("Abrir LIA • LEAL AI", () => new LiaForm().Show(this));
            }
            else if (title == "Utilitários")
            {
                AddMenu("Alterar tela principal...", () => { if(mainScreenPicture != null) ChangeMainScreenImage(mainScreenPicture); });
                AddMenu("Dados da empresa...", () => ShowCompanyRegistration(false));
                AddMenu("Backup", Backup);
                AddMenu("Configurações", OpenSettings);
                AddMenu("Usuários e acessos...", () => {
                    if (!Auth.IsAdmin) { MessageBox.Show("Somente ADMINISTRADOR pode gerenciar usuários.", "Acesso negado", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    OpenUsers();
                });
                AddMenu("Recuperação por e-mail...", () => { if (!Auth.IsAdmin) { MessageBox.Show("Somente ADMINISTRADOR pode configurar o envio de e-mail."); return; } OpenEmailSettings(); });
                AddMenu("Códigos de emergência...", () => {
                    if (!Auth.IsAdmin || Auth.Current == null) { MessageBox.Show("Somente ADMINISTRADOR pode gerar códigos de emergência."); return; }
                    ShowEmergencyCodes(Auth.Current.Id, false);
                });
            }
            else if (title == "Relatórios")
            {
                AddMenu("Abrir Relatórios", () => { if (Auth.IsManager) OpenReports(); else MessageBox.Show("Seu nível de acesso não permite abrir Relatórios."); });
            }
            else if (title == "Ajuda")
            {
                AddMenu("Conheça o menu Cadastro", ShowCadastroHelp);
                AddMenu("Tutorial de Primeiro Acesso", () => OpenFirstAccessTutorial(false));
                AddMenu("Atalhos do PDV", () => MessageBox.Show("F2  Finalizar venda\nF5  Código do produto\nF7  Remover item\nESC  Fechar janela", "Atalhos do LEAL INFO PDV"));
                AddMenu("Atualizações do sistema", () => UpdateManager.ShowUpdateCenter(this));
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
        AddTool(bar, "BACKUP", "backup.png", Backup);
        AddTool(bar, "CONFIGURAÇÕES", "settings.png", OpenSettings);
        AddTool(bar, "LIA\nAI", "lealinfo_app_icon.png", () => new LiaForm().Show(this));
        AddTool(bar, "SAIR", "exit.png", ConfirmExit);

        // Distribui todos os atalhos pela largura disponível.
        // Assim não existe barra de rolagem horizontal, independentemente
        // da resolução da tela.
        void ResizeShortcutBar()
        {
            if (bar.Controls.Count == 0) return;

            int usable = Math.Max(980, bar.ClientSize.Width - bar.Padding.Horizontal - 4);
            int each = Math.Max(88, usable / bar.Controls.Count);

            foreach (Control shortcut in bar.Controls)
            {
                shortcut.Width = Math.Max(86, each - shortcut.Margin.Horizontal);

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
        body.Controls.Add(monitor);
        monitor.BringToFront();
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

        card.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, card.Width - 3, card.Height - 3);
            int radius = 16;
            using var gp = new System.Drawing.Drawing2D.GraphicsPath();
            gp.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            gp.AddArc(rect.Right-radius, rect.Y, radius, radius, 270, 90);
            gp.AddArc(rect.Right-radius, rect.Bottom-radius, radius, radius, 0, 90);
            gp.AddArc(rect.X, rect.Bottom-radius, radius, radius, 90, 90);
            gp.CloseFigure();
            using var bg = new System.Drawing.Drawing2D.LinearGradientBrush(
                rect,
                hover ? Color.FromArgb(18, 125, 190) : Color.FromArgb(8, 80, 135),
                Color.FromArgb(3, 48, 88), 90f);
            e.Graphics.FillPath(bg, gp);
            using var pen = new Pen(
                hover ? Color.FromArgb(120,225,255) : Color.FromArgb(45,135,190),
                hover ? 2f : 1f);
            e.Graphics.DrawPath(pen, gp);
        };

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", iconFile);
        Image? icon = null;
        if (File.Exists(path))
        {
            using var src = Image.FromFile(path);
            icon = new Bitmap(src);
        }

        var pic = new PictureBox
        {
            Width = 42,
            Height = 42,
            Left = (cardW - 42) / 2,
            Top = 4,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Image = icon,
            Cursor = Cursors.Hand
        };

        var caption = new Label
        {
            Text = text,
            Width = cardW,
            Height = 52,
            Left = 0,
            Top = 48,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
            AutoEllipsis = false,
            Cursor = Cursors.Hand
        };

        card.Controls.Add(pic);
        card.Controls.Add(caption);

        int target = 42;
        var timer = new System.Windows.Forms.Timer { Interval = 12 };

        void SetHover(bool on)
        {
            hover = on;
            target = on ? 52 : 42;
            card.Invalidate();
            timer.Start();
        }

        timer.Tick += (_, _) =>
        {
            int diff = target - pic.Width;
            if (Math.Abs(diff) <= 1)
            {
                pic.Size = new Size(target, target);
                pic.Left = (card.Width - target) / 2;
                pic.Top = hover ? 0 : 4;
                timer.Stop();
                return;
            }
            int step = Math.Max(2, Math.Abs(diff)/3);
            int next = pic.Width + Math.Sign(diff)*step;
            pic.Size = new Size(next,next);
            pic.Left = (card.Width-next)/2;
            pic.Top = hover ? Math.Max(0,4-(next-42)/3) : 4;
            pic.BringToFront();
        };

        void Enter(object? s, EventArgs e) => SetHover(true);
        void Leave(object? s, EventArgs e)
        {
            var pt = card.PointToClient(Cursor.Position);
            if (!card.ClientRectangle.Contains(pt)) SetHover(false);
        }
        foreach (Control c in new Control[]{card,pic,caption})
        {
            c.MouseEnter += Enter;
            c.MouseLeave += Leave;
        }

        void Run(object? s, EventArgs e) => action();
        card.Click += Run;
        pic.Click += Run;
        caption.Click += Run;

        card.Disposed += (_, _) =>
        {
            timer.Dispose();
            pic.Image?.Dispose();
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
        bar.Controls.Add(add);bar.Controls.Add(reset);bar.Controls.Add(toggle);
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
            role.Items.AddRange(new[]{"ADMINISTRADOR","GERENTE","OPERADOR"});role.SelectedIndex=2;
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
        MessageBox.Show($"Empresa: LEAL INFO CONECTADO\nSistema: LEAL INFO PDV\nWindows 11 64 bits\n\nSerial exclusivo:\n{Database.DeviceSerial()}\n\nBanco local:\n{Database.DbPath}",
            "Configurações",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

    private void Backup()
    {
        try { Database.Backup(); Info("Backup criado com sucesso em:\n" + Database.BackupFolder); }
        catch(Exception ex){ Info("Erro no backup:\n"+ex.Message); }
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
        var pixInfo = CenterInfo("Confirme o recebimento do PIX antes de concluir a venda.", 205, 12);
        pixInfo.Font = new Font("Segoe UI", 11);
        tabPix.Controls.Add(pixInfo);

        var pixConfirm = BigConfirm("PIX RECEBIDO • CONFIRMAR");
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
            result.Add(new PaymentPart { Method = "PIX", Amount = total });
            f.DialogResult = DialogResult.OK;
            f.Close();
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

            if (theme == "PDV Rosa")
            {
                // Fundo não uniforme: camadas de rosé/vinho + brilho perolado + trama acetinada.
                SetTexture(f, Color.FromArgb(48, 7, 34), Color.FromArgb(103, 18, 73), Color.FromArgb(255, 112, 185), false, 1301);
                SetTexture(body, Color.FromArgb(54, 8, 38), Color.FromArgb(112, 22, 79), Color.FromArgb(255, 105, 181), false, 1302);
                SetTexture(header, Color.FromArgb(91, 10, 62), Color.FromArgb(151, 24, 98), Color.FromArgb(255, 143, 198), false, 1303);
                SetTexture(photoShowcase, Color.FromArgb(77, 14, 58), Color.FromArgb(132, 29, 91), Color.FromArgb(255, 142, 199), false, 1304);
                SetTexture(left, Color.FromArgb(72, 12, 54), Color.FromArgb(126, 26, 88), Color.FromArgb(255, 128, 190), false, 1305);
                SetTexture(right, Color.FromArgb(255, 247, 251), Color.FromArgb(248, 222, 237), Color.FromArgb(226, 111, 166), true, 1306);
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
                    lbl.ForeColor = theme == "Clean Pro" ? textDark : Color.White;
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
                    itemCmd.CommandText = """
                        INSERT INTO sale_items(sale_id,product_id,description,qty,unit_price,total)
                        VALUES($sale,$product,$description,$qty,$unit,$total);
                        UPDATE products SET stock=stock-$qty WHERE id=$product;
                        """;
                    itemCmd.Parameters.AddWithValue("$sale", saleId);
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
