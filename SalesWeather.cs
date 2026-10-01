using System.Drawing.Drawing2D;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LealInfoPDV;

internal sealed record WeatherLocation(string City, string State, double Latitude, double Longitude);
internal sealed record WeatherReading(WeatherLocation Location, double Temperature, double Humidity, double Wind,
    string Symbol, DateTimeOffset ForecastAt, DateTimeOffset UpdatedAt, DateTimeOffset CheckedAt, bool Offline);

internal sealed class SalesWeatherService
{
    private static readonly HttpClient SharedClient = CreateClient();
    private static readonly SemaphoreSlim GeoGate = new(1);
    private static DateTimeOffset lastGeoRequest;
    private readonly HttpClient client;
    private readonly string cacheFolder;
    private readonly Func<DateTimeOffset> clock;
    private readonly string geocoder;
    internal SalesWeatherService(HttpClient? client = null, string? cacheFolder = null, Func<DateTimeOffset>? clock = null, string? geocoder = null)
    {
        this.client = client ?? SharedClient;
        this.cacheFolder = cacheFolder ?? Path.Combine(Database.AppFolder, "Weather");
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.geocoder = geocoder ?? "https://nominatim.openstreetmap.org/search";
    }
    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LealInfoPDV/10.349 (+https://github.com/lealinfopdvnovo/leal-info-pdv)");
        return client;
    }
    internal static (string City, string State) ParseCity(string value)
    {
        var match = Regex.Match(value.Trim(), @"^(.*?)(?:\s*[/,\-]\s*([A-Za-z]{2}))?$");
        var city = match.Groups[1].Value.Trim();
        var state = match.Groups[2].Value.ToUpperInvariant();
        if (city.Length < 2 || city.Equals("BRASIL", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cadastre a cidade da empresa.");
        if (state.Length > 0 && !States.ContainsKey(state)) throw new InvalidOperationException("Confira a UF no cadastro da empresa.");
        return (city, state);
    }
    private static string Normalize(string value) => new string(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant();
    private static readonly Dictionary<string, string> States = new()
    {
        ["AC"]="Acre",["AL"]="Alagoas",["AP"]="Amapá",["AM"]="Amazonas",["BA"]="Bahia",["CE"]="Ceará",["DF"]="Distrito Federal",
        ["ES"]="Espírito Santo",["GO"]="Goiás",["MA"]="Maranhão",["MT"]="Mato Grosso",["MS"]="Mato Grosso do Sul",["MG"]="Minas Gerais",
        ["PA"]="Pará",["PB"]="Paraíba",["PR"]="Paraná",["PE"]="Pernambuco",["PI"]="Piauí",["RJ"]="Rio de Janeiro",["RN"]="Rio Grande do Norte",
        ["RS"]="Rio Grande do Sul",["RO"]="Rondônia",["RR"]="Roraima",["SC"]="Santa Catarina",["SP"]="São Paulo",["SE"]="Sergipe",["TO"]="Tocantins"
    };
    internal sealed record Cache(WeatherLocation Location, string Json, DateTimeOffset CheckedAt, DateTimeOffset Expires, DateTimeOffset? Modified);
    internal async Task<WeatherReading> GetAsync(string registeredCity, CancellationToken token)
    {
        var (city, state) = ParseCity(registeredCity);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(city) + "/" + state)));
        string file = Path.Combine(cacheFolder, key + ".json");
        Cache? cache = null;
        try { if (File.Exists(file)) cache = JsonSerializer.Deserialize<Cache>(await File.ReadAllTextAsync(file, token)); } catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        if (cache != null && cache.Expires > clock())
            return Read(cache, clock(), false);
        try
        {
            WeatherLocation? location = cache?.Location;
            string locationFile = file + ".location";
            if (location == null)
            {
                try { if (File.Exists(locationFile)) location = JsonSerializer.Deserialize<WeatherLocation>(await File.ReadAllTextAsync(locationFile, token)); }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
                location ??= await ResolveAsync(city, state, token);
                try { Directory.CreateDirectory(cacheFolder); await File.WriteAllTextAsync(locationFile, JsonSerializer.Serialize(location), token); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            string lat = (Math.Truncate(location.Latitude * 10000) / 10000).ToString("0.0000", CultureInfo.InvariantCulture);
            string lon = (Math.Truncate(location.Longitude * 10000) / 10000).ToString("0.0000", CultureInfo.InvariantCulture);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.met.no/weatherapi/locationforecast/2.0/compact?lat={lat}&lon={lon}");
            if (cache?.Modified != null) request.Headers.IfModifiedSince = cache.Modified;
            using var response = await client.SendAsync(request, token);
            string json;
            if (response.StatusCode == HttpStatusCode.NotModified && cache != null) json = cache.Json;
            else { response.EnsureSuccessStatusCode(); json = await response.Content.ReadAsStringAsync(token); }
            var expires = response.Content.Headers.Expires ?? clock().AddMinutes(15);
            if (expires < clock().AddMinutes(15)) expires = clock().AddMinutes(15);
            cache = new Cache(location, json, clock(), expires, response.Content.Headers.LastModified ?? cache?.Modified);
            var reading = Read(cache, clock(), false);
            try { Directory.CreateDirectory(cacheFolder); await File.WriteAllTextAsync(file, JsonSerializer.Serialize(cache), token); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return reading;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            token.ThrowIfCancellationRequested();
            if (cache != null) return Read(cache, clock(), true);
            throw new InvalidOperationException("Tempo indisponível. Tentando novamente em breve.", ex);
        }
    }
    private async Task<WeatherLocation> ResolveAsync(string city, string state, CancellationToken token)
    {
        await GeoGate.WaitAsync(token);
        try
        {
            var wait = lastGeoRequest.AddSeconds(1.1) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, token);
            lastGeoRequest = DateTimeOffset.UtcNow;
            string url = geocoder + "?format=jsonv2&addressdetails=1&featureType=city&countrycodes=br&limit=5&city=" + Uri.EscapeDataString(city);
            if (state.Length > 0) url += "&state=" + Uri.EscapeDataString(States[state]);
            using var response = await client.GetAsync(url, token);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var matches = new List<WeatherLocation>();
            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                var address = entry.GetProperty("address");
                string name = "";
                foreach (string field in new[] { "city", "town", "municipality", "village" })
                    if (address.TryGetProperty(field, out var value)) { name = value.GetString() ?? ""; break; }
                if (Normalize(name) != Normalize(city)) continue;
                string uf = address.TryGetProperty("ISO3166-2-lvl4", out var iso) ? (iso.GetString() ?? "").Replace("BR-", "") : "";
                if (uf.Length == 0 && address.TryGetProperty("state", out var stateName))
                    uf = States.FirstOrDefault(pair => Normalize(pair.Value) == Normalize(stateName.GetString() ?? "")).Key ?? "";
                if (state.Length > 0 && uf != state) continue;
                if (uf.Length == 0) continue;
                matches.Add(new WeatherLocation(name, uf, double.Parse(entry.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture), double.Parse(entry.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture)));
            }
            if (matches.Select(match => match.State).Distinct().Count() > 1) throw new InvalidOperationException("Informe cidade e UF no cadastro da empresa.");
            return matches.FirstOrDefault() ?? throw new InvalidOperationException("Cidade não localizada. Confira cidade/UF da empresa.");
        }
        finally { GeoGate.Release(); }
    }
    internal static WeatherReading Read(Cache cache, DateTimeOffset now, bool offline)
    {
        using var doc = JsonDocument.Parse(cache.Json);
        var properties = doc.RootElement.GetProperty("properties");
        var steps = properties.GetProperty("timeseries").EnumerateArray().ToArray();
        var step = steps.OrderBy(item => Math.Abs((DateTimeOffset.Parse(item.GetProperty("time").GetString()!, CultureInfo.InvariantCulture) - now).TotalMinutes)).First();
        var forecastAt = DateTimeOffset.Parse(step.GetProperty("time").GetString()!, CultureInfo.InvariantCulture);
        if (Math.Abs((forecastAt - now).TotalHours) > 2) throw new InvalidOperationException("Previsão desatualizada. Aguardando conexão.");
        var data = step.GetProperty("data");
        var details = data.GetProperty("instant").GetProperty("details");
        string symbol = "cloudy";
        foreach (var period in new[] { "next_1_hours", "next_6_hours", "next_12_hours" })
            if (data.TryGetProperty(period, out var next)) { symbol = next.GetProperty("summary").GetProperty("symbol_code").GetString() ?? symbol; break; }
        return new WeatherReading(cache.Location, details.GetProperty("air_temperature").GetDouble(), details.GetProperty("relative_humidity").GetDouble(),
            details.GetProperty("wind_speed").GetDouble() * 3.6, symbol, forecastAt,
            DateTimeOffset.Parse(properties.GetProperty("meta").GetProperty("updated_at").GetString()!, CultureInfo.InvariantCulture), cache.CheckedAt, offline);
    }
}

internal sealed class SalesWeatherCard : Control
{
    private readonly Func<string> city;
    private readonly SalesWeatherService service;
    private readonly CancellationTokenSource cancellation = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = (15 * 60 + Random.Shared.Next(30, 180)) * 1000 };
    private readonly ToolTip tip = new();
    private bool loading;
    private string message = "Consultando o tempo…";
    internal WeatherReading? Reading { get; private set; }
    internal SalesPalette Palette { get; set; } = SalesPalette.For("Futurista Azul");
    internal static Func<SalesWeatherService>? ServiceFactory { get; set; }
    internal SalesWeatherCard(Func<string> city)
    {
        this.city = city; service = ServiceFactory?.Invoke() ?? new SalesWeatherService();
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Dock = DockStyle.Fill; Margin = new Padding(0, 6, 0, 0); Cursor = Cursors.Hand;
        timer.Tick += async (_, _) => await RefreshWeatherAsync();
        Click += (_, _) => MessageBox.Show(this,
            "A cidade vem de Configuração > Cadastro da Empresa (cidade/UF).\n\nPrevisão atualizada automaticamente enquanto a venda está aberta.\n" +
            "Dados: MET Norway • CC BY 4.0\nhttps://api.met.no/\nhttps://creativecommons.org/licenses/by/4.0/\n" +
            "Localização: © OpenStreetMap contributors • ODbL\nhttps://www.openstreetmap.org/copyright",
            "Tempo da cidade", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    protected override async void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e); timer.Start(); await RefreshWeatherAsync();
    }
    internal async Task RefreshWeatherAsync()
    {
        if (loading || IsDisposed) return;
        loading = true;
        string requestedCity = city();
        try
        {
            if (Reading != null)
            {
                var expected = SalesWeatherService.ParseCity(requestedCity);
                if (!Reading.Location.City.Equals(expected.City, StringComparison.OrdinalIgnoreCase) || (expected.State.Length > 0 && Reading.Location.State != expected.State)) Reading = null;
            }
            var reading = await service.GetAsync(requestedCity, cancellation.Token);
            if (IsDisposed || cancellation.IsCancellationRequested) return;
            Reading = reading; message = "";
            tip.SetToolTip(this, $"{reading.Location.City}/{reading.Location.State}\nPrevisão para {reading.ForecastAt.ToLocalTime():dd/MM HH:mm}\nModelo atualizado: {reading.UpdatedAt.ToLocalTime():dd/MM HH:mm}\nConsulta: {reading.CheckedAt.ToLocalTime():dd/MM HH:mm}\nMET Norway • CC BY 4.0 | © OpenStreetMap contributors\nClique para informações.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!IsDisposed) { Reading = null; message = ex is InvalidOperationException ? ex.Message : "Tempo indisponível. Aguardando conexão."; } }
        finally { loading = false; if (!IsDisposed) Invalidate(); }
    }
    internal static string Description(string symbol) => symbol.Contains("thunder") ? "Trovoadas" : symbol.Contains("snow") || symbol.Contains("sleet") ? "Neve / granizo" : symbol.Contains("rain") ? "Chuva" : symbol.Contains("fog") ? "Neblina" : symbol.StartsWith("clearsky") ? "Céu limpo" : symbol.StartsWith("fair") || symbol.StartsWith("partlycloudy") ? (symbol.EndsWith("_night") ? "Parcialmente nublado" : "Sol entre nuvens") : "Nublado";
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        SalesVisuals.Frame(g, ClientRectangle, palette: Palette);
        float scale = Math.Min(Width / 220f, Height / 155f);
        if (scale <= 0) return;
        void Text(string value, float x, float y, float w, float h, float size, Color color, bool bold = false)
        {
            using var font = new Font("Segoe UI", Math.Max(10, size * scale * 96f / 72f), bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            TextRenderer.DrawText(g, value, font, new Rectangle((int)x, (int)y, (int)w, (int)h), color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        string location = Reading == null ? city().Trim() : Reading.Location.City + "/" + Reading.Location.State;
        Text(location.Length > 0 ? location : "TEMPO DA CIDADE", 8, 4, Width - 16, 24 * scale, 11, Palette.Foreground, true);
        if (Reading == null)
        {
            Text(message, 14, 35 * scale, Width - 28, Height - 45 * scale, 10, Palette.Foreground); return;
        }
        var reading = Reading;
        float cx = Width * .23f, cy = 55 * scale, r = 13 * scale;
        bool rain = reading.Symbol.Contains("rain") || reading.Symbol.Contains("snow") || reading.Symbol.Contains("thunder");
        bool sun = reading.Symbol.StartsWith("clearsky") || reading.Symbol.StartsWith("fair") || reading.Symbol.StartsWith("partlycloudy");
        using var gold = new SolidBrush(Color.FromArgb(255, 196, 40));
        using var ray = new Pen(Color.FromArgb(255, 196, 40), Math.Max(1, 2 * scale));
        if (sun)
        {
            if (reading.Symbol.EndsWith("_night"))
            {
                var saved = g.Save();
                using var cutout = new GraphicsPath();
                cutout.AddEllipse(cx - r * .25f, cy - r * 1.35f, r * 2, r * 2);
                g.SetClip(cutout, CombineMode.Exclude);
                using var moon = new SolidBrush(Color.FromArgb(235, 235, 205));
                g.FillEllipse(moon, cx - r, cy - r, r * 2, r * 2);
                g.Restore(saved);
            }
            else
            {
                g.FillEllipse(gold, cx - r, cy - r, r * 2, r * 2);
                for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(ray, cx + (float)Math.Cos(a) * r * 1.25f, cy + (float)Math.Sin(a) * r * 1.25f, cx + (float)Math.Cos(a) * r * 1.65f, cy + (float)Math.Sin(a) * r * 1.65f); }
            }
        }
        if (!reading.Symbol.StartsWith("clearsky"))
        {
            using var cloud = new SolidBrush(ControlPaint.Light(Palette.Accent, .55f));
            g.FillEllipse(cloud, cx - 22 * scale, cy - 3 * scale, 38 * scale, 22 * scale);
            g.FillEllipse(cloud, cx - 6 * scale, cy - 12 * scale, 29 * scale, 30 * scale);
            if (rain) { using var drop = new Pen(Palette.Accent, 2 * scale); for (int i = 0; i < 3; i++) g.DrawLine(drop, cx - 12 * scale + i * 12 * scale, cy + 23 * scale, cx - 16 * scale + i * 12 * scale, cy + 31 * scale); }
        }
        Text(reading.Temperature.ToString("0", CultureInfo.GetCultureInfo("pt-BR")) + "°C", Width * .43f, 30 * scale, Width * .5f, 43 * scale, 27, Palette.Foreground, true);
        Text(Description(reading.Symbol), 8, 78 * scale, Width - 16, 20 * scale, 10, Palette.Foreground);
        Text($"Umidade {reading.Humidity:0}% • Vento {reading.Wind:0} km/h", 5, 99 * scale, Width - 10, 19 * scale, 8, Palette.Foreground);
        Text((reading.Offline ? "Offline • consulta " : "Previsão • consulta ") + reading.CheckedAt.ToLocalTime().ToString("dd/MM HH:mm"), 5, 119 * scale, Width - 10, 17 * scale, 8, Palette.Foreground);
        Text("MET Norway • OpenStreetMap", 5, Height - 21 * scale, Width - 10, 17 * scale, 7, Palette.Foreground);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { cancellation.Cancel(); timer.Dispose(); tip.Dispose(); cancellation.Dispose(); }
        base.Dispose(disposing);
    }
}
