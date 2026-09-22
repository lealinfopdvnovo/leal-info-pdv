namespace LealInfoPDV;

/// <summary>Padroniza ESC em todas as telas do PDV sem precisar repetir eventos em cada formulário.</summary>
internal sealed class GlobalEscapeCloseFilter : IMessageFilter
{
    private const int WmKeyDown = 0x0100;

    public bool PreFilterMessage(ref Message message)
    {
        if (message.Msg != WmKeyDown || (Keys)(int)message.WParam != Keys.Escape)
            return false;

        var focused = Control.FromHandle(message.HWnd);
        if (FindParent<ComboBox>(focused) is { DroppedDown: true } combo)
        {
            combo.DroppedDown = false;
            return true;
        }

        var active = Application.OpenForms.Cast<Form>()
            .LastOrDefault(form => form.Visible && form.ContainsFocus);
        if (active == null || active.IsDisposed || !active.Enabled)
            return false;

        // Durante o vídeo inicial, o ESC mantém o comportamento existente: pula para o login.
        if (active is SplashForm splash && !splash.IntroFinished)
            return false;

        // Uma atualização em instalação não pode ser interrompida no meio do download.
        if (active.Text == "Atualizando LEAL INFO PDV")
            return true;

        active.BeginInvoke(new Action(() =>
        {
            if (!active.IsDisposed)
            {
                if (active.Modal) active.DialogResult = DialogResult.Cancel;
                active.Close();
            }
        }));
        return true;
    }

    private static T? FindParent<T>(Control? control) where T : Control
    {
        while (control != null)
        {
            if (control is T match) return match;
            control = control.Parent;
        }
        return null;
    }
}
