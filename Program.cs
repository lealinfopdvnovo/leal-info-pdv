using System;
using System.Windows.Forms;

namespace LealInfoPDV;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            Database.Initialize();

            using var entry = new SplashForm();

            if (entry.ShowDialog() != DialogResult.OK)
                return;

            var update = global::UpdateService
                .CheckAsync()
                .GetAwaiter()
                .GetResult();

            if (update != null)
            {
                var resposta = MessageBox.Show(
                    update.Message + "\n\nDeseja baixar e instalar agora?",
                    "Atualização disponível",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (resposta == DialogResult.Yes)
                {
                    string instalador = global::UpdateService
                        .DownloadAsync(update)
                        .GetAwaiter()
                        .GetResult();

                    global::UpdateService.Install(instalador);
                    return;
                }
            }

            var main = new MainForm();
            main.Text = $"LEAL INFO CONECTADO - SISTEMA PDV - V{UpdateManager.CurrentVersion}";
            Application.Run(main);
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
