//using VeinWares.SubtleByte.Utilities;
//using ProjectM;
//using ProjectM.Shared;
//using Stunlock.Core;
//using Unity.Entities;
//using VeinWares.SubtleByte.Extensions;

//namespace VeinWares.SubtleByte.Services
//{
//    public static class RecipeService
//    {
//        private static EntityManager EntityManager => Core.EntityManager;

//        private static PrefabCollectionSystem _prefabCollectionSystem;
//        private static GameDataSystem _gameDataSystem;

//        private static PrefabCollectionSystem PrefabCollectionSystem => _prefabCollectionSystem ??= Core.Server.GetExistingSystemManaged<PrefabCollectionSystem>();
//        private static GameDataSystem GameDataSystem => _gameDataSystem ??= Core.Server.GetExistingSystemManaged<GameDataSystem>();

//        private static readonly PrefabGUID _advancedBloodPress = new(-684391635); // Advanced Blood Press

//        private static readonly PrefabGUID _immortalPotion = new(828432508); // fake blood potion prefab

//        // Input ingredients
//        private static readonly PrefabGUID _warriorBlood = new(1223264867);
//        private static readonly PrefabGUID _scholarBlood = new(1223264867);
//        private static readonly PrefabGUID _demonFragment = new(-77477508);
//        private static readonly PrefabGUID _radiantFiber = new(-182923609);

//        // Reusing blood crystal recipe GUID to avoid dummy prefab issues
//        private static readonly PrefabGUID _immortalBloodRecipe = new(-597461125);

//        public static void ApplyPatches()
//        {
//            if (!PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(_immortalBloodRecipe, out var recipeEntity))
//            {
//                ModLogger.Warn("[RecipeService] Could not find recipe prefab for Immortal Blood override.");
//                return;
//            }

//            var requirements = EntityManager.GetBuffer<RecipeRequirementBuffer>(recipeEntity);
//            requirements.Clear();
//            //requirements.Add(new RecipeRequirementBuffer { Guid = _warriorBlood, Amount = 1 });
//            //requirements.Add(new RecipeRequirementBuffer { Guid = _scholarBlood, Amount = 1 });
//            requirements.Add(new RecipeRequirementBuffer { Guid = _demonFragment, Amount = 5 });
//            //requirements.Add(new RecipeRequirementBuffer { Guid = _radiantFiber, Amount = 10 });

//            var outputs = EntityManager.GetBuffer<RecipeOutputBuffer>(recipeEntity);
//            outputs.Clear();
//            outputs.Add(new RecipeOutputBuffer { Guid = _immortalPotion, Amount = 1 });

//            recipeEntity.With((ref RecipeData data) =>
//            {
//                data.CraftDuration = 10f;
//                data.AlwaysUnlocked = true;
//                data.HideInStation = false;
//                data.HudSortingOrder = 0;
//            });

//            if (!PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(_advancedBloodPress, out var stationEntity))
//            {
//                ModLogger.Warn("[RecipeService] Could not find Advanced Blood Press station.");
//                return;
//            }

//            var refinement = EntityManager.GetBuffer<RefinementstationRecipesBuffer>(stationEntity);
//            refinement.Add(new RefinementstationRecipesBuffer
//            {
//                RecipeGuid = _immortalBloodRecipe,
//                Disabled = false,
//                Unlocked = true
//            });

//            if (GameDataSystem.RecipeHashLookupMap.ContainsKey(_immortalBloodRecipe))
//                GameDataSystem.RecipeHashLookupMap.Remove(_immortalBloodRecipe);

//            GameDataSystem.RecipeHashLookupMap.Add(_immortalBloodRecipe, EntityManager.GetComponentData<RecipeData>(recipeEntity));

//            GameDataSystem.RegisterRecipes();
//            GameDataSystem.RegisterItems();
//            PrefabCollectionSystem.RegisterGameData();

//            ModLogger.Info("[RecipeService] Injected Immortal Blood recipe successfully.");
//        }
//    }
//}