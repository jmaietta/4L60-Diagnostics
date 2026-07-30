using LT1Diagnostics.Protocol.A276;

namespace LT1Diagnostics.Acquisition.A276;

public sealed record A276TransmissionObservation(
    long MonotonicTimestamp,
    A276TransmissionSample Sample);
