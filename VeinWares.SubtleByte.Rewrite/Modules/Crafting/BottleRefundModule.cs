using System.Collections.Generic;
using HarmonyLib;
using ProjectM;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using VeinWares.SubtleByte.Rewrite.Infrastructure;

namespace VeinWares.SubtleByte.Rewrite.Modules.Crafting;

public sealed class BottleRefundModule : IModule
{
    private static readonly PrefabGUID MixedPotionGuid = new(2063723255);
    private static readonly PrefabGUID EmptyBottleGuid = new(-437611596);
    private static readonly PrefabGUID ExternalInventoryGuid = new(1183666186);

    private static BottleRefundModule? _instance;

    private readonly Dictionary<Entity, MixerSnapshot> _snapshots = new(64);
    private readonly Dictionary<Entity, InventoryCacheEntry> _inventoryCache = new(64);
    private readonly HashSet<Entity> _seenMixers = new();
    private readonly List<Entity> _pruneList = new();

    private ModuleContext? _context;
    private uint _updateIndex;
    private bool _disposed;
    private ItemData? _cachedBottleData;

    public void Initialize(ModuleContext context)
    {
        _context = context;
        _instance = this;

        context.Harmony.Patch(
            AccessTools.Method(typeof(BloodMixerSystem_Update), nameof(BloodMixerSystem_Update.OnUpdate)),
            postfix: new HarmonyMethod(typeof(BottleRefundModule), nameof(PostUpdate)));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _snapshots.Clear();
        _inventoryCache.Clear();
        _seenMixers.Clear();
        _pruneList.Clear();
        _cachedBottleData = null;
        _updateIndex = 0;

        if (_instance == this)
        {
            _instance = null;
        }

        _disposed = true;
    }

    private static void PostUpdate(BloodMixerSystem_Update __instance)
    {
        _instance?.RunWithTracking(__instance);
    }

    private void RunWithTracking(BloodMixerSystem_Update system)
    {
        var context = _context;
        if (context is null)
        {
            return;
        }

        context.Performance.Measure(
            nameof(BottleRefundModule) + ".Update",
            () => ProcessSystemUpdate(context, system));
    }

    private void ProcessSystemUpdate(ModuleContext context, BloodMixerSystem_Update system)
    {
        _updateIndex++;

        if (!context.Config.BottleRefundEnabled.Value)
        {
            if (_snapshots.Count > 0)
            {
                _snapshots.Clear();
                _inventoryCache.Clear();
            }
            return;
        }

        var em = system.EntityManager;
        var mixers = system._Query.ToEntityArray(Allocator.Temp);
        try
        {
            _seenMixers.Clear();

            for (int i = 0; i < mixers.Length; i++)
            {
                var mixer = mixers[i];
                _seenMixers.Add(mixer);

                var shared = em.GetComponentData<BloodMixer_Shared>(mixer);
                if (!TryGetOutputInventory(em, mixer, out var outputInv))
                {
                    _snapshots[mixer] = new MixerSnapshot(shared.State, 0);
                    continue;
                }

                var potionCount = CountItems(em, outputInv, MixedPotionGuid);
                if (_snapshots.TryGetValue(mixer, out var snapshot) &&
                    snapshot.State == BloodMixerState.Mixing &&
                    shared.State == BloodMixerState.NotReadyToMix &&
                    potionCount > snapshot.PotionCount)
                {
                    RefundBottle(em, outputInv);
                }

                _snapshots[mixer] = new MixerSnapshot(shared.State, potionCount);
            }
        }
        finally
        {
            mixers.Dispose();
        }

        if (_snapshots.Count > _seenMixers.Count)
        {
            _pruneList.Clear();
            foreach (var kvp in _snapshots)
            {
                if (!_seenMixers.Contains(kvp.Key))
                {
                    _pruneList.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _pruneList.Count; i++)
            {
                var mixer = _pruneList[i];
                _snapshots.Remove(mixer);
                _inventoryCache.Remove(mixer);
            }
        }
    }

    private bool TryGetOutputInventory(EntityManager em, Entity mixer, out Entity inventory)
    {
        if (_inventoryCache.TryGetValue(mixer, out var cacheEntry))
        {
            if (cacheEntry.KnownMissing)
            {
                if ((uint)(_updateIndex - cacheEntry.LastChecked) < 120u)
                {
                    inventory = Entity.Null;
                    return false;
                }

                _inventoryCache.Remove(mixer);
            }
            else
            {
                inventory = cacheEntry.Inventory;
                if (inventory != Entity.Null && em.Exists(inventory) && em.HasBuffer<InventoryBuffer>(inventory))
                {
                    return true;
                }

                _inventoryCache.Remove(mixer);
            }
        }

        inventory = Entity.Null;

        if (em.HasBuffer<Child>(mixer))
        {
            var children = em.GetBuffer<Child>(mixer);
            Entity fallback = Entity.Null;
            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i].Value;
                if (!em.Exists(child) || !em.HasBuffer<InventoryBuffer>(child))
                {
                    continue;
                }

                if (em.HasComponent<PrefabGUID>(child) &&
                    em.GetComponentData<PrefabGUID>(child).GuidHash == ExternalInventoryGuid.GuidHash)
                {
                    inventory = child;
                    _inventoryCache[mixer] = new InventoryCacheEntry(inventory, knownMissing: false, _updateIndex);
                    return true;
                }

                if (fallback == Entity.Null)
                {
                    fallback = child;
                }
            }

            if (fallback != Entity.Null)
            {
                inventory = fallback;
                _inventoryCache[mixer] = new InventoryCacheEntry(inventory, knownMissing: false, _updateIndex);
                return true;
            }
        }

