namespace LealInfoPDV;

internal sealed record KitchenDraftDestination(KitchenItem Item,bool TableOnly);
internal sealed record KitchenRowDestination(bool TableOnly,bool Chosen);
internal static class KitchenDraftDestinations
{
    internal static KitchenDraft[] Create(string reference,string notes,string actor,IReadOnlyList<KitchenDraftDestination> lines)
    {
        if(lines.Count is <1 or >200)throw new InvalidDataException("Adicione de 1 a 200 itens ao pedido.");
        if(lines.Any(l=>l.TableOnly)&&!KitchenTablesView.TableNumber(reference).HasValue)throw new InvalidOperationException("Selecione uma mesa para adicionar consumo.");
        var now=DateTimeOffset.UtcNow;
        return lines.GroupBy(l=>l.TableOnly).Select(g=>new KitchenDraft(Guid.NewGuid(),reference,notes,actor,now,g.Select(l=>l.Item).ToArray(),g.Key).Validate()).ToArray();
    }
}
