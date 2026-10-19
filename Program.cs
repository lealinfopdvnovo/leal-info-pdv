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
        try { TransactionalUpdate.BeforeStartup(); }
        catch(Exception ex) { MessageBox.Show(ex.Message,"Recuperação da atualização",MessageBoxButtons.OK,MessageBoxIcon.Warning); return; }
        // V10.198: WebView2 precisa de uma pasta gravavel pelo usuario.
        // Evita E_ACCESSDENIED quando o PDV esta instalado em Program Files.
        var webViewUserData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LEAL INFO PDV",
            "WebView2");
        Directory.CreateDirectory(webViewUserData);
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewUserData);

        ApplicationConfiguration.Initialize();
        PremiumGlobalTheme.Install();
        InternalWorkspace.Install();
        Application.AddMessageFilter(new GlobalEscapeCloseFilter());

        try
        {
            Network.NetworkConfiguration.Load();
            IDisposable? processOwnership = null;
            using var ownershipLifetime = new OwnershipLifetime(() => processOwnership?.Dispose());
            // Dedicated maintenance process: wait for the operational process to leave before changing any data.
            var argv = Environment.GetCommandLineArgs();
            if (argv.Length == 4 && argv[1] == "--restaurar-automatico")
            {
                var staged = Path.GetFullPath(argv[2]);
                var app = Path.GetFullPath(Database.AppFolder);
                if (!staged.StartsWith(app + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    !Path.GetFileName(staged).StartsWith("restore_pending_", StringComparison.OrdinalIgnoreCase) ||
                    !staged.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Solicitação de restauração inválida.");
                if (!int.TryParse(argv[3], out var previousPid) || previousPid <= 0 || previousPid == Environment.ProcessId)
                    throw new InvalidOperationException("Processo anterior inválido.");
                try
                {
                    using var previous = System.Diagnostics.Process.GetProcessById(previousPid);
                    if (!previous.WaitForExit(60000))
                        throw new InvalidOperationException("O PDV não fechou. Nenhum dado foi alterado.");
                }
                catch (ArgumentException) { /* Process has already exited. */ }
                var reservation = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, Network.NetworkConfiguration.Current.Port);
                using var portLifetime = new OwnershipLifetime(() => reservation.Stop());
                reservation.Start(); // Refuse if another server is still listening.
                processOwnership = new RestoreEngine(Database.AppFolder).AcquireOwnership();
                DatabaseBackupService.RestoreAsync(staged).GetAwaiter().GetResult();
                File.Delete(staged);
                processOwnership.Dispose(); processOwnership = null;
                reservation.Stop();
                var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "LealInfoPDV.exe");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
                {
                    WorkingDirectory = AppContext.BaseDirectory,
                    UseShellExecute = true
                });
                return;
            }
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
            processOwnership = new RestoreEngine(Database.AppFolder).AcquireOwnership();
            if (new RestoreEngine(Database.AppFolder).Recover())
                MessageBox.Show("Uma restauração foi interrompida. O banco anterior foi recuperado e verificado. Tente a restauração novamente quando desejar.", "Recuperação do PDV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            var recoveredNotice = Path.Combine(Database.AppFolder, "restore-recovered.notice");
            if (File.Exists(recoveredNotice))
            {
                MessageBox.Show("Uma restauração foi interrompida. A proteção recuperou e verificou o banco anterior automaticamente.", "Recuperação do PDV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                File.Delete(recoveredNotice);
            }
            if (Network.NetworkConfiguration.Current.Mode != "terminal" && !Licensing.InstallationLicense.EnsureActivated()) return;
            if (Network.NetworkConfiguration.Current.Mode == "server" && !Licensing.InstallationLicense.HasNetwork)
                throw new InvalidOperationException("Ative a licença do servidor antes de abrir o PDV em rede.");
            // O servidor permanece ativo enquanto este PDV estiver aberto.
            using var networkServer = Network.NetworkConfiguration.Current.Mode == "server"
                ? new Network.NetworkDatabaseServer(Network.NetworkConfiguration.Current) : null;
            networkServer?.License.CheckAccess();
            Database.Initialize();
            if (!Environment.GetCommandLineArgs().Contains("--manutencao")) networkServer?.Start();
            UpdateProtectionReceipt.ReportReady();

            TransactionalUpdate.ReportReady();

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
                using var angraSync = Network.NetworkConfiguration.Current.Mode=="terminal" ? null : AngraSyncService.TryStart();
                using var angraAlert = angraSync==null ? null : new AngraProjectAlert(main,angraSync);
                main.Shown += (_,_) => { if(!main.IsDisposed && angraSync!=null) _=angraSync.Connect(); };
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
    private sealed class OwnershipLifetime(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

}
