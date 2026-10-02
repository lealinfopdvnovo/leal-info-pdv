using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace LealInfoPDV.Network;
internal sealed record WireValue(string Kind, string Value)
{
    internal static WireValue From(object? value) => value switch
    {
        null or DBNull => new("null", ""),
        byte[] bytes => new("bytes", Convert.ToBase64String(bytes)),
        bool boolean => new("integer", boolean ? "1" : "0"),
        byte or short or int or long => new("integer", Convert.ToString(value, CultureInfo.InvariantCulture)!),
        float or double or decimal => new("real", Convert.ToString(value, CultureInfo.InvariantCulture)!),
        _ => new("text", Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")
    };
    internal object ToObject() => Kind switch
    {
        "null" => DBNull.Value, "bytes" => Convert.FromBase64String(Value),
        "integer" => long.Parse(Value, CultureInfo.InvariantCulture),
        "real" => double.Parse(Value, CultureInfo.InvariantCulture), _ => Value
    };
}
internal sealed class NetworkRequest
{
    public string Operation { get; set; } = "";
    public string Sql { get; set; } = "";
    public Dictionary<string, WireValue> Parameters { get; set; } = new();
    public string Serial { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Secret { get; set; } = "";
    public string Signature { get; set; } = "";
}
internal sealed class NetworkResponse
{
    public string Error { get; set; } = "";
    public int SqliteErrorCode { get; set; }
    public string Challenge { get; set; } = "";
    public string SignedLicense { get; set; } = "";
    public string ServerSerial { get; set; } = "";
    public int Affected { get; set; }
    public WireValue? Scalar { get; set; }
    public List<string> Columns { get; set; } = new();
    public List<string> Types { get; set; } = new();
    public List<WireValue[]> Rows { get; set; } = new();
}
internal static class NetworkProtocol
{
    internal const int MaxBytes = 16 * 1024 * 1024;
    internal static void Write<T>(Stream stream, T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Resultado muito grande. Reduza o período ou filtro da consulta.");
        stream.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(bytes.Length)));
        stream.Write(bytes); stream.Flush();
    }
    internal static T Read<T>(Stream stream)
    {
        var header = new byte[4]; stream.ReadExactly(header);
        var length = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header));
        if (length is < 1 or > MaxBytes) throw new InvalidDataException("Pacote de rede inválido.");
        var bytes = new byte[length]; stream.ReadExactly(bytes);
        return JsonSerializer.Deserialize<T>(bytes) ?? throw new InvalidDataException("Resposta de rede inválida.");
    }
    internal static NetworkResponse ReadRows(DbDataReader reader)
    {
        var result = new NetworkResponse();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            result.Columns.Add(reader.GetName(i));
            result.Types.Add(reader.GetFieldType(i) == typeof(long) ? "integer" : reader.GetFieldType(i) == typeof(double) ? "real" : reader.GetFieldType(i) == typeof(byte[]) ? "bytes" : "text");
        }
        while (reader.Read())
        {
            var row = new WireValue[reader.FieldCount];
            for (var i = 0; i < row.Length; i++) row[i] = WireValue.From(reader.GetValue(i));
            result.Rows.Add(row);
            if (result.Rows.Count > 50000) throw new InvalidDataException("Consulta extensa demais. Reduza os filtros.");
        }
        return result;
    }
}
