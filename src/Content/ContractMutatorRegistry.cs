using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CatastropheContract.Core;
using CatastropheContract.Core.Contracts;
using CatastropheContract.Core.State;

namespace CatastropheContract.Content;

public static class ContractMutatorRegistry
{
    private static readonly string[] MaxHpNames = { "MaxHealth", "MaxHp", "_maxHealth", "_maxHp" };
    private static readonly string[] CurrentHpNames = { "CurrentHp", "Health", "Hp", "_health", "_hp" };
    private static readonly string[] BlockNames = { "Block", "_block", "CurrentBlock" };

    public static void ApplyRunStartMutators(object runContext, params object?[] extraPlayerCandidates)
    {
        if (!ContractStateStore.CurrentRun.Enabled)
        {
            return;
        }

        List<ContractDefinition> pendingContracts = GetActiveContracts(ContractApplyPhase.RunStart)
            .Where(contract => !ContractStateStore.CurrentRun.AppliedRunStartContracts.Contains(contract.Id))
            .ToList();
        if (pendingContracts.Count == 0)
        {
            return;
        }

        object? player = ResolvePlayer(runContext, extraPlayerCandidates);
        ModLogger.Info($"ApplyRunStartMutators resolved player={player?.GetType().FullName ?? "<null>"}.");

        bool deferredAny = false;
        foreach (ContractDefinition contract in pendingContracts)
        {
            bool completed = ApplyContract(contract, player, runContext, allowDeferred: true);
            if (completed)
            {
                ContractStateStore.CurrentRun.AppliedRunStartContracts.Add(contract.Id);
            }
            else
            {
                deferredAny = true;
            }
        }

        if (deferredAny)
        {
            ModLogger.Info("ApplyRunStartMutators deferred because one or more run-start contracts could not be applied yet.");
        }
        else
        {
            ModLogger.Info("ApplyRunStartMutators completed.");
        }
    }

    public static void ApplyPreCombatMutators(object combatContext, params object?[] extraPlayerCandidates)
    {
        if (!ContractStateStore.CurrentRun.Enabled)
        {
            ModLogger.Info("ApplyPreCombatMutators skipped because CurrentRun.Enabled is false.");
            return;
        }

        ModLogger.Debug("ApplyPreCombatMutators resolving player.");
        object? player = ResolvePlayer(combatContext, extraPlayerCandidates);
        ModLogger.Debug($"ApplyPreCombatMutators resolved player={player?.GetType().FullName ?? "<null>"}.");

        ModLogger.Debug("ApplyPreCombatMutators resolving enemies.");
        List<object> enemies = ContractRuntimeReflection.TryGetEnemies(combatContext).ToList();
        ModLogger.Debug($"ApplyPreCombatMutators resolved enemies={enemies.Count}.");
        ModLogger.Info($"PreCombat start. player={player?.GetType().FullName ?? "<null>"}, enemies={enemies.Count}, contracts={string.Join(", ", ContractStateStore.CurrentRun.SelectedContracts)}");

        foreach (ContractDefinition contract in GetActiveContracts(ContractApplyPhase.PreCombat))
        {
            ApplyContract(contract, player, combatContext);
        }
    }

    public static void ApplyTurnRuleMutators(object combatContext, params object?[] extraPlayerCandidates)
    {
        if (!ContractStateStore.CurrentRun.Enabled)
        {
            return;
        }

        ContractStateStore.OnTurnStarted();
        object? player = ResolvePlayer(combatContext, extraPlayerCandidates);
        foreach (ContractDefinition contract in GetActiveContracts(ContractApplyPhase.TurnRule))
        {
            ApplyContract(contract, player, combatContext);
        }
    }

    public static void ApplyRecurringCombatMutators(object combatContext)
    {
        if (!ContractStateStore.CurrentRun.Enabled)
        {
            return;
        }

        ApplyDebrisCoveredTurnBlock(combatContext, "SetupPlayerTurn");
    }

    public static void ApplyRewardMutators(object rewardContext)
    {
        if (!ContractStateStore.CurrentRun.Enabled)
        {
            return;
        }

        foreach (ContractDefinition contract in GetActiveContracts(ContractApplyPhase.RewardEconomy))
        {
            ApplyContract(contract, rewardContext, rewardContext);
        }
    }

    public static void ApplyBossMutators(object bossContext)
    {
        if (!ContractStateStore.CurrentRun.Enabled)
        {
            return;
        }

        foreach (ContractDefinition contract in GetActiveContracts(ContractApplyPhase.BossOnly))
        {
            LogUnsupported(contract, "BossOnly hooks are not implemented yet.");
        }
    }

