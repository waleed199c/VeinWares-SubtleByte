using System;
using VeinWares.SubtleByte.Template.Infrastructure;

namespace VeinWares.SubtleByte.Template.Modules.Core;

public sealed class HeartbeatModule : IModule
{
    private Runtime.Scheduling.IntervalScheduler.ScheduledHandle _handle;
    private bool _disposed;

    public void Initialize(ModuleContext context)
    {
        _handle = context.Scheduler.Schedule(TimeSpan.FromSeconds(30), () =>
        {
            context.Log.LogDebug("[Heartbeat] SubtleByte template host is running.");
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
