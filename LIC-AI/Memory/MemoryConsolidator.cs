using LicAi.Models;

namespace LicAi.Memory;

public sealed class MemoryConsolidator
{
    public string BuildConsolidationPrompt(string existingSummary, IReadOnlyList<ChatMessage> recentMessages)
    {
        var recent = string.Join("\n", recentMessages.Select(m => $"{m.Role}: {m.Content}"));
        return $"""
Atualize a memoria de longo prazo da LIC AI usando somente fatos realmente uteis para futuras conversas.

Memoria atual:
{existingSummary}

Conversas recentes:
{recent}

Mantenha preferencias, projetos, decisoes, nomes, objetivos e contexto recorrente.
Remova redundancias e detalhes descartaveis.
Nao invente fatos. Nao registre segredos, senhas ou credenciais.
Retorne apenas a memoria consolidada.
""";
    }
}
