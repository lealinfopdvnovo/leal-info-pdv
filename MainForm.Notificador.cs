using System.Runtime.InteropServices;
namespace LealInfoPDV;
public sealed partial class MainForm
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string text);
    private static readonly uint UpdateNotifierMessage = RegisterWindowMessage("LealInfoPDV.OpenUpdateCenter.v1");
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ = UpdateNotifierHost.EnsureStartedAsync();
    }
    protected override void WndProc(ref Message message)
    {
        if ((uint)message.Msg == UpdateNotifierMessage && Visible && !IsDisposed)
        {
            // A sale or other modal operation is never closed, focused, or interrupted by the tray agent.
            if (Application.OpenForms.Cast<Form>().Any(form => form != this && form.Visible))
                _ = UpdateManager.CheckForUpdatesAsync(this, true);
            else _ = UpdateManager.ShowUpdateCenterAsync(this);
            return;
        }
        base.WndProc(ref message);
    }
}
