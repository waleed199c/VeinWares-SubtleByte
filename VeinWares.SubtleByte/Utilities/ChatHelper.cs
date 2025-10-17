using System;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using VeinWares.SubtleByte.Extensions;

namespace VeinWares.SubtleByte.Utilities;

internal static class ChatHelper
{
    private static readonly EntityQueryDesc UserQueryDesc = new()
    {
        All = new[]
        {
            ComponentType.ReadOnly<User>()
        }
    };

    public static bool TrySendSystemMessage(Entity targetEntity, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || !targetEntity.Exists())
        {
            return false;
        }

        var userEntity = targetEntity.GetUserEntity();
        if (!userEntity.Exists())
        {
            return false;
        }

        return TrySendSystemMessageToUser(userEntity, message);
    }

    public static bool TrySendSystemMessage(ulong steamId, string message)
    {
        if (steamId == 0UL || string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var entityManager = Core.EntityManager;
        var query = entityManager.CreateEntityQuery(UserQueryDesc);
        NativeArray<Entity> entities = default;

        try
        {
            entities = query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var user = entityManager.GetComponentData<User>(entity);
                if (user.PlatformId != steamId)
                {
                    continue;
                }

                return Send(entityManager, user, message);
            }
        }
        finally
        {
            if (entities.IsCreated)
            {
                entities.Dispose();
            }

            query.Dispose();
        }

        return false;
    }

    private static bool TrySendSystemMessageToUser(Entity userEntity, string message)
    {
        var entityManager = Core.EntityManager;
        if (!entityManager.Exists(userEntity) || !entityManager.HasComponent<User>(userEntity))
        {
            return false;
        }

        var user = entityManager.GetComponentData<User>(userEntity);
        return Send(entityManager, user, message);
    }

    private static bool Send(EntityManager entityManager, User user, string message)
    {
        try
        {
            FixedString512Bytes chatMessage = default;
            chatMessage.Append(message);
            ServerChatUtils.SendSystemMessageToClient(entityManager, user, ref chatMessage);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
