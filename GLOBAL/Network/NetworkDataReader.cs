using System.Collections;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace LealInfoPDV.Network;
internal sealed class NetworkDataReader : DbDataReader
{
    private readonly NetworkResponse data;
    private int row = -1;
    private bool closed;
    internal NetworkDataReader(NetworkResponse data) => this.data = data;
    public override int FieldCount => data.Columns.Count;
    public override bool HasRows => data.Rows.Count > 0;
    public override bool IsClosed => closed;
    public override int RecordsAffected => -1;
    public override int Depth => 0;
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
    public override bool Read() => !closed && ++row < data.Rows.Count;
    public override bool NextResult() => false;
    public override void Close() => closed = true;
    public override string GetName(int ordinal) => data.Columns[ordinal];
    public override int GetOrdinal(string name)
    {
        var index = data.Columns.FindIndex(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? throw new IndexOutOfRangeException(name) : index;
    }
    public override object GetValue(int ordinal) => data.Rows[row][ordinal].ToObject();
    public override int GetValues(object[] values)
    { var count = Math.Min(values.Length, FieldCount); for (var i = 0; i < count; i++) values[i] = GetValue(i); return count; }
    public override bool IsDBNull(int ordinal) => data.Rows[row][ordinal].Kind == "null";
    public override string GetDataTypeName(int ordinal) => data.Types[ordinal];
    public override Type GetFieldType(int ordinal) => data.Types[ordinal] switch { "integer" => typeof(long), "real" => typeof(double), "bytes" => typeof(byte[]), _ => typeof(string) };
    public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal), CultureInfo.InvariantCulture)!;
    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
    public override DateTime GetDateTime(int ordinal) => DateTime.Parse(GetString(ordinal), CultureInfo.InvariantCulture);
    public override Guid GetGuid(int ordinal) => Guid.Parse(GetString(ordinal));
    public override long GetBytes(int ordinal, long offset, byte[]? buffer, int bufferOffset, int length)
    {
        var bytes = (byte[])GetValue(ordinal); if (buffer == null) return bytes.Length;
        var count = Math.Min(length, Math.Max(0, bytes.Length - checked((int)offset)));
        Array.Copy(bytes, offset, buffer, bufferOffset, count); return count;
    }
    public override long GetChars(int ordinal, long offset, char[]? buffer, int bufferOffset, int length)
    {
        var chars = GetString(ordinal).ToCharArray(); if (buffer == null) return chars.Length;
        var count = Math.Min(length, Math.Max(0, chars.Length - checked((int)offset)));
        Array.Copy(chars, offset, buffer, bufferOffset, count); return count;
    }
    public override IEnumerator GetEnumerator() => new DbEnumerator(this);
    public override DataTable GetSchemaTable()
    {
        var schema = new DataTable();
        schema.Columns.Add("ColumnName", typeof(string)); schema.Columns.Add("ColumnOrdinal", typeof(int));
        schema.Columns.Add("ColumnSize", typeof(int)); schema.Columns.Add("DataType", typeof(Type));
        schema.Columns.Add("AllowDBNull", typeof(bool)); schema.Columns.Add("IsKey", typeof(bool)); schema.Columns.Add("IsUnique", typeof(bool));
        for (var i = 0; i < FieldCount; i++) schema.Rows.Add(GetName(i), i, -1, GetFieldType(i), true, false, false);
        return schema;
    }
}