    public static void HandleAfterDamageHook(string hookName, object[] hookArgs)
    {
        if (!ContractStateStore.CurrentRun.Enabled)
        {
            return;
        }

        bool hasBloodthirsty = ContractCombatRuntimeState.HasBloodthirstyStatuses;
        bool hasDebrisCovered = ContractCombatRuntimeState.HasTrackedDebrisCovered;
        if (!hasBloodthirsty && !hasDebrisCovered)
        {
            return;
        }

        if (!TryExtractDamageEvent(hookName, hookArgs, out object? sourceCreature, out object? targetCreature, out double hpLost))
        {
            if (hasBloodthirsty)
            {
                ModLogger.Debug($"Bloodthirsty damage hook {hookName} observed, but damage event parsing failed. Args=[{DescribeHookArgs(hookArgs)}]");
            }
            return;
        }

        if (!ContractCombatRuntimeState.MarkDamageEventProcessed(hookName, sourceCreature, targetCreature, hpLost))
        {
            return;
        }

        object? player = ResolvePlayer(null, hookArgs);
        object? playerCreature = ContractRuntimeReflection.TryGetCreature(player);
        bool targetIsPlayer = IsSameCreature(targetCreature, playerCreature);
        bool sourceIsPlayer = IsSameCreature(sourceCreature, playerCreature);
        if (hasBloodthirsty)
        {
            ModLogger.Debug(
                $"Bloodthirsty hook {hookName}: source={DescribeCreature(sourceCreature)}, target={DescribeCreature(targetCreature)}, hpLost={hpLost:0.##}, targetIsPlayer={targetIsPlayer}, sourceIsPlayer={sourceIsPlayer}.");
        }

        object? bloodthirstySource = hasBloodthirsty
            ? ResolveBloodthirstySourceCreature(sourceCreature, playerCreature, hookArgs)
            : null;

        if (hasBloodthirsty && targetIsPlayer && bloodthirstySource != null && !IsSameCreature(bloodthirstySource, playerCreature))
        {
            double percent = ContractCombatRuntimeState.GetCustomStatusValue(bloodthirstySource, ContractCombatRuntimeState.BloodthirstyStatusId);
            if (percent > 0.001d)
            {
                TryApplyEnemyLifesteal(bloodthirstySource, hpLost, percent);
            }
        }

        if (hasDebrisCovered && targetCreature != null && !targetIsPlayer)
        {
            TryHandleDebrisCoveredDamage(targetCreature, hpLost);
        }
    }

    public static void HandleAfterSideTurnStart(string hookName, object[] hookArgs)
    {
        if (!ContractStateStore.CurrentRun.Enabled || !ContractCombatRuntimeState.HasTrackedDebrisCovered)
        {
            return;
        }

        object? player = ResolvePlayer(null, hookArgs);
        object? playerCreature = ContractRuntimeReflection.TryGetCreature(player);
        object? sideCreature = TryResolveCreatureFromHookArgs(hookArgs, "Creature", "Owner", "Character", "Source", "Target");
        if (sideCreature == null)
        {
            sideCreature = hookArgs
                .Select(ContractRuntimeReflection.TryGetCreature)
                .FirstOrDefault(candidate => candidate != null && !IsSameCreature(candidate, playerCreature));
        }

        if (sideCreature == null || IsSameCreature(sideCreature, playerCreature))
        {
            return;
        }

        int stacks = ContractCombatRuntimeState.GetDebrisCoveredStacks(sideCreature);
        if (stacks <= 0)
        {
            return;
        }

        ContractDefinition? contract = FindActiveContractByGroup("debris_covered");
        if (contract == null)
        {
            return;
        }

        bool applied = ApplyDelta(contract, sideCreature, stacks, BlockNames);
        if (applied)
        {
            ModLogger.Info($"Custom debris-covered plating triggered via {hookName}: granted {stacks} block to {sideCreature.GetType().FullName}.");
        }
    }

    public static void HandleFutureSpecialRuleHook(string hookName, object[] hookArgs)
    {
        if (!ContractStateStore.CurrentRun.Enabled)
        {
            return;
        }

        if (!ContractStateStore.CurrentRun.Effects.MaxHpLockedToOne
            && ContractStateStore.CurrentRun.Effects.DeathCountdownTurn <= 0)
        {
            return;
        }

        ModLogger.Debug($"Observed future special-rule hook {hookName} with {hookArgs.Length} args.");
    }

    public static IEnumerable<ContractDefinition> GetActiveContracts(ContractApplyPhase phase)
    {
        foreach (string contractId in ContractStateStore.CurrentRun.SelectedContracts)
        {
            ContractDefinition? definition = ContractDatabase.TryGet(contractId);
            if (definition != null && definition.ApplyPhase == phase)
            {
                yield return definition;
            }
        }
    }

