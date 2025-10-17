using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using ProjectM;
using ProjectM.Gameplay.Systems;
using Unity.Collections;
using Unity.Entities;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Infrastructure;
using VeinWares.SubtleByte.Services.FactionInfamy;

namespace VeinWares.SubtleByte.Modules.FactionInfamy;

internal static class FactionInfamyHooks
{
    private static readonly Dictionary<Type, bool> PatchedTypes = new();
    private static ManualLogSource? _log;
    private static bool _deathQueryWarningLogged;

    public static void Initialize(ModuleContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        _log = context.Log;
        _deathQueryWarningLogged = false;
        PatchDeathSystem(context.Harmony);
    }

    private static void PatchDeathSystem(Harmony harmony)
    {
        if (PatchedTypes.ContainsKey(typeof(DeathEventSystem)))
        {
            return;
        }

        var method = AccessTools.Method(typeof(DeathEventSystem), nameof(DeathEventSystem.OnUpdate));
        if (method is null)
        {
            throw new InvalidOperationException("Unable to locate DeathEventSystem.OnUpdate for infamy hooks.");
        }

        harmony.Patch(
            method,
            postfix: new HarmonyMethod(typeof(FactionInfamyHooks), nameof(OnDeathSystemUpdated)));

        PatchedTypes[typeof(DeathEventSystem)] = true;
    }

    private static void OnDeathSystemUpdated(DeathEventSystem __instance)
    {
        if (!FactionInfamySystem.Enabled)
        {
            return;
        }

        var query = Traverse.Create(__instance).Field("_DeathEventQuery").GetValue<EntityQuery>();
        if (query == null || !query.IsCreated)
        {
            if (!_deathQueryWarningLogged)
            {
                _log?.LogWarning("[Infamy] Unable to access death event query; faction infamy kills cannot be processed.");
                _deathQueryWarningLogged = true;
            }

            return;
        }

        NativeArray<DeathEvent> events;
        try
        {
            events = query.ToComponentDataArray<DeathEvent>(Allocator.Temp);
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Infamy] Failed to read death events: {ex.Message}");
            return;
        }

        using (events)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var deathEvent = events[i];
                ProcessDeathEvent(__instance.EntityManager, deathEvent);
            }
        }
    }

    private static void ProcessDeathEvent(EntityManager entityManager, DeathEvent deathEvent)
    {
        var victim = deathEvent.DeadEntity;
        if (!victim.Exists())
        {
            return;
        }

        if (victim.TryGetSteamId(out var victimSteamId) && victimSteamId != 0)
        {
            FactionInfamySystem.RegisterDeath(victimSteamId);
            return;
        }

        var killer = deathEvent.KillerEntity;
        if (!killer.Exists() || !killer.IsPlayer())
        {
            return;
        }

        if (!killer.TryGetSteamId(out var killerSteamId) || killerSteamId == 0)
        {
            return;
        }

        if (!FactionInfamyHelper.TryResolveFaction(victim, entityManager, out var factionId, out var isVBlood))
        {
            return;
        }

        var baseHate = FactionInfamyHelper.CalculateBaseHate(victim, entityManager, isVBlood);
        if (baseHate <= 0f)
        {
            return;
        }

        FactionInfamySystem.RegisterCombatStart(killerSteamId);
        FactionInfamySystem.RegisterHateGain(killerSteamId, factionId, baseHate);
    }
}

internal static class FactionInfamyHelper
{
    public static bool TryResolveFaction(Entity entity, EntityManager entityManager, out string factionId, out bool isVBlood)
    {
        factionId = string.Empty;
        isVBlood = false;

        if (!entity.Exists())
        {
            return false;
        }

        PrefabGUID prefabGuid = default;
        if (entityManager.HasComponent<PrefabGUID>(entity))
        {
            prefabGuid = entityManager.GetComponentData<PrefabGUID>(entity);
        }

        if (entityManager.HasComponent<FactionReference>(entity))
        {
            var faction = entityManager.GetComponentData<FactionReference>(entity);
            factionId = faction.FactionGuid.GuidHash.ToString();
        }
        else if (prefabGuid.GuidHash != 0)
        {
            factionId = prefabGuid.GuidHash.ToString();
        }
        else
        {
            return false;
        }

        if (entityManager.HasComponent<VBloodUnit>(entity))
        {
            isVBlood = true;
        }
        else if (prefabGuid.GuidHash != 0)
        {
            if (TryGetPrefabName(prefabGuid, out var name) && name.IndexOf("VBlood", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isVBlood = true;
            }
        }

        return true;
    }

    public static float CalculateBaseHate(Entity entity, EntityManager entityManager, bool isVBlood)
    {
        var baseValue = 5f;
        if (entityManager.HasComponent<UnitLevel>(entity))
        {
            var level = entityManager.GetComponentData<UnitLevel>(entity);
            baseValue += Math.Max(0f, level.Level);
        }

        if (entityManager.HasComponent<UnitStatBuffState>(entity))
        {
            baseValue += 5f;
        }

        if (isVBlood)
        {
            baseValue *= 2f;
        }

        return baseValue;
    }

    private static bool TryGetPrefabName(PrefabGUID prefabGuid, out string name)
    {
        name = string.Empty;

        var collection = Core.PrefabCollectionSystem;
        if (collection != null && collection._PrefabGuidToNameDictionary.TryGetValue(prefabGuid, out var prefabName) && !string.IsNullOrWhiteSpace(prefabName))
        {
            name = prefabName;
            return true;
        }

        return false;
    }

}
