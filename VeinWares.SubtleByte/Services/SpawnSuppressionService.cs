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

    private static readonly IReadOnlyList<ComponentRemovalTarget> AdditionalFeedComponentTypes = ResolveAdditionalFeedComponentTypes();

    public static bool SuppressSpawnComponents(
        EntityManager entityManager,
        Entity entity,
        bool suppressFeed,
        bool suppressCharm,
        string? context = null)
    {
        if (!entity.Exists() || (!suppressFeed && !suppressCharm))
        {
            return false;
        }

        var removedComponents = new List<string>();

        if (suppressFeed)
        {
            SuppressFeedComponents(entityManager, entity, removedComponents);
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

    private static void SuppressFeedComponents(EntityManager entityManager, Entity entity, List<string> removedComponents)
    {
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

        foreach (var component in AdditionalFeedComponentTypes)
        {
            if (!entityManager.HasComponent(entity, component.ComponentType))
            {
                continue;
            }

            entityManager.RemoveComponent(entity, component.ComponentType);
            removedComponents.Add(component.DisplayName);
        }
    }

    private static void SuppressCharmComponents(EntityManager entityManager, Entity entity, List<string> removedComponents)
    {
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
                var typeInfo = TypeManager.GetTypeInfo(componentType.TypeIndex);
                var managedTypeName = typeInfo.DebugTypeName.ToString();

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

    private static IReadOnlyList<ComponentRemovalTarget> ResolveAdditionalFeedComponentTypes()
    {
        return AdditionalFeedComponentTypeNames
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
