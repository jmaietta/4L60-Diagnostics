using LT1Diagnostics.Protocol.A276;

namespace LT1Diagnostics.Protocol.Scheduling;

public sealed record AldlScheduledRequest(
    string RequestId,
    byte DatasetId,
    TimeSpan Period,
    TimeSpan InitialDelay);

public sealed record AldlDueRequest(
    string RequestId,
    byte DatasetId,
    ReadOnlyMemory<byte> Frame,
    long DueTimestamp);

public sealed class AldlRequestSchedule
{
    private readonly IReadOnlyList<ScheduleState> _states;

    public AldlRequestSchedule(IEnumerable<AldlScheduledRequest> requests, long startTimestamp)
    {
        ArgumentNullException.ThrowIfNull(requests);
        AldlScheduledRequest[] definitions = requests.ToArray();
        if (definitions.Length == 0)
        {
            throw new ArgumentException("At least one scheduled request is required.", nameof(requests));
        }

        if (definitions.Select(request => request.RequestId).Distinct(StringComparer.Ordinal).Count() != definitions.Length)
        {
            throw new ArgumentException("Scheduled request IDs must be unique.", nameof(requests));
        }

        _states = definitions.Select(definition =>
        {
            if (string.IsNullOrWhiteSpace(definition.RequestId))
            {
                throw new ArgumentException("A scheduled request ID cannot be blank.", nameof(requests));
            }

            if (definition.Period <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(requests), "Every scheduled request needs an explicit positive period.");
            }

            if (definition.InitialDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(requests), "A scheduled request delay cannot be negative.");
            }

            _ = A276MessageFactory.GetMode1DataByteCount(definition.DatasetId);
            return new ScheduleState(
                definition,
                checked(startTimestamp + definition.InitialDelay.Ticks));
        }).ToArray();
    }

    public IReadOnlyList<AldlDueRequest> TakeDue(long timestamp)
    {
        var due = new List<AldlDueRequest>();
        foreach (ScheduleState state in _states.OrderBy(state => state.NextDueTimestamp).ThenBy(state => state.Definition.RequestId, StringComparer.Ordinal))
        {
            if (state.NextDueTimestamp > timestamp)
            {
                continue;
            }

            long originalDue = state.NextDueTimestamp;
            due.Add(new AldlDueRequest(
                state.Definition.RequestId,
                state.Definition.DatasetId,
                A276MessageFactory.CreateMode1Request(state.Definition.DatasetId),
                originalDue));

            do
            {
                state.NextDueTimestamp = checked(state.NextDueTimestamp + state.Definition.Period.Ticks);
            }
            while (state.NextDueTimestamp <= timestamp);
        }

        return due;
    }

    private sealed class ScheduleState(AldlScheduledRequest definition, long nextDueTimestamp)
    {
        public AldlScheduledRequest Definition { get; } = definition;

        public long NextDueTimestamp { get; set; } = nextDueTimestamp;
    }
}
