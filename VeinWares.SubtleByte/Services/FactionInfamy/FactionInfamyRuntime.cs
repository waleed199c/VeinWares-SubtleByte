using System;
using System.Collections.Concurrent;
using BepInEx.Logging;

#nullable enable

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamyRuntime
{
    private static readonly ConcurrentQueue<Action> MainThreadActions = new();
    private static ManualLogSource? _log;
    private static bool _initialized;

    public static event Action<FactionInfamyPlayerSnapshot>? PlayerHateChanged;
    public static event Action<ulong>? PlayerHateCleared;

    public static void Initialize(ManualLogSource log)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        _log = log;
        _initialized = true;
    }

    public static void Shutdown()
    {
        _initialized = false;
        _log = null;

        while (MainThreadActions.TryDequeue(out _))
        {
        }

        PlayerHateChanged = null;
        PlayerHateCleared = null;
    }

    public static void ProcessQueues()
    {
        if (!_initialized)
        {
            return;
        }

        while (MainThreadActions.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _log?.LogError($"[Infamy] Exception while running queued action: {ex}");
            }
        }
    }

    public static void NotifyPlayerHateChanged(FactionInfamyPlayerSnapshot snapshot)
    {
        if (!_initialized)
        {
            return;
        }

        Enqueue(() =>
        {
            try
            {
                PlayerHateChanged?.Invoke(snapshot);
            }
            catch (Exception ex)
            {
                _log?.LogError($"[Infamy] Listener threw while handling hate update for {snapshot.SteamId}: {ex}");
            }
        });
    }

    public static void NotifyPlayerHateCleared(ulong steamId)
    {
        if (!_initialized)
        {
            return;
        }

        Enqueue(() =>
        {
            try
            {
                PlayerHateCleared?.Invoke(steamId);
            }
            catch (Exception ex)
            {
                _log?.LogError($"[Infamy] Listener threw while handling hate clear for {steamId}: {ex}");
            }
        });
    }

    private static void Enqueue(Action action)
    {
        if (action is null)
        {
            return;
        }

        MainThreadActions.Enqueue(action);
    }
}
