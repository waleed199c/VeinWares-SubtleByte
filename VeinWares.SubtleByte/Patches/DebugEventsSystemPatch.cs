using HarmonyLib;
using ProjectM;
using Stunlock.Core;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Services;
using VeinWares.SubtleByte.Utilities;

namespace VeinWares.SubtleByte.Patches
{
    [HarmonyPatch(typeof(ProjectM.Gameplay.Systems.InteractValidateAndStopSystemServer), "OnUpdate")]
    internal static class DebugEventsSystemPatch
    {
        // Events
        static readonly PrefabGUID EV_Sit = new PrefabGUID(-13752172);
        static readonly PrefabGUID EV_Travel = new PrefabGUID(559608494);

        // Player state
        static readonly PrefabGUID Buff_SeatBusy = new PrefabGUID(1845376969);

        // The actual Darkness throne object (from your log)
        static readonly int ThroneDarknessTargetGuid = -633207199;

        // per-player cooldown to avoid multi-fire during sit→travel sequence
        static readonly Dictionary<ulong, int> _cooldownUntilTick = new();
        static int _tick;
        const int CooldownTicks = 90; // ~1.5s @ 60tps

        static void Prefix(ProjectM.Gameplay.Systems.InteractValidateAndStopSystemServer __instance)
        {
            _tick++;
            var em = __instance.EntityManager;

            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<PrefabGUID>(),
                ComponentType.ReadOnly<Attach>(),
                ComponentType.ReadOnly<SpellTarget>()
            );

            var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var e in entities)
            {
                PrefabGUID evGuid; Attach at; SpellTarget st;
                try
                {
                    evGuid = em.GetComponentData<PrefabGUID>(e);
                    at = em.GetComponentData<Attach>(e);
                    st = em.GetComponentData<SpellTarget>(e);
                }
                catch { continue; }

                // only our darkness signals
                bool isSit = evGuid.GuidHash == EV_Sit.GuidHash;
                bool isTravel = evGuid.GuidHash == EV_Travel.GuidHash;
                if (!isSit && !isTravel) continue;

                // target must be the Darkness throne object
                var target = st.Target._Entity;
                if (target == Entity.Null || !em.HasComponent<PrefabGUID>(target)) continue;
                var targetGuid = em.GetComponentData<PrefabGUID>(target).GuidHash;
                if (targetGuid != ThroneDarknessTargetGuid) continue;

                var character = at.Parent;
                if (!character.Exists() || !character.IsPlayer()) continue;

                // sit requires seat-busy (helps avoid noise)
                if (isSit && !character.HasBuff(Buff_SeatBusy)) continue;

                var sid = character.GetSteamId();
                if (sid == 0) continue;

                // cooldown gate
                if (_cooldownUntilTick.TryGetValue(sid, out var until) && until > _tick)
                    continue;
                _cooldownUntilTick[sid] = _tick + CooldownTicks;

                var name = character.GetPlayerName();
                Core.Log.LogInfo($"[ThroneDetect] Darkness confirmed for {name} ({sid}) via {(isTravel ? "Travel" : "Sit")}.");

                // 🎁 Apply relic bundle now
                RelicService.GrantAllRelics(character);
            }
            entities.Dispose();
            // periodic cleanup
            if ((_tick & 0xFF) == 0)
            {
                var rm = new List<ulong>();
                foreach (var kv in _cooldownUntilTick)
                    if (kv.Value <= _tick) rm.Add(kv.Key);
                foreach (var id in rm) _cooldownUntilTick.Remove(id);
            }
        }
    }
}
