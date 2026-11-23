using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LealInfoPDV.Network;
using Microsoft.Win32;

namespace LealInfoUniversalSetup;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm());
    }
}

internal sealed class SetupForm : Form
{
    private readonly TextBox key = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Button install = new() { Text = "Instalar", AutoSize = true };
    private readonly Label status = new() { AutoSize = true };

    public SetupForm()
    {
        Text = "Setup Universal — LEAL INFO PDV";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(640, 295);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "Use a conta do Windows que vai operar o PDV.", AutoSize = true });
        layout.Controls.Add(new TextBox { Text = Setup.DeviceSerial(), ReadOnly = true, Dock = DockStyle.Fill });
        layout.Controls.Add(key);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var import = new Button { Text = "Importar licença", AutoSize = true };
        import.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Licença LEAL INFO|*.leallicenca" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                try { key.Text = File.ReadAllText(dialog.FileName); }
                catch (IOException) { status.Text = "Não foi possível ler o arquivo de licença."; }
            }
        };
        actions.Controls.Add(import);
        actions.Controls.Add(install);
        layout.Controls.Add(actions);
        layout.Controls.Add(status);
        install.Click += Install;
        FormClosing += (_, e) => e.Cancel = !install.Enabled;
    }

    private async void Install(object? sender, EventArgs args)
    {
        install.Enabled = false;
        key.Enabled = false;
        status.Text = "Validando a licença e buscando o pacote completo...";
        try
        {
            var executable = await Setup.InstallAsync(key.Text);
            key.Clear();
            status.Text = "Instalação concluída. Abra o PDV para restaurar o backup.";
            var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "LEAL INFO PDV.url");
            File.WriteAllText(shortcut, "[InternetShortcut]\r\nURL=" + new Uri(executable).AbsoluteUri + "\r\n");
        }
        catch (Exception error)
        {
            status.Text = error.Message;
        }
        finally
        {
            install.Enabled = true;
            key.Enabled = true;
        }
    }
}

internal static class Setup
{
    private const string Feed = "https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/clients/";
    private const long MaxZipBytes = 1024L * 1024 * 1024;
    private const long MaxExpandedBytes = 3 * MaxZipBytes;
    private static readonly HttpClient Http = new(new SocketsHttpHandler { AllowAutoRedirect = false });
    internal sealed record Offer(string InstallType, string TargetClientCode, string Version, string PackageUrl, string Sha256);

