using System.Buffers.Binary;

namespace LT1Diagnostics.Acquisition.RawSessions;

internal static class RawSessionFormat
{
    public const ushort FileVersion = 1;
    public const ushort RecordVersion = 1;
    public const int PreambleLength = 16;
    public const int RecordHeaderLength = 48;
    public const int MaximumPayloadLength = 16 * 1024 * 1024;
    public const uint RecordMagic = 0x3152444C;

    public static ReadOnlySpan<byte> FileMagic => "LT1RAW\r\n"u8;

    public static void WritePreamble(Span<byte> destination)
    {
        if (destination.Length < PreambleLength)
        {
            throw new ArgumentException("The destination is too small for the raw-session preamble.", nameof(destination));
        }

        destination[..PreambleLength].Clear();
        FileMagic.CopyTo(destination);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[8..], FileVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[10..], PreambleLength);
    }

    public static ushort ValidatePreamble(ReadOnlySpan<byte> preamble)
    {
        if (preamble.Length != PreambleLength || !preamble[..8].SequenceEqual(FileMagic))
        {
            throw new InvalidDataException("The stream is not an LT1Diagnostics raw-session file.");
        }

        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(preamble[8..]);
        ushort length = BinaryPrimitives.ReadUInt16LittleEndian(preamble[10..]);
        if (length != PreambleLength)
        {
            throw new InvalidDataException("The raw-session preamble length is invalid.");
        }

        if (version != FileVersion)
        {
            throw new NotSupportedException($"Raw-session file version {version} is not supported.");
        }

        return version;
    }
}

