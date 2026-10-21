using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LealInfoPDV;

internal sealed record KitchenItem(string Description, decimal Quantity, string Notes, long? ProductId = null, [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] decimal? UnitPrice=null, [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] int? Position=null);
internal sealed record KitchenDraft(Guid Id, string Reference, string Notes, string Operator, DateTimeOffset CreatedUtc, KitchenItem[] Items, [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] bool TableOnly=false)
{
    internal KitchenDraft Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Operator) || Operator.Length > 160 || Reference is null || Notes is null || Reference.Length > 160 || Notes.Length > 2000 ||
            CreatedUtc.Offset != TimeSpan.Zero || CreatedUtc.Year < 2020 || Items is null || Items.Length is < 1 or > 200)
            throw new InvalidDataException("Identificação, operador ou itens do pedido inválidos.");
        foreach (var item in Items)
            if (item is null || string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 240 || item.Notes is null || item.Notes.Length > 1000 ||
                item.Quantity <= 0 || item.Quantity > 100000 || decimal.Round(item.Quantity, 3) != item.Quantity || item.ProductId is <= 0)
                throw new InvalidDataException("Informe descrição e quantidade positiva, com até três casas decimais, em cada item.");
        return this;
    }
    internal string Json => JsonSerializer.Serialize(this);
    internal string Hash => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)));
    internal static decimal Quantity(string text)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(text.Trim(), @"^[0-9]+(?:[.,][0-9]{1,3})?$"))
            throw new InvalidDataException("Quantidade inválida. Use, por exemplo, 1 ou 1,500.");
        return decimal.Parse(text.Trim().Replace(',', '.'), CultureInfo.InvariantCulture);
    }
}
internal sealed record KitchenOrder(Guid Id, string Reference, string Notes, string Operator, string Status, long Revision,
    DateTimeOffset CreatedUtc, DateTimeOffset ReceivedUtc, DateTimeOffset UpdatedUtc, KitchenItem[] Items, bool TableOnly=false, bool Paid=false);
internal sealed record KitchenEvent(string OperationId, string Status, DateTimeOffset AtUtc, string Operator);
