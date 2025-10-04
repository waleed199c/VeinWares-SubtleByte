using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Utilities;


namespace VeinWares.SubtleByte.Services
{
    public static class ItemStackService
    {
        public static void ApplyPatches()
        {
            foreach (var entry in ItemStackConfig.Entries)
            {
                var prefabGuid = new PrefabGUID(entry.PrefabGUID);
                var prefabMap = Core.Server.GetExistingSystemManaged<PrefabCollectionSystem>()._PrefabGuidToEntityMap;

                if (!prefabMap.TryGetValue(prefabGuid, out var prefabEntity))
                {
                    ModLogger.Warn($"[Services] Prefab not found for GUID {entry.PrefabGUID}");
                    continue;
                }

                var itemData = prefabEntity.Read<ItemData>();
                itemData.MaxAmount = entry.StackSize > 4000 ? 4000 : entry.StackSize;
                prefabEntity.Write(itemData);

                var gameDataMap = Core.Server.GetExistingSystemManaged<GameDataSystem>().ItemHashLookupMap;
                gameDataMap[prefabGuid] = itemData;

                ModLogger.Info($"[Services] Set max stack to {itemData.MaxAmount} for GUID={entry.PrefabGUID}");
            }

        }
    }
}