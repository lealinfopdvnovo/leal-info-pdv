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
            Database.Initialize();

            var update = global::UpdateService.CheckAsync().GetAwaiter().GetResult();

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
                    using var entry = new SplashForm();
                    loginResult = entry.ShowDialog();
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
