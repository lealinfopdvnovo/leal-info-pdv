using System.Text.Json;

namespace LealInfoProtection;

public static class EmbeddedPlan
{
    public static BridgePlan Load()
    {
        using var stream = typeof(EmbeddedPlan).Assembly.GetManifestResourceStream("LealInfoProtecao001.plan.json")
            ?? throw new InvalidDataException("Plano de atualização ausente. Esta compilação não pode proteger o cliente.");
        var plan = JsonSerializer.Deserialize<BridgePlan>(stream) ?? throw new InvalidDataException("Plano inválido.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(plan.SourceCommit, "^[a-f0-9]{40}$")) throw new InvalidDataException("Commit de origem inválido.");
        return plan;
    }
}
