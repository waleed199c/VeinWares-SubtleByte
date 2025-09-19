using HarmonyLib;
using ProjectM;
using System;

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
                    //Core.Log.LogInfo("[Core] Initialization complete. Unpatching...");
                    Plugin.Harmony.Unpatch(
                        typeof(SpawnTeamSystem_OnPersistenceLoad).GetMethod("OnUpdate"),
                        typeof(InitializationPatch).GetMethod("Postfix")
                    );
                }
            }
            catch (Exception ex)
            {
                Core.Log.LogError($"[Core] Initialization failed: {ex.Message}");
            }
        }
    }
}
