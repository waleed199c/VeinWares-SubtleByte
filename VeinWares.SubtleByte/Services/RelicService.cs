using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Services
{
    internal static class RelicService
    {
        public static readonly PrefabGUID Relic_Monster = new PrefabGUID(1068709119);
        public static readonly PrefabGUID Relic_Behemoth = new PrefabGUID(-1703886455);
        public static readonly PrefabGUID Relic_Paladin = new PrefabGUID(-1161197991);
        public static readonly PrefabGUID Relic_Manticore = new PrefabGUID(-238197495);

        static readonly PrefabGUID[] Set = { Relic_Monster, Relic_Behemoth, Relic_Paladin, Relic_Manticore };

        public static void GrantAllRelics(Entity character)
        {
            if (!character.Exists() || !character.IsPlayer()) return;

            var name = character.GetPlayerName();
            var sid = character.GetSteamId();

            foreach (var guid in Set)
            {
                // apply if missing
                if (!character.HasBuff(guid))
                {
                    if (character.TryApplyAndGetBuff(guid, out var buff) && buff.Exists())
                    {
                        MakePersistent(buff);
                        ModLogger.Info($"[RelicPatch] Applied {Label(guid)} → {name} ({sid}) + PersistThroughDeath.");
                    }
                    else
                    {
                        ModLogger.Warn($"[RelicPatch] Failed applying {Label(guid)} → {name} ({sid}).");
                    }
                }
                else
                {
                    // ensure persistence on already-present buff
                    if (character.TryGetBuff(guid, out var buffEnt) && buffEnt.Exists())
                    {
                        if (MakePersistent(buffEnt))
                            ModLogger.Info($"[RelicPatch] Ensured PersistThroughDeath on {Label(guid)} → {name} ({sid}).");
                    }
                }
            }
        }

        static bool MakePersistent(Entity buffEntity)
        {
            if (!buffEntity.Has<Buff_Persists_Through_Death>())
            {
                buffEntity.Add<Buff_Persists_Through_Death>();
                return true;
            }
            return false;
        }

        static string Label(PrefabGUID g)
        {
            if (g.GuidHash == Relic_Monster.GuidHash) return "Monster";
            if (g.GuidHash == Relic_Behemoth.GuidHash) return "Behemoth";
            if (g.GuidHash == Relic_Paladin.GuidHash) return "Paladin";
            if (g.GuidHash == Relic_Manticore.GuidHash) return "Manticore";
            return g.GuidHash.ToString();
        }
    }
}
