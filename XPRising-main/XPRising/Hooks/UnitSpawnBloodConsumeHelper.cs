using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ProjectM;
using Unity.Entities;
using XPRising.Utils;
using XPShared;

namespace XPRising.Hooks;

internal static class UnitSpawnBloodConsumeHelper
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

    private static readonly List<Type> AdditionalFeedComponentTypes = ResolveAdditionalFeedComponentTypes();

    public static bool SuppressFeedingComponents(Entity entity, Plugin.LogSystem logSystem)
    {
        var entityManager = Plugin.Server.EntityManager;
        var removedComponents = new List<string>();

        if (entity.TryRemoveComponent<BloodConsumeSource>())
        {
            removedComponents.Add(nameof(BloodConsumeSource));
        }

        if (entity.TryRemoveComponent<FeedableInventory>())
        {
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

        var prefabName = DebugTool.GetPrefabName(entity);
        Plugin.Log(logSystem, LogLevel.Info,
            () => $"Removed feed components [{string.Join(", ", removedComponents)}] from {prefabName} (Entity {entity.Index}).");
        return true;
    }

    private static bool TryRemoveComponent(EntityManager entityManager, Entity entity, Type componentType)
    {
        try
        {
            var typeIndex = TypeManager.GetTypeIndex(componentType);
            var resolvedComponent = ComponentType.FromTypeIndex(typeIndex);
            if (!entity.Has(resolvedComponent))
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

    private static List<Type> ResolveAdditionalFeedComponentTypes()
    {
        return AdditionalFeedComponentTypeNames
            .Select(FindType)
            .Where(type => type != null)
            .ToList()!;
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
