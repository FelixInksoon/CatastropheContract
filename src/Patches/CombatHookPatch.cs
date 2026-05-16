using System;
using System.Collections.Generic;
using CatastropheContract.Content;
using CatastropheContract.Core;
using HarmonyLib;

namespace CatastropheContract.Patches;

[HarmonyPatch]
public static class CombatHookPatch
{
    private static readonly string[] HookMethodNames =
    {
        "AfterDamageGiven",
        "AfterDamageReceived",
        "AfterModifyingHpLostAfterOsty",
        "AfterSideTurnStart",
        "AfterCurrentHpChanged",
        "AfterPlayerTurnStart"
    };

    static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        Type? hookType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
        if (hookType == null)
        {
            yield break;
        }

        foreach (string methodName in HookMethodNames)
        {
            System.Reflection.MethodInfo? method = AccessTools.Method(hookType, methodName);
            if (method == null)
            {
                continue;
            }

            ModLogger.Info($"CombatHookPatch targeting Hook.{methodName}");
            yield return method;
        }
    }

    static void Prefix(System.Reflection.MethodBase __originalMethod, object[] __args)
    {
        switch (__originalMethod.Name)
        {
            case "AfterDamageGiven":
            case "AfterDamageReceived":
            case "AfterModifyingHpLostAfterOsty":
            case "AfterCurrentHpChanged":
                ContractMutatorRegistry.HandleAfterDamageHook(__originalMethod.Name, __args);
                break;
            case "AfterSideTurnStart":
                ContractMutatorRegistry.HandleAfterSideTurnStart(__originalMethod.Name, __args);
                break;
            case "AfterPlayerTurnStart":
                ContractMutatorRegistry.HandleFutureSpecialRuleHook(__originalMethod.Name, __args);
                break;
        }
    }
}
