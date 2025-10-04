using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using BepInEx;
using Unity.Entities;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Services
{
    internal static class PrestigeLiveSync
    {
        // ---- File & JSON ----
        private static readonly string PrestigeFile = Path.Combine(
            Paths.ConfigPath, "Bloodcraft", "PlayerLeveling", "player_prestiges.json"
        );
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        private sealed class PlayerPrestigeMap : Dictionary<string, Dictionary<string, int>> { }

        // ---- Watcher ----
        private static FileSystemWatcher _watcher;
        // debounce to coalesce multiple FS events
        private static Timer _debounceTimer;
        private const int DebounceMs = 300;

        // last snapshot of Experience prestige (SteamID -> level)
        private static readonly Dictionary<ulong, int> _snapshot = new();
        // online players we care about (SteamID -> Character entity)
        private static readonly Dictionary<ulong, Entity> _online = new();

        private static readonly object _lock = new();

        public static void Initialize()
        {
            // seed snapshot (if file exists)
            TryRefreshSnapshot();

            // setup watcher
            try
            {
                var dir = Path.GetDirectoryName(PrestigeFile);
                var file = Path.GetFileName(PrestigeFile);
                if (dir == null || file == null) return;

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                _watcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
                };
                _watcher.Changed += OnChanged;
                _watcher.Created += OnChanged;
                _watcher.Renamed += OnRenamed;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                ModLogger.Error($"[PrestigeSync] FileSystemWatcher init failed: {e.Message}");
            }
        }

        public static void OnPlayerConnected(ulong steamId, Entity character)
        {
            lock (_lock)
            {
                _online[steamId] = character;
            }
            // apply immediately based on current snapshot
            Core.RunNextFrame(() => ApplyFromSnapshot(steamId, character));
        }

        public static void OnPlayerDisconnected(ulong steamId)
        {
            lock (_lock)
            {
                _online.Remove(steamId);
            }
        }

        // ---- FS events (debounced) ----
        private static void OnChanged(object s, FileSystemEventArgs e)
        {
            DebounceReload();
        }
        private static void OnRenamed(object s, RenamedEventArgs e)
        {
            DebounceReload();
        }

        private static void DebounceReload()
        {
            lock (_lock)
            {
                _debounceTimer ??= new Timer(_ =>
                {
                    try
                    {
                        // jump back to main thread to interact with Entities safely
                        Core.RunNextFrame(ApplyDiffToOnlinePlayers);
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error($"[PrestigeSync] Debounce apply failed: {ex}");
                    }
                }, null, Timeout.Infinite, Timeout.Infinite);

                _debounceTimer.Change(DebounceMs, Timeout.Infinite);
            }
        }

        // ---- Core logic ----

        private static void ApplyDiffToOnlinePlayers()
        {
            Dictionary<ulong, int> newSnap;
            if (!TryReadExpMap(out newSnap))
            {
                ModLogger.Warn("[PrestigeSync] Could not read prestige file; skipping update.");
                return;
            }

            List<(ulong steamId, int oldLv, int newLv, Entity ch)> changes = new();

            lock (_lock)
            {
                // compute diff for online players only
                foreach (var kv in _online.ToArray())
                {
                    var steamId = kv.Key;
                    var ch = kv.Value;
                    if (!ch.Exists() || !ch.IsPlayer()) continue;

                    var oldLv = _snapshot.TryGetValue(steamId, out var ov) ? ov : 0;
                    var newLv = newSnap.TryGetValue(steamId, out var nv) ? nv : 0;
                    if (oldLv != newLv)
                        changes.Add((steamId, oldLv, newLv, ch));
                }

                // replace snapshot
                _snapshot.Clear();
                foreach (var kv in newSnap) _snapshot[kv.Key] = kv.Value;
            }

            foreach (var (steamId, oldLv, newLv, ch) in changes)
            {
                int target = Math.Clamp(newLv, 0, 10);
                if (target >= 2)
                {
                    PrestigeMini.ApplyLevel(ch, target);
                    ModLogger.Info($"[PrestigeSync] {ch.GetPlayerName()} prestige changed {oldLv}→{newLv}, applied L{target}.");
                }
                else
                {
                    PrestigeMini.Clear(ch);
                    ModLogger.Info($"[PrestigeSync] {ch.GetPlayerName()} prestige changed {oldLv}→{newLv}, cleared (below 2).");
                }
            }
        }

        private static void ApplyFromSnapshot(ulong steamId, Entity ch)
        {
            int lvl = 0;
            lock (_lock)
            {
                _snapshot.TryGetValue(steamId, out lvl);
            }
            int target = Math.Clamp(lvl, 0, 10);
            if (target >= 2) PrestigeMini.ApplyLevel(ch, target);
            else PrestigeMini.Clear(ch);
        }

        private static void TryRefreshSnapshot()
        {
            if (TryReadExpMap(out var map))
            {
                lock (_lock)
                {
                    _snapshot.Clear();
                    foreach (var kv in map) _snapshot[kv.Key] = kv.Value;
                }
            }
        }

        // read Experience prestige -> map of steamId to level
        private static bool TryReadExpMap(out Dictionary<ulong, int> map)
        {
            map = new();
            try
            {
                if (!File.Exists(PrestigeFile)) return false;

                // avoid sharing violation by brief retry
                string json = null!;
                for (int i = 0; i < 5; i++)
                {
                    try { json = File.ReadAllText(PrestigeFile); break; }
                    catch (IOException) { Thread.Sleep(20); }
                }
                if (string.IsNullOrEmpty(json)) return false;

                var root = JsonSerializer.Deserialize<PlayerPrestigeMap>(json, JsonOpts);
                if (root == null) return false;

                foreach (var (idStr, dict) in root)
                {
                    if (ulong.TryParse(idStr, out var sid) && dict != null)
                    {
                        if (dict.TryGetValue("Experience", out int exp) ||
                            dict.TryGetValue("experience", out exp))
                            map[sid] = exp;
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                ModLogger.Error($"[PrestigeSync] Read error: {e.Message}");
                return false;
            }
        }
    }
}