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
        var portable=new BagOrder { Id=finalBag.Id,MerchantId=finalBag.MerchantId,Operation=finalBag.Operation,Scheduling=finalBag.Scheduling,
            Items=finalBag.Items.Select(i=>new BagItem {Id=i.Id,Name=i.Name,ReportedQuantity=i.ReportedQuantity,Unavailable=i.Unavailable,UnitCents=i.UnitCents,GrossCents=i.GrossCents}).ToArray(),
            Benefits=finalBag.Benefits.Select(b=>new BagBenefit {Target=b.Target,TargetId=b.TargetId,Liability=b.Liability,Cents=b.Cents}).ToArray() };
        try
        {
            var selection=ScenarioSelection.Create(OrderId,MerchantId,Lines[0].ItemId,Lines[1].ItemId,Lines[2].ItemId);
            var review=ScenarioInspection.Inspect(portable,selection);
            return new PickingReview(review.QuantitiesMatch,finalBag.Benefits.Any(b=>b.Cents>0),
                review.Criteria.Where(c=>c.QuantityCriterion && c.State!=InspectionState.Pass).Select(c=>c.Detail).ToList().AsReadOnly());
        }
        catch(ReceptionFailure){throw new ContractException("scenario identity/items");}
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
