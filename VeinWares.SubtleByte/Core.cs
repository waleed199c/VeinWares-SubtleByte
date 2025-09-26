using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using ProjectM;
using ProjectM.Scripting;
using System;
using System.Collections;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using VeinWares.SubtleByte.Services;
using VeinWares.SubtleByte.Utilities;
using Object = UnityEngine.Object;

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

        private static GameObject _coroutineGO;
        private static CoroutineRunner _runner;
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
            catch (Exception e) { Log.LogError($"[Coroutine] RunNextFrame error: {e}"); }
        }

        private static IEnumerator RunDelayedCo(float seconds, Action action)
        {
            yield return new WaitForSeconds(seconds);
            try { action(); }
            catch (Exception e) { Log.LogError($"[Coroutine] RunDelayed error: {e}"); }
        }
    }

}
