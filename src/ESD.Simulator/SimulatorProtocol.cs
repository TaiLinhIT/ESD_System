using ESD.Core;

namespace ESD.Simulator;

/// <summary>
/// Self-contained CRC-16 + frame builder — mirrors EsdProtocol exactly
/// so the simulator produces frames identical to real hardware.
/// (We duplicate here so the simulator exe has no dependency on ESD.Device internals.)
/// </summary>
internal static class SimProtocol
{
    public const byte Stx     = 0x7E;
    public const byte Etx     = 0x7F;
    public const byte Version = 0x01;

    // ── CRC-16/IBM (Modbus) ─────────────────────────────────────────────────
    public static ushort Crc16(ReadOnlySpan<byte> data)
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

    // ── Frame builder ────────────────────────────────────────────────────────
    /// <summary>
    /// Builds: STX | VER | LEN(2) | ADDR | CMD | SEQ | DATA | CRC16(2) | ETX
    /// CRC covers: VER + LEN(2) + ADDR + CMD + SEQ + DATA
    /// </summary>
    public static byte[] BuildFrame(byte address, EsdCommand cmd, byte seq, byte[]? data = null)
    {
        data ??= [];
        ushort dataLen      = (ushort)data.Length;
        int    crcBodyLen   = 6 + data.Length;

        Span<byte> crcBody = stackalloc byte[crcBodyLen];
        crcBody[0] = Version;
        crcBody[1] = (byte)(dataLen >> 8);
        crcBody[2] = (byte)(dataLen & 0xFF);
        crcBody[3] = address;
        crcBody[4] = (byte)cmd;
        crcBody[5] = seq;
        data.CopyTo(crcBody[6..]);

        ushort crc = Crc16(crcBody);

        byte[] frame = new byte[1 + crcBodyLen + 2 + 1];
        int i = 0;
        frame[i++] = Stx;
        crcBody.CopyTo(frame.AsSpan(i)); i += crcBodyLen;
        frame[i++] = (byte)(crc & 0xFF);
        frame[i++] = (byte)(crc >> 8);
        frame[i]   = Etx;
        return frame;
    }

    // ── Try parse (for receiving commands from the ESD app) ─────────────────
    public static bool TryParse(ReadOnlySpan<byte> buf, out ParsedFrame? frame, out int consumed)
    {
        frame = null; consumed = 0;
        int stx = buf.IndexOf(Stx);
        if (stx < 0)  { consumed = buf.Length; return false; }
        if (stx > 0)  { consumed = stx; return false; }
        if (buf.Length < 10) return false;

        ushort dataLen  = (ushort)((buf[2] << 8) | buf[3]);
        int    total    = 10 + dataLen;
        if (buf.Length < total) return false;
        if (buf[total - 1] != Etx) { consumed = 1; return false; }

        var    crcRegion = buf.Slice(1, 6 + dataLen);
        ushort expected  = Crc16(crcRegion);
        ushort actual    = (ushort)(buf[7 + dataLen] | buf[8 + dataLen] << 8);
        if (expected != actual) { consumed = 1; return false; }

        frame = new ParsedFrame(
            Address : buf[4],
            Command : (EsdCommand)buf[5],
            Sequence: buf[6],
            Data    : buf.Slice(7, dataLen).ToArray());
        consumed = total;
        return true;
    }
}

internal sealed record ParsedFrame(byte Address, EsdCommand Command, byte Sequence, byte[] Data);
