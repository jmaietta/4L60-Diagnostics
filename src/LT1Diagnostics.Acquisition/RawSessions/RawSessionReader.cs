using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace LT1Diagnostics.Acquisition.RawSessions;

public sealed class RawSessionReader
{
    private readonly Stream _stream;

    public RawSessionReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The raw-session stream must be readable.", nameof(stream));
        }

        _stream = stream;
    }

    public async IAsyncEnumerable<RawSessionRecord> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_stream.CanSeek)
        {
            _stream.Position = 0;
        }

        byte[] preamble = new byte[RawSessionFormat.PreambleLength];
        int preambleRead = await ReadUpToAsync(_stream, preamble, cancellationToken).ConfigureAwait(false);
        if (preambleRead != preamble.Length)
        {
            throw new InvalidDataException("The raw-session preamble is truncated.");
        }

        RawSessionFormat.ValidatePreamble(preamble);

        while (true)
        {
            var header = new byte[RawSessionFormat.RecordHeaderLength];
            int headerRead = await ReadUpToAsync(_stream, header, cancellationToken).ConfigureAwait(false);
            if (headerRead == 0)
            {
                yield break;
            }

            if (headerRead != header.Length)
            {
                yield return CreateUnreadableRecord(header.AsMemory(0, headerRead), RawSessionIntegrityStatus.Truncated);
                yield break;
            }

            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != RawSessionFormat.RecordMagic)
            {
                yield return CreateUnreadableRecord(header, RawSessionIntegrityStatus.InvalidRecordMagic);
                yield break;
            }

            ushort typeId = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4));
            ushort version = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6));
            long sequence = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8));
            long monotonicTimestamp = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(16));
            long utcTicks = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(24));
            var attributes = (RawSessionRecordAttributes)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(32));
            int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(36));
            uint expectedPayloadCrc = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(40));
            uint expectedHeaderCrc = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(44));

            if (payloadLength is < 0 or > RawSessionFormat.MaximumPayloadLength)
            {
                yield return new RawSessionRecord(
                    typeId,
                    version,
                    sequence,
                    monotonicTimestamp,
                    FromUtcTicks(utcTicks),
                    attributes,
                    ReadOnlyMemory<byte>.Empty,
                    RawSessionIntegrityStatus.InvalidPayloadLength,
                    header);
                yield break;
            }

            var payload = new byte[payloadLength];
            int payloadRead = await ReadUpToAsync(_stream, payload, cancellationToken).ConfigureAwait(false);
            RawSessionIntegrityStatus status;
            if (payloadRead != payloadLength)
            {
                status = RawSessionIntegrityStatus.Truncated;
                Array.Resize(ref payload, payloadRead);
            }
            else if (Crc32.Compute(header.AsSpan(0, 44)) != expectedHeaderCrc)
            {
                status = RawSessionIntegrityStatus.HeaderChecksumMismatch;
            }
            else if (Crc32.Compute(payload) != expectedPayloadCrc)
            {
                status = RawSessionIntegrityStatus.PayloadChecksumMismatch;
            }
            else if (version != RawSessionFormat.RecordVersion)
            {
                status = RawSessionIntegrityStatus.UnsupportedRecordVersion;
            }
            else
            {
                status = RawSessionIntegrityStatus.Valid;
            }

            yield return new RawSessionRecord(
                typeId,
                version,
                sequence,
                monotonicTimestamp,
                FromUtcTicks(utcTicks),
                attributes,
                payload,
                status,
                header);

            if (status == RawSessionIntegrityStatus.Truncated)
            {
                yield break;
            }
        }
    }

    private static RawSessionRecord CreateUnreadableRecord(
        ReadOnlyMemory<byte> header,
        RawSessionIntegrityStatus status) => new(
            0,
            0,
            -1,
            0,
            DateTimeOffset.UnixEpoch,
            RawSessionRecordAttributes.None,
            ReadOnlyMemory<byte>.Empty,
            status,
            header);

    private static DateTimeOffset FromUtcTicks(long ticks)
    {
        try
        {
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTimeOffset.UnixEpoch;
        }
    }

    private static async Task<int> ReadUpToAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
