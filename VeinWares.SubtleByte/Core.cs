using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using ProjectM;
using ProjectM.Scripting;
using System;
using System.Collections;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Services;
using VeinWares.SubtleByte.Utilities;
using Object = UnityEngine.Object;

namespace VeinWares.SubtleByte
{
    internal static class Core
    {
        private static readonly object _worldSync = new();
        private static World? _server;
        private static SystemService? _systemService;

        public static World Server => TryResolveServerWorld() ?? throw new InvalidOperationException("Server world is not available.");
        public static EntityManager EntityManager => Server.EntityManager;
        public static ManualLogSource Log => Plugin.LogInstance;

        public static PrefabCollectionSystem PrefabCollectionSystem { get; set; }
        public static ServerScriptMapper ServerScriptMapper { get; set; }
        public static ServerGameManager ServerGameManager => SystemService.ServerScriptMapper.GetServerGameManager();
        public static SystemService SystemService
        {
            get
            {
                var world = TryResolveServerWorld();
                if (world == null)
                {
                    throw new InvalidOperationException("Server world is not available.");
                }

                return _systemService ??= new SystemService(world);
            }
        }

        private static CoroutineRunner _runner;
        public static bool _hasInitialized = false;
        private static bool _initializationWarningLogged;

        public static void Initialize()
        {
            if (_hasInitialized) return;

            lock (_worldSync)
            {
                if (_hasInitialized)
                {
                    return;
                }

                var serverWorld = TryResolveServerWorld();
                if (serverWorld == null)
                {
                    if (!_initializationWarningLogged)
                    {
                        ModLogger.Warn("[Core] Initialization deferred; waiting for the Server world to be created.");
                        _initializationWarningLogged = true;
                    }

                    return;
                }

                _initializationWarningLogged = false;

                ModLogger.Info("[Core] Initialization started...");

                PrefabCollectionSystem = serverWorld.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (SubtleBytePluginConfig.ItemStackServiceEnabled)
                {
                    ItemStackService.ApplyPatches();
                }
                //RecipeService.ApplyPatches();

                _hasInitialized = true;
                ModLogger.Info("[Core] Initialization complete.");
            }
        }
        static World? TryResolveServerWorld()
        {
            lock (_worldSync)
            {
                if (_server != null && _server.IsCreated)
                {
                    return _server;
                }

                var world = World.s_AllWorlds.ToArray().FirstOrDefault(world => world.Name == "Server");
                if (world == null || !world.IsCreated)
                {
                    _server = null;
                    _systemService = null;
                    return null;
                }

                if (!ReferenceEquals(_server, world))
                {
                    _systemService = null;
                }

                _server = world;
                return _server;
            }
        }

        private static void EnsureCoroutineRunner()
        {
            if (_runner == null)
            {
                var go = new GameObject("SubtleByteCoroutineRunner");
                GameObject.DontDestroyOnLoad(go);
                _runner = go.AddComponent<CoroutineRunner>();
                Object.DontDestroyOnLoad(go);
            }
        }

        // ---------- Public API ----------

        public static Coroutine StartCoroutine(IEnumerator routine)
         {
             EnsureCoroutineRunner();
             return _runner.StartCoroutine(routine.WrapToIl2Cpp());
         }
        public static void StopCoroutine(Coroutine coroutine)
        {
            if (_runner == null || coroutine == null) return;
            _runner.StopCoroutine(coroutine);
        }

        public static void StopAllCoroutines()
        {
            if (_runner != null)
                _runner.StopAllCoroutines();
        }

        public static void RunNextFrame(Action action)
        {
            if (action == null) return;
            StartCoroutine(RunNextFrameCo(action));
        }

        public static void RunDelayed(float seconds, Action action)
        {
            if (action == null) return;
            StartCoroutine(RunDelayedCo(seconds, action));
        }

        // ---------- IEnumerators ----------

        private static IEnumerator RunNextFrameCo(Action action)
        {
            yield return null;
            try { action(); }
            catch (Exception e) { ModLogger.Error($"[Coroutine] RunNextFrame error: {e}"); }
        }

        private static IEnumerator RunDelayedCo(float seconds, Action action)
        {
            yield return new WaitForSeconds(seconds);
            try { action(); }
            catch (Exception e) { ModLogger.Error($"[Coroutine] RunDelayed error: {e}"); }
        }
    }

}
