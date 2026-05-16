using System;
using System.Collections.Generic;
using System.Linq;
using CatastropheContract.Core;
using CatastropheContract.Core.State;
using HarmonyLib;

namespace CatastropheContract.Patches;

[HarmonyPatch]
public static class CharacterSelectPatch
{
    private static readonly string[] TypeNameCandidates =
    {
        "MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.CustomRun.NCustomRunScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.CustomRun.NCustomRunLoadScreen"
    };

    private static readonly string[] TypeNameFragments =
    {
        "CharacterSelect",
        "CustomRun"
    };

    private static readonly string[] MethodNameCandidates =
    {
        "OnEmbarkPressed",
        "ReadyButtonPressed",
        "OnReadyButtonPressed",
        "StartNewSingleplayerRun"
    };

    static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        HashSet<System.Reflection.MethodBase> yielded = new();

        foreach (string typeName in TypeNameCandidates)
        {
            Type? explicitType = AccessTools.TypeByName(typeName);
            if (explicitType == null)
            {
                continue;
            }

            foreach (System.Reflection.MethodBase method in GetCandidateMethods(explicitType))
            {
                if (yielded.Add(method))
                {
                    yield return method;
                }
            }
        }

        foreach (Type type in AccessTools.AllTypes())
        {
            string fullName = type.FullName ?? string.Empty;
            if (!TypeNameFragments.Any(fragment => fullName.Contains(fragment, StringComparison.Ordinal)))
            {
                continue;
            }

            foreach (System.Reflection.MethodBase method in GetCandidateMethods(type))
            {
                if (yielded.Add(method))
                {
                    yield return method;
                }
            }
        }
    }

    static bool Prefix(System.Reflection.MethodBase __originalMethod, object __instance)
    {
        ModLogger.Debug($"Embark validation hook hit: {__originalMethod.DeclaringType?.FullName}.{__originalMethod.Name}");

        string characterId = ReflectionBridge.TryReadCharacterId(__instance) ?? "*";
        ContractStateStore.SetSelection(
            ContractStateStore.Persistent.LastEnabled,
            ContractStateStore.Persistent.LastSelectedContracts,
            characterId);
        ModLogger.Info($"Prepared contract run state for character {characterId} at risk {ContractStateStore.CurrentRun.RiskLevel}.");

        if (!ContractStateStore.CurrentRun.Enabled || ContractStateStore.CurrentRun.SelectedContracts.Count == 0)
        {
            return true;
        }

        if (!ReflectionBridge.TryBuildEmbarkConflictMessage(__instance, out string message))
        {
            return true;
        }

        ModLogger.Warn($"Blocking {__originalMethod.Name} because embark validation found conflicts.");
        ReflectionBridge.ShowEmbarkBlockedPopup(__instance, message);
        return false;
    }

    private static IEnumerable<System.Reflection.MethodBase> GetCandidateMethods(Type type)
    {
        foreach (string methodName in MethodNameCandidates)
        {
            foreach (System.Reflection.MethodBase method in AccessTools.GetDeclaredMethods(type))
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (method is not System.Reflection.MethodInfo methodInfo)
                {
                    continue;
                }

                if (methodInfo.ReturnType != typeof(void))
                {
                    continue;
                }

                yield return methodInfo;
            }
        }
    }
}
