using LT1Diagnostics.Domain.Definitions;

namespace LT1Diagnostics.Domain.Diagnostics;

public sealed record TransmissionObservation(
    TimeSpan Elapsed,
    double EngineSpeedRpm,
    double VehicleSpeedMph,
    int CommandedGear,
    double SlipRpm,
    double TransmissionFluidTemperatureCelsius,
    double TransmissionIgnitionVoltage,
    double CurrentTorqueSignalPressurePsi,
    double ReferenceForceMotorCurrentAmps,
    double ActualForceMotorCurrentAmps,
    bool TccControlCommanded,
    bool TccEnabled,
    bool ShiftSolenoidACommanded,
    bool ShiftSolenoidBCommanded,
    VerificationStatus VerificationStatus);

public enum TransmissionEventKind
{
    CommandedGearChanged,
    TccCommandChanged,
    TccEnableStateChanged,
}

public sealed record TransmissionEvent(
    TimeSpan Elapsed,
    TransmissionEventKind Kind,
    string Summary);

public sealed record ObservedRange(double Minimum, double Maximum);

public sealed record TransmissionSessionAnalysis(
    int SampleCount,
    TimeSpan Duration,
    ObservedRange? EngineSpeedRpm,
    ObservedRange? VehicleSpeedMph,
    ObservedRange? SlipRpm,
    ObservedRange? TransmissionFluidTemperatureCelsius,
    IReadOnlyList<TransmissionEvent> Events,
    VerificationStatus VerificationStatus,
    string InterpretationBoundary);