    private static bool ApplyContract(ContractDefinition contract, object? primaryTarget, object? context, bool allowDeferred = false)
    {
        if (!contract.IsImplemented)
        {
            LogUnsupported(contract, "Marked as not implemented.");
            return true;
        }

        bool completed = true;
        foreach (ContractEffect effect in contract.Effects)
        {
            switch (effect.Kind)
            {
                case ContractEffectKind.PlayerMaxHpLossPercent:
                    completed &= ApplyPlayerMaxHpPercentLoss(contract, primaryTarget, effect.Value, allowDeferred);
                    break;
                case ContractEffectKind.PotionSlotCap:
                    completed &= ApplySetNumber(contract, primaryTarget, effect.Value, allowDeferred, "PotionSlots", "PotionSlotCount", "MaxPotionSlots", "_potionSlots");
                    break;
                case ContractEffectKind.MaxHpLockedToOne:
                    completed &= ApplySetNumber(contract, primaryTarget, 1, allowDeferred, MaxHpNames);
                    completed &= ApplySetNumber(contract, primaryTarget, 1, allowDeferred, CurrentHpNames);
                    break;
                case ContractEffectKind.PlayerStartWithWeak:
                    completed &= ApplyPowerOrFallback(
                        contract,
                        primaryTarget,
                        primaryTarget,
                        effect.Value,
                        new[] { "MegaCrit.Sts2.Core.Models.Powers.WeakPower" },
                        "Weak",
                        "_weak");
                    break;
                case ContractEffectKind.PlayerStartWithFrail:
                    completed &= ApplyPowerOrFallback(
                        contract,
                        primaryTarget,
                        primaryTarget,
                        effect.Value,
                        new[] { "MegaCrit.Sts2.Core.Models.Powers.FrailPower" },
                        "Frail",
                        "_frail");
                    break;
                case ContractEffectKind.PlayerStartWithDexterityPenalty:
                    completed &= ApplyPowerOrFallback(
                        contract,
                        primaryTarget,
                        primaryTarget,
                        -effect.Value,
                        new[] { "MegaCrit.Sts2.Core.Models.Powers.DexterityPower" },
                        "Dexterity",
                        "_dexterity");
                    break;
                case ContractEffectKind.PlayerStartWithStrengthPenalty:
                    completed &= ApplyPowerOrFallback(
                        contract,
                        primaryTarget,
                        primaryTarget,
                        -effect.Value,
                        new[] { "MegaCrit.Sts2.Core.Models.Powers.StrengthPower" },
                        "Strength",
                        "_strength");
                    break;
                case ContractEffectKind.EnemyUnblockedDamageLifestealPercent:
                    ApplyToEnemies(contract, context, enemy => ApplyEnemyBloodthirstyStatus(contract, enemy, effect.Value));
                    break;
                case ContractEffectKind.EnemyStartWithThorns:
                    ApplyToEnemies(
                        contract,
                        context,
                        enemy => ApplyPowerOrFallback(
                            contract,
                            enemy,
                            enemy,
                            effect.Value,
                            new[] { "MegaCrit.Sts2.Core.Models.Powers.ThornsPower" },
                            "Thorns",
                            "_thorns"));
                    break;
                case ContractEffectKind.EnemyStartWithStrength:
                    ApplyToEnemies(
                        contract,
                        context,
                        enemy => ApplyPowerOrFallback(
                            contract,
                            enemy,
                            enemy,
                            effect.Value,
                            new[] { "MegaCrit.Sts2.Core.Models.Powers.StrengthPower" },
                            "Strength",
                            "_strength"));
                    break;
                case ContractEffectKind.EnemyMaxHpPercent:
                    ApplyToEnemies(contract, context, enemy => ApplyEnemyMaxHpBoost(contract, enemy, effect.Value));
                    break;
                case ContractEffectKind.EnemyStartWithBlock:
                    ApplyToEnemies(contract, context, enemy => ApplyDelta(contract, enemy, effect.Value, BlockNames));
                    break;
                case ContractEffectKind.EnemyStartWithArtifact:
                    ApplyToEnemies(
                        contract,
                        context,
                        enemy => ApplyPowerOrFallback(
                            contract,
                            enemy,
                            enemy,
                            effect.Value,
                            new[] { "MegaCrit.Sts2.Core.Models.Powers.ArtifactPower" },
                            "Artifact",
                            "_artifact"));
                    break;
                case ContractEffectKind.EnemyStartWithPlatedArmor:
                    ApplyToEnemies(contract, context, enemy => ApplyEnemyStartingPlating(contract, enemy, effect.Value));
                    break;
                case ContractEffectKind.GoldGainPercentPenalty:
                    ApplyGoldPenalty(contract, context, effect.Value);
                    break;
                case ContractEffectKind.NoHealing:
                    ModLogger.Info($"{contract.Id} enabled: player healing will be blocked by HealingPatch.");
                    break;
                case ContractEffectKind.DeathCountdownTurn:
                    ApplyDeathCountdown(contract, primaryTarget, (int)effect.Value);
                    break;
                case ContractEffectKind.HideEnemyIntent:
                case ContractEffectKind.LinearMap:
                case ContractEffectKind.EliteEncounterRatio:
                case ContractEffectKind.BossesRequiredPerAct:
                case ContractEffectKind.XCostCardReduction:
                case ContractEffectKind.AllGainedCardsAreEthereal:
                case ContractEffectKind.DeckSizeCap:
                case ContractEffectKind.LimitedTurnOneCardOnlyFrequency:
                    LogUnsupported(contract, $"Effect {effect.Kind} is staged for a later gameplay hook.");
                    break;
            }
        }

        return completed;
    }

