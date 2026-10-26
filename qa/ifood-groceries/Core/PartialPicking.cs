namespace LealInfo.IFood.Groceries;

public enum PlannedPickingAction { RequestCancellation, PickAllThree, PickOnlyOne }
public sealed record PickingLine(Guid ItemId, int OrderedQuantity, int PlannedQuantity, PlannedPickingAction Action);
public sealed record PickingReview(bool QuantitiesMatch, bool FinalDiscountPresent,
    IReadOnlyList<string> Issues)
{
    // A sponsorship can be a promotion. Coupon application and cancellation requests
    // require separate real evidence; neither is proved by final quantities alone.
    public bool RequiresOriginalOrderEvidence => true;
    public bool RequiresCouponEvidence => true;
    public bool RequiresCancellationRequestEvidence => true;
}

/// <summary>Local preparation of the official 3 x 3 scenario. Sends no picking commands.</summary>
public sealed class PartialPickingPlan
{
    public Guid OrderId { get; }
    public Guid MerchantId { get; }
    public IReadOnlyList<PickingLine> Lines { get; }

    public PartialPickingPlan(Guid testOrder, Guid testMerchant, Guid firstItem, Guid secondItem, Guid thirdItem)
    {
        var ids = new[] { firstItem, secondItem, thirdItem };
        if (testOrder == Guid.Empty || testMerchant == Guid.Empty || ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != 3)
            throw new ArgumentException("Three distinct item IDs and test order/merchant required");
        OrderId = testOrder; MerchantId = testMerchant;
        Lines = Array.AsReadOnly(new[] {
            new PickingLine(firstItem, 3, 0, PlannedPickingAction.RequestCancellation),
            new PickingLine(secondItem, 3, 3, PlannedPickingAction.PickAllThree),
            new PickingLine(thirdItem, 3, 1, PlannedPickingAction.PickOnlyOne)
        });
    }

    public PickingReview Review(Order finalBag)
    {
        if (finalBag.Id != OrderId || finalBag.MerchantId != MerchantId) throw new ContractException("scenario order/merchant");
        var issues = new List<string>();
        if (finalBag.Items.Select(i => i.Id).Distinct().Count() != finalBag.Items.Count)
            throw new ContractException("scenario duplicate items");
        if (finalBag.Items.Count != 3 || finalBag.Items.Any(i => !Lines.Any(l => l.ItemId == i.Id)))
            issues.Add("O cenario exige exatamente os tres produtos previstos; substituicoes precisam de outro roteiro.");
        foreach (var line in Lines)
        {
            var item = finalBag.Items.SingleOrDefault(i => i.Id == line.ItemId);
            if (item is null) { issues.Add("Produto previsto ausente; nao inferir cancelamento pela ausencia da linha."); continue; }
            if (line.Action == PlannedPickingAction.RequestCancellation)
            {
                if (!item.Unavailable || item.AvailableQuantity != 0)
                    issues.Add("Item 1 precisa de unavailable=true; quantidade zero sozinha nao prova indisponibilidade.");
            }
            else if (item.Unavailable || item.ReportedQuantity != line.PlannedQuantity || item.AvailableQuantity != line.PlannedQuantity)
                issues.Add(line.Action == PlannedPickingAction.PickAllThree ?
                    "Item 2 precisa de unavailable=false e quantidade 3." : "Item 3 precisa de unavailable=false e quantidade 1.");
        }
        return new PickingReview(issues.Count == 0, finalBag.Benefits.Any(b => b.Cents > 0), issues.AsReadOnly());
    }
}

public sealed record PreparedBag(Receipt Receipt, PickingReview? Review);

/// <summary>Joins RFI-gated persistence to the offline scenario checker. No ACK or PDV writes.</summary>
public sealed class FinalBagPreparation(Inbox inbox)
{
    public async Task<PreparedBag> ReceiveAsync(Signal signal, PartialPickingPlan plan,
        Func<Guid, CancellationToken, Task<string>> fetch, CancellationToken ct = default)
    {
        if (signal.OrderId != plan.OrderId || signal.MerchantId != plan.MerchantId)
            throw new ContractException("preparation identity");
        var receipt = await inbox.ReceiveAsync(signal, fetch, ct).ConfigureAwait(false);
        return new PreparedBag(receipt, receipt.Order is null ? null : plan.Review(receipt.Order));
    }
}
