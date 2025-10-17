using System;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using VeinWares.SubtleByte.Services.FactionInfamy;

#nullable enable

namespace VeinWares.SubtleByte.Patches;

[HarmonyPatch(typeof(BuffDebugSystem), nameof(BuffDebugSystem.OnUpdate))]
internal static class BuffDebugSystemInfamyPatch
{
    private const int BuffInCombatHash = 581443919;
    private const int BuffOutOfCombatHash = 897325455;

    private static AccessTools.FieldRef<BuffDebugSystem, EntityQuery>? _buffQueryAccessor;
    private static bool _buffQueryUnavailable;

    private static void Postfix(BuffDebugSystem __instance)
    {
        if (!FactionInfamySystem.Enabled || _buffQueryUnavailable)
        {
            return;
        }

        if (!TryGetBuffQuery(__instance, out var query))
        {
            return;
        }

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

                if (!__instance.EntityManager.TryGetComponentData(buffEntity, out EntityOwner entityOwner))
                {
                    continue;
                }

                var owner = entityOwner.Owner;
                if (owner == Entity.Null)
                {
                    continue;
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
                    FactionInfamySystem.RegisterCombatStart(steamId);
                }
                else
                {
                    FactionInfamySystem.RegisterCombatEnd(steamId);
                }
            }
        }
        finally
        {
            buffEntities.Dispose();
        }
    }

    private static bool TryGetBuffQuery(BuffDebugSystem instance, out EntityQuery query)
    {
        query = default;

        if (_buffQueryAccessor is null)
        {
            try
            {
                _buffQueryAccessor = AccessTools.FieldRefAccess<BuffDebugSystem, EntityQuery>("__query_401358787_0");
            }
            catch (ArgumentException)
            {
                _buffQueryUnavailable = true;
                return false;
            }
        }

        query = _buffQueryAccessor!(instance);
        return !query.Equals(default(EntityQuery));
    }
}
