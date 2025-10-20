using ProjectM;
using ProjectM.Network;
using ProjectM.Scripting;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Services;

namespace VeinWares.SubtleByte.Utilities
{
    internal static class Buffs
    {
        static ServerGameManager ServerGameManager => Core.ServerGameManager;
        static SystemService SystemService => Core.SystemService;
        static DebugEventsSystem DebugEventsSystem => SystemService.DebugEventsSystem;

        // Example Buff Prefabs – replace with your GUIDs
        public static readonly PrefabGUID BloodmoonBuff = new PrefabGUID(-560523291);
        public static readonly PrefabGUID CrownBuff = new PrefabGUID(1516078366);     

        /// <summary>
        /// Applies a buff to the entity with no lifetime (persistent until removed).
        /// </summary>
        public static bool TryApplyBuffWithLifeTimeNone(this Entity entity, PrefabGUID buffPrefabGuid)
        {
            if (entity.TryApplyAndGetBuff(buffPrefabGuid, out Entity buffEntity))
            {
                if (buffEntity.Has<LifeTime>())
                {
                    buffEntity.With((ref LifeTime lifeTime) =>
                    {
                        lifeTime.Duration = 0f;
                        lifeTime.EndAction = LifeTimeEndAction.None;
                    });
                }
                return true;
            }   
            return false;
        }

        public static bool TryApplyVisualBuff(this Entity entity, PrefabGUID buffPrefab)
        {
            Entity buffEntity;
            if (!entity.TryApplyAndGetBuff(buffPrefab, out buffEntity))
            {
                if (!entity.TryGetBuff(buffPrefab, out buffEntity))
                {
                    return false;
                }
            }

            if (!buffEntity.Exists())
            {
                return false;
            }

            if (buffEntity.Has<ApplyBuffOnGameplayEvent>()) buffEntity.Remove<ApplyBuffOnGameplayEvent>();
            if (buffEntity.Has<RemoveBuffOnGameplayEvent>()) buffEntity.Remove<RemoveBuffOnGameplayEvent>();
            if (buffEntity.Has<RemoveBuffOnGameplayEventEntry>()) buffEntity.Remove<RemoveBuffOnGameplayEventEntry>();
            if (buffEntity.Has<CreateGameplayEventsOnSpawn>()) buffEntity.Remove<CreateGameplayEventsOnSpawn>();
            if (buffEntity.Has<GameplayEventListeners>()) buffEntity.Remove<GameplayEventListeners>();
            if (buffEntity.Has<DealDamageOnGameplayEvent>()) buffEntity.Remove<DealDamageOnGameplayEvent>();
            if (buffEntity.Has<HealOnGameplayEvent>()) buffEntity.Remove<HealOnGameplayEvent>();
            if (buffEntity.Has<DestroyOnGameplayEvent>()) buffEntity.Remove<DestroyOnGameplayEvent>();
            if (buffEntity.Has<WeakenBuff>()) buffEntity.Remove<WeakenBuff>();
            if (buffEntity.Has<AmplifyBuff>()) buffEntity.Remove<AmplifyBuff>();
            if (buffEntity.Has<ReplaceAbilityOnSlotBuff>()) buffEntity.Remove<ReplaceAbilityOnSlotBuff>();
            if (buffEntity.Has<ModifyMovementSpeedBuff>()) buffEntity.Remove<ModifyMovementSpeedBuff>();
            if (buffEntity.Has<BloodBuffScript_ChanceToResetCooldown>()) buffEntity.Remove<BloodBuffScript_ChanceToResetCooldown>();

            if (!buffEntity.Has<Buff_Persists_Through_Death>())
            {
                buffEntity.Add<Buff_Persists_Through_Death>();
            }

            if (buffEntity.Has<LifeTime>())
            {
                buffEntity.With((ref LifeTime lifeTime) =>
                {
                    lifeTime.Duration = 0f;
                    lifeTime.EndAction = LifeTimeEndAction.None;
                });
            }

            return true;
        }

        public static void TryApplyPermanentBuff(this Entity player, PrefabGUID buffPrefab)
        {
            player.TryApplyVisualBuff(buffPrefab);
        }


        /// <summary>
        /// Applies the buff and gets the buff entity back.
        /// </summary>
        public static bool TryApplyAndGetBuff(this Entity entity, PrefabGUID buffPrefabGuid, out Entity buffEntity)
        {
            buffEntity = Entity.Null;

            if (!entity.HasBuff(buffPrefabGuid))
            {
                ApplyBuffDebugEvent applyBuffDebugEvent = new()
                {
                    BuffPrefabGUID = buffPrefabGuid,
                    Who = entity.GetNetworkId(),
                };

                FromCharacter fromCharacter = new()
                {
                    Character = entity,
                    User = entity.IsPlayer() ? entity.GetUserEntity() : entity
                };

                DebugEventsSystem.ApplyBuff(fromCharacter, applyBuffDebugEvent);

                return entity.TryGetBuff(buffPrefabGuid, out buffEntity);
            }

            return false;
        }

        /// <summary>
        /// Checks if an entity currently has the buff.
        /// </summary>
        public static bool HasBuff(this Entity entity, PrefabGUID buffPrefabGuid)
        {
            return ServerGameManager.TryGetBuff(entity, buffPrefabGuid.ToIdentifier(), out _);
        }

        /// <summary>
        /// Try to get an active buff from an entity.
        /// </summary>
        public static bool TryGetBuff(this Entity entity, PrefabGUID buffPrefabGUID, out Entity buffEntity)
        {
            return ServerGameManager.TryGetBuff(entity, buffPrefabGUID.ToIdentifier(), out buffEntity);
        }

        /// <summary>
        /// Removes a buff if present.
        /// </summary>
        public static void TryRemoveBuff(this Entity entity, PrefabGUID buffPrefabGuid)
        {
            if (entity.TryGetBuff(buffPrefabGuid, out Entity buffEntity))
            {
                buffEntity.DestroyBuff();
            }
        }

        public static void RemoveBuff(Entity Character, PrefabGUID buffPrefab)
        {
            if (BuffUtility.TryGetBuff(Core.EntityManager, Character, buffPrefab, out var buffEntity))
            {
                DestroyUtility.Destroy(Core.EntityManager, buffEntity, DestroyDebugReason.TryRemoveBuff);
            }
        }

        public static bool EnsurePersistsThroughDeath(this Entity buffEntity)
        {
           if (!buffEntity.Exists()) return false;
           if (!buffEntity.Has<Buff_Persists_Through_Death>())
           {
               buffEntity.Add<Buff_Persists_Through_Death>();
               return true;
          }
            return false;
        }
    }
}
