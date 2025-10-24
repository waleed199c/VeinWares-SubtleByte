using System;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Infrastructure;
using VeinWares.SubtleByte.Services.FactionInfamy;

namespace VeinWares.SubtleByte.Modules.FactionInfamy;

internal sealed class FactionInfamyModule : IModule, IUpdateModule
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

        _enabled = context.Config.InfamySystemEnabled.Value && SubtleBytePluginConfig.InfamySystemEnabled;
        if (!_enabled)
        {
            context.Log.LogInfo("[Infamy] Faction Infamy system disabled via configuration.");
            return;
        }

        FactionInfamyRuntime.Initialize(context.Log);
        FactionInfamyChatConfig.Initialize(context.Log);

        var snapshot = FactionInfamyConfig.CreateSnapshot();
        FactionInfamyAmbushData.Initialize(context.Log);
        FactionInfamySystem.Initialize(snapshot, context.Log);
        FactionInfamyAmbushService.Initialize(context.Log);

        _autosaveHandle = context.Scheduler.Schedule(
            snapshot.AutosaveInterval,
            FactionInfamySystem.FlushPersistence,
            runImmediately: false);
        _autosaveRegistered = true;

        context.Log.LogInfo("[Infamy] Faction Infamy module initialised.");
    }

    public void OnUpdate(float deltaTime)
    {
        if (!_enabled || _disposed)
        {
            return;
        }

        FactionInfamyRuntime.ProcessQueues();

        FactionInfamySystem.Tick(deltaTime);

        FactionInfamyRuntime.ProcessQueues();
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
            FactionInfamySystem.FlushPersistence();
            FactionInfamySystem.Shutdown();
            FactionInfamyRuntime.Shutdown();
            FactionInfamyAmbushService.Shutdown();
            FactionInfamyAmbushData.Shutdown();
            FactionInfamyChatConfig.Shutdown();
        }

        _disposed = true;
    }
}
