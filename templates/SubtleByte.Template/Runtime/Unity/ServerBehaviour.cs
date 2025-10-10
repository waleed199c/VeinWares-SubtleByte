using UnityEngine;
using VeinWares.SubtleByte.Template.Infrastructure;

namespace VeinWares.SubtleByte.Template.Runtime.Unity;

public sealed class ServerBehaviour : MonoBehaviour
{
    internal static ModuleHost? Host { get; set; }

    private void Update()
    {
        var host = Host;
        if (host is null)
        {
            return;
        }

        host.Tick(Time.deltaTime);
    }
}
