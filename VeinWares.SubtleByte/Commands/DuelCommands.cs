using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using VeinWares.SubtleByte.Extensions;
using VeinWares.SubtleByte.Services;

namespace VeinWares.SubtleByte.Commands;

public static class DuelCommands
{
    [Command("duel summon", adminOnly: true, description: "Summon a VBlood challenger and duel arena at your location." )]
    public static void SummonDuel(ChatCommandContext ctx, int prefabId = -1905691330, int duelCount = 1, int maxParticipantsPerDuel = 10)
    {
        var player = ctx.Event.SenderCharacterEntity;
        if (!player.Exists())
        {
            ctx.Reply("[Duel] Unable to locate your character entity.");
            return;
        }

        duelCount = math.max(1, math.min(duelCount, 6));
        maxParticipantsPerDuel = math.max(1, math.min(maxParticipantsPerDuel, 30));

        var entityManager = Core.EntityManager;

        float3 origin;
        quaternion rotation;
        if (entityManager.TryGetComponentData(player, out LocalTransform transform))
        {
            origin = transform.Position;
            rotation = transform.Rotation;
        }
        else if (entityManager.TryGetComponentData(player, out LocalToWorld localToWorld))
        {
            origin = localToWorld.Position;
            rotation = quaternion.identity;
        }
        else
        {
            ctx.Reply("[Duel] Unable to resolve your position.");
            return;
        }

        var forward = math.mul(rotation, new float3(0f, 0f, 1f));
        forward.y = 0f;
        if (math.lengthsq(forward) < 0.01f)
        {
            forward = new float3(0f, 0f, 1f);
        }
        forward = math.normalizesafe(forward, new float3(0f, 0f, 1f));

        var centerPosition = origin + forward * 2.5f;
        var challengerPosition = origin + forward * 6f;

        var prefab = new PrefabGUID(prefabId);
        var scheduled = DuelSummonService.TrySummonForPlayer(
            player,
            prefab,
            centerPosition,
            challengerPosition,
            maxParticipantsPerDuel,
            duelCount - 1,
            explicitParticipants: null,
            forwardHint: forward);
        if (!scheduled)
        {
            ctx.Reply("[Duel] Failed to schedule duel summon.");
            return;
        }

        ctx.Reply(
            $"[Duel] Summoning challenger {prefabId} for {duelCount} duel{(duelCount == 1 ? string.Empty : "s")} with up to {maxParticipantsPerDuel} players each.");
    }
}
