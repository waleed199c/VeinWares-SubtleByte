﻿using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using BepInEx.Logging;
using Stunlock.Core;
using XPRising.Models;
using XPRising.Transport;
using XPRising.Utils;
using XPRising.Utils.Prefabs;
using Cache = XPRising.Utils.Cache;
using LogSystem = XPRising.Plugin.LogSystem;

namespace XPRising.Systems
{
    public class ExperienceSystem
    {
        private static EntityManager _entityManager = Plugin.Server.EntityManager;
        
        public static float ExpMultiplier = 1.5f;
        public static float VBloodMultiplier = 15;
        public static int MaxLevel = 100;
        public static float GroupMaxDistance = 50;
        public static float LevelRange = 20;

        public static float PvpXpLossPercent = 0;
        public static float PveXpLossPercent = 10;
        
        // Encourage group play by buffing XP for groups
        public static float GroupXpBuffGrowth = 0.3f;
        public static float MaxGroupXpBuff = 2.0f;
        
        /*
         * The following values have been tweaked to have the following stats:
         * Total xp: 355,085
         * Last level xp: 7,7765
         *
         * Assuming:
         * - ExpMultiplier = 1.5
         * - Ignoring VBlood bonus (15x in last column)
         * - MaxLevel = 90 (Anything above 90 will be a slog as most mobs stop at 83)
         * - Max mob level = 83 (Dracula is 90, but ignoring him)
         * - Minimum allowed level difference = -12
         *
         *    mob level=> | same   | +5   |  -5  | +5 => -5 | same (VBlood only) |
         * _______________|________|______|______|__________|____________________|
         * Total kills    | 3864   | 2845 | 6309 | 4002     | 257                |
         * lvl 0 kills    | 10     | 2    | 10   | 2        | 1                  |
         * Last lvl kills | 85     | 85   | 160  | 85       | 6                  |
         *
         * +5/-5 offset to levels in the above table as still clamped to the range [1, 100].
         *
         * To increase the kill counts across the board, reduce ExpMultiplier.
         * If you want to tweak the curve, lowering ExpConstant and raising ExpPower can be done in tandem to flatten the
         * curve (attempting to ensure that the total kills or last lvl kills stay the same).
         *
         * VBlood entry in the table (naively) assumes that the player is killing a VBlood at the same level (when some
         * of those do not exist).
         *
         */
        private const float ExpConstant = 0.3f;
        private const float ExpPower = 2.2f;

        // This is updated on server start-up to match server settings start level
        public static int StartingExp = 0;
        private static int MinLevel => ConvertXpToLevel(StartingExp);
        private static int MaxXp => ConvertLevelToXp(MaxLevel);

        private static HashSet<Units> _noExpUnits = new(
            FactionUnits.farmNonHostile.Select(u => u.type)
                .Union(FactionUnits.farmFood.Select(u => u.type))
                .Union(FactionUnits.otherNonHostile.Select(u => u.type)));

        private static HashSet<Units> _minimalExpUnits = new()
        {
            Units.CHAR_Militia_Nun,
            Units.CHAR_Mutant_RatHorror
        };
        
        // We can add various mobs/groups/factions here to reduce or increase XP gain
        private static float ExpValueMultiplier(PrefabGUID entityPrefab, bool isVBlood)
        {
            if (isVBlood) return VBloodMultiplier;
            var unit = Helper.ConvertGuidToUnit(entityPrefab);
            if (_noExpUnits.Contains(unit)) return 0;
            if (_minimalExpUnits.Contains(unit)) return 0.1f;
            
            return 1;
        }
        
        public static bool IsPlayerLoggingExperience(ulong steamId)
        {
            return Database.PlayerPreferences[steamId].LoggingExp;
        }