    internal static string DeviceSerial()
    {
        string source;
        try
        {
            using var registry = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            source = registry?.GetValue("MachineGuid")?.ToString() ?? Environment.MachineName;
        }
        catch { source = Environment.MachineName; }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("LEALINFO|" + source)));
        return $"LI-{hash[..4]}-{hash[4..8]}-{hash[8..12]}-{hash[12..16]}";
    }

    internal static async Task<string> InstallAsync(string signed)
    {
        signed = signed.Trim();
        if (signed.StartsWith("LEAL1-", StringComparison.Ordinal))
            signed = Encoding.UTF8.GetString(Convert.FromBase64String(signed[6..]));
        var serial = DeviceSerial();
        var license = NetworkLicense.Decode(signed, serial, SellerPublicKey.Pem);
        if (!license.Active || license.ClientCode.Length == 0 || license.ExpiresUtc <= DateTimeOffset.UtcNow)
            throw new InvalidDataException("A licença precisa estar ativa e ter o código do cliente.");

        var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LEAL INFO PDV");
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LealInfoPDV");
        if (Directory.Exists(target) || Directory.Exists(data))
            throw new IOException("Este computador já tem arquivos do PDV. Use o atualizador para uma instalação existente.");

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(25));
        using var response = await Http.GetAsync(Feed + license.ClientCode + "/install.json", timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException($"O cliente {license.ClientCode} ainda não tem pacote completo de primeira instalação publicado.");
        response.EnsureSuccessStatusCode();
        var offer = JsonSerializer.Deserialize<Offer>(await response.Content.ReadAsStringAsync(timeout.Token));
        ValidateOffer(offer, license.ClientCode);

        var working = Path.Combine(Path.GetDirectoryName(target)!, ".leal-info-setup-" + Guid.NewGuid().ToString("N"));
        var stagedData = data + ".setup-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(working);
        try
        {
            var package = Path.Combine(working, "download.zip");
            await DownloadAsync(new Uri(offer!.PackageUrl), package, timeout.Token);
            await using (var input = File.OpenRead(package))
            {
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, timeout.Token));
                if (!hash.Equals(offer.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("O pacote baixado não confere com o manifesto.");
            }

            var staging = Directory.CreateDirectory(Path.Combine(working, "program")).FullName;
            await Task.Run(() => Extract(package, staging), timeout.Token);
            var store = new NetworkLicense(Path.Combine(stagedData, "network.license"), serial);
            store.Activate(signed);
            Directory.Move(staging, target);
            try
            {
                Directory.Move(stagedData, data);
            }
            catch
            {
                Directory.Move(target, staging);
                throw;
            }
            return Path.Combine(target, "LealInfoPDV.exe");
        }
        finally
        {
            if (Directory.Exists(stagedData))
                Directory.Delete(stagedData, true);
            Directory.Delete(working, true);
        }
    }

    internal static void ValidateOffer(Offer? offer, string client)
    {
        if (offer is null || offer.InstallType != "complete" || offer.TargetClientCode != client ||
            !Version.TryParse(offer.Version, out _) || offer.Sha256 is not { Length: 64 } ||
            !offer.Sha256.All(Uri.IsHexDigit) ||
            !Uri.TryCreate(offer.PackageUrl, UriKind.Absolute, out var url) || url.Scheme != "https" ||
            url.Host != "github.com" || !url.IsDefaultPort || url.UserInfo.Length != 0 ||
            !url.AbsolutePath.StartsWith("/lealinfopdvnovo/leal-info-pdv-updates/releases/download/client-" + client + "-install-", StringComparison.Ordinal))
            throw new InvalidDataException("O manifesto não oferece uma instalação completa válida para esta licença.");
    }

    private static async Task DownloadAsync(Uri url, string destination, CancellationToken cancel)
    {
        for (var redirect = 0; redirect < 4; redirect++)
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
            if ((int)response.StatusCode is >= 300 and <= 399 && response.Headers.Location is Uri location)
            {
                var next = new Uri(url, location);
                if (next.Scheme != "https" || !next.IsDefaultPort || next.UserInfo.Length != 0 ||
                    next.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                    throw new InvalidDataException("O download redirecionou para um destino inesperado.");
                url = next;
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxZipBytes)
                throw new InvalidDataException("O pacote excede o limite de download.");
            await using var source = await response.Content.ReadAsStreamAsync(cancel);
            await using var output = File.Create(destination);
            var buffer = new byte[81920];
            int count;
            while ((count = await source.ReadAsync(buffer, cancel)) > 0)
            {
                if (output.Length + count > MaxZipBytes)
                    throw new InvalidDataException("O pacote excede o limite de download.");
                await output.WriteAsync(buffer.AsMemory(0, count), cancel);
            }
            return;
        }
        throw new InvalidDataException("O download retornou redirecionamentos demais.");
    }

    internal static void Extract(string package, string staging)
    {
        using var zip = ZipFile.OpenRead(package);
        long length = 0;
        foreach (var entry in zip.Entries)
        {
            var pieces = entry.FullName.TrimEnd('/').Split('/');
            if (pieces.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith('.') || p.EndsWith(' ') ||
                p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) ||
                ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000 ||
                pieces.Any(p => p is ".pdv-update" or ".pdv-initial" ||
                    p.EndsWith(".license", StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("O pacote contém um caminho ou arquivo inválido.");
            if (entry.Name.Length == 0)
                continue;
            length = checked(length + entry.Length);
            if (length > MaxExpandedBytes)
                throw new InvalidDataException("O pacote excede o limite de instalação.");
            var file = Path.Combine(staging, Path.Combine(pieces));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            entry.ExtractToFile(file);
        }

        foreach (var name in new[] { "LealInfoPDV.exe", "LealInfoPDV.dll", "LealInfoPDV.runtimeconfig.json", "LealInfoPDV.deps.json", "hostfxr.dll" })
            if (!File.Exists(Path.Combine(staging, name)))
                throw new InvalidDataException("O pacote não contém todos os componentes de uma instalação completa.");
    }
}
