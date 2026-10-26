using System.Globalization;
namespace LealInfo.IFood.Groceries;

public sealed class ContractException : Exception
{
    public ContractException(string field) : base("Contrato Virtual Bag inválido: " + field) { }
}
public sealed record Item(Guid Id, string Name, int ReportedQuantity, bool Unavailable,
    decimal UnitCents, decimal GrossCents)
{
    public int AvailableQuantity => Unavailable ? 0 : ReportedQuantity;
}
public sealed record Benefit(string Target, string? TargetId, string Liability, decimal Cents);
public sealed record Order(Guid Id, Guid MerchantId, string Operation, string Scheduling,
    DateTimeOffset? WindowFrom, DateTimeOffset? WindowTo, IReadOnlyList<Item> Items,
    IReadOnlyList<Benefit> Benefits, decimal BagGrossCents, decimal PaymentCents);

// Both Windows Framework and .NET 8 use the same strict parser; no divergent copies.
public static class VirtualBag
{
    public static Order Parse(string json, Guid expectedOrder, Guid testMerchant)
    {
        try
        {
            var bag=PortableBag.Parse(json,expectedOrder,testMerchant);
            return new Order(bag.Id,bag.MerchantId,bag.Operation,bag.Scheduling,
                bag.WindowFrom is null ? null : DateTimeOffset.Parse(bag.WindowFrom,CultureInfo.InvariantCulture),
                bag.WindowTo is null ? null : DateTimeOffset.Parse(bag.WindowTo,CultureInfo.InvariantCulture),
                Array.AsReadOnly(bag.Items.Select(i=>new Item(i.Id,i.Name,i.ReportedQuantity,i.Unavailable,i.UnitCents,i.GrossCents)).ToArray()),
                Array.AsReadOnly(bag.Benefits.Select(b=>new Benefit(b.Target,b.TargetId,b.Liability,b.Cents)).ToArray()),bag.BagGrossCents,bag.PaymentCents);
        }
        catch(ReceptionFailure){throw new ContractException("identidade, unavailable, quantidades, valores ou janela de atendimento");}
    }
}
