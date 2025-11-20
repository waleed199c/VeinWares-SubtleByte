using System;
using System.Collections.Generic;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using VeinWares.SubtleByte.Services.FactionInfamy;
using VeinWares.SubtleByte.Services;

#nullable enable

namespace VeinWares.SubtleByte.Patches;

[HarmonyPatch(typeof(BuffDebugSystem), nameof(BuffDebugSystem.OnUpdate))]
internal static class BuffDebugSystemInfamyPatch
{
    private const int BuffInCombatHash = 581443919;
    private const int BuffOutOfCombatHash = 897325455;

    private static readonly EntityQueryDesc BuffQueryDescription = new()
    {
        All = new ComponentType[]
        {
            ComponentType.ReadOnly<Buff>(),
            ComponentType.ReadOnly<SpawnTag>(),
            ComponentType.ReadOnly<PrefabGUID>(),
            ComponentType.ReadOnly<EntityOwner>()
        }
    };

    private static bool _buffQueryUnavailable;
    private static readonly HashSet<ulong> PlayersInCombat = new();

    private static void Postfix(BuffDebugSystem __instance)
    {
        if (!FactionInfamySystem.Enabled || _buffQueryUnavailable)
        {
            return;
        }

        EntityQuery query;
        try
        {
            query = __instance.EntityManager.CreateEntityQuery(BuffQueryDescription);
        }
        catch (Exception)
        {
            _buffQueryUnavailable = true;
            return;
        }

        try
        {
            var buffEntities = query.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var buffEntity in buffEntities)
                {
                    if (!__instance.EntityManager.TryGetComponentData(buffEntity, out PrefabGUID prefab))
                {
                    continue;
                }

                var guidHash = prefab.GuidHash;
                var combatStart = guidHash == BuffInCombatHash;
                var combatEnd = guidHash == BuffOutOfCombatHash;

                if (!combatStart && !combatEnd)
                {
                    continue;
                }

                var combatState = combatStart && !combatEnd;

                if (!__instance.EntityManager.TryGetComponentData(buffEntity, out EntityOwner entityOwner))
                {
                    continue;
                }

                var owner = entityOwner.Owner;
                if (owner == Entity.Null)
                {
                    continue;
                }

                if (__instance.EntityManager.HasComponent<VBloodDuelChallenger>(owner) ||
                    __instance.EntityManager.HasComponent<VBloodDuelInstance>(owner))
                {
                    DuelSummonService.UpdateCombatState(__instance.EntityManager, owner, combatState);
                }

                if (!__instance.EntityManager.TryGetComponentData(owner, out PlayerCharacter playerCharacter))
                {
                    continue;
                }

                    if (!__instance.EntityManager.TryGetComponentData(playerCharacter.UserEntity, out User user))
                    {
                        continue;
                    }

                    var steamId = user.PlatformId;
                    if (steamId == 0UL)
                    {
                        continue;
                    }

                    if (combatStart)
                    {
                        var isNewCombat = PlayersInCombat.Add(steamId);
                        FactionInfamySystem.RegisterCombatStart(steamId);
                        if (isNewCombat)
                        {
                            FactionInfamyAmbushService.TryTriggerAmbush(__instance.EntityManager, owner, steamId);
                        }
                    }
                    else
                    {
                        FactionInfamySystem.RegisterCombatEnd(steamId);
                        PlayersInCombat.Remove(steamId);
                    }

                    DuelSummonService.UpdateCombatState(__instance.EntityManager, owner, combatState);
                }
            }
            finally
            {
                buffEntities.Dispose();
            }
        }
        finally
        {
            query.Dispose();
        }
    }
}