        public static void ExpMonitor(List<Alliance.ClosePlayer> closeAllies, PrefabGUID victimPrefab, int victimLevel, bool isVBlood)
        {
            var multiplier = ExpValueMultiplier(victimPrefab, isVBlood);
            // Early exit to entirely stop XP calculations when the multiplier is 0.
            if (multiplier == 0) return;
            
            var sumGroupLevel = closeAllies.Sum(x => x.playerLevel);
            var avgGroupLevel = (int)Math.Floor(closeAllies.Average(x => x.playerLevel));

            // Calculate an XP bonus that grows as groups get larger
            var baseGroupXpBonus = Math.Min(Math.Pow(1 + GroupXpBuffGrowth, closeAllies.Count - 1), MaxGroupXpBuff);

            Plugin.Log(LogSystem.Xp, LogLevel.Info, "Running Assign EXP for all close allied players");
            var useGroupBonus = GroupMaxDistance > 0 && closeAllies.Count > 1;
            foreach (var teammate in closeAllies) {
                Plugin.Log(LogSystem.Xp, LogLevel.Info, $"Assigning EXP to {teammate.steamID}: LVL: {teammate.playerLevel}, IsKiller: {teammate.isTrigger}, IsVBlood: {isVBlood}");
                
                // Calculate the portion of the total XP that this player should get.
                var groupPortion = sumGroupLevel == 0 || teammate.playerLevel == 0 ?
                    1.0d / (sumGroupLevel + closeAllies.Count) : // If either the group or player is at level 0, add 1 to everyone's level for this proportion calculation
                    (double)teammate.playerLevel / sumGroupLevel; // portion == ratio of player level to group level
                var groupMultiplier = useGroupBonus ? baseGroupXpBonus * groupPortion : 1.0;
                Plugin.Log(LogSystem.Xp, LogLevel.Info, $"--- multipliers: {multiplier:F3}, base group: {baseGroupXpBonus:F3} * {teammate.playerLevel} / {sumGroupLevel} = {groupMultiplier:F3}");
                // Calculate the player level as the max of (playerLevel, avgGroupLevel). This has 2 results:
                // - Stop higher level players "reducing" their level for XP calculations
                // - Prevent lower level players from getting too much XP from killing higher level mobs
                var calculatedPlayerLevel = Math.Max(avgGroupLevel, teammate.playerLevel);
                // Assign XP to the player as if they were at this calculated level.
                AssignExp(teammate, calculatedPlayerLevel, victimLevel, multiplier * groupMultiplier);
            }
        }

        private static void AssignExp(Alliance.ClosePlayer player, int calculatedPlayerLevel, int mobLevel, double multiplier) {
            if (player.currentXp >= ConvertLevelToXp(MaxLevel)) return;
            
            var xpGained = CalculateXp(calculatedPlayerLevel, mobLevel, multiplier);
            if (Database.PlayerPreferences[player.steamID].ScrollingCombatText)
            {
                Helper.CreateXpText(player.triggerPosition, xpGained, player.userComponent.LocalCharacter._Entity, player.userEntity);
            }

            var newXp = Math.Max(player.currentXp, 0) + xpGained;
            SetXp(player.steamID, newXp);

            GetLevelAndProgress(newXp, out var level, out var progressPercent, out var earned, out var needed);
            Plugin.Log(LogSystem.Xp, LogLevel.Info, $"Gained {xpGained} from Lv.{mobLevel} [{earned}/{needed} (total {newXp})]");
            ClientActionHandler.SendXpData(player.userComponent, level, progressPercent, earned, needed, xpGained);
            if (IsPlayerLoggingExperience(player.steamID))
            {
                var message =
                    L10N.Get(L10N.TemplateKey.XpGain)
                        .AddField("{xpGained}", xpGained.ToString())
                        .AddField("{mobLevel}", mobLevel.ToString())
                        .AddField("{earned}", earned.ToString())
                        .AddField("{needed}", needed.ToString());
                
                Output.SendMessage(player.userEntity, message);
            }
            
            CheckAndApplyLevel(player.userComponent.LocalCharacter._Entity, player.userEntity, player.steamID);
        }

        private static int CalculateXp(int playerLevel, int mobLevel, double multiplier) {
            // Using a min level difference here to ensure that the user can get a basic level of XP
            var levelDiff = mobLevel - playerLevel;
            
            var baseXpGain = (int)(Math.Max(1, mobLevel * multiplier * (1 + Math.Min(mobLevel - (mobLevel/LevelRange)*levelDiff, levelDiff)*(1/LevelRange)))*ExpMultiplier);

            Plugin.Log(LogSystem.Xp, LogLevel.Info, $"--- Max(1, {mobLevel} * {multiplier:F3} * (1 + {levelDiff}))*{ExpMultiplier}  => {baseXpGain} => Clamped between [1,inf]");
            // Clamp the XP gain to be within 1 XP and "maxGain" XP.
            return Math.Max(baseXpGain, 1);
        }
        
