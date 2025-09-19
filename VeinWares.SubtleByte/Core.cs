using BepInEx.Logging;
using ProjectM;
using ProjectM.Scripting;
using Stunlock.Core;
using System;
using System.Linq;
using Unity.Entities;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Services;

namespace VeinWares.SubtleByte
{
    internal static class Core
    {
        public static World Server { get; } = GetServerWorld() ?? throw new Exception("There is no Server world!"); public static EntityManager EntityManager => Server.EntityManager;
        public static ManualLogSource Log => Plugin.LogInstance;

        public static PrefabCollectionSystem PrefabCollectionSystem { get; set; }
        public static ServerScriptMapper ServerScriptMapper { get; set; }
        public static ServerGameManager ServerGameManager => SystemService.ServerScriptMapper.GetServerGameManager();
        public static SystemService SystemService { get; } = new(Server);

        public static bool _hasInitialized = false;

        public static void Initialize()
        {
            if (_hasInitialized) return;
            _hasInitialized = true;

            Log?.LogInfo("[Core] Initialization started...");
            PrefabCollectionSystem = Server.GetExistingSystemManaged<PrefabCollectionSystem>();
            ItemStackService.ApplyPatches();
            //RecipeService.ApplyPatches();

            Log?.LogInfo("[Core] Initialization complete.");
        }
        static World GetServerWorld()
        {
            return World.s_AllWorlds.ToArray().FirstOrDefault(world => world.Name == "Server");
        }

    }
}