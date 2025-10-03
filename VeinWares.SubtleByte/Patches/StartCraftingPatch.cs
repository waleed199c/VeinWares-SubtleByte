//using VeinWares.SubtleByte.Utilities;
//using HarmonyLib;
//using ProjectM;
//using ProjectM.Network;
//using Stunlock.Core;
//using System.Collections.Generic;
//using Unity.Collections;
//namespace VeinWares.SubtleByte.Patches
//{
//    [HarmonyPatch(typeof(StartCraftingSystem), nameof(StartCraftingSystem.OnUpdate))]
//    public static class StartCraftingPatch
//    {
//        private static readonly PrefabGUID ImmortalRecipe = new(-597461125);
//        private static readonly PrefabGUID FakeBloodPotion = new(1223264867);

//        public static void Prefix(StartCraftingSystem __instance)
//        {
//            var entityManager = __instance.EntityManager;
//            var events = __instance._StartCraftItemEventQuery.ToEntityArray(Allocator.Temp);

//            foreach (var evtEntity in events)
//            {
//                if (!entityManager.HasComponent<StartCraftItemEvent>(evtEntity) ||
//                    !entityManager.HasComponent<FromCharacter>(evtEntity))
//                    continue;

//                var evt = entityManager.GetComponentData<StartCraftItemEvent>(evtEntity);

//                if (!evt.RecipeId.Equals(ImmortalRecipe))
//                    continue;

//                var fromCharacter = entityManager.GetComponentData<FromCharacter>(evtEntity);
//                var character = fromCharacter.Character;

//                if (!entityManager.HasComponent<InventoryBuffer>(character))
//                    continue;

//                var inventory = entityManager.GetBuffer<InventoryBuffer>(character);
//                var bloodTypes = new HashSet<int>();

//                foreach (var item in inventory)
//                {
//                    if (!item.ItemType.Equals(FakeBloodPotion)) continue;
//                    if (!entityManager.HasComponent<StoredBlood>(item.ItemEntity.GetEntityOnServer())) continue;

//                    var blood = entityManager.GetComponentData<StoredBlood>(item.ItemEntity.GetEntityOnServer());
//                    if (blood.BloodQuality >= 100f)
//                        bloodTypes.Add(blood.PrimaryBloodType.GuidHash);
//                }

//                if (bloodTypes.Count < 2)
//                {
//                    SBlog.Warn("[StartCraftingPatch] Invalid blood types used. Cancelling craft.");
//                    entityManager.DestroyEntity(evtEntity);
//                }
//                else
//                {
//                    SBlog.Info("[StartCraftingPatch] Valid blood types confirmed. Craft proceeds.");
//                }
//            }

//            events.Dispose();
//        }
//    }
//}