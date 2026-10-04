namespace ESD.Device.Protocol;

/// <summary>CRC-16/IBM (Modbus variant) — poly 0xA001, init 0xFFFF.</summary>
internal static class Crc16
{
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1);
        }
        return crc;
    }

    public static bool Validate(ReadOnlySpan<byte> frameWithCrc)
    {
        // last 2 bytes are CRC (little-endian), before ETX
        if (frameWithCrc.Length < 2) return false;
        ushort expected = Compute(frameWithCrc[..^2]);
        ushort actual   = (ushort)(frameWithCrc[^2] | frameWithCrc[^1] << 8);
        return expected == actual;
    }

    public static (byte Lo, byte Hi) ToBytes(ushort crc) =>
        ((byte)(crc & 0xFF), (byte)(crc >> 8));
}
