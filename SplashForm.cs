namespace LealInfoPDV;

public sealed class SplashForm : Form
{
    private LoginForm? login;

    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(2,10,22);
        ShowInTaskbar = false;
        KeyPreview = true;
        Shown += (_, _) => LoadLogin();
    }

    private void LoadLogin()
    {
        if (login is not null) return;

        login = new LoginForm
        {
            EmbeddedMode = true,
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            WindowState = FormWindowState.Normal,
            Dock = DockStyle.Fill,
            TopMost = false
        };

        login.FormClosed += (_, _) =>
        {
            DialogResult = login.DialogResult == DialogResult.OK
                ? DialogResult.OK
                : DialogResult.Cancel;
            Close();
        };

        Controls.Add(login);
        login.Show();
        ApplyCurrentVersionToLogin(login);
        login.BringToFront();
    }

    private static void ApplyCurrentVersionToLogin(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Label label &&
                label.Text.Contains("ACESSO SEGURO", StringComparison.OrdinalIgnoreCase))
            {
                label.Text = $"LEAL INFO CONECTADO  •  ACESSO SEGURO  •  V{UpdateManager.CurrentVersion}";
            }

            if (control.HasChildren)
                ApplyCurrentVersionToLogin(control);
        }
    }
}
