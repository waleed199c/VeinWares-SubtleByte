using Stunlock.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Unity.Entities;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Services
{
    public static class OneTimeCleanupService
    {
        // Put the old buff GUIDs here (from your list)
        private static readonly PrefabGUID[] OldBuffs =
        {
            new PrefabGUID(-773025435),
            new PrefabGUID(1359282533),
            new PrefabGUID(-1055766373),
            new PrefabGUID(894725875),
            new PrefabGUID(-993492354),
            new PrefabGUID(-536284884),
            new PrefabGUID(-901503997),
            new PrefabGUID(-488475343),
            new PrefabGUID(-1104282069),
        };

        private static readonly string StateFile = Path.Combine(
            BepInEx.Paths.ConfigPath, "VeinWares SubtleByte", "cleanup_done.json"
        );

        private static HashSet<ulong> _done;
        private static bool _loaded;

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
                if (!File.Exists(StateFile)) { _done = new HashSet<ulong>(); return; }

                var json = File.ReadAllText(StateFile);
                var ids = JsonSerializer.Deserialize<List<ulong>>(json) ?? new List<ulong>();
                _done = new HashSet<ulong>(ids);
            }
            catch (Exception e)
            {
                _done = new HashSet<ulong>();
                SBlog.Error($"[Cleanup] Failed to load state: {e}");
            }
        }

        private static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_done);
                File.WriteAllText(StateFile, json);
            }
            catch (Exception e)
            {
                SBlog.Error($"[Cleanup] Failed to save state: {e}");
            }
        }

        /// Run once per player: remove any legacy buffs if present, mark done.
        public static void RunOnce(ulong platformId, Entity character)
        {
            EnsureLoaded();
            if (_done.Contains(platformId)) return;
            if (!character.Exists()) return;

            int removed = 0;
            foreach (var guid in OldBuffs)
            {
                if (character.TryGetBuff(guid, out var buffEntity))
                {
                    buffEntity.DestroyBuff();
                    removed++;
                }
            }

            _done.Add(platformId);
            Save();

            if (removed > 0)
                SBlog.Info($"[Cleanup] Removed {removed} legacy buff(s) from {platformId}.");
        }
    }
}
