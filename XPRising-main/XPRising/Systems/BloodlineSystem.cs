﻿using ProjectM;
using ProjectM.Network;
using BepInEx.Logging;
using Unity.Entities;
using Stunlock.Core;
using XPRising.Utils;
using XPRising.Utils.Prefabs;
using LogSystem = XPRising.Plugin.LogSystem;

namespace XPRising.Systems
{
    public class BloodlineSystem
    {
        public static readonly Dictionary<PrefabGUID, GlobalMasterySystem.MasteryType> BuffToBloodTypeMap = new()
        {
            // Seems like we only need to check against the T1 options
            { new PrefabGUID((int)Effects.AB_BloodBuff_Worker_Tier1), GlobalMasterySystem.MasteryType.BloodWorker },
            { new PrefabGUID((int)Effects.AB_BloodBuff_Warrior_Tier1), GlobalMasterySystem.MasteryType.BloodWarrior },
            { new PrefabGUID((int)Effects.AB_BloodBuff_Scholar_Tier1), GlobalMasterySystem.MasteryType.BloodScholar },
            { new PrefabGUID((int)Effects.AB_BloodBuff_Rogue_Tier1), GlobalMasterySystem.MasteryType.BloodRogue },
            { new PrefabGUID((int)Effects.AB_BloodBuff_Mutant_Tier1), GlobalMasterySystem.MasteryType.BloodMutant },
            { new PrefabGUID((int)Effects.AB_BloodBuff_Draculin_Tier1), GlobalMasterySystem.MasteryType.BloodDraculin },
            { new PrefabGUID((int)Effects.AB_BloodBuff_Dracula_Tier1), GlobalMasterySystem.MasteryType.BloodDracula },
            { new PrefabGUID((int)Effects.AB_BloodBuff_Creature_Tier1), GlobalMasterySystem.MasteryType.BloodCreature },
            { new PrefabGUID((int)Effects.AB_BloodBuff_Corruption_Tier1), GlobalMasterySystem.MasteryType.BloodCorruption },
            { new PrefabGUID((int)Effects.AB_BloodBuff_Brute_Tier1), GlobalMasterySystem.MasteryType.BloodBrute }
        };
        
        private static EntityManager _em = Plugin.Server.EntityManager;
        private static Random _random = new Random();

        public static bool MercilessBloodlines = true;
        public const int BloodTypeCount = 10;
        public static int VBloodAddsXTypes = BloodTypeCount;

        public static double VBloodMultiplier = 15;
        public static double MasteryGainMultiplier = 1.0;

