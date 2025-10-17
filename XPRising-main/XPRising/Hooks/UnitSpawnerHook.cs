﻿using System.Collections.Generic;
using BepInEx.Logging;
using ProjectM;
using ProjectM.Shared;
using HarmonyLib;
using ProjectM.Scripting;
using ProjectM.Sequencer;
using Stunlock.Core;
using Unity.Entities;
using XPRising.Systems;
using XPRising.Utils;
using XPRising.Utils.Prefabs;
using XPShared;
using Faction = ProjectM.Faction;
using Prefabs = XPRising.Utils.Prefabs;

namespace XPRising.Hooks;

[HarmonyPatch(typeof(UnitSpawnerReactSystem), nameof(UnitSpawnerReactSystem.OnUpdate))]
public static class UnitSpawnerReactSystemPatch
{
    public static bool listen = false;
    public static void Prefix(UnitSpawnerReactSystem __instance, out Dictionary<Entity, (int, SpawnUnit.SpawnFaction)> __state)
    {
        __state = new();
        if (!(Plugin.WantedSystemActive || Plugin.RandomEncountersSystemActive)) return;
        
        var entities = __instance.__query_2099432243_0.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entities) {
            if (!__instance.EntityManager.TryGetComponentData<LifeTime>(entity, out var lifetime)) return;

            // If this successfully gets decoded, then this is a custom spawn... or just extremely lucky.
            if (SpawnUnit.DecodeLifetime(lifetime.Duration, out var level, out var faction))
            {
                __state.Add(entity, (level, faction));
                
                // Add this buff for fast spawning?
                // Buff_General_Spawn_Unit_Fast = 507944752,
            }

            if (listen)
            {
                if (Cache.spawnNPC_Listen.TryGetValue(lifetime.Duration, out var content))
                {
                    content.EntityIndex = entity.Index;
                    content.EntityVersion = entity.Version;
                    if (content.Options.Process) content.Process = true;

                    Cache.spawnNPC_Listen[lifetime.Duration] = content;
                    listen = false;
                }
            }
            
            if(Plugin.RandomEncountersSystemActive && Plugin.IsInitialized)
            {
                RandomEncountersSystem.ServerEvents_OnUnitSpawned(__instance.EntityManager, entity);
            }
        }
    }
    
    public static void Postfix(Dictionary<Entity, (int, SpawnUnit.SpawnFaction)> __state)
    {
        if (!(Plugin.WantedSystemActive || Plugin.RandomEncountersSystemActive)) return;

        var currentTime = DateTime.Now;
        var em = Plugin.Server.EntityManager;
        foreach (var data in __state)
        {
            var entity = data.Key;
            var intendedLevel = data.Value.Item1;
            var faction = data.Value.Item2;
            if (faction == SpawnUnit.SpawnFaction.VampireHunters)
            {
                Plugin.Log(Plugin.LogSystem.SquadSpawn, LogLevel.Info, "Attempting to set faction vampire hunters");
                entity.With((ref FactionReference factionReference) =>
                {
                    factionReference.FactionGuid._Value = new PrefabGUID((int)Prefabs.Faction.VampireHunters); 
                });
            }
            
            Plugin.Log(Plugin.LogSystem.SquadSpawn, LogLevel.Info, "Attempting to set level");
            entity.With((ref UnitLevel unitLevel) =>
            {
                unitLevel.Level._Value = intendedLevel;
            });

            // If they get disabled (ie, the user runs far away), just mark them to be destroyed.
            entity.Add<DestroyWhenDisabled>();
            // Remove "Minion" component as none of these should be acting as minions and this will better allow these
            // units to be valid for full XP in Bloodcraft.
            entity.TryRemoveComponent<Minion>();

            if (faction is SpawnUnit.SpawnFaction.WantedUnit or SpawnUnit.SpawnFaction.VampireHunters)
            {
                // Add the entity to our list of spawned entities so we can match them as reducing heat when killed
                WantedSystem.AddAmbushingEntity(entity, currentTime);
            }
        }
    }
}

[HarmonyPatch(typeof(MinionSpawnSystem), nameof(MinionSpawnSystem.OnUpdate))]
public static class MinionSpawnSystem_Patch {
    public static void Prefix(MinionSpawnSystem __instance)
    {
        // This issue and fix only make sense in the context of the Wanted system being active.
        if (!Plugin.WantedSystemActive) return;
        
        var entities = __instance.__query_166459767_0.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entities) {
            // Gloomrot spider-tanks spawn a gloomrot technician minion that does not despawn when the spider-tank gets destroyed
            // by the "Lifetime" component. This will check for that case and destroy the minion on load so it doesn't get stuck.
            // This does not impact the behaviour of the spider-tank (other than it does not drop the technician on death).
            if (Helper.ConvertGuidToUnit(Helper.GetPrefabGUID(entity)) != Units.CHAR_Gloomrot_Technician) continue;

            if (!__instance.EntityManager.TryGetComponentData(entity, out FactionReference faction)) continue;
            if (!__instance.EntityManager.TryGetComponentData(entity, out EntityOwner eo)) continue;
            if (!__instance.EntityManager.TryGetComponentData(eo.Owner, out LifeTime lt) ||
                lt.EndAction == LifeTimeEndAction.Kill) continue;
            if (SpawnUnit.DecodeLifetime(lt.Duration, out _, out _)) {
                // Destroy initial minions as they don't transition properly when their parent is destroyed without being killed.
                DestroyUtility.CreateDestroyEvent(Plugin.Server.EntityManager, entity, DestroyReason.Default, DestroyDebugReason.ByScript);
                DestroyUtility.Destroy(Plugin.Server.EntityManager, entity);
            }
        }
    }
}