    private static void ApplyToEnemies(ContractDefinition contract, object? context, System.Action<object> apply)
    {
        if (context == null)
        {
            LogMissingTarget(contract, "combat context");
            return;
        }

        List<object> enemies = ContractRuntimeReflection.TryGetEnemies(context).ToList();
        if (enemies.Count == 0)
        {
            LogMissingTarget(contract, "enemy list");
            return;
        }

        ModLogger.Info($"Applying {contract.Id} to {enemies.Count} enemies.");

        foreach (object enemy in enemies)
        {
            apply(enemy);
        }
    }

    private static bool ApplyPlayerMaxHpPercentLoss(ContractDefinition contract, object? player, double percentLoss, bool allowDeferred = false)
    {
        if (player == null)
        {
            if (allowDeferred)
            {
                ModLogger.Info($"Deferring {contract.Id}: player target was not available yet.");
                return false;
            }

            LogMissingTarget(contract, "player");
            return false;
        }

        double? currentMax = ContractRuntimeReflection.TryGetNumber(player, MaxHpNames);
        if (!currentMax.HasValue)
        {
            if (allowDeferred)
            {
                ModLogger.Info($"Deferring {contract.Id}: player target exists but HP members were not readable yet.");
                return false;
            }

            LogMemberMiss(contract, "MaxHealth/MaxHp");
            return false;
        }

        double reduced = currentMax.Value * (1.0 - (percentLoss / 100.0));
        bool maxApplied = ContractRuntimeReflection.TrySetNumber(player, reduced, MaxHpNames);
        bool hpApplied = ContractRuntimeReflection.TrySetNumber(player, reduced, CurrentHpNames);

        if (maxApplied && hpApplied)
        {
            ModLogger.Info($"Applied {contract.Id}: reduced player max HP by {percentLoss}% to {reduced:0.##}, current HP synced to {reduced:0.##}.");
            return true;
        }

        if (allowDeferred)
        {
            ModLogger.Info($"Deferring {contract.Id}: player target exists but no writable HP fields were found yet.");
            return false;
        }

        LogMemberMiss(contract, "Health/MaxHealth");
        return false;
    }

    private static bool ApplyEnemyMaxHpBoost(ContractDefinition contract, object enemy, double percentBoost)
    {
        double? currentMax = ContractRuntimeReflection.TryGetNumber(enemy, MaxHpNames);
        if (!currentMax.HasValue)
        {
            LogMemberMiss(contract, "Enemy MaxHealth/MaxHp");
            return false;
        }

        double boosted = currentMax.Value * (1.0 + (percentBoost / 100.0));
        bool maxApplied = ContractRuntimeReflection.TrySetNumber(enemy, boosted, MaxHpNames);
        bool hpApplied = ContractRuntimeReflection.TrySetNumber(enemy, boosted, CurrentHpNames);
        if (maxApplied && hpApplied)
        {
            ModLogger.Info($"Applied {contract.Id}: enemy max HP boosted by {percentBoost}% to {boosted:0.##}.");
            return true;
        }

        LogMemberMiss(contract, "Enemy Health/MaxHealth");
        return false;
    }

    private static bool ApplyDelta(ContractDefinition contract, object? target, double delta, params string[] names)
    {
        if (target == null)
        {
            LogMissingTarget(contract, string.Join("/", names));
            return false;
        }

        double current = ContractRuntimeReflection.TryGetNumber(target, names) ?? 0;
        bool applied = ContractRuntimeReflection.TrySetNumber(target, current + delta, names);
        if (applied)
        {
            ModLogger.Info($"Applied {contract.Id}: {string.Join("/", names)} {(delta >= 0 ? "+" : string.Empty)}{delta:0.##}.");
            return true;
        }

        LogMemberMiss(contract, string.Join("/", names));
        return false;
    }

