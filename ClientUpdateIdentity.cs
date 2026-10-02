using System.Text.RegularExpressions;

namespace LealInfoPDV;

internal static class ClientUpdateIdentity
{
    private static readonly Regex ClientCodePattern = new(@"^[0-9]{3,9}$", RegexOptions.CultureInvariant);

    internal static bool IsValidClientCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && ClientCodePattern.IsMatch(code);

    internal static string? GetManifestPath(string? code) =>
        IsValidClientCode(code) ? $"clients/{code}/version.json" : null;

    internal static bool ManifestMatches(string? requestedClientCode, string? manifestClientCode) =>
        IsValidClientCode(requestedClientCode) &&
        string.Equals(requestedClientCode, manifestClientCode, StringComparison.Ordinal);
}
