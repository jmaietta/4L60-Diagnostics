using LT1Diagnostics.Domain.Definitions;
using LT1Diagnostics.Domain.Diagnostics;

namespace LT1Diagnostics.Analysis;

public static class TransmissionSessionAnalyzer
{
    public static TransmissionSessionAnalysis Analyze(IReadOnlyList<TransmissionObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (observations.Count == 0)
        {
            return new TransmissionSessionAnalysis(
                0,
                TimeSpan.Zero,
                null,
                null,
                null,
                null,
                [],
                VerificationStatus.Unverified,
                "No transmission samples were available for analysis.");
        }

        TransmissionObservation[] ordered = observations
            .OrderBy(observation => observation.Elapsed)
            .ToArray();
        var events = new List<TransmissionEvent>();
        for (int index = 1; index < ordered.Length; index++)
        {
            TransmissionObservation previous = ordered[index - 1];
            TransmissionObservation current = ordered[index];
            if (current.CommandedGear != previous.CommandedGear)
            {
                events.Add(new TransmissionEvent(
                    current.Elapsed,
                    TransmissionEventKind.CommandedGearChanged,
                    $"PCM command changed from gear {previous.CommandedGear} to gear {current.CommandedGear}."));
            }

            if (current.TccControlCommanded != previous.TccControlCommanded)
            {
                events.Add(new TransmissionEvent(
                    current.Elapsed,
                    TransmissionEventKind.TccCommandChanged,
                    current.TccControlCommanded
                        ? "PCM began commanding torque-converter-clutch control."
                        : "PCM stopped commanding torque-converter-clutch control."));
            }

            if (current.TccEnabled != previous.TccEnabled)
            {
                events.Add(new TransmissionEvent(
                    current.Elapsed,
                    TransmissionEventKind.TccEnableStateChanged,
                    current.TccEnabled
                        ? "The reported torque-converter-clutch enable state turned on."
                        : "The reported torque-converter-clutch enable state turned off."));
            }
        }

        VerificationStatus status = ordered.All(item => item.VerificationStatus == VerificationStatus.Verified)
            ? VerificationStatus.Verified
            : VerificationStatus.Unverified;
        return new TransmissionSessionAnalysis(
            ordered.Length,
            ordered[^1].Elapsed - ordered[0].Elapsed,
            Range(ordered.Select(item => item.EngineSpeedRpm)),
            Range(ordered.Select(item => item.VehicleSpeedMph)),
            Range(ordered.Select(item => item.SlipRpm)),
            Range(ordered.Select(item => item.TransmissionFluidTemperatureCelsius)),
            events.AsReadOnly(),
            status,
            status == VerificationStatus.Verified
                ? "Observed events are descriptive; condition-matched baselines are required before classifying them as normal or abnormal."
                : "Documentary decoding is awaiting vehicle validation. Events are descriptive and are not repair conclusions.");
    }

    private static ObservedRange Range(IEnumerable<double> values)
    {
        double[] materialized = values.ToArray();
        return new ObservedRange(materialized.Min(), materialized.Max());
    }
}
