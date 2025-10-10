using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using VeinWares.SubtleByte.Rewrite.Configuration;
using VeinWares.SubtleByte.Rewrite.Infrastructure;
using VeinWares.SubtleByte.Rewrite.Infrastructure.Diagnostics;
using VeinWares.SubtleByte.Rewrite.Modules.Core;
using VeinWares.SubtleByte.Rewrite.Modules.Crafting;
using VeinWares.SubtleByte.Rewrite.Runtime.Unity;

namespace VeinWares.SubtleByte.Rewrite;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("gg.deca.VampireCommandFramework", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "veinwares.subtlebyte.rewrite";
    public const string PluginName = "SubtleByte Rewrite";
    public const string PluginVersion = "0.1.0";

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

        var config = new RewriteConfig(Config);
        var performanceTracker = new PerformanceTracker(Log, thresholdMilliseconds: 5.0);
        _moduleHost = ModuleHost.Create(Log, performanceTracker, new[]
        {
            () => new HeartbeatModule(),
            () => new BottleRefundModule(),
        }, config);

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
