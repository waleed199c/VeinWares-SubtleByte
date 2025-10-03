using HarmonyLib;
using ProjectM;
using System;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Patches
{
    [HarmonyPatch(typeof(SpawnTeamSystem_OnPersistenceLoad), nameof(SpawnTeamSystem_OnPersistenceLoad.OnUpdate))]
    internal static class InitializationPatch
    {
        static void Postfix()
        {
            try
            {
                Core.Initialize();

                if (Core._hasInitialized)
                {
                    //SBlog.Info("[Core] Initialization complete. Unpatching...");
                    Plugin.Harmony.Unpatch(
                        typeof(SpawnTeamSystem_OnPersistenceLoad).GetMethod("OnUpdate"),
                        typeof(InitializationPatch).GetMethod("Postfix")
                    );
                }
            }
            catch (Exception ex)
            {
                SBlog.Error($"[Core] Initialization failed: {ex.Message}");
            }
        }
    }
}