        public static void DeathXpLoss(Entity playerEntity, Entity killerEntity) {
            var pvpKill = !playerEntity.Equals(killerEntity) && _entityManager.TryGetComponentData<PlayerCharacter>(killerEntity, out _);
            var xpLossPercent = pvpKill ? PvpXpLossPercent : PveXpLossPercent;
            
            if (xpLossPercent == 0)
            {
                Plugin.Log(LogSystem.Xp, LogLevel.Info, $"xpLossPercent is 0. No lost XP. (PvP: {pvpKill})");
                return;
            }
            
            var player = _entityManager.GetComponentData<PlayerCharacter>(playerEntity);
            var userEntity = player.UserEntity;
            var user = _entityManager.GetComponentData<User>(userEntity);
            var steamID = user.PlatformId;

            var exp = GetXp(steamID);
            
            GetLevelAndProgress(exp, out _, out _, out _, out var needed);
            
            var calculatedNewXp = exp - needed * (xpLossPercent/100);

            // The minimum our XP is allowed to drop to
            var minXp = ConvertLevelToXp(ConvertXpToLevel(exp));
            var currentXp = Math.Max((int)Math.Ceiling(calculatedNewXp), minXp);
            var xpLost = exp - currentXp;
            Plugin.Log(LogSystem.Xp, LogLevel.Info, $"Calculated XP: {steamID}: {currentXp} = Max({exp} - {needed} * {xpLossPercent/100}, {minXp}) => Max({calculatedNewXp}, {minXp}) => [lost {xpLost}]");
            if (xpLost == 0) return;
            
            SetXp(steamID, currentXp);

            // We likely don't need to use ApplyLevel() here (as it shouldn't drop below the current level) but do it anyway as XP has changed.
            CheckAndApplyLevel(playerEntity, userEntity, steamID);
            GetLevelAndProgress(currentXp, out var level, out var progressPercent, out var earned, out needed);
            
            // Make sure we send xpLost as a negative value to ensure it gets displayed that way!
            ClientActionHandler.SendXpData(user, level, progressPercent, earned, needed, -xpLost);

            var message =
                L10N.Get(L10N.TemplateKey.XpLost)
                    .AddField("{xpLost}", xpLost.ToString())
                    .AddField("{earned}", earned.ToString())
                    .AddField("{needed}", needed.ToString());

            Output.SendMessage(userEntity, message);
        }

        public static void CheckAndApplyLevel(Entity entity, Entity user, ulong steamID)
        {
            var level = ConvertXpToLevel(GetXp(steamID));
            if (level < MinLevel)
            {
                level = MinLevel;
                SetXp(steamID, StartingExp);
            }
            else if (level > MaxLevel)
            {
                level = MaxLevel;
                SetXp(steamID, MaxXp);
            }

            var userData = Plugin.Server.EntityManager.GetComponentData<User>(user);
            if (Cache.player_level.TryGetValue(steamID, out var storedLevel))
            {
                if (storedLevel < level)
                {
                    // Apply the level up buff
                    if (BuffUtil.LevelUpBuffId != 0)
                    {
                        BuffUtil.ApplyBuff(user, entity, BuffUtil.LevelUpBuff);
                    }
                    
                    // Send a level up message
                    var message =
                        L10N.Get(L10N.TemplateKey.XpLevelUp).AddField("{level}", level.ToString());
                    Output.SendMessage(user, message);
                }

                Plugin.Log(LogSystem.Xp, LogLevel.Info,
                    $"Set player level: LVL: {level} (stored: {storedLevel}) XP: {GetXp(steamID)}");
            }
            else
            {
                Plugin.Log(LogSystem.Xp, LogLevel.Info,
                    $"Player logged in: LVL: {level} (stored: {storedLevel}) XP: {GetXp(steamID)}");
            }

            Cache.player_level[steamID] = level;

            ApplyLevel(entity, level);
            
            // Re-apply the buff now that we have set the level.
            BuffUtil.ApplyStatBuffOnDelay(userData, user, userData.LocalCharacter._Entity);
        }
        
        public static void ApplyLevel(Entity entity, int level)
        {
            Equipment equipment = Plugin.Server.EntityManager.GetComponentData<Equipment>(entity);
            Plugin.Log(LogSystem.Xp, LogLevel.Info, $"Current gear levels: A:{equipment.ArmorLevel.Value} W:{equipment.WeaponLevel.Value} S:{equipment.SpellLevel.Value}");
            // Brute blood potentially modifies ArmorLevel, so set ArmorLevel 0 so our XP level doesn't conflict with it.
            // We are using the WeaponLevel as the XP level, as the SpellLevel has some strange things occur when you equip/unequip the spell slot
            equipment.ArmorLevel._Value = 0;
            equipment.WeaponLevel._Value = level;
            equipment.SpellLevel._Value = 0;

            Plugin.Server.EntityManager.SetComponentData(entity, equipment);
        }

