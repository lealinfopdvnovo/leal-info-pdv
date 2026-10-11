using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace LealInfoPDV.Licensing;

internal sealed record MachineLicenseIdentity(string Serial, string LegacySerial, bool HardwareBacked)
{
    private static readonly Lazy<MachineLicenseIdentity> cached = new(Read);
    internal static MachineLicenseIdentity Current => cached.Value;
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetSystemFirmwareTable(uint provider, uint tableId, byte[]? data, uint size);
    private static MachineLicenseIdentity Read()
    {
        var legacy = Database.DeviceSerial();
        try
        {
            const uint rsmb = 0x52534D42;
            var size = GetSystemFirmwareTable(rsmb, 0, null, 0);
            if (size is >= 8 and <= 1048576)
            {
                var bytes = new byte[size];
                if (GetSystemFirmwareTable(rsmb, 0, bytes, size) == size && FromSmbios(bytes) is string serial)
                    return new(serial, legacy, true);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or ExternalException) { }
        // Não simula estabilidade: sem UUID adequado mantém compatibilidade e marca a limitação.
        return new(legacy, legacy, false);
    }
    internal static string? FromSmbios(byte[] raw)
    {
        if (raw.Length < 8) return null;
        var length = BitConverter.ToUInt32(raw, 4);
        if (length > raw.Length - 8) return null;
        var end = 8 + (int)length;
        for (var offset = 8; offset + 4 <= end;)
        {
            var type = raw[offset]; var size = raw[offset + 1];
            if (size < 4 || offset + size > end) return null;
            if (type == 1 && size >= 25)
            {
                var uuid = raw.AsSpan(offset + 8, 16);
                if (uuid.IndexOfAnyExcept((byte)0) < 0 || uuid.IndexOfAnyExcept((byte)255) < 0) return null;
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("LEALINFO-HARDWARE-V1|" + Convert.ToHexString(uuid))));
                return $"LH-{hash[..4]}-{hash[4..8]}-{hash[8..12]}-{hash[12..16]}";
            }
            offset += size;
            while (offset + 1 < end && !(raw[offset] == 0 && raw[offset + 1] == 0)) offset++;
            if (offset + 1 >= end) return null;
            offset += 2;
            if (type == 127) break;
        }
        return null;
    }
}