    private static bool ApplySetNumber(ContractDefinition contract, object? target, double value, bool allowDeferred = false, params string[] names)
    {
        if (target == null)
        {
            if (allowDeferred)
            {
                ModLogger.Info($"Deferring {contract.Id}: target '{string.Join("/", names)}' was not available yet.");
                return false;
            }

            LogMissingTarget(contract, string.Join("/", names));
            return false;
        }

        bool applied = ContractRuntimeReflection.TrySetNumber(target, value, names);
        if (applied)
        {
            ModLogger.Info($"Applied {contract.Id}: set {string.Join("/", names)} to {value:0.##}.");
            return true;
        }

        if (allowDeferred)
        {
            ModLogger.Info($"Deferring {contract.Id}: member '{string.Join("/", names)}' was not writable yet.");
            return false;
        }

        LogMemberMiss(contract, string.Join("/", names));
        return false;
    }

    private static bool ApplyPowerOrFallback(
        ContractDefinition contract,
        object? target,
        object? source,
        double amount,
        string[] powerTypeNames,
        params string[] fallbackNames)
    {
        if (target == null)
        {
            LogMissingTarget(contract, string.Join("/", fallbackNames));
            return false;
        }

        ModLogger.Info(
            $"ApplyPowerOrFallback start for {contract.Id}. powerTypes=[{string.Join(", ", powerTypeNames)}], fallback=[{string.Join(", ", fallbackNames)}], amount={amount:0.##}, targetType={target.GetType().FullName}");

        bool appliedViaPower = ContractRuntimeReflection.TryApplyPower(target, source, amount, powerTypeNames);
        ModLogger.Info($"ApplyPowerOrFallback result for {contract.Id}: TryApplyPower={appliedViaPower}.");

        if (appliedViaPower)
        {
            ModLogger.Info($"Applied {contract.Id} via PowerCmd ({string.Join(", ", powerTypeNames)}) amount={amount:0.##}.");
            return true;
        }

        return ApplyDelta(contract, target, amount, fallbackNames);
    }

    private static void ApplyDeathCountdown(ContractDefinition contract, object? player, int countdownTurn)
    {
        if (ContractStateStore.CurrentRun.CurrentCombatTurn < countdownTurn)
        {
            return;
        }

        ApplySetNumber(contract, player, 0, false, CurrentHpNames);
        ModLogger.Warn($"Countdown triggered for {contract.Id} on turn {ContractStateStore.CurrentRun.CurrentCombatTurn}.");
    }

    private static void ApplyGoldPenalty(ContractDefinition contract, object? rewardContext, double percentPenalty)
    {
        if (rewardContext == null)
        {
            LogMissingTarget(contract, "reward context");
            return;
        }

        bool applied = false;
        foreach (string name in new[] { "Gold", "GoldReward", "GoldAmount", "_gold", "_goldReward" })
        {
            double? current = ContractRuntimeReflection.TryGetNumber(rewardContext, name);
            if (!current.HasValue)
            {
                continue;
            }

            double reduced = current.Value * (1.0 - (percentPenalty / 100.0));
            applied |= ContractRuntimeReflection.TrySetNumber(rewardContext, reduced, name);
        }

        object? rewards = ContractRuntimeReflection.TryGetByNames(rewardContext, "Rewards", "_rewards", "RewardNodes");
        if (rewards is IEnumerable enumerable)
        {
            foreach (object? reward in enumerable)
            {
                if (reward == null)
                {
                    continue;
                }

                foreach (string name in new[] { "Gold", "GoldReward", "Amount", "_amount" })
                {
                    double? current = ContractRuntimeReflection.TryGetNumber(reward, name);
                    if (!current.HasValue)
                    {
                        continue;
                    }

                    double reduced = current.Value * (1.0 - (percentPenalty / 100.0));
                    applied |= ContractRuntimeReflection.TrySetNumber(reward, reduced, name);
                }
            }
        }

        if (applied)
        {
            ModLogger.Info($"Applied {contract.Id}: reduced gold rewards by {percentPenalty}%.");
            return;
        }

        LogMemberMiss(contract, "Gold reward fields");
    }

