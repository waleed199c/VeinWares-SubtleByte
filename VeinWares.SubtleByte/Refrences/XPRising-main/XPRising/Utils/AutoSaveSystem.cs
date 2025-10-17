﻿using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using Stunlock.Core;
using XPRising.Models;
using XPRising.Systems;
using LogSystem = XPRising.Plugin.LogSystem;

namespace XPRising.Utils
{
    //-- AutoSave is now directly hooked into the Server game save activity.
    public static class AutoSaveSystem
    {
        public static string ConfigFolder = "XPRising";
        
        // Config paths
        public static readonly string BasePath = Paths.ConfigPath ?? Path.Combine("BepInEx", "config");
        public static string ConfigPath => Path.Combine(BasePath, ConfigFolder);
        public static string SavesPath => Path.Combine(BasePath, ConfigFolder, "Data");
        public static string BackupsPath => Path.Combine(BasePath, ConfigFolder, SavesPath, "Backup");
        public static bool JsonConfigPrettyPrinted = true;

        private static Regex _folderValidation = new Regex(@"([^\w]+)");
        
        // Config files
        private const string PowerUpJson = "powerUp.json";
        private const string WaypointsJson = "waypoints.json";
        private const string PlayerLogoutJson = "playerLogout.json";
        private const string PlayerExperienceJson = "playerExperience.json";
        private const string PlayerAbilityPointsJson = "playerAbilityPoints.json";
        private const string PlayerLevelStatsJson = "playerLevelStats.json";
        private const string ExperienceClassStatsJson = "experienceClassStats.json";
        private const string CommandPermissionJson = "commandPermission.json";
        private const string UserPermissionJson = "userPermission.json";
        private const string PlayerMasteryJson = "playerMasteryStats.json";
        private const string GlobalMasteryConfigJson = "globalMasteryConfig.json";
        private const string PlayerPreferencesJson = "playerPreferences.json";
        private const string PlayerWantedLevelJson = "playerWantedLevel.json";
        private const string ChallengesJson = "challenges.json";
        private const string ChallengeStatsJson = "challengeStats.json";

