namespace LealInfoPDV;

public sealed partial class MainForm
{
    private void OpenConsultaCentral()
    {
        using var form = new ConsultaCentralForm((permission, action) => RunAllowed(permission, action),
            OpenProducts, OpenCustomers, OpenHistory, OpenOrders, OpenDeliveries, OpenQuotes);
        form.ShowDialog(this);
    }
}
