using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using VampireCommandFramework;
using VeinWares.SubtleByte.Config;

namespace VeinWares.SubtleByte
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("gg.deca.VampireCommandFramework")]
    public class Plugin : BasePlugin
    {
        private Harmony _harmony;
        internal static Plugin Instance { get; set; }
        public static Harmony Harmony => Instance._harmony;
        internal static ManualLogSource LogInstance => Instance.Log;
        public override void Load()
        {
            Instance = this;
            if (Application.productName != "VRisingServer")
                return;
            Core.Log.LogInfo($"[Bootstrap] {MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loading...");
            ItemStackConfig.Load();
            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());


            CommandRegistry.RegisterAll();
        }

        public override bool Unload()
        {
            CommandRegistry.UnregisterAssembly();
            _harmony?.UnpatchSelf();
            return true;
        }
    }

}
