using System.Buffers.Binary;
using LT1Diagnostics.Domain.Definitions;
using LT1Diagnostics.Protocol.Aldl;

namespace LT1Diagnostics.Protocol.A276;

public sealed record A276LoggedDtc(
    int Code,
    string SourceTitle,
    int DataByteOffset,
    int Bit);

public sealed record A276TransmissionSample(
    double ThrottleVoltage,
    int ThrottleRawCounts,
    double EngineSpeedRpm,
    double VehicleSpeedMph,
    double CurrentTorqueSignalPressurePsi,
    double ReferenceForceMotorCurrentAmps,
    double ActualForceMotorCurrentAmps,
    double ForceMotorDutyCyclePercent,
    byte RangeFlags,
    double TransmissionIgnitionVoltage,
    int CommandedGear,
    double LatestOneTwoShiftErrorSeconds,
    double LatestTwoThreeShiftErrorSeconds,
    double SlipRpm,
    double LatestOneTwoShiftTimeSeconds,
    double LatestTwoThreeShiftTimeSeconds,
    int TransmissionPromId,
    double ThreeTwoSolenoidDutyCyclePercent,
    double OutputSpeedRpm,
    double CoolantTemperatureCelsius,
    double TransmissionFluidTemperatureCelsius,
    double TccDutyCyclePercent,
    bool TccControlCommanded,
    bool TccEnabled,
    bool ShiftSolenoidACommanded,
    bool ShiftSolenoidBCommanded,
    IReadOnlyList<A276LoggedDtc> LoggedTransmissionDtcs,
    ReadOnlyMemory<byte> RawDataBytes,
    VerificationStatus VerificationStatus);

public static class A276TransmissionDecoder
{
    private static readonly IReadOnlyList<A276LoggedDtc> TransmissionDtcMap =
    [
        new(24, "OUTPUT SPEED LOW", 0, 7),
        new(76, "LONG SYSTEM VOLTAGE HIGH", 2, 0),
        new(75, "SYSTEM VOLTAGE LOW", 2, 1),
        new(74, "ASR ACTIVE FAULT", 2, 2),
        new(73, "FORCE MOTOR CURRENT", 2, 3),
        new(72, "OUTPUT SPEED LOSS", 2, 4),
        new(59, "TRANSMISSION TEMPERATURE LOW", 2, 5),
        new(58, "TRANSMISSION TEMPERATURE HIGH", 2, 6),
        new(28, "PRESSURE SWITCH MANIFOLD", 2, 7),
        new(86, "LOW RATIO", 3, 0),
        new(85, "TCC STUCK ON", 3, 1),
        new(84, "3-2 DOWNSHIFT FEEDBACK FAULT (ODM)", 3, 2),
        new(83, "TCC CONTROL FEEDBACK FAULT (ODM)", 3, 3),
        new(82, "SHIFT A SOLENOID FAULT (ODM)", 3, 4),
        new(81, "SHIFT B SOLENOID FAULT (ODM)", 3, 5),
        new(80, "TRANSMISSION COMPONENT SLIPPING", 3, 6),
        new(79, "TRANSMISSION OVER TEMPERATURE", 3, 7),
        new(94, "TRANS MANUAL LIGHT FAULT (ODM)", 4, 0),
        new(93, "SERVICE VEH. SOON LIGHT FAULT (ODM)", 4, 1),
        new(92, "TRANS PERF LIGHT FAULT (ODM)", 4, 2),
        new(91, "1-4, 2-5 SHIFT LIGHT FAULT (ODM)", 4, 3),
        new(90, "TCC ENABLE FAULT (ODM)", 4, 4),
        new(89, "MAX ADAPT AND LONG SHIFT", 4, 5),
        new(87, "HIGH RATIO", 4, 7),
    ];

    public static IReadOnlyList<A276LoggedDtc> LoggedTransmissionDtcDefinitions => TransmissionDtcMap;

    public static A276TransmissionSample DecodeMode1Message1(AldlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!A276MessageFactory.TryIdentifyMode1Dataset(frame, out byte datasetId) || datasetId != 1)
        {
            throw new ArgumentException("The frame is not a valid A276 Mode 1 Message 1 response.", nameof(frame));
        }

        ReadOnlySpan<byte> data = frame.Payload.Span;
        ushort engineSpeedRaw = BinaryPrimitives.ReadUInt16BigEndian(data[7..9]);
        short slipRaw = BinaryPrimitives.ReadInt16BigEndian(data[21..23]);
        ushort promId = BinaryPrimitives.ReadUInt16BigEndian(data[25..27]);
        ushort outputSpeedRaw = BinaryPrimitives.ReadUInt16BigEndian(data[29..31]);
        ushort tccDutyRaw = BinaryPrimitives.ReadUInt16BigEndian(data[33..35]);
        byte outputModeWord = data[37];

        return new A276TransmissionSample(
            data[5] * (5.0 / 255.0),
            data[6],
            engineSpeedRaw / 8.0,
            data[9] / 2.0,
            data[10],
            data[11] / 51.2,
            data[12] / 51.2,
            data[13] / 2.55,
            data[14],
            data[15] / 10.0,
            data[16] + 1,
            data[19] / 40.0,
            data[20] / 40.0,
            slipRaw / 8.0,
            data[23] / 40.0,
            data[24] / 40.0,
            promId,
            data[27] / 2.55,
            outputSpeedRaw / 8.0,
            (data[31] * 0.75) - 40.0,
            (data[32] * 0.75) - 40.0,
            tccDutyRaw / 655.36,
            IsSet(outputModeWord, 0),
            IsSet(outputModeWord, 1),
            IsSet(outputModeWord, 2),
            IsSet(outputModeWord, 3),
            DecodeLoggedTransmissionDtcs(data),
            data.ToArray(),
            VerificationStatus.Unverified);
    }

    private static IReadOnlyList<A276LoggedDtc> DecodeLoggedTransmissionDtcs(ReadOnlySpan<byte> data)
    {
        var result = new List<A276LoggedDtc>();
        foreach (A276LoggedDtc definition in TransmissionDtcMap)
        {
            if (IsSet(data[definition.DataByteOffset], definition.Bit))
            {
                result.Add(definition);
            }
        }

        return result.AsReadOnly();
    }

    private static bool IsSet(byte value, int bit) => (value & (1 << bit)) != 0;
}
