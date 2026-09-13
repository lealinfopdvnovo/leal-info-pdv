using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public sealed class UpdateInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public static class UpdateService
{
    private const string VersionUrl =
        "https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/version.json";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static Version Normalize(Version v) => new(
        v.Major,
        v.Minor,
        v.Build < 0 ? 0 : v.Build,
        v.Revision < 0 ? 0 : v.Revision);

    private static Version GetInstalledVersion()
    {
        var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version
            ?? Assembly.GetExecutingAssembly().GetName().Version
            ?? new Version(0, 0, 0, 0);
        return Normalize(assemblyVersion);
    }

    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            string json = await Http.GetStringAsync(VersionUrl);
            UpdateInfo? update = JsonSerializer.Deserialize<UpdateInfo>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (update == null ||
                string.IsNullOrWhiteSpace(update.Version) ||
                string.IsNullOrWhiteSpace(update.DownloadUrl))
                return null;

            if (!Version.TryParse(update.Version.TrimStart('v', 'V'), out var parsedAvailable))
                return null;

            Version installed = GetInstalledVersion();
            Version available = Normalize(parsedAvailable);
            return available > installed ? update : null;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string> DownloadAsync(UpdateInfo update)
    {
        string destination = Path.Combine(
            Path.GetTempPath(),
            $"LEAL_INFO_PDV_Update_{update.Version}.exe");

        byte[] file = await Http.GetByteArrayAsync(update.DownloadUrl);
        await File.WriteAllBytesAsync(destination, file);
        return destination;
    }

    public static void Install(string installerPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        });

        Environment.Exit(0);
    }
}
