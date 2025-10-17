using HarmonyLib;
using ProjectM;
using Unity.Collections;
using Unity.Entities;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Services.FactionInfamy;

namespace VeinWares.SubtleByte.Patches;

[HarmonyPatch(typeof(DeathEventListenerSystem), nameof(DeathEventListenerSystem.OnUpdate))]
internal static class DeathEventListenerSystemInfamyPatch
{
    private static void Postfix(DeathEventListenerSystem __instance)
    {
        if (!FactionInfamySystem.Enabled)
        {
            return;
        }

        var deathEvents = __instance._DeathEventQuery.ToComponentDataArray<DeathEvent>(Allocator.Temp);
        try
        {
            foreach (var deathEvent in deathEvents)
            {
                HandlePlayerDeath(deathEvent.Died);
                HandleKill(__instance.EntityManager, deathEvent);
            }
        }
        finally
        {
            deathEvents.Dispose();
        }
    }

    private static void HandleKill(EntityManager entityManager, DeathEvent deathEvent)
    {
        var victim = deathEvent.Died;
        if (!QualifiesAsInfamyKill(entityManager, victim))
        {
            return;
        }

        var killer = ResolveKiller(entityManager, deathEvent.Killer);
        if (killer == Entity.Null || killer == victim)
        {
            return;
        }

        if (!killer.Has<PlayerCharacter>() || !killer.TryGetSteamId(out var steamId) || steamId == 0UL)
        {
            return;
        }

        if (!FactionInfamyVictimResolver.TryGetHateForVictim(victim, out var factionId, out var baseHate))
        {
            return;
        }

        FactionInfamySystem.RegisterCombatStart(steamId);
        FactionInfamySystem.RegisterHateGain(steamId, factionId, baseHate);
    }

    private static void HandlePlayerDeath(Entity victim)
    {
        if (!victim.TryGetSteamId(out var steamId) || steamId == 0UL)
        {
            return;
        }

        FactionInfamySystem.RegisterDeath(steamId);
    }

    private static bool QualifiesAsInfamyKill(EntityManager entityManager, Entity victim)
    {
        if (victim == Entity.Null)
        {
            return false;
        }

        if (entityManager.HasComponent<Minion>(victim))
        {
            return false;
        }

        if (!entityManager.HasComponent<UnitLevel>(victim))
        {
            return false;
        }

        if (!entityManager.HasComponent<Movement>(victim))
        {
            return false;
        }

        return true;
    }

    private static Entity ResolveKiller(EntityManager entityManager, Entity killer)
    {
        if (killer == Entity.Null)
        {
            return Entity.Null;
        }

        if (entityManager.HasComponent<Minion>(killer) && entityManager.TryGetComponentData<EntityOwner>(killer, out var owner))
        {
            return owner.Owner;
        }

        return killer;
    }
}
