using System;
using System.Collections.Generic;
using System.IO;
using BepInEx; 
using System.Text.Json;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Services
{
    internal static class BloodcraftPrestigeReader
    {
        // BepInEx/config/Bloodcraft/PlayerLeveling/player_prestiges.json
        private static readonly string PrestigeFile = Path.Combine(
            Paths.ConfigPath, "Bloodcraft", "PlayerLeveling", "player_prestiges.json"
        );

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// Expected shape (robust):
        /// {
        ///   "7656119...": { "Experience": 3, "Worker": 0, ... },
        ///   ...
        /// }
        private sealed class PlayerPrestigeMap : Dictionary<string, Dictionary<string, int>> { }

        public static bool TryGetExperiencePrestige(ulong steamId, out int level)
        {
            level = 0;
            try
            {
                if (!File.Exists(PrestigeFile)) return false;

                var json = File.ReadAllText(PrestigeFile);
                var root = JsonSerializer.Deserialize<PlayerPrestigeMap>(json, JsonOpts);
                if (root == null) return false;

                if (!root.TryGetValue(steamId.ToString(), out var dict) || dict == null) return false;
                if (dict.TryGetValue("Experience", out var exp)) { level = exp; return true; }
                if (dict.TryGetValue("experience", out exp)) { level = exp; return true; } // extra leniency
            }
            catch (Exception e)
            {
                SBlog.Error($"[SubtleByte.Prestige] Failed reading Bloodcraft prestige JSON: {e.Message}");
            }
            return false;
        }
    }
}