using System.Text.Json;

namespace LealInfoPDV;

/// <summary>
/// Controla o saldo de conversação da LIA por licença.
/// Comandos locais do PDV não chamam Consume; somente conversação LIA/IA.
/// </summary>
public static class LiaUsageManager
{
    private sealed record UsageState(string LicenseId, long RemainingSeconds, DateTime UpdatedAtUtc, int LastWarningMinutes);

    private static readonly object Sync = new();
    private static UsageState? _state;

    private static string UsageDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LEAL INFO", "PDV");

    private static string UsagePath => Path.Combine(UsageDirectory, "lia-usage.json");

    public static bool HasConversationQuota =>
        LicenseManager.IsValid && (LicenseManager.IsPlus || LicenseManager.IsPro);

    public static TimeSpan InitialQuota => LicenseManager.Edition switch
    {
        LicenseEdition.Plus => TimeSpan.FromHours(1),
        LicenseEdition.Pro => TimeSpan.FromHours(5),
        _ => TimeSpan.Zero
    };

    public static TimeSpan Remaining
    {
        get
        {
            lock (Sync)
            {
                EnsureLoaded();
                return TimeSpan.FromSeconds(Math.Max(0, _state?.RemainingSeconds ?? 0));
            }
        }
    }

    public static bool HasTimeRemaining => Remaining > TimeSpan.Zero;

    public static string RemainingText
    {
        get
        {
            if (!HasConversationQuota) return string.Empty;
            var t = Remaining;
            return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }
    }

    public static string ExhaustedMessage => LicenseManager.Edition switch
    {
        LicenseEdition.Plus => "Seu saldo de conversa da LIA terminou. Os comandos locais do PDV continuam funcionando normalmente.",
        LicenseEdition.Pro => "Seu pacote de 5 horas da LIA terminou. Os comandos locais do PDV continuam funcionando. Para voltar a conversar com a LIA, ative uma nova recarga de 5 horas.",
        _ => "Conversa da LIA indisponível neste plano."
    };

    /// <summary>
    /// Desconta tempo apenas de conversação LIA/IA. Não usar para comandos locais.
    /// </summary>
    public static bool Consume(TimeSpan elapsed)
    {
        if (!HasConversationQuota || elapsed <= TimeSpan.Zero)
            return HasTimeRemaining;

        lock (Sync)
        {
            EnsureLoaded();
            if (_state is null || _state.RemainingSeconds <= 0)
                return false;

            var seconds = Math.Max(1L, (long)Math.Ceiling(elapsed.TotalSeconds));
            var remaining = Math.Max(0L, _state.RemainingSeconds - seconds);
            _state = _state with { RemainingSeconds = remaining, UpdatedAtUtc = DateTime.UtcNow };
            Save();
            return remaining > 0;
        }
    }

    /// <summary>
    /// Retorna cada aviso somente uma vez por pacote: 30, 10 e 5 minutos.
    /// </summary>
    public static string? TakeWarningIfNeeded()
    {
        if (!HasConversationQuota) return null;

        lock (Sync)
        {
            EnsureLoaded();
            if (_state is null || _state.RemainingSeconds <= 0) return null;

            int remainingMinutes = (int)Math.Ceiling(_state.RemainingSeconds / 60d);
            int warning = remainingMinutes <= 5 ? 5 : remainingMinutes <= 10 ? 10 : remainingMinutes <= 30 ? 30 : 0;
            if (warning == 0 || _state.LastWarningMinutes == warning) return null;

            _state = _state with { LastWarningMinutes = warning, UpdatedAtUtc = DateTime.UtcNow };
            Save();
            return $"Aviso: restam aproximadamente {warning} minutos de conversa com a LIA.";
        }
    }

    /// <summary>
    /// Recarrega manualmente o pacote da licença atual. Deve ser chamado somente após validação comercial/ativação da recarga.
    /// PLUS recebe 1 hora; PRO recebe 5 horas.
    /// </summary>
    public static void RechargeCurrentPackage()
    {
        if (!HasConversationQuota) return;
        lock (Sync)
        {
            var licenseId = LicenseManager.Current.LicenseId ?? string.Empty;
            _state = new UsageState(licenseId, (long)InitialQuota.TotalSeconds, DateTime.UtcNow, 0);
            Save();
        }
    }

    /// <summary>
    /// Reinicia o estado quando uma nova licença/recarga com LicenseId diferente é ativada.
    /// </summary>
    public static void Reload()
    {
        lock (Sync)
        {
            _state = null;
            EnsureLoaded();
        }
    }

    private static void EnsureLoaded()
    {
        if (_state is not null) return;

        var licenseId = LicenseManager.Current.LicenseId ?? string.Empty;
        var initialSeconds = Math.Max(0L, (long)InitialQuota.TotalSeconds);

        try
        {
            if (File.Exists(UsagePath))
            {
                var saved = JsonSerializer.Deserialize<UsageState>(File.ReadAllText(UsagePath));
                if (saved is not null &&
                    saved.LicenseId.Equals(licenseId, StringComparison.OrdinalIgnoreCase))
                {
                    _state = saved with { RemainingSeconds = Math.Max(0L, saved.RemainingSeconds) };
                    return;
                }
            }
        }
        catch
        {
            // Se o arquivo estiver corrompido, recria com o saldo da licença atual.
        }

        _state = new UsageState(licenseId, initialSeconds, DateTime.UtcNow, 0);
        Save();
    }

    private static void Save()
    {
        if (_state is null) return;
        Directory.CreateDirectory(UsageDirectory);
        var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(UsagePath, json);
    }
}
