using System;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Infrastructure;
using VeinWares.SubtleByte.Services.Wanted;

namespace VeinWares.SubtleByte.Modules.Wanted;

internal sealed class WantedModule : IModule, IUpdateModule
{
    private bool _enabled;
    private bool _disposed;
    private Runtime.Scheduling.IntervalScheduler.ScheduledHandle _autosaveHandle;
    private bool _autosaveRegistered;

    public void Initialize(ModuleContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        _enabled = context.Config.WantedSystemEnabled.Value && SubtleBytePluginConfig.WantedSystemEnabled;
        if (!_enabled)
        {
            context.Log.LogInfo("[Wanted] Wanted system disabled via configuration.");
            return;
        }

        var snapshot = WantedConfig.CreateSnapshot();
        WantedSystem.Initialize(snapshot, context.Log);

        _autosaveHandle = context.Scheduler.Schedule(
            snapshot.AutosaveInterval,
            WantedSystem.FlushPersistence,
            runImmediately: false);
        _autosaveRegistered = true;

        context.Log.LogInfo("[Wanted] Wanted module initialised.");
    }

    public void OnUpdate(float deltaTime)
    {
        if (!_enabled || _disposed)
        {
            return;
        }

        WantedSystem.Tick(deltaTime);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_autosaveRegistered)
        {
            _autosaveHandle.Dispose();
        }

        if (_enabled)
        {
            WantedSystem.FlushPersistence();
            WantedSystem.Shutdown();
        }

        _disposed = true;
    }
}
