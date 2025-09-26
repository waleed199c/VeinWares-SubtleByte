using ProjectM;
using ProjectM.Gameplay.Scripting;
using ProjectM.Network;
using ProjectM.Scripting;
using Stunlock.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using VeinWares.SubtleByte;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Services
{
    internal static class PrestigeMini
    {
        private static PrefabGUID HijackedBuff => new PrefabGUID(SubtleBytePrestigeConfig.HijackedBuffGuid());

        public static void InitializePrestigeConfig()
        {
            SubtleBytePrestigeConfig.LoadOrCreate();
        }

        /// <summary>
        /// Unified entrypoint: ALWAYS remove existing → wait a tick → reapply fresh → rewrite stats.
        /// </summary>
        public static void ApplyLevel(Entity character, int level)
        {
            // Basic validation outside the iterator (safe to throw/log here)
            if (!character.Exists() || !character.IsPlayer())
            {
                Core.Log.LogWarning("[PrestigeMini] invalid player");
                return;
            }

            var user = character.GetUserEntity();
            if (user == Entity.Null || !user.Exists())
            {
                Core.Log.LogWarning("[PrestigeMini] no user");
                return;
            }

            Core.StartCoroutine(ReapplyFresh_Co(character, user, level));
        }

        /// <summary>
        /// Coroutine that guarantees the live buff instance picks up new values.
        /// NOTE: No try/catch here (C# forbids yield inside try/catch).
        /// </summary>
        private static IEnumerator ReapplyFresh_Co(Entity character, Entity user, int level)
        {
            var buffPrefab = HijackedBuff;

            // 1) Remove any existing instance
            Buffs.RemoveBuff(character, buffPrefab);

            // 2) Wait a few frames until it is actually gone (ECS processing)
            for (int i = 0; i < 8; i++)
            {
                if (!BuffUtility.HasBuff(Core.EntityManager, character, buffPrefab))
                    break;
                yield return null;
            }

            // 3) Spawn a fresh instance (immortal, persists through death)
            if (!TryAddBuff(user, character, buffPrefab, durationSeconds: -1, immortal: true))
            {
                Core.Log.LogError("[PrestigeMini] apply failed");
                yield break;
            }

            if (!BuffUtility.TryGetBuff(Core.EntityManager, character, buffPrefab, out var buffEntity) || !buffEntity.Exists())
            {
                Core.Log.LogWarning("[PrestigeMini] live buff not found after add");
                yield break;
            }

            // Persist & strip prefab noise (idempotent)
            if (!buffEntity.Has<Buff_Persists_Through_Death>()) buffEntity.Add<Buff_Persists_Through_Death>();
            if (buffEntity.Has<RemoveBuffOnGameplayEvent>()) buffEntity.Remove<RemoveBuffOnGameplayEvent>();
            if (buffEntity.Has<RemoveBuffOnGameplayEventEntry>()) buffEntity.Remove<RemoveBuffOnGameplayEventEntry>();
            if (buffEntity.Has<SpawnStructure_WeakenState_DataShared>()) buffEntity.Remove<SpawnStructure_WeakenState_DataShared>();
            if (buffEntity.Has<ScriptSpawn>()) buffEntity.Remove<ScriptSpawn>();
            if (buffEntity.Has<CreateGameplayEventsOnSpawn>()) buffEntity.Remove<CreateGameplayEventsOnSpawn>();
            if (buffEntity.Has<GameplayEventListeners>()) buffEntity.Remove<GameplayEventListeners>();

            // 4) Overwrite stats from config (cumulative 2..level)
            var rules = SubtleBytePrestigeConfig.GetMergedRules(level);
            if (rules == null)
            {
                Core.Log.LogError("[PrestigeMini] GetMergedRules returned null.");
                yield break;
            }

            var buffer = Core.EntityManager.AddBuffer<ModifyUnitStatBuff_DOTS>(buffEntity);
            buffer.Clear();

            int added = 0;
            foreach (var line in rules)
            {
                if (!Enum.TryParse<UnitStatType>(line.statType, true, out var statType))
                {
                    Core.Log.LogWarning($"[PrestigeMini] Unknown UnitStatType: {line.statType}");
                    continue;
                }

                var cap = ParseCap(line.attributeCap);
                var mtyp = ParseMod(line.modification);

                buffer.Add(new ModifyUnitStatBuff_DOTS
                {
                    AttributeCapType = cap,
                    StatType = statType,
                    Value = line.value,
                    ModificationType = mtyp,
                    Modifier = 1,
                    Id = ModificationId.NewId(0)
                });
                added++;
            }

            Core.Log.LogInfo($"[PrestigeMini] Applied fresh prestige L{level} ({added} lines) → {character.GetPlayerName()} (buff={buffPrefab.GuidHash}).");
        }

        public static void Clear(Entity character)
        {
            try
            {
                if (!character.Exists()) return;
                Buffs.RemoveBuff(character, HijackedBuff);
                Core.Log.LogInfo($"[PrestigeMini] Cleared prestige buff from {character.GetPlayerName()}.");
            }
            catch (Exception e)
            {
                Core.Log.LogError($"[PrestigeMini] Clear exception: {e}");
            }
        }

        // --- parsing helpers ---
        private static AttributeCapType ParseCap(string s)
        {
            if (string.Equals(s, "Uncapped", StringComparison.OrdinalIgnoreCase)) return AttributeCapType.Uncapped;
            if (string.Equals(s, "SoftCapped", StringComparison.OrdinalIgnoreCase)) return AttributeCapType.SoftCapped;
            if (string.Equals(s, "HardCapped", StringComparison.OrdinalIgnoreCase)) return AttributeCapType.HardCapped;
            return AttributeCapType.Uncapped;
        }

        private static ModificationType ParseMod(string s)
        {
            if (string.Equals(s, "Add", StringComparison.OrdinalIgnoreCase)) return ModificationType.Add;
            if (string.Equals(s, "Multiply", StringComparison.OrdinalIgnoreCase)) return ModificationType.Multiply;
            if (string.Equals(s, "Set", StringComparison.OrdinalIgnoreCase)) return ModificationType.Set;
            return ModificationType.Add;
        }

        // Kindred-style addbuff (unchanged)
        private static bool TryAddBuff(Entity user, Entity character, PrefabGUID buffPrefab, int durationSeconds = 0, bool immortal = true)
        {
            var des = Core.Server.GetExistingSystemManaged<DebugEventsSystem>();
            var ev = new ApplyBuffDebugEvent { BuffPrefabGUID = buffPrefab };
            var from = new FromCharacter { User = user, Character = character };

            if (!BuffUtility.TryGetBuff(Core.Server.EntityManager, character, buffPrefab, out Entity buffEntity))
            {
                des.ApplyBuff(from, ev);

                if (!BuffUtility.TryGetBuff(Core.Server.EntityManager, character, buffPrefab, out buffEntity))
                    return false;

                if (immortal)
                {
                    buffEntity.Add<Buff_Persists_Through_Death>();
                    if (buffEntity.Has<RemoveBuffOnGameplayEvent>()) buffEntity.Remove<RemoveBuffOnGameplayEvent>();
                    if (buffEntity.Has<RemoveBuffOnGameplayEventEntry>()) buffEntity.Remove<RemoveBuffOnGameplayEventEntry>();
                }

                if (durationSeconds > -1 && durationSeconds != 0)
                {
                    if (!buffEntity.Has<LifeTime>())
                    {
                        buffEntity.Add<LifeTime>();
                        buffEntity.Write(new LifeTime { EndAction = LifeTimeEndAction.Destroy });
                    }
                    var lt = buffEntity.Read<LifeTime>();
                    lt.Duration = durationSeconds;
                    buffEntity.Write(lt);
                }
                else if (durationSeconds == -1)
                {
                    if (buffEntity.Has<LifeTime>())
                    {
                        var lt = buffEntity.Read<LifeTime>();
                        lt.EndAction = LifeTimeEndAction.None;
                        buffEntity.Write(lt);
                    }
                }

                return true;
            }
            return false;
        }
    }
}
