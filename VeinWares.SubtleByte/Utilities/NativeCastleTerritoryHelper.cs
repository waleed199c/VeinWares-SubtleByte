using System;
using System.Reflection;
using ProjectM.CastleBuilding;
using VeinWares.SubtleByte;
using Unity.Mathematics;

namespace VeinWares.SubtleByte.Utilities;

internal static class NativeCastleTerritoryHelper
{
    private delegate bool Contains3Delegate(CastleTerritory territory, float3 position);
    private delegate bool Contains2Delegate(CastleTerritory territory, float2 position);
    private delegate bool Contains3HeightDelegate(CastleTerritory territory, float3 position, float height);

    private static Contains3Delegate? Contains3;
    private static Contains2Delegate? Contains2;
    private static Contains3HeightDelegate? Contains3Height;
    private static bool ResolverAvailable;
    private static bool _loggedInvocationFailure;

    static NativeCastleTerritoryHelper()
    {
        try
        {
            var assembly = typeof(CastleTerritory).Assembly;
            var extensionsType = assembly.GetType("ProjectM.CastleBuilding.CastleTerritoryExtensions");
            if (extensionsType is null)
            {
                return;
            }

            foreach (var method in extensionsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (method.ReturnType != typeof(bool))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(CastleTerritory))
                {
                    if (parameters[1].ParameterType == typeof(float3) && Contains3 is null)
                    {
                        TryBind(method, ref Contains3);
                    }
                    else if (parameters[1].ParameterType == typeof(float2) && Contains2 is null)
                    {
                        TryBind(method, ref Contains2);
                    }
                }
                else if (parameters.Length == 3
                    && parameters[0].ParameterType == typeof(CastleTerritory)
                    && parameters[1].ParameterType == typeof(float3)
                    && parameters[2].ParameterType == typeof(float)
                    && Contains3Height is null)
                {
                    TryBind(method, ref Contains3Height);
                }
            }

            ResolverAvailable = Contains3 is not null || Contains3Height is not null || Contains2 is not null;
        }
        catch (Exception exception)
        {
            Core.Log?.LogWarning($"[Territory] Failed to bind native helpers: {exception}");
        }
    }

    public static bool TryContains(in CastleTerritory territory, in float3 position, out bool contains)
    {
        contains = false;

        if (!ResolverAvailable)
        {
            return false;
        }

        try
        {
            if (Contains3 is not null)
            {
                contains = Contains3(territory, position);
                return true;
            }

            if (Contains3Height is not null)
            {
                contains = Contains3Height(territory, position, position.y);
                return true;
            }

            if (Contains2 is not null)
            {
                contains = Contains2(territory, new float2(position.x, position.z));
                return true;
            }
        }
        catch (Exception exception)
        {
            if (!_loggedInvocationFailure)
            {
                _loggedInvocationFailure = true;
                Core.Log?.LogWarning($"[Territory] Native helper invocation failed: {exception}");
            }
        }

        return false;
    }

    private static void TryBind<T>(MethodInfo method, ref T? target)
        where T : class
    {
        if (target is not null)
        {
            return;
        }

        try
        {
            target = (T)method.CreateDelegate(typeof(T));
        }
        catch (Exception exception)
        {
            Core.Log?.LogDebug($"[Territory] Unable to bind native helper '{method.Name}': {exception.Message}");
        }
    }
}
