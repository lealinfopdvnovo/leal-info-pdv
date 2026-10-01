using System.Net;
using System.Text.Json;

namespace LealInfoPDV.Network;
public sealed class NetworkSettingsForm : Form
{
    public NetworkSettingsForm()
    {
        Text = "Rede e licença — LEAL INFO PDV"; StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(700, 550); MinimumSize = new Size(650, 500);
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(224, 239, 248);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        Controls.Add(panel);
        Label Label(string text, int height = 54) => new() { Text = text, Width = 640, Height = height, ForeColor = Color.FromArgb(4,70,112) };
        Button Button(string text, Action action)
        {
            var button = new Button { Text = text, Width = 620, Height = 44, BackColor = Color.FromArgb(4,70,112), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            button.Click += (_,_) => { try { action(); } catch(Exception ex) { MessageBox.Show(this, ex.Message, "Rede e licença", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }; return button;
        }
        var current = NetworkConfiguration.Current;
        panel.Controls.Add(Label("1 servidor + 1 terminal incluído. Cada computador adicional exige um ponto contratado.\nO servidor também conta como um computador.", 60));
        panel.Controls.Add(Label("Modalidades disponíveis: pagamento único ou mensalidade. A modalidade e os pontos são liberados pelo vendedor através da licença.", 54));
        panel.Controls.Add(Label("Serial deste computador: " + Database.DeviceSerial(), 30));
        var status = Label("", 74); panel.Controls.Add(status);
        var licence = NetworkDatabaseServer.Current?.License ?? (current.Mode == "server" ? new NetworkLicense(Path.Combine(Database.AppFolder, "network.license"), Database.DeviceSerial()) : null);
        void RefreshStatus()
        {
            if (licence == null) status.Text = current.Mode == "terminal" ? "TERMINAL • Servidor: " + current.Host + "\nA licença é validada no servidor a cada conexão." : "MODO LOCAL • Rede ainda não configurada.";
            else { var t = licence.Terms; status.Text = $"SERVIDOR • {licence.RegisteredCount}/{t.ComputerLimit} computadores cadastrados\nModalidade: {(t.BillingMode == "unico" ? "Pagamento único" : t.BillingMode == "mensal" ? "Mensalidade" : "A definir pelo vendedor")}" + (t.ExpiresUtc == null ? "" : " • Válida até: " + t.ExpiresUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")); }
        }
        RefreshStatus();
        if (current.Mode == "local")
        {
            var port = new NumericUpDown { Minimum = 1024, Maximum = 65535, Value = 47821, Width = 180 };
            panel.Controls.Add(Label("Porta do servidor (padrão: 47821)", 26)); panel.Controls.Add(port);
            panel.Controls.Add(Button("USAR ESTE COMPUTADOR COMO SERVIDOR", () =>
            {
                NetworkConfiguration.Save(NetworkConfiguration.NewServer((int)port.Value));
                MessageBox.Show(this, "Servidor configurado. Feche e abra o PDV para ativar a rede. Depois exporte o arquivo de conexão para o terminal.\n\nNo Firewall do Windows, permita o PDV somente na rede privada."); Close();
            }));
        }
        if (current.Mode == "server")
        {
            var host = new TextBox { Text = Environment.MachineName, Width = 620 };
            panel.Controls.Add(Label("Nome ou IP fixo do servidor na rede local", 26)); panel.Controls.Add(host);
            panel.Controls.Add(Button("EXPORTAR CONEXÃO PARA O TERMINAL", () =>
            {
                if (NetworkDatabaseServer.Current == null) throw new InvalidOperationException("Abra normalmente o PDV servidor para exportar a conexão.");
                if (string.IsNullOrWhiteSpace(host.Text) || Uri.CheckHostName(host.Text.Trim()) == UriHostNameType.Unknown) throw new InvalidOperationException("Informe um nome ou IP válido.");
                using var dialog = new SaveFileDialog { FileName = "Conexao_LEAL_INFO_PDV.lealrede", Filter = "Conexão do PDV|*.lealrede" };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var connection = new NetworkConfiguration { Mode = "terminal", Host = host.Text.Trim(), Port = current.Port,
                    PairingSecret = current.PairingSecret, CertificateHash = NetworkDatabaseServer.Current.CertificateHash };
                File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(connection));
                MessageBox.Show(this, "Conexão exportada. Importe este arquivo no computador terminal. Guarde o arquivo com o administrador.");
            }));
            panel.Controls.Add(Button("IMPORTAR LICENÇA DO VENDEDOR / RENOVAÇÃO", () =>
            {
                using var dialog = new OpenFileDialog { Filter = "Licença do PDV|*.leallicenca" };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                licence!.Import(File.ReadAllText(dialog.FileName)); RefreshStatus();
                MessageBox.Show(this, "Licença validada. A quantidade de computadores e a modalidade foram atualizadas.");
            }));
        }
        if (current.Mode is "local" or "terminal")
            panel.Controls.Add(Button("IMPORTAR CONEXÃO E USAR COMO TERMINAL", () =>
            {
                using var dialog = new OpenFileDialog { Filter = "Conexão do PDV|*.lealrede" };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var connection = JsonSerializer.Deserialize<NetworkConfiguration>(File.ReadAllText(dialog.FileName)) ?? throw new InvalidDataException("Arquivo de conexão inválido.");
                if (connection.Mode != "terminal" || string.IsNullOrWhiteSpace(connection.Host) || connection.Port is < 1024 or > 65535 ||
                    connection.CertificateHash.Length != 64 || Convert.FromBase64String(connection.PairingSecret).Length != 32)
                    throw new InvalidDataException("Arquivo de conexão inválido.");
                NetworkConfiguration.Save(connection);
                MessageBox.Show(this, "Terminal configurado. Feche e abra o PDV. A licença será validada no servidor antes de permitir o acesso. Os dados locais anteriores permanecem preservados."); Close();
            }));
        panel.Controls.Add(Label("O servidor deve permanecer ligado e com o PDV aberto. Se houver perda da rede, o terminal avisa e interrompe as operações até recuperar a conexão.", 56));
    }
}
