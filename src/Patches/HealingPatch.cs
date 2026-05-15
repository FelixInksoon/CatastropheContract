using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CatastropheContract.Content;
using CatastropheContract.Core;
using CatastropheContract.Core.State;
using HarmonyLib;

namespace CatastropheContract.Patches;

[HarmonyPatch]
public static class HealingPatch
{
    private sealed record HealSnapshot(object Target, double HealthBefore);

    static IEnumerable<MethodBase> TargetMethods()
    {
        Assembly? sts2 = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == "sts2");
        if (sts2 == null)
        {
            yield break;
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (Type type in GetTypesSafe(sts2))
        {
            MethodInfo[] methods;
            try
            {
                methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            }
            catch
            {
                continue;
            }

            foreach (MethodInfo method in methods.Where(method => method.Name == "Heal"))
            {
                string key = $"{type.FullName}::{method}";
                if (!seen.Add(key))
                {
                    continue;
                }

                ModLogger.Info($"HealingPatch targeting {type.FullName}.{method.Name}");
                yield return method;
            }
        }
    }

    static void Prefix(MethodBase __originalMethod, object? __instance, object[] __args, out HealSnapshot? __state)
    {
        __state = null;
        if (!ContractStateStore.CurrentRun.Enabled || !ContractStateStore.CurrentRun.Effects.NoHealing)
        {
            return;
        }

        object? target = TryFindPlayerLikeTarget(__instance, __args);
        if (target == null)
        {
            return;
        }

        double? hp = ContractRuntimeReflection.TryGetNumber(target, "Health", "Hp", "_health", "_hp");
        if (!hp.HasValue)
        {
            return;
        }

        __state = new HealSnapshot(target, hp.Value);
        ModLogger.Info($"HealingPatch observed {DescribeMethod(__originalMethod)} on {target.GetType().FullName} with health {hp.Value:0.##}.");
    }

    static void Postfix(MethodBase __originalMethod, HealSnapshot? __state)
    {
        if (__state == null)
        {
            return;
        }

        double? hpAfter = ContractRuntimeReflection.TryGetNumber(__state.Target, "Health", "Hp", "_health", "_hp");
        if (!hpAfter.HasValue || hpAfter.Value <= __state.HealthBefore + 0.001d)
        {
            return;
        }

        bool reverted = ContractRuntimeReflection.TrySetNumber(__state.Target, __state.HealthBefore, "Health", "Hp", "_health", "_hp");
        if (reverted)
        {
            ModLogger.Info(
                $"HealingPatch blocked healing from {DescribeMethod(__originalMethod)}. Restored HP from {hpAfter.Value:0.##} to {__state.HealthBefore:0.##}.");
        }
        else
        {
            ModLogger.Warn(
                $"HealingPatch detected healing via {DescribeMethod(__originalMethod)} but could not restore HP on {__state.Target.GetType().FullName}.");
        }
    }

    private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null)!;
        }
    }

    private static object? TryFindPlayerLikeTarget(object? instance, IEnumerable<object?> args)
    {
        if (instance != null && IsPlayerLike(instance))
        {
            return instance;
        }

        foreach (object? arg in args)
        {
            if (arg != null && IsPlayerLike(arg))
            {
                return arg;
            }
        }

        return null;
    }

    private static bool IsPlayerLike(object candidate)
    {
        string typeName = candidate.GetType().FullName ?? string.Empty;
        if (typeName.Contains("Player", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        object? owner = ContractRuntimeReflection.TryGetByNames(candidate, "Player", "Owner", "Source", "Controller");
        return owner != null && owner != candidate && IsPlayerLike(owner);
    }

    private static string DescribeMethod(MethodBase method)
    {
        return $"{method.DeclaringType?.FullName ?? "<unknown>"}.{method.Name}";
    }
}
