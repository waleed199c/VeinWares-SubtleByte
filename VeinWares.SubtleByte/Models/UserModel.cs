using ProjectM.Network;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VeinWares.SubtleByte.Extensions;

namespace VeinWares.SubtleByte.Models
{
    public class UserModel
    {
        public Entity UserEntity { get; }
        public Entity CharacterEntity { get; }
        public string CharacterName { get; }
        public ulong PlatformId { get; }

        public UserModel(Entity userEntity)
        {
            UserEntity = userEntity;

            var user = userEntity.Read<User>();
            CharacterEntity = user.LocalCharacter._Entity;
            CharacterName = user.CharacterName.ToString();
            PlatformId = user.PlatformId;
        }

        public float3 Position =>
            UserEntity.Read<LocalToWorld>().Position;
    }
}
