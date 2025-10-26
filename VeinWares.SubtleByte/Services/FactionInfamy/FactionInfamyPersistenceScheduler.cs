using System;
using System.Collections.Generic;
using BepInEx.Logging;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Runtime.Scheduling;

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamyPersistenceScheduler
{
    private static ManualLogSource? _log;
    private static PersistenceSettings _settings = null!;
    private static IntervalScheduler.ScheduledHandle _handle;
    private static bool _handleRegistered;
    private static bool _initialized;
    private static Func<Dictionary<string, PlayerHateRecord>>? _snapshotFactory;
    private static bool _dirty;

    public static void Initialize(
        PersistenceSettings settings,
        ManualLogSource log,
        IntervalScheduler scheduler,
        Func<Dictionary<string, PlayerHateRecord>> snapshotFactory)
    {
        if (scheduler is null)
        {
            throw new ArgumentNullException(nameof(scheduler));
        }

        if (snapshotFactory is null)
        {
            throw new ArgumentNullException(nameof(snapshotFactory));
        }

        Shutdown();

        _log = log ?? throw new ArgumentNullException(nameof(log));
        _settings = settings;
        _snapshotFactory = snapshotFactory;

        _handle = scheduler.Schedule(settings.AutosaveInterval, Persist, runImmediately: false);
        _handleRegistered = true;
        _initialized = true;
    }

    public static void MarkDirty()
    {
        _dirty = true;
    }

    public static void FlushNow()
    {
        if (!_initialized)
        {
            return;
        }

        Persist();
    }

    public static void Shutdown()
    {
        if (_handleRegistered)
        {
            _handle.Dispose();
            _handleRegistered = false;
        }

        if (_initialized)
        {
            Persist();
        }

        _initialized = false;
        _snapshotFactory = null;
        _log = null;
    }

    private static void Persist()
    {
        if (!_dirty || _snapshotFactory is null)
        {
            return;
        }

        try
        {
            var snapshot = _snapshotFactory();
            if (snapshot is null)
            {
                return;
            }

            FactionInfamyPersistence.Save(snapshot, _settings.AutosaveBackupCount);
            _dirty = false;
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Infamy] Failed to save hate data: {ex.Message}");
        }
    }
}
