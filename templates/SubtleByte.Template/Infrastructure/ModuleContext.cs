using BepInEx.Logging;
using HarmonyLib;
using VeinWares.SubtleByte.Template.Infrastructure.Diagnostics;
using VeinWares.SubtleByte.Template.Runtime.Scheduling;

namespace VeinWares.SubtleByte.Template.Infrastructure;

public sealed class ModuleContext
{
    public ModuleContext(ManualLogSource log, IntervalScheduler scheduler, Harmony harmony, PerformanceTracker performanceTracker)
    {
        Log = log;
        Scheduler = scheduler;
        Harmony = harmony;
        Performance = performanceTracker;
    }

    public ManualLogSource Log { get; }

    public IntervalScheduler Scheduler { get; }

    public Harmony Harmony { get; }

    public PerformanceTracker Performance { get; }
}
