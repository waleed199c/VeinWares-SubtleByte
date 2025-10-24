using System;
using System.Diagnostics;
using System.IO;
using BepInEx.Logging;

#nullable enable

namespace VeinWares.SubtleByte.Infrastructure.Diagnostics;

public sealed class PerformanceTracker
{
    private const long DefaultMaxLogBytes = 5L * 1024 * 1024; // 5 MiB

    private readonly ManualLogSource _log;
    private readonly double _thresholdMilliseconds;
    private readonly string? _logFilePath;
    private readonly bool _isEnabled;
    private readonly Stopwatch _stopwatch = new();
    private readonly object _fileLock = new();
    private readonly long _maxLogBytes;

    public PerformanceTracker(ManualLogSource log, double thresholdMilliseconds, string? logFilePath = null, long? maxLogBytes = null, bool isEnabled = true)
    {
        _log = log;
        _thresholdMilliseconds = thresholdMilliseconds;
        _maxLogBytes = Math.Max(1024, maxLogBytes ?? DefaultMaxLogBytes);
        _isEnabled = isEnabled;

        if (!_isEnabled)
        {
            _logFilePath = null;
            return;
        }

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

        if (!_isEnabled)
        {
            action();
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
                var message = $"[Perf] {label} took {elapsedMs:F2} ms (threshold {_thresholdMilliseconds:F2} ms).";
                WriteEntry(message);
            }
        }
    }

    private void WriteEntry(string message)
    {
        if (!_isEnabled)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_logFilePath))
        {
            lock (_fileLock)
            {
                File.AppendAllText(_logFilePath!, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
                EnforceFileSizeLimit();
            }
            return;
        }

        _log.LogWarning(message);
    }

    private void EnforceFileSizeLimit()
    {
        if (string.IsNullOrEmpty(_logFilePath))
        {
            return;
        }

        try
        {
            var info = new FileInfo(_logFilePath);
            if (!info.Exists || info.Length <= _maxLogBytes)
            {
                return;
            }

            var backupPath = _logFilePath + ".bak";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Move(_logFilePath!, backupPath);
            File.WriteAllText(_logFilePath!, string.Empty);
            _log.LogInfo($"[Perf] Performance log rotated to '{backupPath}'.");
        }
        catch (Exception ex)
        {
            _log.LogWarning($"[Perf] Failed to rotate performance log '{_logFilePath}': {ex.Message}.");
        }
    }
}
