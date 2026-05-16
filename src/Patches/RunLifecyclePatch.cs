using System;
using System.Collections.Generic;
using CatastropheContract.Content;
using CatastropheContract.Core;
using CatastropheContract.Core.State;
using HarmonyLib;

namespace CatastropheContract.Patches;

[HarmonyPatch]
public static class RunLifecyclePatch
{
    static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        Type? runManager = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
        if (runManager != null)
        {
            foreach (string methodName in new[] { "StartRun", "GenerateRooms", "WinRun", "AbandonRunAsync", "GoToTimelineAfterRun" })
            {
                System.Reflection.MethodInfo? method = AccessTools.Method(runManager, methodName);
                if (method != null)
                {
                    ModLogger.Info($"RunLifecyclePatch targeting RunManager.{methodName}");
                    yield return method;
                }
            }
        }

        Type? combatManager = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatManager");
        if (combatManager != null)
        {
            foreach (string methodName in new[] { "BeforeCombatStart", "StartCombatInternal", "SetupPlayerTurn", "AfterCombatVictory", "EndCombatInternal" })
            {
                System.Reflection.MethodInfo? method = AccessTools.Method(combatManager, methodName);
                if (method != null)
                {
                    ModLogger.Info($"RunLifecyclePatch targeting CombatManager.{methodName}");
                    yield return method;
                }
            }
        }
    }

    static void Prefix(System.Reflection.MethodBase __originalMethod, object __instance)
    {
        string methodName = __originalMethod.Name;
        switch (methodName)
        {
            case "StartRun":
                ModLogger.Info(
                    $"Run start observed. Enabled={ContractStateStore.CurrentRun.Enabled}, Risk={ContractStateStore.CurrentRun.RiskLevel}, Contracts=[{string.Join(", ", ContractStateStore.CurrentRun.SelectedContracts)}]");
                ContractMutatorRegistry.ApplyRunStartMutators(__instance);
                break;
            case "BeforeCombatStart":
            case "StartCombatInternal":
                ModLogger.Info($"Combat start hook observed via {methodName}. Build={ModLogger.BuildMarker}. Enabled={ContractStateStore.CurrentRun.Enabled}, Contracts=[{string.Join(", ", ContractStateStore.CurrentRun.SelectedContracts)}]");
                ContractStateStore.OnCombatStarted();
                break;
            case "SetupPlayerTurn":
                ModLogger.Info($"SetupPlayerTurn prefix observed. Build={ModLogger.BuildMarker}. PreCombatApplied={ContractStateStore.CurrentRun.PreCombatAppliedThisFight}.");
                break;
            case "AfterCombatVictory":
            case "EndCombatInternal":
                ContractMutatorRegistry.ApplyRewardMutators(__instance);
                break;
        }
    }

    static void Postfix(System.Reflection.MethodBase __originalMethod, object __instance)
    {
        string methodName = __originalMethod.Name;
        switch (methodName)
        {
            case "GenerateRooms":
                ModLogger.Info("RunManager.GenerateRooms observed for future map mutation support.");
                break;
            case "SetupPlayerTurn":
                if (ContractStateStore.MarkPreCombatApplied())
                {
                    ModLogger.Info($"Applying pre-combat mutators at SetupPlayerTurn postfix. Build={ModLogger.BuildMarker}");
                    ContractMutatorRegistry.ApplyPreCombatMutators(__instance);
                }
                else
                {
                    ModLogger.Info($"Skipping pre-combat mutators at SetupPlayerTurn postfix because they were already applied. Build={ModLogger.BuildMarker}");
                }

                ContractMutatorRegistry.ApplyTurnRuleMutators(__instance);
                break;
            case "WinRun":
                ContractStateStore.CommitRunResult(true);
                ContractStateStore.ResetRun();
                break;
            case "AbandonRunAsync":
            case "GoToTimelineAfterRun":
                ContractStateStore.CommitRunResult(false);
                ContractStateStore.ResetRun();
                break;
        }
    }
}
