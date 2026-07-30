using System.Buffers.Binary;

namespace LT1Diagnostics.Acquisition.RawSessions;

public sealed class RawSessionWriter : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly SemaphoreSlim _appendGate = new(1, 1);
    private long _nextSequence;
    private bool _disposed;

    public RawSessionWriter(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite || !stream.CanSeek)
        {
            throw new ArgumentException("The raw-session stream must be writable and seekable.", nameof(stream));
        }

        _stream = stream;
        _leaveOpen = leaveOpen;
        InitializeStream();
    }

    public async ValueTask<long> AppendAsync(
        RawSessionRecordType recordType,
        long monotonicTimestamp,
        DateTimeOffset wallClockTimestamp,
        RawSessionRecordAttributes attributes,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (payload.Length > RawSessionFormat.MaximumPayloadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "The payload exceeds the format limit.");
        }

        await _appendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            long sequence = _nextSequence++;
            var header = new byte[RawSessionFormat.RecordHeaderLength];
            BinaryPrimitives.WriteUInt32LittleEndian(header, RawSessionFormat.RecordMagic);
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), (ushort)recordType);
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), RawSessionFormat.RecordVersion);
            BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(8), sequence);
            BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(16), monotonicTimestamp);
            BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(24), wallClockTimestamp.UtcTicks);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(32), (uint)attributes);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(36), payload.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(40), Crc32.Compute(payload.Span));
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(44), Crc32.Compute(header.AsSpan(0, 44)));

            _stream.Seek(0, SeekOrigin.End);
            // Once a record commit begins, finish its header and payload as one logical
            // append. Cancellation may prevent entry through the gate, but must not tear
            // a record after its header has reached the session stream.
            await _stream.WriteAsync(header, CancellationToken.None).ConfigureAwait(false);
            await _stream.WriteAsync(payload, CancellationToken.None).ConfigureAwait(false);
            return sequence;
        }
        finally
        {
            _appendGate.Release();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _appendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _appendGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _appendGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _stream.FlushAsync().ConfigureAwait(false);
            if (!_leaveOpen)
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
            }

            _disposed = true;
        }
        finally
        {
            _appendGate.Release();
            _appendGate.Dispose();
        }
    }

    private void InitializeStream()
    {
        if (_stream.Length == 0)
        {
            var preamble = new byte[RawSessionFormat.PreambleLength];
            RawSessionFormat.WritePreamble(preamble);
            _stream.Write(preamble);
            _nextSequence = 0;
            return;
        }

        _stream.Position = 0;
        var preambleBuffer = new byte[RawSessionFormat.PreambleLength];
        ReadExactly(_stream, preambleBuffer);
        RawSessionFormat.ValidatePreamble(preambleBuffer);

        long nextSequence = 0;
        var header = new byte[RawSessionFormat.RecordHeaderLength];
        while (_stream.Position < _stream.Length)
        {
            int headerRead = ReadUpTo(_stream, header);
            if (headerRead != header.Length)
            {
                throw new InvalidDataException("Cannot append after a truncated record header; preserve the file for recovery.");
            }

            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != RawSessionFormat.RecordMagic)
            {
                throw new InvalidDataException("Cannot append after an invalid record header.");
            }

            int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(36));
            if (payloadLength is < 0 or > RawSessionFormat.MaximumPayloadLength ||
                _stream.Length - _stream.Position < payloadLength)
            {
                throw new InvalidDataException("Cannot append after a truncated or invalid record payload.");
            }

            nextSequence = checked(BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(8)) + 1);
            _stream.Seek(payloadLength, SeekOrigin.Current);
        }

        _nextSequence = nextSequence;
        _stream.Seek(0, SeekOrigin.End);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int total = ReadUpTo(stream, buffer);
        if (total != buffer.Length)
        {
            throw new InvalidDataException("The raw-session preamble is truncated.");
        }
    }

    private static int ReadUpTo(Stream stream, Span<byte> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer[total..]);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
