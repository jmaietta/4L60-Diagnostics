using System.Diagnostics;

namespace LT1Diagnostics.Transport.Abstractions;

public static class MonotonicClock
{
    public static long GetTimestamp() => Stopwatch.GetElapsedTime(0, Stopwatch.GetTimestamp()).Ticks;
}
