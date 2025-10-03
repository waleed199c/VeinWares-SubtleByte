using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Scripting;
using System;
using Unity.Entities;

namespace VeinWares.SubtleByte.Services
{
    internal class SystemService(World world)
    {
        readonly World _world = world ?? throw new ArgumentNullException(nameof(world));

        DebugEventsSystem _debugEventsSystem;
        public DebugEventsSystem DebugEventsSystem => _debugEventsSystem ??= GetSystem<DebugEventsSystem>();

        ServerScriptMapper _serverScriptMapper;
        public ServerScriptMapper ServerScriptMapper => _serverScriptMapper ??= GetSystem<ServerScriptMapper>();

        PrefabCollectionSystem _prefabCollectionSystem;
        public PrefabCollectionSystem PrefabCollectionSystem => _prefabCollectionSystem ??= GetSystem<PrefabCollectionSystem>();

        T GetSystem<T>() where T : ComponentSystemBase =>
            _world.GetExistingSystemManaged<T>() ??
            throw new InvalidOperationException($"[{_world.Name}] - failed to get ({Il2CppType.Of<T>().FullName})");
    }

}
