using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace LealInfoPDV;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // V10.198: WebView2 precisa de uma pasta gravavel pelo usuario.
        // Evita E_ACCESSDENIED quando o PDV esta instalado em Program Files.
        var webViewUserData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LEAL INFO PDV",
            "WebView2");
        Directory.CreateDirectory(webViewUserData);
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewUserData);

        ApplicationConfiguration.Initialize();
        Application.AddMessageFilter(new GlobalEscapeCloseFilter());

        try
        {
            Network.NetworkConfiguration.Load();
            if (Environment.GetCommandLineArgs().Contains("--check-lia"))
            {
                try {
                    if(Network.NetworkConfiguration.Current.Mode=="terminal") { using var remote=Database.Open(); }
                    else { Licensing.InstallationLicense.LoadLocal(); Licensing.InstallationLicense.Store!.CheckAccessReadOnly(); }
                    Environment.ExitCode=Licensing.InstallationLicense.HasLia && Licensing.InstallationLicense.IsActivated(Licensing.InstallationLicense.Current!) ? 0:2;
                }catch { Environment.ExitCode=2; }
                return;
            }
            if (Environment.GetCommandLineArgs().Contains("--rede"))
            {
                using var settings = new Network.NetworkSettingsForm();
                settings.ShowDialog();
                return;
            }
            if (Network.NetworkConfiguration.Current.Mode != "terminal" && !Licensing.InstallationLicense.EnsureActivated()) return;
            if (Network.NetworkConfiguration.Current.Mode == "server" && !Licensing.InstallationLicense.HasNetwork)
                throw new InvalidOperationException("A edição Standard usa somente este computador. Contate o vendedor para contratar Plus ou Pro.");
            // O servidor permanece ativo enquanto este PDV estiver aberto.
            using var networkServer = Network.NetworkConfiguration.Current.Mode == "server"
                ? new Network.NetworkDatabaseServer(Network.NetworkConfiguration.Current) : null;
            networkServer?.License.CheckAccess();
            Database.Initialize();
            if (!Environment.GetCommandLineArgs().Contains("--manutencao")) networkServer?.Start();

            // Nunca bloqueia a abertura do caixa aguardando Internet.
            // A verificação silenciosa continua depois que a tela principal já abriu.
            global::UpdateInfo? update = null;

            if (update != null)
            {
                var resposta = MessageBox.Show(
                    update.Message + "\n\nDeseja baixar e instalar agora?",
                    "Atualização disponível",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (resposta == DialogResult.Yes)
                {
                    using var progresso = new Form
                    {
                        Text = "Atualizando LEAL INFO PDV",
                        StartPosition = FormStartPosition.CenterScreen,
                        Width = 520,
                        Height = 210,
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        MaximizeBox = false,
                        MinimizeBox = false,
                        ControlBox = false,
                        TopMost = true,
                        BackColor = Color.FromArgb(7, 31, 52),
                        Font = new Font("Segoe UI", 10)
                    };
                    var titulo = new Label { Text = $"BAIXANDO ATUALIZAÇÃO V{update.Version}", Dock = DockStyle.Top, Height = 65, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font("Segoe UI", 16, FontStyle.Bold) };
                    var barra = new ProgressBar { Left = 45, Top = 82, Width = 410, Height = 25, Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Continuous };
                    var percentual = new Label { Text = "0%", Left = 35, Top = 116, Width = 435, Height = 30, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(74, 215, 255), Font = new Font("Segoe UI", 12, FontStyle.Bold) };
                    progresso.Controls.AddRange(new Control[] { titulo, barra, percentual });
                    progresso.Show();
                    progresso.Refresh();
                    Application.DoEvents();

                    var indicador = new Progress<int>(p =>
                    {
                        p = Math.Clamp(p, 0, 100);
                        barra.Value = p;
                        percentual.Text = p < 100 ? $"{p}%  •  BAIXANDO..." : "100%  •  ABRINDO INSTALADOR...";
                        progresso.Refresh();
                    });

                    var downloadTask = global::UpdateService.DownloadAsync(update, indicador);
                    while (!downloadTask.IsCompleted)
                    {
                        Application.DoEvents();
                        Thread.Sleep(25);
                    }

                    string? instalador = downloadTask.GetAwaiter().GetResult();
                    Application.DoEvents();
                    progresso.Close();

                    if (!string.IsNullOrWhiteSpace(instalador) && System.IO.File.Exists(instalador))
                    {
                        global::UpdateService.Install(instalador);
                        return;
                    }

                    MessageBox.Show(
                        "Não foi possível baixar a atualização. O PDV será aberto normalmente.",
                        "Atualização do LEAL INFO PDV",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }

            var firstAccess = true;
            while (true)
            {
                DialogResult loginResult;
                if (firstAccess)
                {
                    if (Licensing.InstallationLicense.Current?.Plan == "standard")
                    {
                        using(var opening = new Licensing.BrandSplashForm()) opening.ShowDialog();
                        using var standardLogin = new LoginForm();loginResult = standardLogin.ShowDialog();
                    }
                    else { using var entry = new SplashForm(); loginResult = entry.ShowDialog(); }
                    firstAccess = false;
                }
                else
                {
                    using var login = new LoginForm();
                    loginResult = login.ShowDialog();
                }

                if (loginResult != DialogResult.OK) return;

                using var main = new MainForm();
                main.Text = $"LEAL INFO CONECTADO - SISTEMA PDV - V{UpdateManager.CurrentVersion}";
                using var clockSync = new SystemClockSync(main);
                Application.Run(main);
                if (!main.LogoutRequested) return;
            }
        }
        catch (Network.NetworkAccessException ex)
        {
            MessageBox.Show(ex.Message, "Licença / conexão do PDV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "O LEAL INFO PDV encontrou um erro ao iniciar:\n\n" + ex.Message,
                "LEAL INFO PDV",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
