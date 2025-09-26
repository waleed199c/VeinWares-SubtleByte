using ProjectM;
using Stunlock.Core;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Collections;
using Unity.Entities;
using VampireCommandFramework;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Commands
{
    public static class BuffClearCommands
    {
        // .cb <guid ...>  → clear buffs from YOU
        // Examples:
        //   .cb 1068709119
        //   .cb 1068709119 -1703886455 0x3F2A1B0C
        [Command("clear buffs", shortHand: "cb", description: "Clear one or more buffs (by GUID) from yourself.")]
        public static void ClearMyBuffs(ChatCommandContext ctx, params string[] guids)
        {
            var player = ctx.Event.SenderCharacterEntity;
            if (!player.Exists()) { ctx.Reply("[Clear] No character."); return; }

            var parsed = ParseGuids(guids);
            if (parsed.Length == 0) { ctx.Reply("[Clear] Provide one or more GUIDs."); return; }

            int removed = 0;
            foreach (var g in parsed)
            {
                var guid = new PrefabGUID(g);
                // TryRemoveBuff returns void; detect via TryGetBuff first
                if (player.TryGetBuff(guid, out var buffEntity))
                {
                    buffEntity.DestroyBuff();
                    removed++;
                }
            }

            ctx.Reply($"[Clear] Removed {removed}/{parsed.Length} buff(s) from you.");
        }

        // .cba <guid ...> → ADMIN: clear buffs from ALL online players
        // Examples:
        //   .cba 1068709119
        //   .cba 1068709119 -1703886455
        [Command("clear buffs all", shortHand: "cba", adminOnly: true, description: "Admin: clear one or more buffs (by GUID) from all online players.")]
        public static void ClearBuffsAll(ChatCommandContext ctx, params string[] guids)
        {
            var parsed = ParseGuids(guids);
            if (parsed.Length == 0) { ctx.Reply("[ClearAll] Provide one or more GUIDs."); return; }

            var em = Core.EntityManager;
            var q = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PlayerCharacter>() }
            });
            var players = q.ToEntityArray(Allocator.Temp);

            int affectedPlayers = 0, totalRemovals = 0;
            foreach (var p in players)
            {
                if (!p.Exists()) continue;

                bool removedAny = false;
                foreach (var g in parsed)
                {
                    var guid = new PrefabGUID(g);
                    if (p.TryGetBuff(guid, out var buffEntity))
                    {
                        buffEntity.DestroyBuff();
                        totalRemovals++;
                        removedAny = true;
                    }
                }
                if (removedAny) affectedPlayers++;
            }
            players.Dispose();

            ctx.Reply($"[ClearAll] Removed {totalRemovals} instance(s) across {affectedPlayers} player(s).");
        }

        // Accepts tokens like: 1068709119  -1703886455  0x3F2A1B0C
        private static int[] ParseGuids(string[] tokens)
        {
            if (tokens == null || tokens.Length == 0) return Array.Empty<int>();

            return tokens
                .SelectMany(t => Regex.Split(t ?? string.Empty, @"[\s,;]+"))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(ParseOne)
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .Distinct()
                .ToArray();
        }

        private static int? ParseOne(string s)
        {
            s = s.Trim();
            try
            {
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt32(s.Substring(2), 16);
                return int.Parse(s);
            }
            catch { return null; }
        }
    }
}
