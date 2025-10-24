using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using VampireCommandFramework;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Infrastructure;
using VeinWares.SubtleByte.Infrastructure.Diagnostics;
using VeinWares.SubtleByte.Modules.Core;
using VeinWares.SubtleByte.Modules.Crafting;
using VeinWares.SubtleByte.Runtime.Unity;
using VeinWares.SubtleByte.Services;
using VeinWares.SubtleByte.Utilities;
using VeinWares.SubtleByte.Modules.FactionInfamy;

namespace VeinWares.SubtleByte
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("gg.deca.VampireCommandFramework")]
    public class Plugin : BasePlugin
    {
        private Harmony _harmony;
        private ModuleHost _moduleHost;
        private ServerBootstrap _serverBootstrap;

        internal static Plugin Instance { get; set; }
        public static Harmony Harmony => Instance._harmony;
        internal static ManualLogSource LogInstance => Instance.Log;
        public override void Load()
        {
            Instance = this;
            if (Application.productName != "VRisingServer")
                return;

            SubtleBytePluginConfig.Initialize();
            ModLogger.Info($"[Bootstrap] {MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loading...");
            ClassInjector.RegisterTypeInIl2Cpp<CoroutineRunner>();
            ClassInjector.RegisterTypeInIl2Cpp<ModuleHostBehaviour>();
            ItemStackConfig.Load();
            PrestigeMini.InitializePrestigeConfig();
            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            PrestigeLiveSync.Initialize();

            var performanceLogDirectory = Path.Combine(Paths.BepInExRootPath, "Performance");
            var performanceLogPath = Path.Combine(performanceLogDirectory, "performance.log");
            var performanceTracker = new PerformanceTracker(Log, thresholdMilliseconds: 5.0, performanceLogPath, isEnabled: false);
            var moduleConfig = new ModuleConfig(
                SubtleBytePluginConfig.EmptyBottleRefundEnabledEntry,
                SubtleBytePluginConfig.InfamySystemEnabledEntry);
            _moduleHost = ModuleHost.Create(Log, performanceTracker, new Func<IModule>[]
            {
                () => new HeartbeatModule(),
                () => new BottleRefundModule(),
                () => new FactionInfamyModule(),
            }, moduleConfig);

            _moduleHost.Initialize();
            _serverBootstrap = ServerBootstrap.Start(_moduleHost, Log);

            CommandRegistry.RegisterAll();
        }

        public override bool Unload()
        {
            CommandRegistry.UnregisterAssembly();

            _serverBootstrap?.Dispose();
            _serverBootstrap = null;

            _moduleHost?.Dispose();
            _moduleHost = null;

            _harmony?.UnpatchSelf();
            return true;
        }
    }

}
