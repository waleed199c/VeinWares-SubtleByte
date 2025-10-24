using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using ProjectM;
using Unity.Collections;
using Unity.Entities;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Services;

internal static class SpawnSuppressionService
{
    private static readonly string[] AdditionalFeedComponentTypeNames =
    {
        "ProjectM.FeedBuff",
        "ProjectM.FeedState",
        "ProjectM.FeedSourceBuff",
        "ProjectM.FeedTarget",
        "ProjectM.FeedTargetBuff",
        "ProjectM.FeedCooldown",
        "ProjectM.Feedable",
    };

    private static readonly IReadOnlyList<ComponentRemovalTarget> AdditionalFeedComponentTypes =
        ResolveComponentTypes(AdditionalFeedComponentTypeNames);

    private static readonly string[] CharmComponentTypeNames =
    {
        "ProjectM.CharmSource",
    };

    private static readonly IReadOnlyList<ComponentRemovalTarget> CharmComponentTypes =
        ResolveComponentTypes(CharmComponentTypeNames);

    public static bool SuppressSpawnComponents(
        EntityManager entityManager,
        Entity entity,
        bool suppressFeed,
        bool suppressCharm,
        string? context = null,
        int? forcedBloodQualityTier = null)
    {
        if (!entity.Exists() || (!suppressFeed && !suppressCharm))
        {
            return false;
        }

        var removedComponents = new List<string>();

        if (suppressFeed)
        {
            SuppressFeedComponents(entityManager, entity, removedComponents, forcedBloodQualityTier);
        }

        if (suppressCharm)
        {
            SuppressCharmComponents(entityManager, entity, removedComponents);
        }

        if (removedComponents.Count == 0)
        {
            return false;
        }

        var contextSuffix = string.IsNullOrWhiteSpace(context) ? string.Empty : $" ({context})";
        ModLogger.Info($"[Spawn] Removed components [{string.Join(", ", removedComponents)}] from entity {entity.Index}{contextSuffix}.");
        return true;
    }

    private static void SuppressFeedComponents(
        EntityManager entityManager,
        Entity entity,
        List<string> removedComponents,
        int? forcedBloodQualityTier)
    {
        var suppressedQuality = ApplyBloodConsumeSourceSuppression(entityManager, entity, removedComponents, forcedBloodQualityTier);
        var removedAny = false;

        if (entity.Has<FeedableInventory>())
        {
            entity.Remove<FeedableInventory>();
            removedComponents.Add(nameof(FeedableInventory));
            removedAny = true;
        }

        foreach (var component in AdditionalFeedComponentTypes)
        {
            if (!entityManager.HasComponent(entity, component.ComponentType))
            {
                continue;
            }

            entityManager.RemoveComponent(entity, component.ComponentType);
            removedComponents.Add(component.DisplayName);
            removedAny = true;
        }

        if (!suppressedQuality.HasValue && removedAny)
        {
            var fallbackQuality = CalculateSuppressedBloodQuality(ReadSpawnBloodQuality(entityManager, entity), forcedBloodQualityTier);
            if (TrySetSpawnBloodQuality(entityManager, entity, fallbackQuality))
            {
                removedComponents.Add($"{nameof(UnitSpawnData)}.{nameof(UnitSpawnData.BloodQuality)}={(int)MathF.Round(fallbackQuality)}");
            }
        }
    }

    private static float? ApplyBloodConsumeSourceSuppression(
        EntityManager entityManager,
        Entity entity,
        List<string> removedComponents,
        int? forcedBloodQualityTier)
    {
        if (!entityManager.TryGetComponentData(entity, out BloodConsumeSource bloodConsumeSource))
        {
            return null;
        }

        var sourceQuality = DetermineBloodQualitySource(entityManager, entity, bloodConsumeSource);
        var suppressedQuality = CalculateSuppressedBloodQuality(sourceQuality, forcedBloodQualityTier);
        var changed = false;

        if (bloodConsumeSource.CanBeConsumed)
        {
            bloodConsumeSource.CanBeConsumed = false;
            removedComponents.Add($"{nameof(BloodConsumeSource)}.{nameof(BloodConsumeSource.CanBeConsumed)}=false");
            changed = true;
        }

        if (!Approximately(bloodConsumeSource.BloodQuality, suppressedQuality))
        {
            bloodConsumeSource.BloodQuality = suppressedQuality;
            removedComponents.Add($"{nameof(BloodConsumeSource)}.{nameof(BloodConsumeSource.BloodQuality)}={(int)MathF.Round(suppressedQuality)}");
            changed = true;
        }

        if (changed)
        {
            entityManager.SetComponentData(entity, bloodConsumeSource);
        }

        if (TrySetSpawnBloodQuality(entityManager, entity, suppressedQuality))
        {
            removedComponents.Add($"{nameof(UnitSpawnData)}.{nameof(UnitSpawnData.BloodQuality)}={(int)MathF.Round(suppressedQuality)}");
        }

        return suppressedQuality;
    }

    private static float DetermineBloodQualitySource(EntityManager entityManager, Entity entity, in BloodConsumeSource bloodConsumeSource)
    {
        if (entityManager.TryGetComponentData(entity, out UnitSpawnData spawnData) && spawnData.BloodQuality >= 0f)
        {
            return spawnData.BloodQuality;
        }

        if (bloodConsumeSource.BloodQuality >= 0f)
        {
            return bloodConsumeSource.BloodQuality;
        }

        return 0f;
    }

