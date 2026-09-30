namespace ESD.UI.Services;

public class ModbusService
{
    public ushort CalculateCrc16(byte[] data)
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

    public byte[] BuildReadHoldingRegisters(byte slaveId, ushort startAddress, ushort quantity)
    {
        var frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = 0x03;
        frame[2] = (byte)(startAddress >> 8);
        frame[3] = (byte)(startAddress & 0xFF);
        frame[4] = (byte)(quantity >> 8);
        frame[5] = (byte)(quantity & 0xFF);
        ushort crc = CalculateCrc16(frame[..6]);
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)(crc >> 8);
        return frame;
    }
}