        public static void UpdateBloodline(Entity killer, Entity victim, bool killOnly)
        {
            if (killer == victim) return;
            if (_em.HasComponent<Minion>(victim)) return;

            var victimLevel = _em.GetComponentData<UnitLevel>(victim);
            var killerUserEntity = _em.GetComponentData<PlayerCharacter>(killer).UserEntity;
            var killerUserComponent = _em.GetComponentData<User>(killerUserEntity);
            var steamID = killerUserComponent.PlatformId;
            Plugin.Log(LogSystem.Bloodline, LogLevel.Info, $"Updating bloodline mastery for {steamID}");
            
            double growthVal = Math.Clamp(victimLevel.Level.Value - ExperienceSystem.GetLevel(steamID), 1, 10);
            
            var (killerBloodType, killerBloodQuality, isKillerVBlood) = Helper.GetBloodInfo(killer);
            if (killerBloodType == BloodType.Unknown || isKillerVBlood){
                Plugin.Log(LogSystem.Bloodline, LogLevel.Info, $"killer does not have blood: Killer ({killer}), Victim ({victim})");
                return; 
            }

            GlobalMasterySystem.MasteryType playerMasteryToUpdate = GlobalMasterySystem.MasteryType.None;
            var growthModifier = killOnly ? 0.4 : 1.0;
            var (victimBloodType, victimBloodQuality, isVictimVBlood) = Helper.GetBloodInfo(victim);
            if (victimBloodType == BloodType.Unknown)
            {
                Plugin.Log(LogSystem.Bloodline, LogLevel.Info, $"victim does not have blood: Killer ({killer}), Victim ({victim}");
                return;
            }

            if (isVictimVBlood)
            {
                victimBloodQuality = 100f;
                growthModifier = VBloodMultiplier; 
                // When running the kill only step for VBloods, only add to the current bloodline, not multi-bloodlines
                if (VBloodAddsXTypes > 0 && !killOnly)
                {
                    var pmd = Database.PlayerMastery[steamID];
                    if (VBloodAddsXTypes >= BloodTypeCount)
                    {
                        Plugin.Log(LogSystem.Bloodline, LogLevel.Info, () => $"Adding V Blood bonus to all blood types.");
                        foreach (var bloodType in BuffToBloodTypeMap.Values)
                        {
                            var bloodTypeGrowth = growthVal * BloodGrowthMultiplier(growthModifier, victimBloodQuality);
                            GlobalMasterySystem.BankMastery(steamID, victim, bloodType, ApplyMasteryMultiplier(bloodType, bloodTypeGrowth));
                        }
                    }
                    else
                    {
                        var selectedBloodTypes =
                            BuffToBloodTypeMap.Values.OrderBy(x => _random.Next()).Take(VBloodAddsXTypes);
                        Plugin.Log(LogSystem.Bloodline, LogLevel.Info, () => $"Adding V Blood bonus to {VBloodAddsXTypes} blood types: {string.Join(",", selectedBloodTypes)}");
                        foreach (var bloodType in selectedBloodTypes)
                        {
                            var bloodTypeGrowth = growthVal * BloodGrowthMultiplier(growthModifier, victimBloodQuality);
                            GlobalMasterySystem.BankMastery(steamID, victim, bloodType, ApplyMasteryMultiplier(bloodType, bloodTypeGrowth));
                        }
                    }
                    return;
                }
                else
                {
                    playerMasteryToUpdate = BloodToMastery(killerBloodType);
                }
            }
            else
            {
                playerMasteryToUpdate = BloodToMastery(victimBloodType);
            }

            if (playerMasteryToUpdate == GlobalMasterySystem.MasteryType.None)
            {
                Plugin.Log(LogSystem.Bloodline, LogLevel.Info, $"victim has frail blood, not modifying: Killer ({killer}), Victim ({victim})");
                return;
            }
            
            var playerMasterydata = Database.PlayerMastery[steamID];
            var bloodlineMastery = playerMasterydata[playerMasteryToUpdate];
            growthVal *= BloodGrowthMultiplier(growthModifier, victimBloodQuality);
            
            if (MercilessBloodlines && victimBloodQuality <= bloodlineMastery.Mastery)
            {
                Plugin.Log(LogSystem.Bloodline, LogLevel.Info,
                    $"merciless bloodlines exit: victim blood quality less than killer mastery: Killer ({bloodlineMastery.Mastery}), Victim ({victimBloodQuality})");
                if (Cache.PlayerHasUINotifications(steamID))
                {
                    var message = L10N.Get(L10N.TemplateKey.BloodlineMercilessErrorWeak);
                    var preferences = Database.PlayerPreferences[steamID];
                    XPShared.Transport.Utils.ServerSendNotification(killerUserComponent, "bloodline", message.Build(preferences.Language), LogLevel.Warning);
                }
                else if (Database.PlayerPreferences[steamID].LoggingMastery)
                {
                    var message = L10N.Get(L10N.TemplateKey.BloodlineMercilessErrorWeak);
                    Output.SendMessage(killerUserEntity, message);
                }
                return;
            }

            if (_em.HasComponent<PlayerCharacter>(victim))
            {
                var victimGear = _em.GetComponentData<Equipment>(victim);
                var bonusMastery = victimGear.ArmorLevel + victimGear.WeaponLevel + victimGear.SpellLevel;
                growthVal *= (1 + (bonusMastery * 0.01));
                
                Plugin.Log(LogSystem.Bloodline, LogLevel.Info, $"Bonus bloodline mastery {bonusMastery:F3}]");
            }

            growthVal = ApplyMasteryMultiplier(playerMasteryToUpdate, growthVal);
            
            GlobalMasterySystem.BankMastery(steamID, victim, playerMasteryToUpdate, growthVal);
        }

        public static GlobalMasterySystem.MasteryType BloodMasteryType(Entity entity)
        {
            var (bloodType, _, _) = Helper.GetBloodInfo(entity);
            return BloodToMastery(bloodType);
        }

        private static GlobalMasterySystem.MasteryType BloodToMastery(BloodType blood)
        {
            if (blood == BloodType.None) {
                return GlobalMasterySystem.MasteryType.None;
            }

            return (GlobalMasterySystem.MasteryType)blood;
        }

        private static double BloodGrowthMultiplier(double modifier, double quality)
        {
            return modifier * quality * 0.01f;
        }

        private static double ApplyMasteryMultiplier(GlobalMasterySystem.MasteryType bloodType, double mastery)
        {
            Plugin.Log(LogSystem.Bloodline, LogLevel.Info,
                () => $"Blood growth {Enum.GetName(bloodType)}: [{mastery:F3} * 0.05 * {MasteryGainMultiplier:F3} => {mastery * 0.05 * MasteryGainMultiplier:F3}]");
            return mastery * 0.05 * MasteryGainMultiplier;
        }
    }
}