    private static void ApplyEnemyStartingPlating(ContractDefinition contract, object enemy, double stacks)
    {
        ModLogger.Info(
            $"ApplyPowerOrFallback start for {contract.Id}. powerTypes=[MegaCrit.Sts2.Core.Models.Powers.PlatingPower, MegaCrit.Sts2.Core.Models.Powers.MetallicizePower], fallback=[Plating, _plating, Metallicize, _metallicize], amount={stacks:0.##}, targetType={enemy.GetType().FullName}");

        bool appliedViaPower = ContractRuntimeReflection.TryApplyPower(
            enemy,
            enemy,
            stacks,
            "MegaCrit.Sts2.Core.Models.Powers.PlatingPower",
            "MegaCrit.Sts2.Core.Models.Powers.MetallicizePower");

        if (appliedViaPower)
        {
            ModLogger.Info(
                $"Applied {contract.Id} via PowerCmd (MegaCrit.Sts2.Core.Models.Powers.PlatingPower, MegaCrit.Sts2.Core.Models.Powers.MetallicizePower) amount={stacks:0.##}.");
            ApplyDelta(contract, enemy, stacks, BlockNames);
            return;
        }

        bool appliedViaMember = ApplyDelta(contract, enemy, stacks, "Plating", "_plating", "Metallicize", "_metallicize");
        if (appliedViaMember)
        {
            ApplyDelta(contract, enemy, stacks, BlockNames);
            ModLogger.Info($"Applied {contract.Id} via visible member fallback amount={stacks:0.##}.");
            return;
        }

        int stackCount = Math.Max(0, Convert.ToInt32(Math.Floor(stacks)));
        ContractCombatRuntimeState.RegisterDebrisCovered(enemy, stackCount);
        ModLogger.Warn($"Applied {contract.Id}: original visible plating could not be resolved, fell back to custom runtime plating x{stackCount}.");
    }

    private static void ApplyEnemyBloodthirstyStatus(ContractDefinition contract, object enemy, double percent)
    {
        ContractCombatRuntimeState.SetCustomStatusValue(enemy, ContractCombatRuntimeState.BloodthirstyStatusId, percent);
        bool appliedVisiblePower = ContractRuntimeReflection.TryApplyPower(
            enemy,
            enemy,
            percent,
            "MegaCrit.Sts2.Core.Models.Powers.RavenousPower");
        ModLogger.Info($"Applied {contract.Id}: attached custom bloodthirsty status {percent:0.##}% to {enemy.GetType().FullName}.");
        if (appliedVisiblePower)
        {
            ModLogger.Info($"Applied {contract.Id} visible power layer via MegaCrit.Sts2.Core.Models.Powers.RavenousPower amount={percent:0.##}.");
        }
    }

    private static void TryApplyEnemyLifesteal(object sourceCreature, double hpLost, double percent)
    {
        ContractDefinition? contract = FindActiveContractByGroup("bloodthirsty");
        if (contract == null)
        {
            return;
        }

        double healAmount = Math.Floor(hpLost * (percent / 100.0));
        if (healAmount <= 0.001d)
        {
            return;
        }

        double? currentHp = ContractRuntimeReflection.TryGetNumber(sourceCreature, CurrentHpNames);
        double? maxHp = ContractRuntimeReflection.TryGetNumber(sourceCreature, MaxHpNames);
        if (!currentHp.HasValue || !maxHp.HasValue || currentHp.Value <= 0.001d)
        {
            LogMemberMiss(contract, "Enemy Health/MaxHealth");
            return;
        }

        double healedHp = Math.Min(maxHp.Value, currentHp.Value + healAmount);
        if (healedHp <= currentHp.Value + 0.001d)
        {
            return;
        }

        bool applied = ContractRuntimeReflection.TryHeal(sourceCreature, sourceCreature, healAmount);
        if (!applied)
        {
            applied = ContractRuntimeReflection.TrySetNumber(sourceCreature, healedHp, CurrentHpNames);
        }

        if (applied)
        {
            ModLogger.Info(
                $"Applied {contract.Id}: {sourceCreature.GetType().FullName} leeched {healAmount:0.##} HP from {hpLost:0.##} unblocked damage.");
        }
        else
        {
            LogMemberMiss(contract, "Enemy Health");
        }
    }

    private static void ApplyDebrisCoveredTurnBlock(object? combatContext, string channel)
    {
        if (!ContractCombatRuntimeState.HasTrackedDebrisCovered)
        {
            return;
        }

        ContractDefinition? contract = FindActiveContractByGroup("debris_covered");
        if (contract == null || combatContext == null)
        {
            return;
        }

        List<object> enemies = ContractRuntimeReflection.TryGetEnemies(combatContext).ToList();
        if (enemies.Count == 0)
        {
            return;
        }

        foreach (object enemy in enemies)
        {
            int stacks = ContractCombatRuntimeState.GetDebrisCoveredStacks(enemy);
            if (stacks <= 0)
            {
                continue;
            }

            bool applied = ApplyDelta(contract, enemy, stacks, BlockNames);
            if (applied)
            {
                ModLogger.Info($"Custom debris-covered plating triggered via {channel}: granted {stacks} block to {enemy.GetType().FullName}.");
            }
        }
    }

