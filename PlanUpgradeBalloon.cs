using System.Runtime.CompilerServices;

namespace LealInfoPDV;

/// <summary>
/// Balão comercial discreto exibido uma única vez por sessão, alguns segundos após o MainForm abrir.
/// Não altera o fluxo de login nem a abertura oficial da LIA.
/// </summary>
public sealed class PlanUpgradeBalloon : Form
{
    public PlanUpgradeBalloon()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Width = 410;
        Height = 185;
        BackColor = Color.FromArgb(3, 18, 36);
        Font = new Font("Segoe UI", 10);

        string titulo;
        string texto;

        if (LicenseManager.IsStandard)
        {
            titulo = "CONHEÇA O PLANO PLUS";
            texto = "Seu LEAL INFO PDV pode ir além. Conheça o próximo plano e libere mais recursos para o dia a dia.";
        }
        else if (LicenseManager.IsPlus)
        {
            titulo = "CONHEÇA O PLANO PRO";
            texto = "Tenha a experiência completa do LEAL INFO PDV com os recursos avançados da LIA e do plano PRO.";
        }
        else
        {
            titulo = "LIA PRO • EXTENSÃO";
            texto = "Você já está no plano PRO. Quando precisar, consulte as opções de extensão dos recursos da LIA.";
        }

        var fechar = new Button
        {
            Text = "×",
            Width = 38,
            Height = 34,
            Left = 362,
            Top = 6,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = BackColor,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            TabStop = false,
            Cursor = Cursors.Hand
        };
        fechar.FlatAppearance.BorderSize = 0;
        fechar.Click += (_, _) => Close();
        Controls.Add(fechar);

        Controls.Add(new Label
        {
            Text = titulo,
            Left = 22,
            Top = 25,
            Width = 330,
            Height = 30,
            ForeColor = Color.FromArgb(124, 238, 255),
            Font = new Font("Segoe UI", 13, FontStyle.Bold)
        });

        Controls.Add(new Label
        {
            Text = texto,
            Left = 22,
            Top = 67,
            Width = 360,
            Height = 72,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10)
        });

        Controls.Add(new Label
        {
            Text = "LEAL INFO CONECTADO",
            Left = 22,
            Top = 146,
            Width = 230,
            Height = 22,
            ForeColor = Color.FromArgb(110, 180, 215),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        });

        Shown += (_, _) => Posicionar();
    }

    private void Posicionar()
    {
        var area = Screen.FromControl(this).WorkingArea;
        Location = new Point(area.Right - Width - 24, area.Bottom - Height - 24);
    }
}

internal static class PlanUpgradeBalloonBootstrap
{
    private static bool exibido;

    [ModuleInitializer]
    internal static void Init()
    {
        Application.Idle += OnApplicationIdle;
    }

    private static void OnApplicationIdle(object? sender, EventArgs e)
    {
        if (exibido) return;

        var main = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
        if (main == null || main.IsDisposed || !main.Visible) return;

        exibido = true;
        Application.Idle -= OnApplicationIdle;

        var timer = new System.Windows.Forms.Timer { Interval = 8000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();

            if (main.IsDisposed || !main.Visible || !LicenseManager.IsValid) return;

            var balao = new PlanUpgradeBalloon();
            balao.Show(main);
        };
        timer.Start();
    }
}
