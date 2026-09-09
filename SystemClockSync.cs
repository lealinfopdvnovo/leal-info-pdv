namespace LealInfoPDV;

internal sealed class SystemClockSync : IDisposable
{
    private readonly Form main;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };

    public SystemClockSync(Form mainForm)
    {
        main = mainForm;
        timer.Tick += (_, _) => Atualizar();
        main.Shown += (_, _) => Atualizar();
        main.FormClosed += (_, _) => Dispose();
        timer.Start();
    }

    private void Atualizar()
    {
        if (main.IsDisposed) return;

        var agora = DateTime.Now;
        AtualizarControles(main, agora);
    }

    private static void AtualizarControles(Control raiz, DateTime agora)
    {
        foreach (Control control in raiz.Controls)
        {
            if (control is StatusStrip strip)
            {
                foreach (ToolStripItem item in strip.Items)
                {
                    if (item is ToolStripStatusLabel label && label.Text.StartsWith("Data:", StringComparison.OrdinalIgnoreCase))
                        label.Text = $"Data: {agora:dd/MM/yyyy} • Hora: {agora:HH:mm:ss}";
                }
            }

            if (control is Label lbl && lbl.Text.StartsWith("TECNOLOGIA QUE CONECTA  •  Atendente:", StringComparison.Ordinal))
            {
                var partes = lbl.Text.Split("  •  ", StringSplitOptions.None);
                var atendente = partes.Length >= 2 ? partes[1] : "Atendente: ADMIN";
                lbl.Text = $"TECNOLOGIA QUE CONECTA  •  {atendente}  •  {agora:dd/MM/yyyy HH:mm:ss}";
            }

            if (control.HasChildren)
                AtualizarControles(control, agora);
        }
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();
    }
}
