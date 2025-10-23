using System;
using System.Collections.Generic;
using System.Linq;
using ProjectM;
using Unity.Entities;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Services;

internal static class SpawnFeedSuppressionService
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

    private static readonly IReadOnlyList<Type> AdditionalFeedComponentTypes = ResolveAdditionalFeedComponentTypes();

    public static bool SuppressFeedingComponents(EntityManager entityManager, Entity entity, string? context = null)
    {
        if (!entity.Exists())
        {
            return false;
        }

        var removedComponents = new List<string>();

        if (entity.Has<BloodConsumeSource>())
        {
            entity.Remove<BloodConsumeSource>();
            removedComponents.Add(nameof(BloodConsumeSource));
        }

        if (entity.Has<FeedableInventory>())
        {
            entity.Remove<FeedableInventory>();
            removedComponents.Add(nameof(FeedableInventory));
        }

        foreach (var componentType in AdditionalFeedComponentTypes)
        {
            if (componentType == null)
            {
                continue;
            }

            if (TryRemoveComponent(entityManager, entity, componentType))
            {
                removedComponents.Add(componentType.Name);
            }
        }

        if (removedComponents.Count == 0)
        {
            return false;
        }

        var contextSuffix = string.IsNullOrWhiteSpace(context) ? string.Empty : $" ({context})";
        ModLogger.Info($"[Spawn] Removed feed components [{string.Join(", ", removedComponents)}] from entity {entity.Index}{contextSuffix}.");
        return true;
    }

    private static bool TryRemoveComponent(EntityManager entityManager, Entity entity, Type componentType)
    {
        try
        {
            var typeIndex = TypeManager.GetTypeIndex(componentType);
            var resolvedComponent = ComponentType.FromTypeIndex(typeIndex);
            if (!entityManager.HasComponent(entity, resolvedComponent))
            {
                return false;
            }

            entityManager.RemoveComponent(entity, resolvedComponent);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<Type> ResolveAdditionalFeedComponentTypes()
    {
        return AdditionalFeedComponentTypeNames
            .Select(FindType)
            .Where(type => type != null)
            .ToArray()!;
    }

    private static Type? FindType(string typeName)
    {
        var type = Type.GetType(typeName);
        if (type != null)
        {
            return type;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
