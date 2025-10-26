using System;
using System.Collections;
using BepInEx.Logging;
using UnityEngine;
using VeinWares.SubtleByte;

#nullable enable

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamyAutosaveTimer
{
    private static ManualLogSource? _log;
    private static Coroutine? _routine;
    private static WaitForSeconds? _delay;
    private static Action? _flushAction;
    private static bool _active;

    public static void Initialize(TimeSpan interval, ManualLogSource log, Action flushAction)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (flushAction is null)
        {
            throw new ArgumentNullException(nameof(flushAction));
        }

        Shutdown();

        _log = log;
        _flushAction = flushAction;

        if (interval <= TimeSpan.Zero)
        {
            _log.LogWarning("[Infamy] Autosave timer disabled because the configured interval is not positive.");
            return;
        }

        var seconds = Math.Max(1f, (float)interval.TotalSeconds);
        _delay = new WaitForSeconds(seconds);
        _routine = Core.StartCoroutine(RunTimer());
        _active = true;
        _log.LogInfo($"[Infamy] Autosave timer started ({seconds:0.#}s interval).");
    }

    public static void Shutdown()
    {
        if (_routine is not null)
        {
            Core.StopCoroutine(_routine);
            _routine = null;
        }

        _delay = null;

        if (_active)
        {
            _log?.LogInfo("[Infamy] Autosave timer stopped.");
            _active = false;
        }

        _flushAction = null;
        _log = null;
    }

    private static IEnumerator RunTimer()
    {
        while (true)
        {
            if (_delay is null)
            {
                yield break;
            }

            yield return _delay;

            try
            {
                _flushAction?.Invoke();
            }
            catch (Exception ex)
            {
                _log?.LogError($"[Infamy] Autosave flush failed: {ex.Message}");
            }
        }
    }
}
