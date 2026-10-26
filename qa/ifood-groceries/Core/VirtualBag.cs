using System.Globalization;
using System.Text.Json;

namespace LealInfo.IFood.Groceries;

public sealed class ContractException : Exception
{
    public ContractException(string field) : base("Contrato Virtual Bag inválido: " + field) { }
}
public sealed record Item(Guid Id, string Name, int ReportedQuantity, bool Unavailable,
    decimal UnitCents, decimal GrossCents)
{
    // Keep the cancelled line for audit; never turn it back into a saleable line.
    public int AvailableQuantity => Unavailable ? 0 : ReportedQuantity;
}
public sealed record Benefit(string Target, string? TargetId, string Liability, decimal Cents);
public sealed record Order(Guid Id, Guid MerchantId, string Operation, string Scheduling,
    DateTimeOffset? WindowFrom, DateTimeOffset? WindowTo, IReadOnlyList<Item> Items,
    IReadOnlyList<Benefit> Benefits, decimal BagGrossCents, decimal PaymentCents);

public static class VirtualBag
{
    public static Order Parse(string json, Guid expectedOrder, Guid testMerchant)
    {
        if (json.Length > 1_048_576) throw new ContractException("size");
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            RejectDuplicateProperties(doc.RootElement);
            var r = doc.RootElement;
            if (Id(r, "id") != expectedOrder || Id(Field(r, "merchant"), "id") != testMerchant)
                throw new ContractException("order/merchant");
            if (!Boolean(r, "test") || Text(r, "category") != "GROCERY")
                throw new ContractException("test/category");
            var bag = Field(r, "bag");
            var list = Field(bag, "items");
            if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > 500)
                throw new ContractException("bag.items");
            var items = new List<Item>(); var ids = new HashSet<Guid>();
            foreach (var i in list.EnumerateArray())
            {
                var id = Id(i, "uniqueId");
                if (!ids.Add(id)) throw new ContractException("duplicate item id");
                var unavailable = Boolean(i, "unavailable");
                var quantity = Field(i, "quantity");
                if (!quantity.TryGetInt32(out var n) || n < 0 || n > 100_000 || (!unavailable && n == 0))
                    throw new ContractException("quantity");
                if (Boolean(Field(i, "product"), "isVariableWeight"))
                    throw new ContractException("variable weight not supported in this initial unit-item scope");
                var prices = Field(i, "prices");
                items.Add(new Item(id, Text(i, "name"), n, unavailable,
                    Money(Field(prices, "unitValue")), Money(Field(prices, "grossValue"))));
            }
            var op = Field(r, "operationMode"); var mode = Text(op, "type");
            if (mode is not ("DELIVERY" or "TAKEOUT")) throw new ContractException("operationMode.type");
            var timing = Text(op, "schedulingType");
            if (timing is not ("IMMEDIATE" or "TIME_SLOT")) throw new ContractException("schedulingType");
            DateTimeOffset? from = null, to = null;
            var modeData = Field(op, mode == "TAKEOUT" ? "takeout" : "delivery");
            var timeName = mode == "TAKEOUT" ? "takeoutTime" : "deliveryTime";
            if (modeData.TryGetProperty(timeName, out var time) && time.ValueKind == JsonValueKind.Object &&
                time.TryGetProperty("window", out var window) && window.ValueKind == JsonValueKind.Object)
            {
                from = Date(window, "from"); to = Date(window, "to");
                if (from >= to) throw new ContractException("time window order");
            }
            if (timing == "TIME_SLOT" && from is null) throw new ContractException("scheduled window required");
            var benefits = new List<Benefit>();
            if (r.TryGetProperty("benefit", out var benefit) && benefit.ValueKind != JsonValueKind.Null)
            {
                foreach (var b in Field(benefit, "benefits").EnumerateArray())
                    foreach (var sponsor in Field(b, "sponsorships").EnumerateArray())
                        benefits.Add(new Benefit(Text(b, "target"), b.TryGetProperty("targetId", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null,
                            Text(sponsor, "liability"), Money(Field(sponsor, "amount"))));
            }
            decimal paid = 0;
            foreach (var method in Field(Field(r, "payment"), "methods").EnumerateArray())
                paid = checked(paid + Money(Field(method, "amount")));
            // Preserve server amounts and sponsorships; do not redistribute a coupon or post finance here.
            return new Order(expectedOrder, testMerchant, mode, timing, from, to, items, benefits,
                Money(Field(Field(bag, "prices"), "grossValue")), paid);
        }
        catch (ContractException) { throw; }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or OverflowException)
        { throw new ContractException("JSON/types"); }
    }
    private static JsonElement Field(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) ? v : throw new ContractException(key);
    private static string Text(JsonElement e, string key)
    {
        var v = Field(e, key);
        return v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 and <= 1024 } s ? s : throw new ContractException(key);
    }
    private static Guid Id(JsonElement e, string key) => Guid.TryParse(Text(e, key), out var id) && id != Guid.Empty ? id : throw new ContractException(key);
    private static bool Boolean(JsonElement e, string key) => Field(e, key).ValueKind switch
    { JsonValueKind.True => true, JsonValueKind.False => false, _ => throw new ContractException(key) };
    private static decimal Money(JsonElement e)
    {
        var value = Field(e, "value");
        if (!value.TryGetDecimal(out var cents) || cents < 0 || decimal.Truncate(cents) != cents) throw new ContractException("money cents");
        if (e.TryGetProperty("currency", out var c) && (c.ValueKind != JsonValueKind.String || c.GetString() != "BRL")) throw new ContractException("currency");
        return cents;
    }
    private static DateTimeOffset Date(JsonElement e, string key)
    {
        var s = Text(e, key);
        if (!(s.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(s, @"[+-]\d{2}:\d{2}$")) ||
            !DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) throw new ContractException(key);
        return d;
    }
    private static void RejectDuplicateProperties(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in e.EnumerateObject()) { if (!names.Add(p.Name)) throw new ContractException("duplicate JSON property"); RejectDuplicateProperties(p.Value); }
        }
        else if (e.ValueKind == JsonValueKind.Array) foreach (var v in e.EnumerateArray()) RejectDuplicateProperties(v);
    }
}