        if (TryResolveInventoryByQuery(em, mixer, out inventory))
        {
            _inventoryCache[mixer] = new InventoryCacheEntry(inventory, knownMissing: false, _updateIndex);
            return true;
        }

        _inventoryCache[mixer] = new InventoryCacheEntry(Entity.Null, knownMissing: true, _updateIndex);
        return false;
    }

    private static int CountItems(EntityManager em, Entity inventory, PrefabGUID item)
    {
        if (!em.HasBuffer<InventoryBuffer>(inventory))
        {
            return 0;
        }

        var buffer = em.GetBuffer<InventoryBuffer>(inventory);
        var total = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            var slot = buffer[i];
            if (slot.ItemType.GuidHash == item.GuidHash && slot.Amount > 0)
            {
                total += slot.Amount;
            }
        }

        return total;
    }

    private void RefundBottle(EntityManager em, Entity inventory)
    {
        if (!TryGetBottleData(em, out var bottleData))
        {
            return;
        }

        var map = new NativeParallelHashMap<PrefabGUID, ItemData>(1, Allocator.Temp);
        map.TryAdd(EmptyBottleGuid, bottleData);

        var settings = AddItemSettings.Create(
            entityManager: em,
            itemDataMap: map,
            equipIfPossible: false,
            previousItemEntity: default,
            startIndex: default,
            onlyFillEmptySlots: false,
            onlyCheckOneSlot: false,
            dropRemainder: false,
            inventoryInstanceIndex: default);

        _ = InventoryUtilitiesServer.TryAddItem(settings, inventory, EmptyBottleGuid, 1);
    }

    private bool TryGetBottleData(EntityManager em, out ItemData data)
    {
        if (_cachedBottleData.HasValue)
        {
            data = _cachedBottleData.Value;
            return true;
        }

        if (TryResolveItemData(em, EmptyBottleGuid, out data))
        {
            _cachedBottleData = data;
            return true;
        }

        return false;
    }

    private static bool TryResolveItemData(EntityManager em, PrefabGUID guid, out ItemData itemData)
    {
        itemData = default;

        try
        {
            var collection = em.World.GetExistingSystemManaged<PrefabCollectionSystem>();
            if (collection != null &&
                collection.PrefabLookupMap.TryGetValue(guid, out var prefab) &&
                prefab != Entity.Null &&
                em.HasComponent<ItemData>(prefab))
            {
                itemData = em.GetComponentData<ItemData>(prefab);
                return true;
            }
        }
        catch
        {
            // ignored – fall back to scan below
        }

        var desc = new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<Unity.Entities.Prefab>(),
                ComponentType.ReadOnly<PrefabGUID>(),
                ComponentType.ReadOnly<ItemData>()
            }
        };

        var query = em.CreateEntityQuery(desc);
        var entities = query.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (em.GetComponentData<PrefabGUID>(entity).GuidHash != guid.GuidHash)
            {
                continue;
            }

            itemData = em.GetComponentData<ItemData>(entity);
            return true;
        }

        return false;
    }

    private static bool TryResolveInventoryByQuery(EntityManager em, Entity mixer, out Entity inventory)
    {
        inventory = Entity.Null;

        var attachmentQueryDesc = new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<Attach>(),
                ComponentType.ReadOnly<PrefabGUID>(),
                ComponentType.ReadOnly<InventoryBuffer>()
            }
        };

        var query = em.CreateEntityQuery(attachmentQueryDesc);
        var entities = query.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            if (em.GetComponentData<Attach>(entity).Parent != mixer || !em.HasBuffer<InventoryBuffer>(entity))
            {
                continue;
            }

            if (em.HasComponent<PrefabGUID>(entity) &&
                em.GetComponentData<PrefabGUID>(entity).GuidHash == ExternalInventoryGuid.GuidHash)
            {
                inventory = entity;
                return true;
            }

            if (inventory == Entity.Null)
            {
                inventory = entity;
            }
        }

        return inventory != Entity.Null;
    }

    private readonly record struct MixerSnapshot(BloodMixerState State, int PotionCount);

    private readonly struct InventoryCacheEntry
    {
        public InventoryCacheEntry(Entity inventory, bool knownMissing, uint lastChecked)
        {
            Inventory = inventory;
            KnownMissing = knownMissing;
            LastChecked = lastChecked;
        }

        public Entity Inventory { get; }

        public bool KnownMissing { get; }

        public uint LastChecked { get; }
    }
}
