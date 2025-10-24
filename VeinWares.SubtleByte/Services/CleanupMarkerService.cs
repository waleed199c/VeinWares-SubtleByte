using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;

namespace VeinWares.SubtleByte.Services;

internal static class CleanupMarkerService
{
    private const string MarkerFileName = "cleanup_done.json";

    private static readonly HashSet<string> WatchedPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<FileSystemWatcher> Watchers = new();
    private static ManualLogSource? _log;

    public static void Initialize(ManualLogSource log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));

        Shutdown();

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Paths.ConfigPath,
            Path.Combine(Paths.ConfigPath, "VeinWares SubtleByte")
        };

        foreach (var directory in candidates)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                _log?.LogDebug($"[CleanupGuard] Skipping directory '{directory}' due to error: {ex.Message}");
                continue;
            }

            var markerPath = Path.Combine(directory, MarkerFileName);
            TryDeleteMarker(markerPath);
            TryWatchDirectory(directory, markerPath);
        }
    }

    public static void Shutdown()
    {
        foreach (var watcher in Watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnMarkerChanged;
                watcher.Changed -= OnMarkerChanged;
                watcher.Renamed -= OnMarkerRenamed;
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                _log?.LogDebug($"[CleanupGuard] Failed to dispose watcher: {ex.Message}");
            }
        }

        Watchers.Clear();
        WatchedPaths.Clear();
    }

    private static void TryWatchDirectory(string directory, string markerPath)
    {
        if (WatchedPaths.Contains(directory))
        {
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(directory, MarkerFileName)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            watcher.Created += OnMarkerChanged;
            watcher.Changed += OnMarkerChanged;
            watcher.Renamed += OnMarkerRenamed;

            Watchers.Add(watcher);
            WatchedPaths.Add(directory);

            // Delete again in case the marker was created between initial delete and watcher creation.
            TryDeleteMarker(markerPath);
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[CleanupGuard] Failed to watch '{directory}' for cleanup markers: {ex.Message}");
        }
    }

    private static void OnMarkerChanged(object sender, FileSystemEventArgs e)
    {
        if (!string.Equals(Path.GetFileName(e.Name), MarkerFileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TryDeleteMarker(e.FullPath);
    }

    private static void OnMarkerRenamed(object sender, RenamedEventArgs e)
    {
        if (!string.Equals(Path.GetFileName(e.Name), MarkerFileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TryDeleteMarker(e.FullPath);
    }

    private static void TryDeleteMarker(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            File.Delete(path);
            _log?.LogInfo($"[CleanupGuard] Removed unexpected cleanup marker at '{path}'.");
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[CleanupGuard] Failed to delete cleanup marker '{path}': {ex.Message}");
        }
    }
}
