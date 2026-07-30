using LT1Diagnostics.Protocol.Scheduling;

namespace LT1Diagnostics.Protocol.Tests;

public sealed class AldlRequestScheduleTests
{
    [Fact]
    public void ScheduleUsesOnlyExplicitPeriodsAndMaintainsStableOrder()
    {
        var schedule = new AldlRequestSchedule(
        [
            new("transmission", 1, TimeSpan.FromTicks(20), TimeSpan.Zero),
            new("identity", 4, TimeSpan.FromTicks(100), TimeSpan.FromTicks(10)),
        ],
        startTimestamp: 1_000);

        AldlDueRequest first = Assert.Single(schedule.TakeDue(1_000));
        Assert.Equal("transmission", first.RequestId);
        Assert.Empty(schedule.TakeDue(1_009));
        AldlDueRequest second = Assert.Single(schedule.TakeDue(1_010));
        Assert.Equal("identity", second.RequestId);
        AldlDueRequest third = Assert.Single(schedule.TakeDue(1_020));
        Assert.Equal("transmission", third.RequestId);
    }

    [Fact]
    public void ScheduleRejectsUndocumentedDataset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AldlRequestSchedule(
        [
            new("unknown", 3, TimeSpan.FromSeconds(1), TimeSpan.Zero),
        ],
        startTimestamp: 0));
    }
}
