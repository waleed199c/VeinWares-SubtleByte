using System;
using System.Collections.Generic;

namespace VeinWares.SubtleByte.Runtime.Scheduling;

public sealed class IntervalScheduler : IDisposable
{
    private readonly List<ScheduledAction> _actions = new();
    private bool _disposed;
    private int _nextId = 1;

    public ScheduledHandle Schedule(TimeSpan interval, Action action, bool runImmediately = false)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(IntervalScheduler));
        }

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");
        }

        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        var scheduled = new ScheduledAction(_nextId++, interval, action, runImmediately);
        _actions.Add(scheduled);
        return new ScheduledHandle(this, scheduled.Id);
    }

    public void Update(float deltaTime)
    {
        if (_disposed || _actions.Count == 0)
        {
            return;
        }

        var delta = TimeSpan.FromSeconds(Math.Max(0f, deltaTime));

        for (var index = 0; index < _actions.Count; index++)
        {
            var entry = _actions[index];
            if (!entry.IsActive)
            {
                continue;
            }

            entry.Accumulator += delta;
            if (entry.RunImmediately)
            {
                entry.RunImmediately = false;
                SafeInvoke(entry.Callback);
                entry.Accumulator = TimeSpan.Zero;
            }
            else if (entry.Accumulator >= entry.Interval)
            {
                entry.Accumulator -= entry.Interval;
                SafeInvoke(entry.Callback);
            }

            _actions[index] = entry;
        }

        _actions.RemoveAll(static action => !action.IsActive);
    }

    public void Cancel(int id)
    {
        for (var index = 0; index < _actions.Count; index++)
        {
            if (_actions[index].Id == id)
            {
                var entry = _actions[index];
                entry.IsActive = false;
                _actions[index] = entry;
                break;
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _actions.Clear();
    }

    private static readonly BepInEx.Logging.ManualLogSource SchedulerLog =
        BepInEx.Logging.Logger.CreateLogSource("SubtleByte.Scheduler");

    private static void SafeInvoke(Action callback)
    {
        try
        {
            callback();
        }
        catch (Exception ex)
        {
            SchedulerLog.LogError($"Scheduled action threw: {ex}");
        }
    }

    private struct ScheduledAction
    {
        public ScheduledAction(int id, TimeSpan interval, Action callback, bool runImmediately)
        {
            Id = id;
            Interval = interval;
            Callback = callback;
            RunImmediately = runImmediately;
            Accumulator = TimeSpan.Zero;
            IsActive = true;
        }

        public int Id { get; }
        public TimeSpan Interval { get; }
        public Action Callback { get; }
        public bool RunImmediately { get; set; }
        public TimeSpan Accumulator { get; set; }
        public bool IsActive { get; set; }
    }

    public readonly struct ScheduledHandle : IDisposable
    {
        private readonly IntervalScheduler? _scheduler;
        private readonly int _id;

        internal ScheduledHandle(IntervalScheduler scheduler, int id)
        {
            _scheduler = scheduler;
            _id = id;
        }

        public void Dispose()
        {
            _scheduler?.Cancel(_id);
        }
    }
}
