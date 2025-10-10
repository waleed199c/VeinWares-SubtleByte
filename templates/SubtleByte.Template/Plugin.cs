using System.IO;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using VeinWares.SubtleByte.Template.Infrastructure;
using VeinWares.SubtleByte.Template.Infrastructure.Diagnostics;
using VeinWares.SubtleByte.Template.Modules.Core;
using VeinWares.SubtleByte.Template.Runtime.Unity;

namespace VeinWares.SubtleByte.Template;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("gg.deca.VampireCommandFramework", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "veinwares.subtlebyte.template";
    public const string PluginName = "SubtleByte Performance Template";
    public const string PluginVersion = "0.0.1";

    private ModuleHost? _moduleHost;
    private ServerBootstrap? _bootstrap;

    public override void Load()
    {
        if (Application.productName != "VRisingServer")
        {
            Log.LogWarning($"{PluginName} is designed for the dedicated server build. Skipping initialization for '{Application.productName}'.");
            return;
        }

        ClassInjector.RegisterTypeInIl2Cpp<ModuleHostBehaviour>();

        var performanceLogPath = Path.Combine(Paths.ConfigPath, "VeinWares SubtleByte", "template-performance.log");
        var performanceTracker = new PerformanceTracker(Log, thresholdMilliseconds: 5.0, performanceLogPath);
        _moduleHost = ModuleHost.Create(Log, performanceTracker, new[]
        {
            () => new HeartbeatModule(),
        });

        _moduleHost.Initialize();
        _bootstrap = ServerBootstrap.Start(_moduleHost, Log);

        Log.LogInfo($"{PluginName} {PluginVersion} initialized with {_moduleHost.ModuleCount} module(s).");
    }

    public override bool Unload()
    {
        _bootstrap?.Dispose();
        _bootstrap = null;

        _moduleHost?.Dispose();
        _moduleHost = null;

        return true;
    }
}
