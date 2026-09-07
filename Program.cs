using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LealInfoPDV;

internal static class Program
{
    [STAThread]
    static async Task Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            Database.Initialize();

            using var entry = new SplashForm();

            if (entry.ShowDialog() != DialogResult.OK)
                return;

            var update = await global::UpdateService.CheckAsync();

            if (update != null)
            {
                var resposta = MessageBox.Show(
                    update.Message + "\n\nDeseja baixar e instalar agora?",
                    "Atualização disponível",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (resposta == DialogResult.Yes)
                {
                    string instalador =
                        await global::UpdateService.DownloadAsync(update);

                    global::UpdateService.Install(instalador);
                    return;
                }
            }

            Application.Run(new MainForm());
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
