using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using System;
using System.Runtime.InteropServices;
using Unity.Entities;
namespace VeinWares.SubtleByte.Extensions
{
    public static class Extensions
    {
        static EntityManager EntityManager => Core.EntityManager;

        public unsafe static T Read<T>(this Entity entity) where T : struct
        {
            var ct = new ComponentType(Il2CppType.Of<T>());
            void* rawPointer = Core.Server.EntityManager.GetComponentDataRawRO(entity, ct.TypeIndex);
            return Marshal.PtrToStructure<T>(new IntPtr(rawPointer));
        }
        public static DynamicBuffer<T> ReadBuffer<T>(this Entity entity) where T : struct
        {
            return EntityManager.GetBuffer<T>(entity);
        }
        public static DynamicBuffer<T> AddBuffer<T>(this Entity entity) where T : struct
        {
            return EntityManager.AddBuffer<T>(entity);
        }
        public unsafe static void Write<T>(this Entity entity, T componentData) where T : struct
        {
            var ct = new ComponentType(Il2CppType.Of<T>());
            byte[] byteArray = StructureToByteArray(componentData);
            int size = Marshal.SizeOf<T>();

            fixed (byte* p = byteArray)
            {
                Core.Server.EntityManager.SetComponentDataRaw(entity, ct.TypeIndex, p, size);
            }
        }

        public static void With<T>(this Entity entity, ActionRef<T> action) where T : struct
        {
            T data = entity.Read<T>();
            action(ref data);
            Core.Server.EntityManager.SetComponentData(entity, data);
        }

        public static void Add<T>(this Entity entity)
        {
            var ct = new ComponentType(Il2CppType.Of<T>());
            Core.Server.EntityManager.AddComponent(entity, ct);
        }

        public static void Remove<T>(this Entity entity)
        {
            var ct = new ComponentType(Il2CppType.Of<T>());
            Core.Server.EntityManager.RemoveComponent(entity, ct);
        }

        private static byte[] StructureToByteArray<T>(T obj) where T : struct
        {
            int size = Marshal.SizeOf(obj);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(obj, ptr, false);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        public static bool HasValue(this Entity entity)
        {
            return entity != Entity.Null;
        }
        const string PREFIX = "Entity(";
        const int LENGTH = 7;
        public static bool IndexWithinCapacity(this Entity entity)
        {
            string entityStr = entity.ToString();
            ReadOnlySpan<char> span = entityStr.AsSpan();

            if (!span.StartsWith(PREFIX)) return false;
            span = span[LENGTH..];

            int colon = span.IndexOf(':');
            if (colon <= 0) return false;

            ReadOnlySpan<char> tail = span[(colon + 1)..];

            int closeRel = tail.IndexOf(')');
            if (closeRel <= 0) return false;

            // Parse numbers
            if (!int.TryParse(span[..colon], out int index)) return false;
            if (!int.TryParse(tail[..closeRel], out _)) return false;

            // Single unsigned capacity check
            int capacity = EntityManager.EntityCapacity;
            bool isValid = (uint)index < (uint)capacity;

            if (!isValid)
            {
                // SBlog.Warn($"Entity index out of range! ({index}>{capacity})");
            }

            return isValid;
        }
        public static bool Exists(this Entity entity)
        {
            return entity.HasValue() && entity.IndexWithinCapacity() && EntityManager.Exists(entity);
        }
        public static void DestroyBuff(this Entity buffEntity)
        {
            if (buffEntity.Exists()) DestroyUtility.Destroy(EntityManager, buffEntity, DestroyDebugReason.TryRemoveBuff);
        }

        public static NetworkId GetNetworkId(this Entity entity)
        {
            if (entity.TryGetComponent(out NetworkId networkId))
            {
                return networkId;
            }

            return NetworkId.Empty;
        }
        public static ulong GetSteamId(this Entity entity)
        {
            if (entity.TryGetComponent(out PlayerCharacter playerCharacter))
            {
                return playerCharacter.UserEntity.GetUser().PlatformId;
            }
            else if (entity.TryGetComponent(out User user))
            {
                return user.PlatformId;
            }

            return 0; // maybe this should be -1 instead since steamId 0 sneaks in to weird places sometimes? noting for later
        }

        public static bool TryGetComponent<T>(this Entity entity, out T componentData) where T : struct
        {
            componentData = default;

            if (entity.Has<T>())
            {
                componentData = entity.Read<T>();
                return true;
            }

            return false;
        }
        public static bool Has<T>(this Entity entity) where T : struct
        {
            return EntityManager.HasComponent(entity, new(Il2CppType.Of<T>()));
        }

        public static User GetUser(this Entity entity)
        {
            if (entity.TryGetComponent(out User user)) return user;
            else if (entity.TryGetComponent(out PlayerCharacter playerCharacter) && playerCharacter.UserEntity.TryGetComponent(out user)) return user;

            return User.Empty;
        }

        public static bool IsPlayer(this Entity entity)
        {
            if (entity.Has<PlayerCharacter>())
            {
                return true;
            }

            return false;
        }

        public static Entity GetUserEntity(this Entity entity)
        {
            if (entity.TryGetComponent(out PlayerCharacter playerCharacter)) return playerCharacter.UserEntity;
            else if (entity.Has<User>()) return entity;

            return Entity.Null;
        }

        // Custom delegate to match the 'ref' style for With<T>
        public delegate void ActionRef<T>(ref T data);

        public static bool TryGetSteamId(this Entity entity, out ulong steamId)
        {
            steamId = 0UL;
            if (!entity.Exists()) return false;

            Entity userEntity = entity;

            // If it's not a User, try resolve owning User from Character
            if (!EntityManager.HasComponent<User>(userEntity))
                userEntity = entity.GetUserEntity();

            if (userEntity == Entity.Null || !EntityManager.HasComponent<User>(userEntity))
                return false;

            var user = EntityManager.GetComponentData<User>(userEntity);
            steamId = user.PlatformId;
            return true;
        }

      

        /// <summary>Try get player name (CharacterName from User) from either Character or User entity.</summary>
        public static bool TryGetPlayerName(this Entity entity, out string name)
        {
            name = string.Empty;
            if (!entity.Exists()) return false;

            Entity userEntity = entity;

            if (!EntityManager.HasComponent<User>(userEntity))
                userEntity = entity.GetUserEntity();

            if (userEntity == Entity.Null || !EntityManager.HasComponent<User>(userEntity))
                return false;

            var user = EntityManager.GetComponentData<User>(userEntity);
            try
            {
                name = user.CharacterName.ToString();
                return true;
            }
            catch
            {
                // Some builds use FixedString variants; ToString() is still safe, but guard anyway
                name = string.Empty;
                return false;
            }
        }

        /// <summary>Convenience: get player name or "Unknown".</summary>
        public static string GetPlayerName(this Entity entity)
            => entity.TryGetPlayerName(out var n) ? n : "Unknown";
        
    }
}