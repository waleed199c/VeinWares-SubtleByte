using UnityEngine;
using VeinWares.SubtleByte.Template.Infrastructure;

namespace VeinWares.SubtleByte.Template.Runtime.Unity;

public sealed class ServerBehaviour : MonoBehaviour
{
    private ModuleHost? _host;

    internal void Bind(ModuleHost host)
    {
        _host = host;
    }

    private void Update()
    {
        if (_host is null)
        {
            return;
        }

        _host.Tick(Time.deltaTime);
    }
}
