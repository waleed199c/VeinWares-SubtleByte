using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using ProjectM;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VeinWares.SubtleByte;

namespace VeinWares.SubtleByte.Services.FactionInfamy;

internal static class FactionInfamyAmbushData
{
    private const string SquadFileName = "FactionInfamyAmbushSquads.json";
    private const string LootFileName = "FactionInfamyAmbushLoot.json";
    private const int ReloadDebounceMs = 300;

    private static readonly string ConfigDirectory = Path.Combine(Paths.ConfigPath, "VeinWares SubtleByte");
    private static readonly string SquadConfigPath = Path.Combine(ConfigDirectory, SquadFileName);
    private static readonly string LootConfigPath = Path.Combine(ConfigDirectory, LootFileName);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private static readonly object Sync = new();

    private static ManualLogSource? _log;
    private static bool _initialized;
    private static SquadConfigSnapshot _squadSnapshot = SquadConfigSnapshot.Empty;
    private static LootConfigSnapshot _lootSnapshot = LootConfigSnapshot.Empty;
    private static FileSystemWatcher? _squadWatcher;
    private static FileSystemWatcher? _lootWatcher;
    private static Timer? _squadReloadTimer;
    private static Timer? _lootReloadTimer;

    private static readonly Dictionary<string, Entity> LootPrefabCache = new(StringComparer.OrdinalIgnoreCase);

    internal static event Action? SquadDefinitionsChanged;
    internal static event Action? LootDefinitionsChanged;

    internal static SquadConfigSnapshot SquadSnapshot => _squadSnapshot;

    internal static LootConfigSnapshot LootSnapshot => _lootSnapshot;

    internal static void Initialize(ManualLogSource log)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            _log = log;

            Directory.CreateDirectory(ConfigDirectory);
            EnsureSquadFile();
            EnsureLootFile();

            _squadSnapshot = SafeLoadSquadSnapshot();
            _lootSnapshot = SafeLoadLootSnapshot();

