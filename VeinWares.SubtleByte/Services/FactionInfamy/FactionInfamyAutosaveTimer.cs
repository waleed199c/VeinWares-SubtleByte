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
    private static WaitForSecondsRealtime? _delay;
    private static Action? _flushAction;
    private static bool _active;
    private static float _intervalSeconds;

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
        _delay = new WaitForSecondsRealtime(seconds);
        _intervalSeconds = seconds;
        _active = true;
        _log.LogInfo($"[Infamy] Autosave timer started ({seconds:0.#}s interval).");
        _routine = Core.StartCoroutine(RunTimer());
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
        _intervalSeconds = 0f;
    }

    private static IEnumerator RunTimer()
    {
        while (true)
        {
            if (_delay is null)
            {
                yield break;
            }

            var intervalSeconds = _intervalSeconds;

            if (intervalSeconds > 0f)
            {
                var eta = DateTimeOffset.Now.AddSeconds(intervalSeconds);
                _log?.LogInfo($"[Infamy] Autosave timer cycle started; next flush in {intervalSeconds:0.#}s (ETA {eta:HH:mm:ss}).");
            }

            yield return _delay;

            _log?.LogInfo("[Infamy] Autosave timer interval elapsed; invoking flush.");

            try
            {
                _flushAction?.Invoke();
            }
            catch (Exception ex)
            {
                _log?.LogError($"[Infamy] Autosave flush failed: {ex.Message}");
            }

            if (intervalSeconds > 0f)
            {
                var nextTarget = DateTimeOffset.Now.AddSeconds(intervalSeconds);
                _log?.LogInfo($"[Infamy] Autosave timer cycle complete; next flush scheduled for {nextTarget:HH:mm:ss}.");
            }
        }
    }
}