        public static int ConvertXpToLevel(int xp)
        {
            // Shortcut for exceptional cases
            if (xp < 1) return 0;
            // Level = CONSTANT * (xp)^1/POWER
            int lvl = (int)Math.Floor(ExpConstant * Math.Pow(xp, 1 / ExpPower));
            return lvl;
        }

        public static int ConvertLevelToXp(int level)
        {
            // Shortcut for exceptional cases
            if (level < 1) return 1;
            // XP = (Level / CONSTANT) ^ POWER
            int xp = (int)Math.Pow(level / ExpConstant, ExpPower);
            // Add 1 to make it show start of this level, rather than end of the previous level.
            return xp + 1;
        }

        public static int GetXp(ulong steamID)
        {
            return Plugin.ExperienceSystemActive ? Math.Max(Database.PlayerExperience.GetValueOrDefault(steamID, StartingExp), StartingExp) : 0;
        }
        
        public static void SetXp(ulong steamID, int exp)
        {
            Database.PlayerExperience[steamID] = Math.Clamp(exp, 0, MaxXp);
        }

        public static int GetLevel(ulong steamID)
        {
            if (Plugin.ExperienceSystemActive)
            {
                return ConvertXpToLevel(GetXp(steamID));
            }
            // Otherwise return the current gear score.
            if (!PlayerCache.FindPlayer(steamID, true, out var playerEntity, out _, out _)) return 0;
            
            Equipment equipment = _entityManager.GetComponentData<Equipment>(playerEntity);
            return (int)(equipment.ArmorLevel.Value + equipment.WeaponLevel.Value + equipment.SpellLevel.Value);
        }

        public static void GetLevelAndProgress(int currentXp, out int level, out float progressPercent, out int earnedXp, out int neededXp) {
            level = ConvertXpToLevel(currentXp);
            var currentLevelXp = ConvertLevelToXp(level);
            var nextLevelXp = ConvertLevelToXp(level + 1);

            neededXp = nextLevelXp - currentLevelXp;
            earnedXp = currentXp - currentLevelXp;
            
            progressPercent = (float)earnedXp / neededXp;
        }

        public static LazyDictionary<string, LazyDictionary<UnitStatType, float>> DefaultExperienceClassStats()
        {
            var classes = new LazyDictionary<string, LazyDictionary<UnitStatType, float>>();
            classes["health"] = new LazyDictionary<UnitStatType, float>() { { UnitStatType.MaxHealth, 0.5f } };
            classes["ppower"] = new LazyDictionary<UnitStatType, float>() { { UnitStatType.PhysicalPower, 0.75f } };
            classes["spower"] = new LazyDictionary<UnitStatType, float>() { { UnitStatType.SpellPower, 0.75f } };
            classes["presist"] = new LazyDictionary<UnitStatType, float>() { { UnitStatType.PhysicalResistance, 0.05f } };
            classes["sresist"] = new LazyDictionary<UnitStatType, float>() { { UnitStatType.SpellResistance, 0.05f } };
            classes["beasthunter"] = new LazyDictionary<UnitStatType, float>()
                { { UnitStatType.DamageVsBeasts, 0.04f }, { UnitStatType.ResistVsBeasts, 4f } };
            classes["undeadhunter"] = new LazyDictionary<UnitStatType, float>()
                { { UnitStatType.DamageVsUndeads, 0.02f }, { UnitStatType.ResistVsUndeads, 2 } };
            classes["manhunter"] = new LazyDictionary<UnitStatType, float>() { { UnitStatType.DamageVsHumans, 0.02f}, { UnitStatType.ResistVsHumans, 2 } };
            classes["demonhunter"] = new LazyDictionary<UnitStatType, float>() { { UnitStatType.DamageVsDemons, 0.02f}, { UnitStatType.ResistVsDemons, 2 } };
            classes["farmer"] = new LazyDictionary<UnitStatType, float>()
            {
                { UnitStatType.ResourceYield, 0.1f }, { UnitStatType.PhysicalPower, -1f },
                { UnitStatType.SpellPower, -0.5f }
            };
            return classes;
        }
    }
}