    private static void SuppressCharmComponents(EntityManager entityManager, Entity entity, List<string> removedComponents)
    {
        if (RemoveKnownCharmComponents(entityManager, entity, removedComponents))
        {
            return;
        }

        var componentTypes = entityManager.GetComponentTypes(entity);
        if (!componentTypes.IsCreated || componentTypes.Length == 0)
        {
            return;
        }

        try
        {
            var toRemove = new List<ComponentType>();

            for (var i = 0; i < componentTypes.Length; i++)
            {
                var componentType = componentTypes[i];
                var managedTypeName = componentType.ToString();

                if (string.IsNullOrEmpty(managedTypeName))
                {
                    continue;
                }

                if (!IsCharmRelated(managedTypeName))
                {
                    continue;
                }

                toRemove.Add(componentType);
                removedComponents.Add(managedTypeName);
            }

            foreach (var componentType in toRemove)
            {
                if (entityManager.HasComponent(entity, componentType))
                {
                    entityManager.RemoveComponent(entity, componentType);
                }
            }
        }
        finally
        {
            componentTypes.Dispose();
        }
    }

    private static bool RemoveKnownCharmComponents(EntityManager entityManager, Entity entity, List<string> removedComponents)
    {
        var removedAny = false;

        foreach (var component in CharmComponentTypes)
        {
            if (!entityManager.HasComponent(entity, component.ComponentType))
            {
                continue;
            }

            entityManager.RemoveComponent(entity, component.ComponentType);
            removedComponents.Add(component.DisplayName);
            removedAny = true;
        }

        return removedAny;
    }

    private static bool IsCharmRelated(string managedTypeName)
    {
        if (string.IsNullOrEmpty(managedTypeName))
        {
            return false;
        }

        if (managedTypeName.IndexOf("Charm", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        return managedTypeName.Contains("ProjectM.", StringComparison.Ordinal);
    }

    private static bool TrySetSpawnBloodQuality(EntityManager entityManager, Entity entity, float suppressedQuality)
    {
        if (!entityManager.TryGetComponentData(entity, out UnitSpawnData spawnData))
        {
            return false;
        }

        if (Approximately(spawnData.BloodQuality, suppressedQuality))
        {
            return false;
        }

        spawnData.BloodQuality = suppressedQuality;
        entityManager.SetComponentData(entity, spawnData);
        return true;
    }

    private static float ReadSpawnBloodQuality(EntityManager entityManager, Entity entity)
    {
        if (entityManager.TryGetComponentData(entity, out UnitSpawnData spawnData) && spawnData.BloodQuality >= 0f)
        {
            return spawnData.BloodQuality;
        }

        return 0f;
    }

    private static float CalculateSuppressedBloodQuality(float currentQuality, int? forcedBloodQualityTier)
    {
        var sanitizedQuality = Clamp(currentQuality, 0f, 100f);

        if (forcedBloodQualityTier.HasValue)
        {
            var tier = Math.Clamp(forcedBloodQualityTier.Value, 1, 5);
            if (tier >= 5)
            {
                return 100f;
            }

            ReadOnlySpan<(float Threshold, float BaseQuality)> forcedTiers = stackalloc (float, float)[]
            {
                (20f, 20f),
                (40f, 40f),
                (60f, 60f),
                (80f, 80f)
            };

            var index = Math.Clamp(tier - 1, 0, forcedTiers.Length - 1);
            var baseQuality = forcedTiers[index].BaseQuality;
            var offsetBaseline = Math.Max(0f, baseQuality - 20f);
            var offset = Clamp(sanitizedQuality - offsetBaseline, 0f, 9f);
            return Clamp(baseQuality + offset, 0f, 100f);
        }

        if (sanitizedQuality >= 100f)
        {
            return 100f;
        }

        ReadOnlySpan<(float Threshold, float BaseQuality)> tiers = stackalloc (float, float)[]
        {
            (20f, 20f),
            (40f, 40f),
            (60f, 60f),
            (80f, 80f),
        };

        foreach (var tier in tiers)
        {
            if (sanitizedQuality > tier.Threshold)
            {
                continue;
            }

            var offsetBaseline = tier.BaseQuality - 20f;
            var offset = Clamp(sanitizedQuality - offsetBaseline, 0f, 9f);
            return tier.BaseQuality + offset;
        }

        return 100f;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static bool Approximately(float left, float right)
    {
        return Math.Abs(left - right) <= 0.001f;
    }

    private static IReadOnlyList<ComponentRemovalTarget> ResolveComponentTypes(IEnumerable<string> typeNames)
    {
        return typeNames
            .Select(FindComponentType)
            .Where(component => component.HasValue)
            .Select(component => component!.Value)
            .ToArray();
    }

    private static ComponentRemovalTarget? FindComponentType(string typeName)
    {
        var type = Type.GetType(typeName);
        if (type != null)
        {
            return TryCreateComponentType(type);
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName);
            if (type != null)
            {
                return TryCreateComponentType(type);
            }
        }

        return null;
    }

    private static ComponentRemovalTarget? TryCreateComponentType(Type managedType)
    {
        try
        {
            var il2CppType = Il2CppType.From(managedType);
            var typeIndex = TypeManager.GetTypeIndex(il2CppType);
            var componentType = ComponentType.FromTypeIndex(typeIndex);
            return new ComponentRemovalTarget(componentType, managedType.Name);
        }
        catch
        {
            return null;
        }
    }

    private readonly record struct ComponentRemovalTarget(ComponentType ComponentType, string DisplayName);
}
