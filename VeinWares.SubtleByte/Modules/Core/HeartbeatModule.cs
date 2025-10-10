using System;
using VeinWares.SubtleByte.Infrastructure;

namespace VeinWares.SubtleByte.Modules.Core;

internal sealed class HeartbeatModule : IModule
{
    private Runtime.Scheduling.IntervalScheduler.ScheduledHandle _handle;
    private bool _disposed;

    public void Initialize(ModuleContext context)
    {
        _handle = context.Scheduler.Schedule(TimeSpan.FromSeconds(30), () =>
        {
            context.Log.LogDebug("[Heartbeat] SubtleByte module host is running.");
        }, runImmediately: true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _handle.Dispose();
        _disposed = true;
    }
}
