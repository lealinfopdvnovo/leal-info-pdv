using System.Drawing;
using System.Windows.Forms;

namespace LealInfoPDV;

// Shared by initial registration and Central de Configurações. Uses the existing delivery service.
internal static class CompanyCepLookup
{
    internal sealed record Binding(Control Panel, Func<Task> CompleteAsync);

    internal static Binding Create(Form owner, TextBox cep, TextBox address, TextBox cityState,
        Func<string, CancellationToken, Task<DeliveryAddress.CepAddress>>? lookup = null)
    {
        lookup ??= DeliveryAddress.LookupAsync;
        cep.Name = "company_cep";
        cep.MaxLength = 9;
        cep.Margin = Padding.Empty;
        var panel = new TableLayoutPanel { Name = "company_cep_panel", Dock = DockStyle.Fill,
            ColumnCount = 2, RowCount = 2, Margin = new Padding(4, 3, 4, 3) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 19));
        var search = new Button { Name = "company_cep_search", Text = "BUSCAR", Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0), FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 163, 224), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        var status = new Label { Name = "company_cep_status", Text = "Digite o CEP (8 dígitos).", Dock = DockStyle.Fill,
            AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8), Margin = Padding.Empty };
        panel.Controls.Add(cep, 0, 0); panel.Controls.Add(search, 1, 0);
        panel.Controls.Add(status, 0, 1); panel.SetColumnSpan(status, 2);
        var debounce = new System.Windows.Forms.Timer { Interval = 550 };
        CancellationTokenSource? pending = null;
        int revision = 0;
        string completed = DeliveryAddress.NormalizeCep(cep.Text);
        bool formatting = false, disposed = false;
        Task active = Task.CompletedTask;
        void CancelPending()
        {
            revision++;
            pending?.Cancel();
            pending = null;
        }
        async Task Find(bool explicitRequest)
        {
            debounce.Stop();
            var digits = DeliveryAddress.NormalizeCep(cep.Text);
            if (digits.Length != 8)
            {
                status.Text = "CEP inválido. Use 8 dígitos ou preencha manualmente.";
                return;
            }
            if (!explicitRequest && (digits == completed || pending != null)) return;
            CancelPending();
            var request = new CancellationTokenSource(); pending = request;
            int mine = revision;
            string beforeAddress = address.Text, beforeCity = cityState.Text;
            status.Text = "Consultando CEP...";
            try
            {
                var result = await lookup(digits, request.Token);
                if (disposed || owner.IsDisposed || mine != revision || DeliveryAddress.NormalizeCep(cep.Text) != digits) return;
                // Never replace manual changes made while the request was in flight.
                if (address.Text != beforeAddress || cityState.Text != beforeCity)
                {
                    status.Text = "Edição manual mantida. BUSCAR para consultar novamente.";
                    completed = digits;
                    return;
                }
                address.Text = string.Join(", ", new[] { result.Street, result.District }.Where(x => !string.IsNullOrWhiteSpace(x)));
                cityState.Text = result.City + " / " + result.Uf.ToUpperInvariant();
                formatting = true;
                cep.Text = digits[..5] + "-" + digits[5..]; cep.SelectionStart = cep.Text.Length;
                formatting = false;
                completed = digits;
                status.Text = "CEP encontrado. Complete número/complemento no endereço.";
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (!disposed && !owner.IsDisposed && mine == revision)
                    status.Text = "CEP não encontrado ou indisponível. Preencha manualmente.";
            }
            finally
            {
                if (pending == request) pending = null;
                request.Dispose();
            }
        }
        Task BeginFind(bool explicitRequest)
        {
            if (pending != null && !explicitRequest) return active;
            return active = Find(explicitRequest);
        }
        async Task CompleteAsync()
        {
            string target;
            do
            {
                target = DeliveryAddress.NormalizeCep(cep.Text);
                await BeginFind(false);
            } while (!disposed && !owner.IsDisposed && target != DeliveryAddress.NormalizeCep(cep.Text));
        }
        cep.TextChanged += (_, _) =>
        {
            if (formatting || disposed) return;
            debounce.Stop(); CancelPending();
            status.Text = "Digite o CEP (8 dígitos).";
            if (DeliveryAddress.NormalizeCep(cep.Text).Length == 8) debounce.Start();
        };
        debounce.Tick += async (_, _) => await BeginFind(false);
        cep.Leave += async (_, _) => { if (!disposed && DeliveryAddress.NormalizeCep(cep.Text).Length == 8) await BeginFind(false); };
        search.Click += async (_, _) => await BeginFind(true);
        panel.Disposed += (_, _) => { disposed = true; debounce.Stop(); debounce.Dispose(); CancelPending(); };
        return new Binding(panel, CompleteAsync);
    }
}
