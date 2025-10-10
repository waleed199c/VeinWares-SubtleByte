using System;
using System.Diagnostics;
using BepInEx.Logging;

namespace VeinWares.SubtleByte.Rewrite.Infrastructure.Diagnostics;

public sealed class PerformanceTracker
{
    private readonly ManualLogSource _log;
    private readonly double _thresholdMilliseconds;

    public PerformanceTracker(ManualLogSource log, double thresholdMilliseconds)
    {
        _log = log;
        _thresholdMilliseconds = Math.Max(0.1, thresholdMilliseconds);
    }

    public void Measure(string label, Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();

        if (stopwatch.Elapsed.TotalMilliseconds > _thresholdMilliseconds)
        {
            _log.LogWarning($"[Perf] {label} took {stopwatch.Elapsed.TotalMilliseconds:F2} ms (threshold {_thresholdMilliseconds:F2} ms).");
        }
    }
}
