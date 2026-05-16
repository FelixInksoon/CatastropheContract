using System;
using System.Collections.Generic;
using CatastropheContract.Core;
using CatastropheContract.Core.State;
using HarmonyLib;

namespace CatastropheContract.Patches;

[HarmonyPatch]
public static class CharacterSelectPatch
{
    static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        Type? type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen");
        if (type == null)
        {
            yield break;
        }

        foreach (string methodName in new[] { "OnEmbarkPressed", "StartNewSingleplayerRun" })
        {
            System.Reflection.MethodInfo? method = AccessTools.Method(type, methodName);
            if (method != null)
            {
                yield return method;
            }
        }
    }

    static void Prefix(object __instance)
    {
        string characterId = ReflectionBridge.TryReadCharacterId(__instance) ?? "*";
        ContractStateStore.SetSelection(
            ContractStateStore.Persistent.LastEnabled,
            ContractStateStore.Persistent.LastSelectedContracts,
            characterId);
        ModLogger.Info($"Prepared contract run state for character {characterId} at risk {ContractStateStore.CurrentRun.RiskLevel}.");
    }
}