        private static DateTime _timeSinceLastAutoSave = DateTime.Now;
        private static DateTime _timeSinceLastBackupSave = DateTime.Now;
        // Have a small buffer that we can use to ensure data gets saved with the intended frequency.
        private static readonly TimeSpan TimeBuffer = TimeSpan.FromSeconds(30);
        public static TimeSpan AutoSaveFrequency { get; set; } = TimeSpan.FromMinutes(2);
        public static TimeSpan BackupFrequency { get; set; } = TimeSpan.Zero;

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            Converters =
            {
                new PrefabGuidConverter(),
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
        public static readonly JsonSerializerOptions PrettyJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            Converters =
            {
                new PrefabGuidConverter(),
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
        private static JsonSerializerOptions _jsonOptions => JsonConfigPrettyPrinted ? PrettyJsonOptions : JsonOptions;

        public static string NormaliseConfigFolder(string serverName)
        {
            return _folderValidation.Replace("XPRising_" + serverName.Trim(), "_");
        }

        private enum LoadMethod
        {
            Both,
            Main,
            Backup,
            None,
        }

        // Returns true on all succeeding, false on any errors
        public static bool SaveDatabase(bool forceSave, bool forceBackup = false)
        {
            if (!Plugin.IsInitialized)
            {
                Plugin.Log(LogSystem.Core, LogLevel.Error, "Attempted to save the DB without properly initialising the DB. Cancelling the save as this would overwrite any existing config with blank data.", true);
                return false;
            }
            var anyErrors = false;
            
            var now = DateTime.Now;
            var autoSave = (now - _timeSinceLastAutoSave) > AutoSaveFrequency;
            if (forceSave || autoSave)
            {
                var message = forceSave ? "Saving DB..." : "Auto-saving DB...";
                Plugin.Log(LogSystem.Core, LogLevel.Info, message, forceSave);
                anyErrors |= !InternalSaveDatabase(SavesPath);
                _timeSinceLastAutoSave = now - TimeBuffer;
            }
            
            var saveBackup = !BackupFrequency.Equals(TimeSpan.Zero) && (now - _timeSinceLastBackupSave) > BackupFrequency;
            if (forceBackup || saveBackup)
            {
                var message = forceSave ? "Saving DB backup..." : "Auto-saving DB backup...";
                Plugin.Log(LogSystem.Core, LogLevel.Info, message, forceSave);
                anyErrors |= !InternalSaveDatabase(BackupsPath);
                _timeSinceLastBackupSave = now - TimeBuffer;
            }

            return !anyErrors;
        }
        
        // Returns true on all succeeding, false on any errors
        private static bool InternalSaveDatabase(string saveFolder)
        {
            var anyErrors = false;
            // Core
            anyErrors |= !SaveDB(saveFolder, CommandPermissionJson, Database.CommandPermission, PrettyJsonOptions);
            anyErrors |= !SaveDB(saveFolder, UserPermissionJson, Database.UserPermission, PrettyJsonOptions);
            anyErrors |= !SaveDB(saveFolder, PlayerPreferencesJson, Database.PlayerPreferences, _jsonOptions);
            anyErrors |= !SaveDB(saveFolder, PlayerLogoutJson, Database.PlayerLogout, _jsonOptions);
            
            if (Plugin.WaypointsActive) anyErrors |= !SaveDB(saveFolder, WaypointsJson, Database.Waypoints, _jsonOptions);
            if (Plugin.PowerUpCommandsActive) anyErrors |= !SaveDB(saveFolder, PowerUpJson, Database.PowerUpList, _jsonOptions);

            if (Plugin.ExperienceSystemActive)
            {
                anyErrors |= !SaveDB(saveFolder, PlayerExperienceJson, Database.PlayerExperience, _jsonOptions);
                anyErrors |= !SaveDB(saveFolder, PlayerAbilityPointsJson, Database.PlayerAbilityIncrease, _jsonOptions);
                anyErrors |= !SaveDB(saveFolder, PlayerLevelStatsJson, Database.PlayerLevelStats, _jsonOptions);
                anyErrors |= !SaveDB(saveFolder, ExperienceClassStatsJson, Database.ExperienceClassStats, PrettyJsonOptions);
            }
            
            if (Plugin.WantedSystemActive)
            {
                anyErrors |= !SaveDB(saveFolder, PlayerWantedLevelJson, Database.PlayerHeat, _jsonOptions);
            }

            if (Plugin.WeaponMasterySystemActive || Plugin.BloodlineSystemActive)
            {
                anyErrors |= !SaveDB(saveFolder, PlayerMasteryJson, Database.PlayerMastery, _jsonOptions);
            }

            if (Plugin.ChallengeSystemActive)
            {
                anyErrors |= !SaveDB(saveFolder, ChallengesJson, ChallengeSystem.ChallengeDatabase, _jsonOptions);
                anyErrors |= !SaveDB(saveFolder, ChallengeStatsJson, ChallengeSystem.PlayerChallengeStats, _jsonOptions);
            }

            Plugin.Log(LogSystem.Core, LogLevel.Info, $"All databases saved to: {saveFolder}");
            return !anyErrors;
        }

        public static void LoadOrInitialiseDatabase()
        {
            InternalLoadDatabase(true, LoadMethod.Both);
        }

        public static bool LoadDatabase(bool loadBackup)
        {
            return InternalLoadDatabase(false, loadBackup ? LoadMethod.Backup : LoadMethod.Main);
        }

        public static bool WipeDatabase()
        {
            return InternalLoadDatabase(true, LoadMethod.None);
        }
        
        private static bool InternalLoadDatabase(bool useInitialiser, LoadMethod loadMethod)
        {
            var anyErrors = false;
            // Core
            anyErrors |= !LoadDB(CommandPermissionJson, loadMethod, useInitialiser, ref Database.CommandPermission, PermissionSystem.DefaultCommandPermissions);
            anyErrors |= !LoadDB(UserPermissionJson, loadMethod, useInitialiser, ref Database.UserPermission);
            anyErrors |= !LoadDB(PlayerPreferencesJson, loadMethod, useInitialiser, ref Database.PlayerPreferences);
            anyErrors |= !LoadDB(PlayerLogoutJson, loadMethod, useInitialiser, ref Database.PlayerLogout);
            
            if (Plugin.WaypointsActive) anyErrors |= !LoadDB(WaypointsJson, loadMethod, useInitialiser, ref Database.Waypoints);
            if (Plugin.PowerUpCommandsActive) anyErrors |= !LoadDB(PowerUpJson, loadMethod, useInitialiser, ref Database.PowerUpList);

            if (Plugin.ExperienceSystemActive)
            {
                anyErrors |= !LoadDB(PlayerExperienceJson, loadMethod, useInitialiser, ref Database.PlayerExperience);
                anyErrors |= !LoadDB(PlayerAbilityPointsJson, loadMethod, useInitialiser, ref Database.PlayerAbilityIncrease);
                anyErrors |= !LoadDB(PlayerLevelStatsJson, loadMethod, useInitialiser, ref Database.PlayerLevelStats);
                anyErrors |= !LoadDB(ExperienceClassStatsJson, loadMethod, useInitialiser, ref Database.ExperienceClassStats, ExperienceSystem.DefaultExperienceClassStats);
            }
            
            if (Plugin.WantedSystemActive)
            {
                anyErrors |= !LoadDB(PlayerWantedLevelJson, loadMethod, useInitialiser, ref Database.PlayerHeat);
                foreach (var (steamID, playerHeat) in Database.PlayerHeat)
                {
                    Plugin.Log(LogSystem.Wanted, LogLevel.Error, $"starting cooldown timer for {steamID}. Has {playerHeat.heat.Count} factions in heat");
                    playerHeat.StartCooldownTimer(steamID);
                }
            }

            if (Plugin.WeaponMasterySystemActive || Plugin.BloodlineSystemActive)
            {
                anyErrors |= !LoadDB(PlayerMasteryJson, loadMethod, useInitialiser, ref Database.PlayerMastery);
            }
            
            // Load the global mastery file
            if (Plugin.WeaponMasterySystemActive || Plugin.BloodlineSystemActive || Plugin.ExperienceSystemActive)
            {
                // Check that the mastery file exists. This will be used for both the mastery systems and the XP level buff system.
                Plugin.Log(Plugin.LogSystem.Mastery, LogLevel.Info, $"Confirming custom preset file exists");
                ConfirmFile(SavesPath, GlobalMasteryConfigJson, () => JsonSerializer.Serialize(GlobalMasterySystem.DefaultMasteryConfig(), PrettyJsonOptions));
                
                // Load the config from file. This is required as we will need to at least load the XP config, regardless of the mastery config preset.
                var config = new GlobalMasteryConfig();
                anyErrors |= LoadDB(GlobalMasteryConfigJson, loadMethod, useInitialiser, ref config, GlobalMasterySystem.DefaultMasteryConfig);
                
                // If we are not using the custom preset, overwrite any existing configuration while keeping the xpBuffConfig section.
                // There is no corresponding SaveDB call, so we want to save this now.
                if (GlobalMasterySystem.MasteryConfigPreset != GlobalMasterySystem.CustomPreset)
                {
                    Plugin.Log(Plugin.LogSystem.Mastery, LogLevel.Info, $"Ensuring '{GlobalMasterySystem.MasteryConfigPreset}' preset file is being written.");
                    var preset = GlobalMasterySystem.DefaultMasteryConfig();
                    preset.XpBuffConfig = config.XpBuffConfig;
                    EnsureFile(SavesPath, GlobalMasteryConfigJson, () => JsonSerializer.Serialize(preset, PrettyJsonOptions));
                    
                    // Set the config to the preset
                    config = preset;
                }
                
                // Load the config (or the default config) into the system.
                GlobalMasterySystem.SetMasteryConfig(config);
            }
            
            if (Plugin.ChallengeSystemActive)
            {
                ConfirmFile(SavesPath, ChallengesJson, () => JsonSerializer.Serialize(ChallengeSystem.DefaultBasicChallenges(), PrettyJsonOptions));
                anyErrors |= !LoadDB(ChallengesJson, loadMethod, useInitialiser, ref ChallengeSystem.ChallengeDatabase);
                anyErrors |= !LoadDB(ChallengeStatsJson, loadMethod, useInitialiser, ref ChallengeSystem.PlayerChallengeStats);
            }

            Plugin.Log(LogSystem.Core, LogLevel.Info, "All database data is now loaded.", true);
            return !anyErrors;
        }
        
        private static bool SaveDB<TData>(string saveFolder, string specificFile, TData data, JsonSerializerOptions options)
        {
            try
            {
                var outputFile = Path.Combine(saveFolder, specificFile);
                File.WriteAllText(outputFile, JsonSerializer.Serialize(data, options));
                Plugin.Log(LogSystem.Core, LogLevel.Info, $"{specificFile} Saved.");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log(LogSystem.Core, LogLevel.Error, $"Could not save DB {specificFile}: {e.Message}", true);
                return false;
            }
        }

        private static bool LoadDB<TData>(string specificFile, LoadMethod loadMethod, bool useInitialiser, ref TData currentRef, Func<TData> initialiser = null) where TData : class, new()
        {
            switch (loadMethod)
            {
                case LoadMethod.Main:
                    if (LoadDB(SavesPath, specificFile, ref currentRef)) return true;
                    break;
                case LoadMethod.Backup:
                    if (LoadDB(BackupsPath, specificFile, ref currentRef)) return true;
                    break;
                case LoadMethod.Both:
                default:
                    // attempt to load the main save first
                    if (LoadDB(SavesPath, specificFile, ref currentRef) ||
                        LoadDB(BackupsPath, specificFile, ref currentRef))
                    {
                        return true;
                    }
                    break;
                case LoadMethod.None:
                    Plugin.Log(LogSystem.Core, LogLevel.Info, $"Initialising DB for {specificFile}");
                    currentRef = initialiser == null ? new TData() : initialiser();
                    return true;
            }

            // If nothing loaded correctly, check if we should use the initialiser or just return the current value.
            if (!useInitialiser) return false;
            
            Plugin.Log(LogSystem.Core, LogLevel.Warning, $"Initialising DB for {specificFile}");
            currentRef = initialiser == null ? new TData() : initialiser();
            return false;
        }

        private static bool LoadDB<TData>(string folder, string specificFile, ref TData data) where TData : class, new()
        {
            // Default JSON content to valid json so that they have a chance to be serialised without errors when loading for the first time.
            var genericType = typeof(TData);
            var isJsonListData = genericType.IsGenericType &&
                                 (genericType.GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>)) ||
                                  genericType.GetGenericTypeDefinition().IsAssignableFrom(typeof(HashSet<>)));
            var defaultContents = isJsonListData ? "[]" : "{}";
            try {
                var saveFile = ConfirmFile(folder, specificFile, () => defaultContents);
                var jsonString = File.ReadAllText(saveFile);
                data = JsonSerializer.Deserialize<TData>(jsonString, _jsonOptions);
                Plugin.Log(LogSystem.Core, LogLevel.Info, $"DB loaded from {specificFile}");
                // return false if the saved file only contains the default contents. This allows the default constructors to run.
                return !defaultContents.Equals(jsonString);
            } catch (Exception e) {
                Plugin.Log(LogSystem.Core, LogLevel.Error, $"Could not load {specificFile}: {e.Message}", true);
                return false;
            }
        }
        
