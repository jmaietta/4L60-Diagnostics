using LT1Diagnostics.Protocol.Aldl;

namespace LT1Diagnostics.Protocol.A276;

public static class A276MessageFactory
{
    public const byte DeviceAddress = 0xF4;

    public const int BaudRate = 8192;

    private static readonly IReadOnlyDictionary<byte, int> Mode1DataByteCounts =
        new Dictionary<byte, int>
        {
            [0] = 60,
            [1] = 46,
            [2] = 53,
            [4] = 45,
            [6] = 38,
        };

    public static IReadOnlyCollection<byte> SupportedMode1Datasets { get; } = [0, 1, 2, 4, 6];

    public static byte[] CreateMode1Request(byte datasetId)
    {
        EnsureSupportedDataset(datasetId);
        return AldlFrameBuilder.Build(DeviceAddress, 0x01, [datasetId]);
    }

    public static byte[] CreateDisableNormalCommunicationsRequest() =>
        AldlFrameBuilder.Build(DeviceAddress, 0x08, []);

    public static byte[] CreateEnableNormalCommunicationsRequest() =>
        AldlFrameBuilder.Build(DeviceAddress, 0x09, []);

    public static byte[] CreateReturnToNormalModeRequest() =>
        AldlFrameBuilder.Build(DeviceAddress, 0x00, []);

    public static bool TryIdentifyMode1Dataset(AldlFrame frame, out byte datasetId)
    {
        ArgumentNullException.ThrowIfNull(frame);
        datasetId = 0;
        if (frame.DeviceAddress != DeviceAddress || frame.Mode != 0x01 || !frame.ChecksumValid)
        {
            return false;
        }

        foreach ((byte candidate, int dataByteCount) in Mode1DataByteCounts)
        {
            if (frame.Payload.Length == dataByteCount)
            {
                datasetId = candidate;
                return true;
            }
        }

        return false;
    }

    public static int GetMode1DataByteCount(byte datasetId)
    {
        EnsureSupportedDataset(datasetId);
        return Mode1DataByteCounts[datasetId];
    }

    private static void EnsureSupportedDataset(byte datasetId)
    {
        if (!Mode1DataByteCounts.ContainsKey(datasetId))
        {
            throw new ArgumentOutOfRangeException(nameof(datasetId), datasetId, "A276 does not document this Mode 1 dataset.");
        }
    }
}
