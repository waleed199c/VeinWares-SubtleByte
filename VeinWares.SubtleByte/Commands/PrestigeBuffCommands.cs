using Newtonsoft.Json.Linq;
using Stunlock.Core;
using System;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using VampireCommandFramework;
using VeinWares.SubtleByte.Config;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Services;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Commands
{
    public static class PrestigeBuffCommands
    {
        // === Player commands ===

        // .tb → toggle prestige buffs
        [Command("tb", description: "Toggle your prestige buffs (on/off).")]
        public static void TogglePrestige(ChatCommandContext ctx)
        {
            var player = ctx.Event.SenderCharacterEntity;
            if (!player.Exists()) { ctx.Reply("[Prestige] No character."); return; }

            int hijackedGuid = SubtleBytePrestigeConfig.HijackedBuffGuid();
            var guid = new PrefabGUID(hijackedGuid);

            bool has = player.HasBuff(guid);
            if (has)
            {
                PrestigeMini.Clear(player);
                ctx.Reply("[Prestige] Disabled.");
                return;
            }

            int level = ReadPrestigeLevel(ctx.Event.User.PlatformId);
            if (level < 2) { ctx.Reply("[Prestige] No eligible prestige level detected."); return; }

            PrestigeMini.Clear(player);
            PrestigeMini.ApplyLevel(player, level);
            ctx.Reply($"[Prestige] Enabled (level {level}).");
        }

        // .sb / sync buffs → reapply appropriate prestige buffs
        [Command("sb", description: "Re-apply your correct prestige buffs.")]
        public static void SyncPrestige(ChatCommandContext ctx)
        {
            var player = ctx.Event.SenderCharacterEntity;
            if (!player.Exists()) { ctx.Reply("[Prestige] No character."); return; }

            int level = ReadPrestigeLevel(ctx.Event.User.PlatformId);
            if (level < 2) { ctx.Reply("[Prestige] No eligible prestige level detected."); return; }

            PrestigeMini.Clear(player);
            PrestigeMini.ApplyLevel(player, level);
            ctx.Reply($"[Prestige] Synchronized (level {level}).");
        }

        [Command("sync buffs")]
        public static void SyncPrestigeAlias(ChatCommandContext ctx) => SyncPrestige(ctx);


        // === Helpers ===

        // Try to read a player's prestige LEVEL robustly from Bloodcraft's file.
        private static int ReadPrestigeLevel(ulong platformId)
        {
            // Uses /BepInEx/config/Bloodcraft/PlayerLeveling/player_prestiges.json
            // and handles the weird shapes safely under Il2Cpp.
            return BloodcraftPrestigeReader.TryGetExperiencePrestige(platformId, out var lvl)
                ? Math.Clamp(lvl, 0, 10)
                : 0;
        }
       
    }
}
