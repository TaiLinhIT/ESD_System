using ESD.Core;

namespace ESD.Device.Protocol;

/// <summary>
/// ESD Protocol V1 frame layout:
/// ┌─────┬─────┬──────┬──────┬─────┬─────┬────────┬───────┬─────┐
/// │ STX │ VER │ LEN  │ ADDR │ CMD │ SEQ │  DATA  │ CRC16 │ ETX │
/// ├─────┼─────┼──────┼──────┼─────┼─────┼────────┼───────┼─────┤
/// │  1  │  1  │  2   │  1   │  1  │  1  │   N    │   2   │  1  │
/// └─────┴─────┴──────┴──────┴─────┴─────┴────────┴───────┴─────┘
/// LEN = number of DATA bytes only.
/// CRC covers: VER + LEN(2) + ADDR + CMD + SEQ + DATA.
/// </summary>
public sealed class EsdProtocol : IProtocol
{
    public const byte Stx     = 0x7E;
    public const byte Etx     = 0x7F;
    public const byte Version = 0x01;

    public const int MaxData = 255; // limit to prevent runaway frames

    // Header offsets (after STX)
    private const int OffVer  = 1;
    private const int OffLenH = 2;
    private const int OffLenL = 3;
    private const int OffAddr = 4;
    private const int OffCmd  = 5;
    private const int OffSeq  = 6;
    private const int OffData = 7;

    /// <summary>Minimum frame length with zero data bytes.</summary>
    private const int MinFrame = 10; // STX VER LEN(2) ADDR CMD SEQ CRC(2) ETX

    public byte[] BuildFrame(byte address, EsdCommand command, byte sequence, byte[]? data = null)
    {
        data ??= [];
        ushort dataLen = (ushort)data.Length;

        // Assemble the portion that is covered by CRC:
        // VER + LEN(2) + ADDR + CMD + SEQ + DATA
        int crcPayloadLen = 6 + data.Length;
        Span<byte> crcPayload = stackalloc byte[crcPayloadLen];
        crcPayload[0] = Version;
        crcPayload[1] = (byte)(dataLen >> 8);
        crcPayload[2] = (byte)(dataLen & 0xFF);
        crcPayload[3] = address;
        crcPayload[4] = (byte)command;
        crcPayload[5] = sequence;
        data.CopyTo(crcPayload[6..]);

        var (crcLo, crcHi) = Crc16.ToBytes(Crc16.Compute(crcPayload));

        // Full frame: STX + crcPayload + CRC(2) + ETX
        byte[] frame = new byte[1 + crcPayloadLen + 2 + 1];
        int i = 0;
        frame[i++] = Stx;
        crcPayload.CopyTo(frame.AsSpan(i));
        i += crcPayloadLen;
        frame[i++] = crcLo;
        frame[i++] = crcHi;
        frame[i]   = Etx;

        return frame;
    }

    public bool TryParse(ReadOnlySpan<byte> buffer, out EsdFrame? frame, out int consumed)
    {
        frame    = null;
        consumed = 0;

        // Find STX
        int stxPos = buffer.IndexOf(Stx);
        if (stxPos < 0) { consumed = buffer.Length; return false; } // discard all

        // Discard bytes before STX
        if (stxPos > 0)
        {
            consumed = stxPos;
            return false;
        }

        // Need at least MinFrame bytes
        if (buffer.Length < MinFrame) return false;

        // Read LEN (big-endian)
        ushort dataLen = (ushort)((buffer[OffLenH] << 8) | buffer[OffLenL]);

        // Bounded data length — prevent runaway frames from rogue bytes
        if (dataLen > MaxData)
        {
            consumed = 1;
            return false; // resync caller
        }

        int totalFrame = MinFrame + dataLen;

        if (buffer.Length < totalFrame) return false; // wait for more bytes

        // Verify ETX
        if (buffer[totalFrame - 1] != Etx)
        {
            // Corrupt frame — skip past this STX and let caller retry
            consumed = 1;
            return false;
        }

        // Validate CRC over: VER + LEN(2) + ADDR + CMD + SEQ + DATA
        var crcRegion = buffer.Slice(OffVer, 6 + dataLen);  // before the 2 CRC bytes
        ushort expected = Crc16.Compute(crcRegion);
        int crcOffset   = OffData + dataLen;
        ushort actual   = (ushort)(buffer[crcOffset] | buffer[crcOffset + 1] << 8);
        if (expected != actual)
        {
            consumed = 1; // skip STX, resync
            throw new InvalidDataException(
                $"CRC mismatch: expected 0x{expected:X4}, got 0x{actual:X4}");
        }

        var data = buffer.Slice(OffData, dataLen).ToArray();
        frame = new EsdFrame(
            Version:  buffer[OffVer],
            Address:  buffer[OffAddr],
            Command:  (EsdCommand)buffer[OffCmd],
            Sequence: buffer[OffSeq],
            Data:     data);

        consumed = totalFrame;
        return true;
    }
}