    private static void TryHandleDebrisCoveredDamage(object targetCreature, double hpLost)
    {
        ContractDefinition? contract = FindActiveContractByGroup("debris_covered");
        if (contract == null)
        {
            return;
        }

        if (ContractCombatRuntimeState.TryConsumeDebrisCoveredOnHpLoss(targetCreature, hpLost, out int remainingStacks))
        {
            ModLogger.Info(
                $"Applied {contract.Id}: {targetCreature.GetType().FullName} lost 1 debris-covered stack after taking HP damage. Remaining={remainingStacks}.");
        }
    }

    private static object? ResolveBloodthirstySourceCreature(object? parsedSourceCreature, object? playerCreature, IEnumerable<object?> hookArgs)
    {
        if (parsedSourceCreature != null
            && !IsSameCreature(parsedSourceCreature, playerCreature)
            && ContractCombatRuntimeState.GetCustomStatusValue(parsedSourceCreature, ContractCombatRuntimeState.BloodthirstyStatusId) > 0.001d)
        {
            return parsedSourceCreature;
        }

        List<object> creatures = hookArgs
            .Select(ContractRuntimeReflection.TryGetCreature)
            .Where(candidate => candidate != null)
            .Cast<object>()
            .Distinct(ReferenceEqualityComparer.Instance)
            .Where(candidate => !IsSameCreature(candidate, playerCreature))
            .ToList();

        object? taggedCreature = creatures.FirstOrDefault(
            candidate => ContractCombatRuntimeState.GetCustomStatusValue(candidate, ContractCombatRuntimeState.BloodthirstyStatusId) > 0.001d);
        if (taggedCreature != null)
        {
            return taggedCreature;
        }

        return ContractCombatRuntimeState.FindEnemiesWithStatus(ContractCombatRuntimeState.BloodthirstyStatusId)
            .FirstOrDefault(candidate => !IsSameCreature(candidate, playerCreature));
    }

    private static bool TryExtractDamageEvent(string hookName, object[] hookArgs, out object? sourceCreature, out object? targetCreature, out double hpLost)
    {
        sourceCreature = null;
        targetCreature = null;
        hpLost = 0;

        object? player = ResolvePlayer(null, hookArgs);
        object? playerCreature = ContractRuntimeReflection.TryGetCreature(player);
        List<object> creatures = hookArgs
            .Select(ContractRuntimeReflection.TryGetCreature)
            .Where(candidate => candidate != null)
            .Cast<object>()
            .Distinct(ReferenceEqualityComparer.Instance)
            .ToList();
        object? directTarget = TryResolveCreatureFromHookArgs(hookArgs, "Target", "Victim", "Defender", "Receiver", "Creature", "Owner");
        object? directSource = TryResolveCreatureFromHookArgs(hookArgs, "Source", "Attacker", "Instigator", "Dealer", "DamageSource", "Owner", "Creature");

        bool playerReferenced = playerCreature != null && hookArgs.Any(arg => IsSameCreature(ContractRuntimeReflection.TryGetCreature(arg), playerCreature));
        targetCreature = directTarget;
        sourceCreature = directSource;

        object? otherCreature = creatures.FirstOrDefault(candidate => playerCreature == null || !IsSameCreature(candidate, playerCreature));
        if (playerCreature != null)
        {
            if (targetCreature == null && sourceCreature != null)
            {
                targetCreature = IsSameCreature(sourceCreature, playerCreature) ? otherCreature : playerCreature;
            }

            if (sourceCreature == null && targetCreature != null)
            {
                sourceCreature = IsSameCreature(targetCreature, playerCreature) ? otherCreature : playerCreature;
            }

            if (targetCreature == null && sourceCreature == null && playerReferenced)
            {
                if (string.Equals(hookName, "AfterDamageReceived", StringComparison.Ordinal))
                {
                    targetCreature = playerCreature;
                    sourceCreature = otherCreature;
                }
                else if (string.Equals(hookName, "AfterDamageGiven", StringComparison.Ordinal))
                {
                    sourceCreature = playerCreature;
                    targetCreature = otherCreature;
                }
                else
                {
                    targetCreature = playerCreature;
                    sourceCreature = otherCreature;
                }
            }
        }

        if (targetCreature == null && creatures.Count > 0)
        {
            targetCreature = creatures[0];
        }

        object? resolvedTargetCreature = targetCreature;
        if (sourceCreature == null)
        {
            sourceCreature = creatures.FirstOrDefault(candidate => !IsSameCreature(candidate, resolvedTargetCreature));
        }

        hpLost = TryExtractDamageAmount(hookArgs);
        return targetCreature != null && hpLost > 0.001d;
    }

