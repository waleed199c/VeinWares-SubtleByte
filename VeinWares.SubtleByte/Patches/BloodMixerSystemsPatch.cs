//#define VW_DEBUG // ← uncomment to enable logs again

using System.Collections.Generic;
using HarmonyLib;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace VeinWares.SubtleByte.Patches
{
    [HarmonyPatch(typeof(BloodMixerSystem_Update), "OnUpdate")]
    internal static class BloodMixerSystem_UpdatePatch
    {
        private static readonly PrefabGUID MixedPotion = new PrefabGUID(2063723255);
        private static readonly PrefabGUID EmptyBottle = new PrefabGUID(-437611596);
        private static readonly PrefabGUID External_Inventory = new PrefabGUID(1183666186);

        // mixer -> (previous state, previous total mixed potion count)
        private static readonly Dictionary<Entity, (BloodMixerState State, int PotionCount)> _prev = new();

        static void Postfix(BloodMixerSystem_Update __instance)
        {
            var em = __instance.EntityManager;
            var mixers = __instance._Query.ToEntityArray(Allocator.Temp);
            try
            {
#if VW_DEBUG
                // Core.Log.LogInfo($"[BottleRefund] mixers in system query = {mixers.Length}");
#endif
                for (int i = 0; i < mixers.Length; i++)
                {
                    var mixer = mixers[i];
                    var bm = em.GetComponentData<BloodMixer_Shared>(mixer);
                    var currentState = bm.State;

                    if (!TryFindOutputInventory(em, mixer, out var outputInv))
                    {
                        _prev[mixer] = (currentState, 0);
                        continue;
                    }

                    int mixedCountNow = CountItem(em, outputInv, MixedPotion);

                    if (_prev.TryGetValue(mixer, out var prev) &&
                        prev.State == BloodMixerState.Mixing &&
                        currentState == BloodMixerState.NotReadyToMix &&
                        mixedCountNow > prev.PotionCount)
                    {
                        // finished craft detected → add 1 Empty Bottle to the same output inventory
                        if (TryResolveItemData(em, EmptyBottle, out var bottleData))
                        {
                            var map = new NativeParallelHashMap<PrefabGUID, ItemData>(1, Allocator.Temp);
                            try
                            {
                                map.TryAdd(EmptyBottle, bottleData);
                                var add = AddItemSettings.Create(
                                    entityManager: em,
                                    itemDataMap: map,
                                    equipIfPossible: false,
                                    previousItemEntity: default,
                                    startIndex: default,
                                    onlyFillEmptySlots: false,
                                    onlyCheckOneSlot: false,
                                    dropRemainder: false,
                                    inventoryInstanceIndex: default);

                                _ = InventoryUtilitiesServer.TryAddItem(add, outputInv, EmptyBottle, 1);
#if VW_DEBUG
                                // Core.Log.LogInfo($"[BottleRefund] finish → +1 Empty Bottle (inv={outputInv.Index}:{outputInv.Version})");
#endif
                            }
                            finally { map.Dispose(); }
                        }
#if VW_DEBUG
                        else
                        {
                            // Core.Log.LogWarning("[BottleRefund] Could not resolve ItemData for Empty Bottle.");
                        }
#endif
                    }

                    _prev[mixer] = (currentState, mixedCountNow);
                }
            }
            finally { mixers.Dispose(); }
        }

        private static bool TryFindOutputInventory(EntityManager em, Entity mixer, out Entity inv)
        {
            inv = Entity.Null;

            var q = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Attach>(),
                    ComponentType.ReadOnly<PrefabGUID>()
                }
            });

            var ents = q.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < ents.Length; i++)
                {
                    var e = ents[i];
                    if (em.GetComponentData<Attach>(e).Parent != mixer) continue;

                    // Prefer explicit external inventory prefab
                    if (em.HasComponent<PrefabGUID>(e) &&
                        em.GetComponentData<PrefabGUID>(e).GuidHash == External_Inventory.GuidHash &&
                        em.HasBuffer<InventoryBuffer>(e))
                    {
                        inv = e;
                        break;
                    }

                    // Fallback: any child with inventory slots
                    if (em.HasBuffer<InventoryBuffer>(e))
                        inv = e;
                }
            }
            finally
            {
                ents.Dispose();
                q.Dispose();
            }

            return inv != Entity.Null;
        }

        private static int CountItem(EntityManager em, Entity inv, PrefabGUID item)
        {
            int total = 0;
            if (!em.HasBuffer<InventoryBuffer>(inv)) return 0;

            var buf = em.GetBuffer<InventoryBuffer>(inv);
            for (int i = 0; i < buf.Length; i++)
            {
                var slot = buf[i];
                if (slot.ItemType.GuidHash == item.GuidHash && slot.Amount > 0)
                    total += slot.Amount;
            }
            return total;
        }

        // -------- ItemData resolution (silent) --------

        private static readonly Dictionary<int, ItemData> _cache = new();

        public static bool TryResolveItemData(EntityManager em, PrefabGUID guid, out ItemData itemData)
        {
            var key = guid.GuidHash;
            if (_cache.TryGetValue(key, out itemData))
                return true;

            // 1) PrefabCollectionSystem
            try
            {
                var pcs = em.World.GetExistingSystemManaged<PrefabCollectionSystem>();
                if (pcs != null &&
                    (pcs.PrefabLookupMap.TryGetValue(guid, out var prefab) ||
                     pcs.PrefabLookupMap.TryGetValue(new PrefabGUID(key), out prefab)) &&
                    prefab != Entity.Null &&
                    em.HasComponent<ItemData>(prefab))
                {
                    itemData = em.GetComponentData<ItemData>(prefab);
                    _cache[key] = itemData;
                    return true;
                }
            }
            catch { /* ignore, fallback to scan */ }

            // 2) Fallback scan of prefab entities
            var q = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Unity.Entities.Prefab>(),
                    ComponentType.ReadOnly<PrefabGUID>(),
                    ComponentType.ReadOnly<ItemData>()
                }
            });

            var entities = q.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    if (em.GetComponentData<PrefabGUID>(e).GuidHash != key) continue;

                    itemData = em.GetComponentData<ItemData>(e);
                    _cache[key] = itemData;
                    return true;
                }
            }
            finally
            {
                entities.Dispose();
                q.Dispose();
            }

            itemData = default;
            return false;
        }
    }
}
