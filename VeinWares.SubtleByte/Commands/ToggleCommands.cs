using Newtonsoft.Json.Linq;
using Stunlock.Core;
using System;
using System.IO;
using VampireCommandFramework;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Services;
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

        [Command("clearrelics", shortHand: "cr")]
        public static void ClearRelicBuffs(ChatCommandContext ctx)
        {
            var player = ctx.Event.SenderCharacterEntity;

            PrefabGUID[] relics = {
                new PrefabGUID(1068709119),
                new PrefabGUID(-1703886455),
                new PrefabGUID(-1161197991),
                new PrefabGUID(-238197495),
            };

            foreach (var relic in relics)
                player.TryRemoveBuff(relic);

            ctx.Reply("[RelicPatch] All relic buffs cleared.");
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
                //SBlog.Info($"[Toggle] {ctx.Name} ({steamId}) has {exp} prestige EXP (needed {requiredExp}).");

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
                SBlog.Error($"[Toggle] Error while handling {displayName}: {ex}");
                ctx.Reply($"[Toggle] Internal error while toggling {displayName}.");
            }
        }
        [Command("prestige_on", adminOnly: true)]
        public static void PrestigeOn(ChatCommandContext ctx, int level = 2)
        {
            var player = ctx.Event.SenderCharacterEntity;
            if (!player.Exists()) { ctx.Reply("[Prestige] No character."); return; }
            PrestigeMini.ApplyLevel(player, level);
            ctx.Reply($"[Prestige] Applied prestige level {level} (cumulative from 2..{level}).");
        }

        [Command("prestige_off", adminOnly: true)]
        public static void PrestigeOff(ChatCommandContext ctx)
        {
            var player = ctx.Event.SenderCharacterEntity;
            if (!player.Exists()) { ctx.Reply("[Prestige] No character."); return; }
            PrestigeMini.Clear(player);
            ctx.Reply("[Prestige] Removed prestige buff.");
        }

        [Command("prestige_reload", adminOnly: true)]
        public static void PrestigeReload(ChatCommandContext ctx)
        {
            SubtleBytePrestigeConfig.Reload();
            ctx.Reply("[Prestige] Reloaded SubtleBytePrestigeConfig.json");
        }

        [Command("toggle hideweapons", shortHand: "hw", description: "Toggle hiding your weapon model (visual only).")]
        public static void ToggleHideWeapons(ChatCommandContext ctx)
        {
            var player = ctx.Event.SenderCharacterEntity;
            if (!player.Exists()) { ctx.Reply("[Hide] No character."); return; }

            // Buff_ChurchOfLight_SlaveMaster_HideWhip_Buff
            var hideBuff = new PrefabGUID(-1104282069);

            if (!player.HasBuff(hideBuff))
            {
                // uses your extension → applies and persists through death (same style as your other toggles)
                player.TryApplyPermanentBuff(hideBuff);
                ctx.Reply("[Hide] Weapons hidden.");
            }
            else
            {
                player.TryRemoveBuff(hideBuff);
                ctx.Reply("[Hide] Weapons visible.");
            }

        }
    }
}