        public static string ConfirmFile(string address, string file, Func<string> defaultContents = null) {
            try {
                Directory.CreateDirectory(address);
            }
            catch (Exception e) {
                throw new Exception("Error creating directory at " + address + "\n Error is: " + e.Message);
            }
            var fileAddress = Path.Combine(address, file);
            try
            {
                // If the file does not exist, create a new empty file there
                if (!File.Exists(fileAddress))
                {
                    File.WriteAllText(fileAddress, defaultContents == null ? "" : defaultContents());
                }
            } catch (Exception e) {
                throw new Exception("Error creating file at " + fileAddress + "\n Error is: " + e.Message);
            }

            return fileAddress;
        }
        
        public static string EnsureFile(string address, string file, Func<string> contents = null) {
            try {
                Directory.CreateDirectory(address);
            }
            catch (Exception e) {
                throw new Exception("Error creating directory at " + address + "\n Error is: " + e.Message);
            }
            var fileAddress = Path.Combine(address, file);
            try
            {
                File.WriteAllText(fileAddress, contents == null ? "" : contents());
            } catch (Exception e) {
                throw new Exception("Error creating file at " + fileAddress + "\n Error is: " + e.Message);
            }

            return fileAddress;
        }
    }
    
    public class PrefabGuidConverter : JsonConverter<PrefabGUID>
    {
        public override PrefabGUID Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            new PrefabGUID(reader.GetInt32()!);

        public override void Write(
            Utf8JsonWriter writer,
            PrefabGUID guid,
            JsonSerializerOptions options) =>
            writer.WriteNumberValue(guid.GuidHash);
    }
}
