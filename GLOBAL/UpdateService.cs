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
    private const string VersionUrl = "https://raw.githubusercontent.com/lealinfopdvnovo/leal-info-pdv-updates/main/version.json";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private static Version Normalize(Version v) => new(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build, v.Revision < 0 ? 0 : v.Revision);

    private static Version GetInstalledVersion()
    {
        var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        return Normalize(assemblyVersion);
    }

    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var url = VersionUrl + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true, NoStore = true };
            request.Headers.UserAgent.ParseAdd("LEAL-INFO-PDV-Updater");
            using var response = await Http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            UpdateInfo? update = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (update == null || string.IsNullOrWhiteSpace(update.Version) || string.IsNullOrWhiteSpace(update.DownloadUrl)) return null;
            if (!Version.TryParse(update.Version.TrimStart('v', 'V'), out var parsedAvailable)) return null;
            return Normalize(parsedAvailable) > GetInstalledVersion() ? update : null;
        }
        catch { return null; }
    }

    public static async Task<string?> DownloadAsync(UpdateInfo update, IProgress<int>? progress = null)
    {
        try
        {
            string destination = Path.Combine(Path.GetTempPath(), $"LEAL_INFO_PDV_Update_{update.Version}.exe");
            using var request = new HttpRequestMessage(HttpMethod.Get, update.DownloadUrl);
            request.Headers.UserAgent.ParseAdd("LEAL-INFO-PDV-Updater");
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                received += read;
                if (total.HasValue && total.Value > 0)
                    progress?.Report((int)Math.Clamp(received * 100 / total.Value, 0, 100));
            }
            await output.FlushAsync().ConfigureAwait(false);
            progress?.Report(100);
            return destination;
        }
        catch { return null; }
    }

    public static void Install(string installerPath)
    {
        if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath)) return;
        Process.Start(new ProcessStartInfo { FileName = installerPath, UseShellExecute = true });
        Environment.Exit(0);
    }
}
