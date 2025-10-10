using System;
using System.Diagnostics;
using BepInEx.Logging;

namespace VeinWares.SubtleByte.Infrastructure.Diagnostics;

internal sealed class PerformanceTracker
{
    private readonly ManualLogSource _log;
    private readonly double _thresholdMilliseconds;
    private readonly Stopwatch _stopwatch = new();

    public PerformanceTracker(ManualLogSource log, double thresholdMilliseconds)
    {
        _log = log;
        _thresholdMilliseconds = thresholdMilliseconds;
    }

    public void Measure(string label, Action action)
    {
        if (action is null)
        {
            return;
        }

        _stopwatch.Restart();
        try
        {
            action();
        }
        finally
        {
            _stopwatch.Stop();
            var elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;
            if (elapsedMs >= _thresholdMilliseconds)
            {
                _log.LogWarning($"[Perf] {label} took {elapsedMs:F2} ms (threshold {_thresholdMilliseconds:F2} ms).");
            }
        }
    }
}
