using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LealInfoPDV;

internal static class PlanVisualPolicyBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += ApplyWhenReady;
    }

    private static void ApplyWhenReady(object? sender, EventArgs e)
    {
        var main = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
        if (main is null || main.IsDisposed || !main.Visible) return;

        Application.Idle -= ApplyWhenReady;
        Apply(main);
        main.Resize += (_, _) => Apply(main);
    }

    private static void Apply(MainForm main)
    {
        try
        {
            var all = Descendants(main).ToList();
            var liaButton = all.OfType<LiaOrbLauncher>().FirstOrDefault();
            var monitorLabel = all.OfType<Label>().FirstOrDefault(l =>
                string.Equals(l.Text?.Trim(), "MONITOR DE ESTOQUE", StringComparison.OrdinalIgnoreCase));
            var monitor = monitorLabel?.Parent as Panel;

            if (LicenseManager.IsStandard)
            {
                if (liaButton is not null)
                    liaButton.Visible = false;

                if (monitor is not null)
                {
                    monitor.Visible = true;
                    monitor.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
                    var host = monitor.Parent;
                    if (host is not null)
                    {
                        monitor.Left = Math.Max(10, host.ClientSize.Width - monitor.Width - 18);
                        monitor.Top = Math.Max(10, host.ClientSize.Height - monitor.Height - 18);
                        monitor.BringToFront();
                    }
                }
            }
            else
            {
                if (monitor is not null)
                    monitor.Visible = false;

                if (liaButton is not null)
                    liaButton.Visible = true;
            }
        }
        catch
        {
            // Política visual nunca deve impedir a abertura do PDV.
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }
}
