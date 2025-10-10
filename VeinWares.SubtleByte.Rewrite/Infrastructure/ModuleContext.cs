using BepInEx.Logging;
using HarmonyLib;
using VeinWares.SubtleByte.Rewrite.Configuration;
using VeinWares.SubtleByte.Rewrite.Infrastructure.Diagnostics;
using VeinWares.SubtleByte.Rewrite.Runtime.Scheduling;

namespace VeinWares.SubtleByte.Rewrite.Infrastructure;

public sealed class ModuleContext
{
    public ModuleContext(
        ManualLogSource log,
        IntervalScheduler scheduler,
        Harmony harmony,
        PerformanceTracker performanceTracker,
        RewriteConfig config)
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

    public RewriteConfig Config { get; }
}
