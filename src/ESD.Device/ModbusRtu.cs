namespace ESD.Device;

public static class ModbusRtu
{
    public static byte[] BuildReadHoldingRegisters(byte slaveId, ushort startAddress, ushort quantity)
    {
        var frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = 0x03;
        frame[2] = (byte)(startAddress >> 8);
        frame[3] = (byte)startAddress;
        frame[4] = (byte)(quantity >> 8);
        frame[5] = (byte)quantity;
        var crc = Crc16(frame.AsSpan(0, 6));
        frame[6] = (byte)crc;
        frame[7] = (byte)(crc >> 8);
        return frame;
    }

    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1);
        }
        return crc;
    }

    public static bool ValidateCrc(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 4) return false;
        var expected = Crc16(frame[..^2]);
        var actual = (ushort)(frame[^2] | frame[^1] << 8);
        return expected == actual;
    }
}
