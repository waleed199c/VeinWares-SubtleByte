using Newtonsoft.Json.Linq;
using Stunlock.Core;
using System;
using System.IO;
using VampireCommandFramework;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Commands
{
    public static class ToggleCommands
    {
        private static readonly string PrestigeFile = Path.Combine(
            BepInEx.Paths.ConfigPath,
            "Bloodcraft",
            "PlayerLeveling",
            "player_prestiges.json"
        );

        [Command("toggle bloodmoon", shortHand: "tbm")]
        public static void ToggleBloodmoon(ChatCommandContext ctx)
        {
            HandleCheck(ctx, 8, "Bloodmoon", Buffs.BloodmoonBuff);
        }

        [Command("toggle crown", shortHand: "tcr")]
        public static void ToggleCrown(ChatCommandContext ctx)
        {
            HandleCheck(ctx, 10, "Crown", Buffs.CrownBuff);
        }

        private static void HandleCheck(ChatCommandContext ctx, int requiredExp, string displayName, PrefabGUID buffPrefab)
        {
            try
            {
                string steamId = ctx.Event.User.PlatformId.ToString();

                // Load the file
                if (!File.Exists(PrestigeFile))
                {
                    ctx.Reply("[Toggle] Prestige data file not found.");
                    return;
                }

                var json = File.ReadAllText(PrestigeFile);
                var root = JObject.Parse(json);

                // Make sure we have data for this steamId
                var playerToken = root[steamId];
                if (playerToken == null)
                {
                    ctx.Reply($"[Toggle] No prestige data found for {ctx.Name} ({steamId}).");
                    return;
                }

                // Convert safely into JObject
                var playerData = playerToken.ToObject<JObject>();
                if (playerData == null)
                {
                    ctx.Reply($"[Toggle] Invalid prestige data format for {ctx.Name}.");
                    return;
                }

                // Grab experience (default to 0 if missing)
                int exp = playerData["Experience"]?.ToObject<int>() ?? 0;

                // Debug logging
                //Core.Log.LogInfo($"[Toggle] {ctx.Name} ({steamId}) has {exp} prestige EXP (needed {requiredExp}).");

                if (exp >= requiredExp)
                {
                    var player = ctx.Event.SenderCharacterEntity;

                    if (!player.HasBuff(buffPrefab))
                    {
                        player.TryApplyPermanentBuff(buffPrefab);
                        ctx.Reply($"[Toggle] {displayName} power activated.");
                    }
                    else
                    {
                        player.TryRemoveBuff(buffPrefab);
                        ctx.Reply($"[Toggle] {displayName} power removed.");
                    }
                }
                else
                {
                    ctx.Reply($"[Toggle] Your prestige ({exp}) is too low for {displayName}. Required: {requiredExp}.");
                }
            }
            catch (Exception ex)
            {
                Core.Log.LogError($"[Toggle] Error while handling {displayName}: {ex}");
                ctx.Reply($"[Toggle] Internal error while toggling {displayName}.");
            }
        }
    }
}
