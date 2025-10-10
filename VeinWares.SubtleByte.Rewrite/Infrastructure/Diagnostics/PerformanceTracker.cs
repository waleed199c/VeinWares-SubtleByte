using System;
using System.Diagnostics;
using System.IO;
using BepInEx.Logging;

#nullable enable

namespace VeinWares.SubtleByte.Rewrite.Infrastructure.Diagnostics;

public sealed class PerformanceTracker
{
    private readonly ManualLogSource _log;
    private readonly double _thresholdMilliseconds;
    private readonly string? _logFilePath;
    private readonly Stopwatch _stopwatch = new();
    private readonly object _fileLock = new();

    public PerformanceTracker(ManualLogSource log, double thresholdMilliseconds, string? logFilePath = null)
    {
        _log = log;
        _thresholdMilliseconds = Math.Max(0.1, thresholdMilliseconds);
        if (!string.IsNullOrWhiteSpace(logFilePath))
        {
            _logFilePath = logFilePath;
            try
            {
                var directory = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(_logFilePath))
                {
                    File.WriteAllText(_logFilePath, string.Empty);
                }

                _log.LogInfo($"[Perf] Performance entries will be written to '{_logFilePath}'.");
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[Perf] Failed to prepare performance log file '{_logFilePath}': {ex.Message}. Falling back to console warnings.");
                _logFilePath = null;
            }
        }
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
            var elapsed = _stopwatch.Elapsed.TotalMilliseconds;
            if (elapsed >= _thresholdMilliseconds)
            {
                var message = $"[Perf] {label} took {elapsed:F2} ms (threshold {_thresholdMilliseconds:F2} ms).";
                WriteEntry(message);
            }
        }
    }

    private void WriteEntry(string message)
    {
        if (!string.IsNullOrEmpty(_logFilePath))
        {
            lock (_fileLock)
            {
                File.AppendAllText(_logFilePath!, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
            }
            return;
        }

        _log.LogWarning(message);
    }
}