    private static double TryExtractDamageAmount(IEnumerable<object?> hookArgs)
    {
        string[] preferredMemberNames =
        {
            "HpLost", "HealthLost", "DamageTaken", "UnblockedDamage", "FinalDamage", "ModifiedHpLost", "HpLostAfterOsty", "Amount", "_amount", "Value", "Damage"
        };

        foreach (string memberName in preferredMemberNames)
        {
            foreach (object? arg in hookArgs)
            {
                if (arg == null)
                {
                    continue;
                }

                double? value = ContractRuntimeReflection.TryGetNumber(arg, memberName);
                if (value.HasValue && value.Value > 0.001d)
                {
                    return value.Value;
                }
            }
        }

        foreach (object? arg in hookArgs)
        {
            if (arg == null)
            {
                continue;
            }

            double? oldHp = ContractRuntimeReflection.TryGetNumber(arg, "OldHp", "OldHealth", "PreviousHp", "PreviousHealth");
            double? newHp = ContractRuntimeReflection.TryGetNumber(arg, "NewHp", "NewHealth", "CurrentHp", "CurrentHealth");
            if (oldHp.HasValue && newHp.HasValue && oldHp.Value - newHp.Value > 0.001d)
            {
                return oldHp.Value - newHp.Value;
            }

            try
            {
                double direct = Convert.ToDouble(arg);
                if (direct > 0.001d)
                {
                    return direct;
                }
            }
            catch
            {
            }
        }

        return 0;
    }

    private static object? TryResolveCreatureFromHookArgs(IEnumerable<object?> hookArgs, params string[] memberNames)
    {
        foreach (object? arg in hookArgs)
        {
            if (arg == null)
            {
                continue;
            }

            foreach (string memberName in memberNames)
            {
                object? nested = ContractRuntimeReflection.TryGetByNames(arg, memberName);
                object? creature = ContractRuntimeReflection.TryGetCreature(nested);
                if (creature != null)
                {
                    return creature;
                }
            }
        }

        return null;
    }

    private static string DescribeCreature(object? creature)
    {
        if (creature == null)
        {
            return "<null>";
        }

        return $"{creature.GetType().FullName}@{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(creature)}";
    }

    private static string DescribeHookArgs(IEnumerable<object?> hookArgs)
    {
        return string.Join(
            " | ",
            hookArgs.Select(
                arg =>
                {
                    if (arg == null)
                    {
                        return "null";
                    }

                    Type type = arg.GetType();
                    object? creature = ContractRuntimeReflection.TryGetCreature(arg);
                    string creatureNote = creature == null ? string.Empty : $"->creature:{creature.GetType().Name}";
                    return $"{type.FullName}{creatureNote}";
                }));
    }

    private static ContractDefinition? FindActiveContractByGroup(string groupId)
    {
        return ContractStateStore.CurrentRun.SelectedContracts
            .Select(ContractDatabase.TryGet)
            .FirstOrDefault(definition => definition != null && string.Equals(definition.GroupId, groupId, StringComparison.Ordinal));
    }

    private static bool IsSameCreature(object? left, object? right)
    {
        if (left == null || right == null)
        {
            return false;
        }

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        object? resolvedLeft = ContractRuntimeReflection.TryGetCreature(left);
        object? resolvedRight = ContractRuntimeReflection.TryGetCreature(right);
        return resolvedLeft != null && resolvedRight != null && ReferenceEquals(resolvedLeft, resolvedRight);
    }

    private static void LogUnsupported(ContractDefinition contract, string reason)
    {
        ModLogger.Warn($"Skipped {contract.Id}: {reason}");
    }

    private static void LogMissingTarget(ContractDefinition contract, string target)
    {
        ModLogger.Warn($"Failed {contract.Id}: target '{target}' was not available in this phase.");
    }

    private static void LogMemberMiss(ContractDefinition contract, string member)
    {
        ModLogger.Warn($"Failed {contract.Id}: target located but member '{member}' was not found or writable.");
    }

    private static object? ResolvePlayer(object? primaryContext, params object?[] extraPlayerCandidates)
    {
        List<object?> candidates = new();
        if (primaryContext != null)
        {
            candidates.Add(primaryContext);
        }

        foreach (object? candidate in extraPlayerCandidates)
        {
            if (candidate != null)
            {
                candidates.Add(candidate);
            }
        }

        return ContractRuntimeReflection.TryGetPlayerFromCandidates(candidates.ToArray());
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
