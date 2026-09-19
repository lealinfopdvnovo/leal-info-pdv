using System.Drawing.Drawing2D;

namespace LealInfoPDV;

public sealed class PixPaymentForm : Form
{
    private readonly AsaasPixService service;
    private readonly decimal value;
    private readonly string customerId;
    private readonly CancellationTokenSource cts = new();
    private readonly PictureBox qr = new() { Size = new Size(300, 300), SizeMode = PictureBoxSizeMode.Zoom };
    private readonly Label status = new() { AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.White };
    private readonly Button copy = new() { Text = "COPIAR PIX", Width = 140, Height = 42, Enabled = false };
    private readonly Button cancel = new() { Text = "CANCELAR", Width = 140, Height = 42 };
    private string payload = "";

    public PixPaymentForm(decimal value, string customerId, AsaasPixService? service = null)
    {
        this.value = value; this.customerId = customerId; this.service = service ?? new AsaasPixService();
        Text = "LEAL INFO PDV • PAGAMENTO PIX";
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(430, 520); BackColor = Color.FromArgb(8, 20, 35); TopMost = true;
        var title = new Label { Text = $"PIX  •  {value:C2}", ForeColor = Color.White, Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true, Location = new Point(120, 24) };
        qr.Location = new Point(65, 82); status.Location = new Point(105, 400);
        copy.Location = new Point(65, 452); cancel.Location = new Point(225, 452);
        Controls.AddRange(new Control[] { title, qr, status, copy, cancel });
        copy.Click += (_, _) => { if (payload.Length > 0) Clipboard.SetText(payload); status.Text = "PIX copiado."; };
        cancel.Click += (_, _) => { cts.Cancel(); DialogResult = DialogResult.Cancel; Close(); };
        Shown += async (_, _) => await StartAsync();
        FormClosed += (_, _) => cts.Dispose();
        Resize += (_, _) => ApplyRoundRegion();
        ApplyRoundRegion();
    }

    private void ApplyRoundRegion()
    {
        using var path = new GraphicsPath();
        const int r = 28; var rect = ClientRectangle; rect.Width--; rect.Height--;
        path.AddArc(rect.Left, rect.Top, r, r, 180, 90); path.AddArc(rect.Right-r, rect.Top, r, r, 270, 90);
        path.AddArc(rect.Right-r, rect.Bottom-r, r, r, 0, 90); path.AddArc(rect.Left, rect.Bottom-r, r, r, 90, 90); path.CloseFigure();
        Region = new Region(path);
    }

    private async Task StartAsync()
    {
        try
        {
            status.Text = "Gerando QR Code...";
            var charge = await service.GerarCobrancaPixAsync(value, customerId, cts.Token);
            payload = charge.PayloadQrCode;
            qr.Image = DecodeQrImage(charge.EncodedImage);
            copy.Enabled = true; status.Text = "Aguardando pagamento...";
            if (await service.VerificarStatusPagamentoAsync(charge.IdTransacao, cts.Token))
            {
                status.Text = "PAGAMENTO CONFIRMADO";
                DialogResult = DialogResult.OK; await Task.Delay(500); Close();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "PIX - LEAL INFO PDV", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Abort; Close();
        }
    }

    private static Image DecodeQrImage(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidOperationException("A API não retornou a imagem do QR Code.");
        var comma = encoded.IndexOf(','); if (comma >= 0) encoded = encoded[(comma + 1)..];
        var bytes = Convert.FromBase64String(encoded);
        using var ms = new MemoryStream(bytes); using var source = Image.FromStream(ms);
        return new Bitmap(source);
    }
}
