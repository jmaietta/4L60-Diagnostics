using System.Collections.Frozen;
using LT1Diagnostics.Protocol.A276;
using LT1Diagnostics.Protocol.Aldl;
using LT1Diagnostics.Transport.Abstractions;

namespace LT1Diagnostics.Simulator;

public enum SimulatorScenarioId
{
    HealthyIdle,
    HealthyRoadTest,
    NormalShifts,
    DelayedOneTwoShift,
    RpmFlare,
    ShiftTieUp,
    FailedTwoThreeShift,
    TccExcessiveSlip,
    TccCycling,
    TftSensorOpen,
    TftSensorShort,
    TftSensorIntermittent,
    PressureControlElectricalFault,
    PacketLoss,
    BadChecksum,
    SerialEcho,
    BusChatter,
    UnexpectedModuleMessages,
    DeviceDisconnectReconnect,
}

public sealed record SimulatorStep(
    TimeSpan Offset,
    TransportChunkKind Kind,
    ReadOnlyMemory<byte> Bytes,
    TransportQuality Quality = TransportQuality.None,
    string? Detail = null);

public sealed record SimulatorScenario(
    SimulatorScenarioId Id,
    string DisplayName,
    IReadOnlyList<SimulatorStep> Steps,
    bool ContainsVerifiedProtocolData = false);

public static class SimulatorScenarioCatalog
{
    private static readonly FrozenDictionary<SimulatorScenarioId, SimulatorScenario> Scenarios =
        Enum.GetValues<SimulatorScenarioId>()
            .ToFrozenDictionary(id => id, CreateScenario);

    public static IReadOnlyCollection<SimulatorScenario> All => Scenarios.Values;

    public static SimulatorScenario Get(SimulatorScenarioId id) => Scenarios[id];

    private static SimulatorScenario CreateScenario(SimulatorScenarioId id)
    {
        string displayName = SplitName(id.ToString());
        var flags = TransportQuality.None;
        if (id is SimulatorScenarioId.BadChecksum)
        {
            flags = TransportQuality.SourceReportedCorrupt | TransportQuality.SimulatedFault;
        }
        else if (id is SimulatorScenarioId.SerialEcho)
        {
            flags = TransportQuality.Echo | TransportQuality.SimulatedFault;
        }
        else if (id is SimulatorScenarioId.BusChatter or SimulatorScenarioId.UnexpectedModuleMessages)
        {
            flags = TransportQuality.UnexpectedTraffic | TransportQuality.SimulatedFault;
        }
        else if (id is not SimulatorScenarioId.HealthyIdle and not SimulatorScenarioId.HealthyRoadTest and not SimulatorScenarioId.NormalShifts)
        {
            flags = TransportQuality.SimulatedFault;
        }

        var steps = new List<SimulatorStep>
        {
            new(TimeSpan.Zero, TransportChunkKind.Connected, ReadOnlyMemory<byte>.Empty, Detail: "Simulator connected."),
            new(TimeSpan.FromMilliseconds(10), TransportChunkKind.Data, CreateFrame(id, 0), flags,
                "Synthetic values in a documentary A276 envelope; not vehicle evidence."),
            new(TimeSpan.FromMilliseconds(20), TransportChunkKind.Data, CreateFrame(id, 1), flags,
                "Synthetic values in a documentary A276 envelope; not vehicle evidence."),
            new(TimeSpan.FromMilliseconds(30), TransportChunkKind.Data, CreateFrame(id, 2), flags,
                "Synthetic values in a documentary A276 envelope; not vehicle evidence."),
        };

        if (id == SimulatorScenarioId.PacketLoss)
        {
            steps.RemoveAt(2);
        }

        if (id == SimulatorScenarioId.DeviceDisconnectReconnect)
        {
            steps.Insert(2, new SimulatorStep(
                TimeSpan.FromMilliseconds(15),
                TransportChunkKind.Disconnected,
                ReadOnlyMemory<byte>.Empty,
                TransportQuality.SimulatedFault,
                "Deterministic simulated removal."));
            steps.Insert(3, new SimulatorStep(
                TimeSpan.FromMilliseconds(18),
                TransportChunkKind.Connected,
                ReadOnlyMemory<byte>.Empty,
                TransportQuality.SimulatedFault,
                "Deterministic simulated reconnect."));
        }

        return new SimulatorScenario(id, displayName, steps.AsReadOnly());
    }

    private static byte[] CreateFrame(SimulatorScenarioId id, byte sequence)
    {
        var data = new byte[A276MessageFactory.GetMode1DataByteCount(1)];
        data[5] = checked((byte)(36 + sequence));
        data[7] = 0x19;
        data[8] = checked((byte)(sequence * 8));
        data[9] = sequence;
        data[10] = 90;
        data[11] = 51;
        data[12] = 51;
        data[13] = 128;
        data[14] = 1 << 6;
        data[15] = 132;
        data[16] = 0;
        data[29] = 0;
        data[30] = sequence;
        data[31] = checked((byte)(80 + sequence));
        data[32] = id switch
        {
            SimulatorScenarioId.TftSensorOpen => 0,
            SimulatorScenarioId.TftSensorShort => byte.MaxValue,
            SimulatorScenarioId.TftSensorIntermittent when sequence == 1 => byte.MaxValue,
            _ => checked((byte)(70 + sequence)),
        };

        if (id == SimulatorScenarioId.PressureControlElectricalFault)
        {
            data[2] = 1 << 3;
        }

        byte deviceAddress = id == SimulatorScenarioId.UnexpectedModuleMessages
            ? (byte)0xE4
            : A276MessageFactory.DeviceAddress;
        byte[] frame = AldlFrameBuilder.Build(deviceAddress, 0x01, data);
        if (id == SimulatorScenarioId.BadChecksum && sequence == 1)
        {
            frame[^1] ^= 0x01;
        }

        return frame;
    }

    private static string SplitName(string value) => string.Concat(
        value.Select((character, index) => index > 0 && char.IsUpper(character)
            ? $" {character}"
            : character.ToString()));
}
