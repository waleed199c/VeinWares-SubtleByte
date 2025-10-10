using BepInEx.Logging;
using HarmonyLib;
using VeinWares.SubtleByte.Infrastructure.Diagnostics;
using VeinWares.SubtleByte.Runtime.Scheduling;

namespace VeinWares.SubtleByte.Infrastructure;

public sealed class ModuleContext
{
    public ModuleContext(
        ManualLogSource log,
        IntervalScheduler scheduler,
        Harmony harmony,
        PerformanceTracker performanceTracker,
        ModuleConfig config)
    {
        Log = log;
        Scheduler = scheduler;
        Harmony = harmony;
        Performance = performanceTracker;
        Config = config;
    }

    public ManualLogSource Log { get; }

    public IntervalScheduler Scheduler { get; }

    public Harmony Harmony { get; }

    public PerformanceTracker Performance { get; }

    public ModuleConfig Config { get; }
}