            StartWatchers();
            _initialized = true;
        }
    }

    internal static void Shutdown()
    {
        lock (Sync)
        {
            _squadWatcher?.Dispose();
            _lootWatcher?.Dispose();
            _squadReloadTimer?.Dispose();
            _lootReloadTimer?.Dispose();
            _squadWatcher = null;
            _lootWatcher = null;
            _squadReloadTimer = null;
            _lootReloadTimer = null;
            _initialized = false;
        }
    }

    internal static bool TryReloadFromCommand(out string message)
    {
        lock (Sync)
        {
            if (!_initialized)
            {
                message = "Ambush data has not been initialised.";
                return false;
            }

            try
            {
                _squadSnapshot = SafeLoadSquadSnapshot();
                _lootSnapshot = SafeLoadLootSnapshot();
                ClearLootPrefabCache();
            }
            catch (Exception ex)
            {
                message = $"Failed to reload ambush configuration: {ex.Message}";
                return false;
            }
        }

        SquadDefinitionsChanged?.Invoke();
        LootDefinitionsChanged?.Invoke();

        message = "Reloaded ambush squad and loot definitions.";
        return true;
    }

    internal static bool TryGetSquadDefinition(string factionId, out AmbushSquadDefinition squad)
    {
        if (string.IsNullOrWhiteSpace(factionId))
        {
            squad = default!;
            return false;
        }

        return _squadSnapshot.Squads.TryGetValue(factionId, out squad);
    }

    internal static IReadOnlyList<PrefabGUID> GetDefaultVisualPool()
    {
        return _squadSnapshot.DefaultVisuals;
    }

    internal static bool TryGetVisualPool(string factionId, out IReadOnlyList<PrefabGUID> pool)
    {
        if (string.IsNullOrWhiteSpace(factionId))
        {
            pool = Array.Empty<PrefabGUID>();
            return false;
        }

        return _squadSnapshot.VisualPools.TryGetValue(factionId, out pool);
    }

    internal static bool TryGetFactionByGuid(int guidHash, out string factionId, out float baseHate)
    {
        if (_squadSnapshot.AggregatedFactions.TryGetValue(guidHash, out factionId))
        {
            baseHate = _squadSnapshot.BaseHateOverrides.TryGetValue(guidHash, out var overrideValue)
                ? overrideValue
                : 0f;
            return true;
        }

        factionId = string.Empty;
        baseHate = 0f;
        return false;
    }

    internal static bool TryResolveFactionGuid(string factionId, out PrefabGUID prefabGuid)
    {
        if (string.IsNullOrWhiteSpace(factionId))
        {
            prefabGuid = default;
            return false;
        }

        return _squadSnapshot.FactionReverseMap.TryGetValue(factionId, out prefabGuid);
    }

    internal static IReadOnlyCollection<string> GetKnownFactions()
    {
        return _squadSnapshot.Squads.Keys;
    }

    internal static bool TryGetLootDefinition(string factionId, out AmbushLootDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(factionId))
        {
            definition = AmbushLootDefinition.Empty;
            return false;
        }

        return _lootSnapshot.Definitions.TryGetValue(factionId, out definition);
    }

    internal static bool TryEnsureLootPrefab(EntityManager entityManager, string factionId, AmbushLootDefinition definition, out Entity prefabEntity)
    {
        lock (Sync)
        {
            if (LootPrefabCache.TryGetValue(factionId, out prefabEntity))
            {
                if (prefabEntity != Entity.Null && entityManager.Exists(prefabEntity))
                {
                    RefreshDropTableEntity(entityManager, prefabEntity, definition);
                    return true;
                }

                LootPrefabCache.Remove(factionId);
            }

            prefabEntity = CreateDropTableEntity(entityManager, definition);
            if (prefabEntity == Entity.Null)
            {
                return false;
            }

            LootPrefabCache[factionId] = prefabEntity;
            return true;
        }
    }

    internal static void ClearLootPrefabCache()
    {
        lock (Sync)
        {
            if (LootPrefabCache.Count == 0)
            {
                return;
            }

            var entityManager = Core.EntityManager;
            var prefabMap = Core.PrefabCollectionSystem?._PrefabGuidToEntityMap;

            foreach (var pair in LootPrefabCache.ToArray())
            {
                var entity = pair.Value;
                if (prefabMap != null && _lootSnapshot.Definitions.TryGetValue(pair.Key, out var definition))
                {
                    prefabMap.Remove(definition.DropTableGuid);
                }

                if (entityManager.Exists(entity))
                {
                    entityManager.DestroyEntity(entity);
                }

                LootPrefabCache.Remove(pair.Key);
            }
        }
    }

    private static SquadConfigSnapshot SafeLoadSquadSnapshot()
    {
        try
        {
            using var stream = File.OpenRead(SquadConfigPath);
            var payload = JsonSerializer.Deserialize<SquadConfigFile>(stream, JsonOptions);
            if (payload is null)
            {
                throw new InvalidOperationException("Squad configuration file was empty.");
            }

            return SquadConfigSnapshot.From(payload, _log);
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Infamy] Failed to load ambush squad configuration ({ex.Message}). Using fallback definitions.");
            return SquadConfigSnapshot.CreateFallback(_log);
        }
    }

    private static LootConfigSnapshot SafeLoadLootSnapshot()
    {
        try
        {
            using var stream = File.OpenRead(LootConfigPath);
            var payload = JsonSerializer.Deserialize<LootConfigFile>(stream, JsonOptions);
            if (payload is null)
            {
                throw new InvalidOperationException("Loot configuration file was empty.");
            }

            return LootConfigSnapshot.From(payload, _log);
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Infamy] Failed to load ambush loot configuration ({ex.Message}). Using fallback definitions.");
            return LootConfigSnapshot.CreateFallback(_log);
        }
    }

    private static void StartWatchers()
    {
        try
        {
            _squadWatcher = CreateWatcher(SquadConfigPath, DebounceSquadReload);
            _lootWatcher = CreateWatcher(LootConfigPath, DebounceLootReload);
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Infamy] Failed to start ambush configuration watchers: {ex.Message}");
        }
    }

    private static FileSystemWatcher CreateWatcher(string path, Action trigger)
    {
        var directory = Path.GetDirectoryName(path) ?? ConfigDirectory;
        var fileName = Path.GetFileName(path);

        var watcher = new FileSystemWatcher(directory, fileName)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        FileSystemEventHandler handler = (_, _) => trigger();
        RenamedEventHandler renamed = (_, _) => trigger();

        watcher.Changed += handler;
        watcher.Created += handler;
        watcher.Renamed += renamed;
        watcher.EnableRaisingEvents = true;

        return watcher;
    }

    private static void DebounceSquadReload()
    {
        lock (Sync)
        {
            _squadReloadTimer ??= new Timer(_ =>
            {
                Core.RunNextFrame(() =>
                {
                    lock (Sync)
                    {
                        _squadSnapshot = SafeLoadSquadSnapshot();
                        ClearLootPrefabCache();
                    }

                    SquadDefinitionsChanged?.Invoke();
                });
            }, null, Timeout.Infinite, Timeout.Infinite);

            _squadReloadTimer.Change(ReloadDebounceMs, Timeout.Infinite);
        }
    }

    private static void DebounceLootReload()
    {
        lock (Sync)
        {
            _lootReloadTimer ??= new Timer(_ =>
            {
                Core.RunNextFrame(() =>
                {
                    lock (Sync)
                    {
                        _lootSnapshot = SafeLoadLootSnapshot();
                        ClearLootPrefabCache();
                    }

                    LootDefinitionsChanged?.Invoke();
                });
            }, null, Timeout.Infinite, Timeout.Infinite);

            _lootReloadTimer.Change(ReloadDebounceMs, Timeout.Infinite);
        }
    }

    private static void EnsureSquadFile()
    {
        if (File.Exists(SquadConfigPath))
        {
            return;
        }

        File.WriteAllText(SquadConfigPath, DefaultSquadJson, Encoding.UTF8);
        _log?.LogInfo($"[Infamy] Created default ambush squad configuration at '{SquadConfigPath}'.");
    }

    private static void EnsureLootFile()
    {
        if (File.Exists(LootConfigPath))
        {
            return;
        }

        File.WriteAllText(LootConfigPath, DefaultLootJson, Encoding.UTF8);
        _log?.LogInfo($"[Infamy] Created default ambush loot configuration at '{LootConfigPath}'.");
    }

    private static Entity CreateDropTableEntity(EntityManager entityManager, AmbushLootDefinition definition)
    {
        if (definition.Entries.Count == 0)
        {
            return Entity.Null;
        }

        try
        {
            var entity = entityManager.CreateEntity(
                typeof(DropTableData),
                typeof(DropTableDataBuffer),
                typeof(PrefabGUID),
                typeof(Prefab),
                typeof(SpawnTag),
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(Translation),
                typeof(Rotation));

            entityManager.SetComponentData(entity, definition.DropTableGuid);

            var dropTableData = new DropTableData
            {
                Guid = definition.DropTableGuid,
                DropTableLevel = definition.Level
            };
            entityManager.SetComponentData(entity, dropTableData);

            var buffer = entityManager.GetBuffer<DropTableDataBuffer>(entity);
            buffer.Clear();
            foreach (var entry in definition.Entries)
            {
                buffer.Add(new DropTableDataBuffer
                {
                    DropRate = entry.DropChance,
                    ItemGuid = entry.ItemGuid,
                    ItemType = entry.ItemType,
                    Quantity = entry.StackSize
                });
            }

            entityManager.SetComponentData(entity, LocalTransform.Identity);
            entityManager.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
            entityManager.SetComponentData(entity, new Translation { Value = float3.zero });
            entityManager.SetComponentData(entity, new Rotation { Value = quaternion.identity });

            var collection = Core.PrefabCollectionSystem;
            if (collection?._PrefabGuidToEntityMap != null)
            {
                collection._PrefabGuidToEntityMap[definition.DropTableGuid] = entity;
            }

            return entity;
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[Infamy] Failed to create drop table prefab for '{definition.FactionId}': {ex.Message}");
            return Entity.Null;
        }
    }

    private static void RefreshDropTableEntity(EntityManager entityManager, Entity entity, AmbushLootDefinition definition)
    {
        if (!entityManager.Exists(entity))
        {
            return;
        }

        entityManager.SetComponentData(entity, definition.DropTableGuid);
        var dropTableData = entityManager.GetComponentData<DropTableData>(entity);
        dropTableData.Guid = definition.DropTableGuid;
        dropTableData.DropTableLevel = definition.Level;
        entityManager.SetComponentData(entity, dropTableData);

        var buffer = entityManager.GetBuffer<DropTableDataBuffer>(entity);
        buffer.Clear();
        foreach (var entry in definition.Entries)
        {
            buffer.Add(new DropTableDataBuffer
            {
                DropRate = entry.DropChance,
                ItemGuid = entry.ItemGuid,
                ItemType = entry.ItemType,
                Quantity = entry.StackSize
            });
        }

        var collection = Core.PrefabCollectionSystem;
        if (collection?._PrefabGuidToEntityMap != null)
        {
            collection._PrefabGuidToEntityMap[definition.DropTableGuid] = entity;
        }
    }

    private static string DefaultSquadJson => @"{
  \"defaultVisuals\": [
    1670636401,
    1199823151,
    -2067402784,
    178225731,
    -2104035188
  ],
  \"factions\": [
    {
      \"id\": \"Bandits\",
      \"factionGuids\": [
        -413163549,
        30052367
      ],
      \"baseUnits\": [
        {
          \"prefabGuid\": -1030822544,
          \"count\": 2,
          \"levelOffset\": -1,
          \"minRange\": 1.5,
          \"maxRange\": 8
        },
        {
          \"prefabGuid\": -301730941,
          \"count\": 2,
          \"levelOffset\": -2,
          \"minRange\": 1,
          \"maxRange\": 6
        }
      ],
      \"representatives\": [
        {
          \"prefabGuid\": -1128238456,
          \"count\": 1,
          \"levelOffset\": 2,
          \"minRange\": 1.5,
          \"maxRange\": 7
        }
      ],
      \"visualBuffs\": [
        1670636401,
        1199823151,
        -2067402784
      ],
      \"seasonal\": [
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": true,
          \"unit\": {
            \"prefabGuid\": -1750347680,
            \"count\": 1,
            \"levelOffset\": 0,
            \"minRange\": 2.5,
            \"maxRange\": 8
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -1146194149,
            \"count\": 1,
            \"levelOffset\": 2,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -458883491,
            \"count\": 1,
            \"levelOffset\": 3,
            \"minRange\": 2,
            \"maxRange\": 9
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": 849891426,
            \"count\": 2,
            \"levelOffset\": 1,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        }
      ],
      \"baseHateOverrides\": [
        {
          \"factionGuid\": 30052367,
          \"hate\": 300
        }
      ]
    },
    {
      \"id\": \"Blackfangs\",
      \"factionGuids\": [
        932337192,
        -1460095921
      ],
      \"baseUnits\": [
        {
          \"prefabGuid\": 1864177126,
          \"count\": 2,
          \"levelOffset\": 0,
          \"minRange\": 1.5,
          \"maxRange\": 7
        },
        {
          \"prefabGuid\": 326501064,
          \"count\": 1,
          \"levelOffset\": 1,
          \"minRange\": 2,
          \"maxRange\": 9
        }
      ],
      \"representatives\": [
        {
          \"prefabGuid\": 1531777139,
          \"count\": 1,
          \"levelOffset\": 2,
          \"minRange\": 2,
          \"maxRange\": 9
        }
      ],
      \"visualBuffs\": [
        1199823151,
        -2067402784,
        178225731
      ],
      \"seasonal\": [
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": true,
          \"unit\": {
            \"prefabGuid\": -1750347680,
            \"count\": 1,
            \"levelOffset\": 0,
            \"minRange\": 2.5,
            \"maxRange\": 8
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -1146194149,
            \"count\": 1,
            \"levelOffset\": 2,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -458883491,
            \"count\": 1,
            \"levelOffset\": 3,
            \"minRange\": 2,
            \"maxRange\": 9
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": 849891426,
            \"count\": 2,
            \"levelOffset\": 1,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        }
      ]
    },
    {
      \"id\": \"Militia\",
      \"factionGuids\": [
        1057375699,
        1094603131,
        2395673,
        887347866,
        1977351396
      ],
      \"baseUnits\": [
        {
          \"prefabGuid\": 1148936156,
          \"count\": 3,
          \"levelOffset\": -1,
          \"minRange\": 2,
          \"maxRange\": 10
        },
        {
          \"prefabGuid\": 794228023,
          \"count\": 1,
          \"levelOffset\": 1,
          \"minRange\": 1.5,
          \"maxRange\": 6
        }
      ],
      \"representatives\": [
        {
          \"prefabGuid\": 2005508157,
          \"count\": 1,
          \"levelOffset\": 2,
          \"minRange\": 2,
          \"maxRange\": 8
        }
      ],
      \"visualBuffs\": [
        178225731,
        1199823151,
        -2104035188
      ],
      \"seasonal\": [
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": true,
          \"unit\": {
            \"prefabGuid\": -1750347680,
            \"count\": 1,
            \"levelOffset\": 0,
            \"minRange\": 2.5,
            \"maxRange\": 8
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -1146194149,
            \"count\": 1,
            \"levelOffset\": 2,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -458883491,
            \"count\": 1,
            \"levelOffset\": 3,
            \"minRange\": 2,
            \"maxRange\": 9
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": 849891426,
            \"count\": 2,
            \"levelOffset\": 1,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        }
      ],
      \"baseHateOverrides\": [
        {
          \"factionGuid\": 2395673,
          \"hate\": 25
        },
        {
          \"factionGuid\": 887347866,
          \"hate\": 300
        },
        {
          \"factionGuid\": 1094603131,
          \"hate\": 15
        }
      ]
    },
    {
      \"id\": \"Gloomrot\",
      \"factionGuids\": [
        -1632475814
      ],
      \"baseUnits\": [
        {
          \"prefabGuid\": -322293503,
          \"count\": 2,
          \"levelOffset\": 0,
          \"minRange\": 3,
          \"maxRange\": 10
        },
        {
          \"prefabGuid\": 1732477970,
          \"count\": 1,
          \"levelOffset\": 2,
          \"minRange\": 4,
          \"maxRange\": 12
        }
      ],
      \"representatives\": [
        {
          \"prefabGuid\": 1401026468,
          \"count\": 1,
          \"levelOffset\": 2,
          \"minRange\": 3,
          \"maxRange\": 10
        }
      ],
      \"visualBuffs\": [
        -2104035188,
        -2067402784,
        178225731
      ],
      \"seasonal\": [
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": true,
          \"unit\": {
            \"prefabGuid\": -1750347680,
            \"count\": 1,
            \"levelOffset\": 0,
            \"minRange\": 2.5,
            \"maxRange\": 8
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -1146194149,
            \"count\": 1,
            \"levelOffset\": 2,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -458883491,
            \"count\": 1,
            \"levelOffset\": 3,
            \"minRange\": 2,
            \"maxRange\": 9
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": 849891426,
            \"count\": 2,
            \"levelOffset\": 1,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        }
      ]
    },
    {
      \"id\": \"Legion\",
      \"factionGuids\": [
        -772044125
      ],
      \"baseUnits\": [
        {
          \"prefabGuid\": 1980594081,
          \"count\": 2,
          \"levelOffset\": 1,
          \"minRange\": 2,
          \"maxRange\": 9
        },
        {
          \"prefabGuid\": -1009917656,
          \"count\": 1,
          \"levelOffset\": 3,
          \"minRange\": 3,
          \"maxRange\": 11
        }
      ],
      \"representatives\": [
        {
          \"prefabGuid\": 1912966420,
          \"count\": 1,
          \"levelOffset\": 2,
          \"minRange\": 3,
          \"maxRange\": 10
        }
      ],
      \"visualBuffs\": [
        -2067402784,
        1199823151,
        -2104035188
      ],
      \"seasonal\": [
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": true,
          \"unit\": {
            \"prefabGuid\": -1750347680,
            \"count\": 1,
            \"levelOffset\": 0,
            \"minRange\": 2.5,
            \"maxRange\": 8
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -1146194149,
            \"count\": 1,
            \"levelOffset\": 2,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -458883491,
            \"count\": 1,
            \"levelOffset\": 3,
            \"minRange\": 2,
            \"maxRange\": 9
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": 849891426,
            \"count\": 2,
            \"levelOffset\": 1,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        }
      ]
    },
    {
      \"id\": \"Undead\",
      \"factionGuids\": [
        929074293
      ],
      \"baseUnits\": [
        {
          \"prefabGuid\": -1287507270,
          \"count\": 3,
          \"levelOffset\": -1,
          \"minRange\": 1.5,
          \"maxRange\": 7
        },
        {
          \"prefabGuid\": -1365627158,
          \"count\": 1,
          \"levelOffset\": 1,
          \"minRange\": 2,
          \"maxRange\": 8
        }
      ],
      \"representatives\": [
        {
          \"prefabGuid\": -1967480038,
          \"count\": 1,
          \"levelOffset\": 2,
          \"minRange\": 2,
          \"maxRange\": 8
        }
      ],
      \"visualBuffs\": [
        -2067402784,
        1199823151,
        -2104035188
      ],
      \"seasonal\": [
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": true,
          \"unit\": {
            \"prefabGuid\": -1750347680,
            \"count\": 1,
            \"levelOffset\": 0,
            \"minRange\": 2.5,
            \"maxRange\": 8
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -1146194149,
            \"count\": 1,
            \"levelOffset\": 2,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -458883491,
            \"count\": 1,
            \"levelOffset\": 3,
            \"minRange\": 2,
            \"maxRange\": 9
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": 849891426,
            \"count\": 2,
            \"levelOffset\": 1,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        }
      ],
      \"baseHateOverrides\": [
        {
          \"factionGuid\": 929074293,
          \"hate\": 5
        }
      ]
    },
    {
      \"id\": \"Werewolf\",
      \"factionGuids\": [
        -2024618997,
        62959306
      ],
      \"baseUnits\": [
        {
          \"prefabGuid\": -951976780,
          \"count\": 3,
          \"levelOffset\": 0,
          \"minRange\": 1.5,
          \"maxRange\": 8
        }
      ],
      \"visualBuffs\": [
        1670636401,
        -2067402784
      ],
      \"seasonal\": [
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": true,
          \"unit\": {
            \"prefabGuid\": -1750347680,
            \"count\": 1,
            \"levelOffset\": 0,
            \"minRange\": 2.5,
            \"maxRange\": 8
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -1146194149,
            \"count\": 1,
            \"levelOffset\": 2,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": -458883491,
            \"count\": 1,
            \"levelOffset\": 3,
            \"minRange\": 2,
            \"maxRange\": 9
          }
        },
        {
          \"season\": \"Halloween\",
          \"useSharedRollCount\": false,
          \"unit\": {
            \"prefabGuid\": 849891426,
            \"count\": 2,
            \"levelOffset\": 1,
            \"minRange\": 2.5,
            \"maxRange\": 10
          }
        }
      ],
      \"baseHateOverrides\": [
        {
          \"factionGuid\": -2024618997,
          \"hate\": 20
        },
        {
          \"factionGuid\": 62959306,
          \"hate\": 20
        }
      ]
    },
    {
      \"id\": \"Critters\",
      \"factionGuids\": [
        1344481611,
        10678632,
        -1671358863
      ],
      \"baseHateOverrides\": []
    }
  ]
}";

    private static string DefaultLootJson => @"{
  \"factions\": [
    {
      \"id\": \"Bandits\",
      \"loot\": [
        {
          \"prefabGuid\": -949672483,
          \"stackSize\": 5,
          \"dropChance\": 0.35
        },
        {
          \"prefabGuid\": 1252507075,
          \"stackSize\": 2,
          \"dropChance\": 0.2
        }
      ]
    },
    {
      \"id\": \"Blackfangs\",
      \"loot\": [
        {
          \"prefabGuid\": -949672483,
          \"stackSize\": 6,
          \"dropChance\": 0.4
        },
        {
          \"prefabGuid\": 301051123,
          \"stackSize\": 1,
          \"dropChance\": 0.15
        }
      ]
    },
    {
      \"id\": \"Militia\",
      \"loot\": [
        {
          \"prefabGuid\": -949672483,
          \"stackSize\": 8,
          \"dropChance\": 0.45
        },
        {
          \"prefabGuid\": -223452038,
          \"stackSize\": 1,
          \"dropChance\": 0.12
        }
      ]
    },
    {
      \"id\": \"Gloomrot\",
      \"loot\": [
        {
          \"prefabGuid\": -949672483,
          \"stackSize\": 10,
          \"dropChance\": 0.35
        },
        {
          \"prefabGuid\": -1328826274,
          \"stackSize\": 1,
          \"dropChance\": 0.2,
          \"type\": \"Group\"
        }
      ]
    },
    {
      \"id\": \"Legion\",
      \"loot\": [
        {
          \"prefabGuid\": -949672483,
          \"stackSize\": 7,
          \"dropChance\": 0.4
        },
        {
          \"prefabGuid\": 624475009,
          \"stackSize\": 1,
          \"dropChance\": 0.1,
          \"type\": \"Group\"
        }
      ]
    },
    {
      \"id\": \"Undead\",
      \"loot\": [
        {
          \"prefabGuid\": -949672483,
          \"stackSize\": 4,
          \"dropChance\": 0.3
        },
        {
          \"prefabGuid\": 147048543,
          \"stackSize\": 1,
          \"dropChance\": 0.2,
          \"type\": \"Group\"
        }
      ]
    },
    {
      \"id\": \"Werewolf\",
      \"loot\": [
        {
          \"prefabGuid\": -949672483,
          \"stackSize\": 5,
          \"dropChance\": 0.5
        },
        {
          \"prefabGuid\": 931859854,
          \"stackSize\": 6,
          \"dropChance\": 0.25
        }
      ]
    }
  ]
}";

    private sealed class SquadConfigSnapshot
    {
        private SquadConfigSnapshot(
            Dictionary<string, AmbushSquadDefinition> squads,
            Dictionary<string, IReadOnlyList<PrefabGUID>> visualPools,
            IReadOnlyList<PrefabGUID> defaultVisuals,
            Dictionary<int, string> aggregatedFactions,
            Dictionary<string, PrefabGUID> reverseMap,
            Dictionary<int, float> baseHateOverrides,
            Dictionary<string, PrefabGUID> factionGuidLookup)
        {
            Squads = squads;
            VisualPools = visualPools;
            DefaultVisuals = defaultVisuals;
            AggregatedFactions = aggregatedFactions;
            FactionReverseMap = reverseMap;
            BaseHateOverrides = baseHateOverrides;
            FactionGuidLookup = factionGuidLookup;
        }

        public static SquadConfigSnapshot Empty { get; } = new(
            new Dictionary<string, AmbushSquadDefinition>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyList<PrefabGUID>>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<PrefabGUID>(),
            new Dictionary<int, string>(),
            new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<int, float>(),
            new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase));

        public Dictionary<string, AmbushSquadDefinition> Squads { get; }
        public Dictionary<string, IReadOnlyList<PrefabGUID>> VisualPools { get; }
        public IReadOnlyList<PrefabGUID> DefaultVisuals { get; }
        public Dictionary<int, string> AggregatedFactions { get; }
        public Dictionary<string, PrefabGUID> FactionReverseMap { get; }
        public Dictionary<int, float> BaseHateOverrides { get; }
        public Dictionary<string, PrefabGUID> FactionGuidLookup { get; }

        public static SquadConfigSnapshot From(SquadConfigFile file, ManualLogSource? log)
        {
            var squads = new Dictionary<string, AmbushSquadDefinition>(StringComparer.OrdinalIgnoreCase);
            var visualPools = new Dictionary<string, IReadOnlyList<PrefabGUID>>(StringComparer.OrdinalIgnoreCase);
            var aggregated = new Dictionary<int, string>();
            var reverse = new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase);
            var baseHate = new Dictionary<int, float>();
            var lookup = new Dictionary<string, PrefabGUID>(StringComparer.OrdinalIgnoreCase);

            var defaultVisuals = (file.DefaultVisuals ?? new List<int>())
                .Select(static guid => new PrefabGUID(guid))
                .ToArray();

            if (file.Factions is null || file.Factions.Count == 0)
            {
                log?.LogWarning("[Infamy] Squad configuration did not define any factions. Using fallback definitions.");
                return CreateFallback(log);
            }

            foreach (var faction in file.Factions)
            {
                if (string.IsNullOrWhiteSpace(faction.Id))
                {
                    continue;
                }

                var id = faction.Id.Trim();
                var baseUnits = ConvertUnits(faction.BaseUnits);
                var representatives = ConvertUnits(faction.Representatives);
                var seasonal = ConvertSeasonal(faction.Seasonal, log);

                var definition = new AmbushSquadDefinition(baseUnits, representatives, seasonal);
                squads[id] = definition;

                if (faction.VisualBuffs is { Count: > 0 })
                {
                    visualPools[id] = faction.VisualBuffs.Select(static guid => new PrefabGUID(guid)).ToArray();
                }

                if (faction.FactionGuids is { Count: > 0 })
                {
                    foreach (var guid in faction.FactionGuids)
                    {
                        aggregated[guid] = id;
                    }

                    var first = faction.FactionGuids[0];
                    reverse[id] = new PrefabGUID(first);
                    lookup[id] = new PrefabGUID(first);
                }

                if (faction.BaseHateOverrides is { Count: > 0 })
                {
                    foreach (var entry in faction.BaseHateOverrides)
                    {
                        baseHate[entry.FactionGuid] = Math.Max(0f, entry.Hate);
                    }
                }
            }

            if (reverse.Count == 0)
            {
                log?.LogWarning("[Infamy] Squad configuration did not provide any faction GUIDs; ambush alignment may fail.");
            }

            return new SquadConfigSnapshot(squads, visualPools, defaultVisuals, aggregated, reverse, baseHate, lookup);
        }

        public static SquadConfigSnapshot CreateFallback(ManualLogSource? log)
        {
            log?.LogWarning("[Infamy] Falling back to built-in ambush squad definitions.");

            var fallbackJson = JsonSerializer.Deserialize<SquadConfigFile>(DefaultSquadJson, JsonOptions);
            if (fallbackJson is null)
            {
                throw new InvalidOperationException("Fallback squad configuration failed to parse.");
            }

            return From(fallbackJson, log);
        }

        private static IReadOnlyList<AmbushUnitDefinition> ConvertUnits(IReadOnlyList<UnitConfig>? units)
        {
            if (units is null || units.Count == 0)
            {
                return Array.Empty<AmbushUnitDefinition>();
            }

            return units
                .Select(static unit => new AmbushUnitDefinition(
                    new PrefabGUID(unit.PrefabGuid),
                    Math.Max(0, unit.Count),
                    unit.LevelOffset,
                    Math.Max(0f, unit.MinRange),
                    Math.Max(unit.MinRange, unit.MaxRange)))
                .ToArray();
        }

        private static IReadOnlyList<AmbushSeasonalDefinition> ConvertSeasonal(IReadOnlyList<SeasonalConfig>? seasonal, ManualLogSource? log)
        {
            if (seasonal is null || seasonal.Count == 0)
            {
                return Array.Empty<AmbushSeasonalDefinition>();
            }

            var list = new List<AmbushSeasonalDefinition>();

            foreach (var entry in seasonal)
            {
                if (entry.Unit is null)
                {
                    continue;
                }

                if (!Enum.TryParse<SeasonalAmbushType>(entry.Season ?? string.Empty, true, out var type))
                {
                    log?.LogWarning($"[Infamy] Unknown seasonal ambush type '{entry.Season}'. Skipping entry.");
                    continue;
                }

                var unit = entry.Unit;
                var definition = new AmbushSeasonalDefinition(
                    type,
                    new AmbushUnitDefinition(
                        new PrefabGUID(unit.PrefabGuid),
                        Math.Max(0, unit.Count),
                        unit.LevelOffset,
                        Math.Max(0f, unit.MinRange),
                        Math.Max(unit.MinRange, unit.MaxRange)),
                    entry.UseSharedRollCount);

                list.Add(definition);
            }

            return list;
        }
    }

    private sealed class LootConfigSnapshot
    {
        private LootConfigSnapshot(Dictionary<string, AmbushLootDefinition> definitions)
        {
            Definitions = definitions;
        }

        public static LootConfigSnapshot Empty { get; } = new(new Dictionary<string, AmbushLootDefinition>(StringComparer.OrdinalIgnoreCase));

        public Dictionary<string, AmbushLootDefinition> Definitions { get; }

        public static LootConfigSnapshot From(LootConfigFile file, ManualLogSource? log)
        {
            var map = new Dictionary<string, AmbushLootDefinition>(StringComparer.OrdinalIgnoreCase);
            if (file.Factions is null || file.Factions.Count == 0)
            {
                log?.LogWarning("[Infamy] Loot configuration did not define any factions. Using fallback loot definitions.");
                return CreateFallback(log);
            }

            foreach (var entry in file.Factions)
            {
                if (string.IsNullOrWhiteSpace(entry.Id) || entry.Loot is null || entry.Loot.Count == 0)
                {
                    continue;
                }

                var id = entry.Id.Trim();
                var entries = new List<AmbushLootEntry>();
                foreach (var drop in entry.Loot)
                {
                    if (drop.DropChance <= 0f || drop.StackSize <= 0)
                    {
                        continue;
                    }

                    var type = Enum.TryParse<DropItemType>(drop.Type ?? "Item", true, out var parsed)
                        ? parsed
                        : DropItemType.Item;

                    entries.Add(new AmbushLootEntry(new PrefabGUID(drop.PrefabGuid), Math.Clamp(drop.StackSize, 1, 999), Math.Clamp(drop.DropChance, 0f, 1f), type));
                }

                if (entries.Count == 0)
                {
                    continue;
                }

                var definition = new AmbushLootDefinition(id, ComputeStableGuid(id), entries);
                map[id] = definition;
            }

            return new LootConfigSnapshot(map);
        }

        public static LootConfigSnapshot CreateFallback(ManualLogSource? log)
        {
            log?.LogWarning("[Infamy] Falling back to default ambush loot definitions.");

            var fallback = JsonSerializer.Deserialize<LootConfigFile>(DefaultLootJson, JsonOptions);
            if (fallback is null)
            {
                throw new InvalidOperationException("Fallback loot configuration failed to parse.");
            }

            return From(fallback, log);
        }

        public int TryGetGuid(string factionId)
        {
            if (Definitions.TryGetValue(factionId, out var definition))
            {
                return definition.DropTableGuid.GuidHash;
            }

            return 0;
        }
    }

    private static PrefabGUID ComputeStableGuid(string value)
    {
        unchecked
        {
            const int offset = (int)2166136261;
            const int prime = 16777619;
            var hash = offset;
            foreach (var ch in value)
            {
                var lower = char.ToLowerInvariant(ch);
                hash ^= lower;
                hash *= prime;
            }

            hash ^= 0x6D6F6453; // "modS"
            return new PrefabGUID(hash);
        }
    }

    private sealed class SquadConfigFile
    {
        [JsonPropertyName("defaultVisuals")]
        public List<int>? DefaultVisuals { get; set; }

        [JsonPropertyName("factions")]
        public List<SquadConfigEntry>? Factions { get; set; }
    }

    private sealed class SquadConfigEntry
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("factionGuids")]
        public List<int>? FactionGuids { get; set; }

        [JsonPropertyName("baseUnits")]
        public List<UnitConfig>? BaseUnits { get; set; }

        [JsonPropertyName("representatives")]
        public List<UnitConfig>? Representatives { get; set; }

        [JsonPropertyName("seasonal")]
        public List<SeasonalConfig>? Seasonal { get; set; }

        [JsonPropertyName("visualBuffs")]
        public List<int>? VisualBuffs { get; set; }

        [JsonPropertyName("baseHateOverrides")]
        public List<BaseHateConfig>? BaseHateOverrides { get; set; }
    }

    private sealed class UnitConfig
    {
        [JsonPropertyName("prefabGuid")]
        public int PrefabGuid { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("levelOffset")]
        public int LevelOffset { get; set; }

        [JsonPropertyName("minRange")]
        public float MinRange { get; set; }

        [JsonPropertyName("maxRange")]
        public float MaxRange { get; set; }
    }

    private sealed class SeasonalConfig
    {
        [JsonPropertyName("season")]
        public string? Season { get; set; }

        [JsonPropertyName("useSharedRollCount")]
        public bool UseSharedRollCount { get; set; }

        [JsonPropertyName("unit")]
        public UnitConfig? Unit { get; set; }
    }

    private sealed class BaseHateConfig
    {
        [JsonPropertyName("factionGuid")]
        public int FactionGuid { get; set; }

        [JsonPropertyName("hate")]
        public float Hate { get; set; }
    }

    private sealed class LootConfigFile
    {
        [JsonPropertyName("factions")]
        public List<LootConfigEntry>? Factions { get; set; }
    }

    private sealed class LootConfigEntry
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("loot")]
        public List<LootDropConfig>? Loot { get; set; }
    }

    private sealed class LootDropConfig
    {
        [JsonPropertyName("prefabGuid")]
        public int PrefabGuid { get; set; }

        [JsonPropertyName("stackSize")]
        public int StackSize { get; set; }

        [JsonPropertyName("dropChance")]
        public float DropChance { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
}

internal readonly record struct AmbushLootEntry(PrefabGUID ItemGuid, int StackSize, float DropChance, DropItemType ItemType);

internal sealed class AmbushLootDefinition
{
    public static AmbushLootDefinition Empty { get; } = new(string.Empty, new PrefabGUID(0), Array.Empty<AmbushLootEntry>());

    public AmbushLootDefinition(string factionId, PrefabGUID dropTableGuid, IReadOnlyList<AmbushLootEntry> entries, int level = 0)
    {
        FactionId = factionId;
        DropTableGuid = dropTableGuid;
        Entries = entries;
        Level = level;
    }

    public string FactionId { get; }

    public PrefabGUID DropTableGuid { get; }

    public IReadOnlyList<AmbushLootEntry> Entries { get; }

    public int Level { get; }
}
