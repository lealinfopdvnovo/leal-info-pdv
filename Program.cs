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
