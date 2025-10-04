using HarmonyLib;
using ProjectM;
using Stunlock.Network;
using System.Collections;
using Unity.Entities;
using UnityEngine;
using VeinWares.SubtleByte.Extensions;   // for .Exists(), .IsPlayer(), .GetUser(), .GetPlayerName()
using VeinWares.SubtleByte.Services;     // PrestigeMini, PrestigeLiveSync, BloodcraftPrestigeReader
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Patches
{
    [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
    internal static class ServerBootstrapSystemPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
        {
            // Resolve approved user -> user entity
            if (!__instance._NetEndPointToApprovedUserIndex.TryGetValue(netConnectionId, out int userIndex))
                return;

            var serverClient = __instance._ApprovedUsersLookup[userIndex];
            var userEntity = serverClient.UserEntity;

            // Defer one frame so LocalCharacter is spawned and linked
            Core.StartCoroutine(HandleConnectNextFrame(userEntity));
        }

        private static IEnumerator HandleConnectNextFrame(Entity userEntity)
        {
            yield return null; // next frame

            if (!userEntity.Exists())
                yield break;

            var user = userEntity.GetUser();                       // ProjectM.Network.User
            var steamId = user.PlatformId;
            var character = user.LocalCharacter.GetEntityOnServer();

            if (!character.Exists())
            {
                // Try one more frame if the character isn’t live yet
                yield return null;
                character = user.LocalCharacter.GetEntityOnServer();
            }
            OneTimeCleanupService.RunOnce(user.PlatformId, character);
            // 1) Register the player with the live file watcher so future changes apply instantly
            PrestigeLiveSync.OnPlayerConnected(steamId, character);

            // 2) Apply once right now from current JSON snapshot (no need to wait for file events)
            if (BloodcraftPrestigeReader.TryGetExperiencePrestige(steamId, out int expPrestige))
            {
                int target = Mathf.Clamp(expPrestige, 0, 10);
                if (target >= 2)
                {
                    PrestigeMini.ApplyLevel(character, target);
                    ModLogger.Info($"[SubtleByte.Prestige] Connected → applied L{target} (cumulative) → {character.GetPlayerName()} ({steamId}).");
                }
                else
                {
                    PrestigeMini.Clear(character);
                    ModLogger.Info($"[SubtleByte.Prestige] Connected → prestige < 2, cleared → {character.GetPlayerName()} ({steamId}).");
                }
            }
            else
            {
                PrestigeMini.Clear(character);
                ModLogger.Info($"[SubtleByte.Prestige] Connected → no prestige record, cleared ({steamId}).");
            }
        }

        [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserDisconnected))]
        static class ServerBootstrapSystem_DisconnectPatch
        {
            [HarmonyPrefix]
            static void Prefix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
            {
                if (!__instance._NetEndPointToApprovedUserIndex.TryGetValue(netConnectionId, out int idx)) return;
                var serverClient = __instance._ApprovedUsersLookup[idx];
                var user = __instance.EntityManager.GetComponentData<ProjectM.Network.User>(serverClient.UserEntity);
                PrestigeLiveSync.OnPlayerDisconnected(user.PlatformId);
            }
        }

    }
}