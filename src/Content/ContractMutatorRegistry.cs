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
                    LogUnsupported(contract, "Unblocked-damage lifesteal requires a combat damage hook.");
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
                    ApplyToEnemies(contract, context, enemy => ApplyDelta(contract, enemy, effect.Value, "Block", "_block", "CurrentBlock"));
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
                    ApplyToEnemies(
                        contract,
                        context,
                        enemy => ApplyPowerOrFallback(
                            contract,
                            enemy,
                            enemy,
                            effect.Value,
                            new[] { "MegaCrit.Sts2.Core.Models.Powers.PlatedArmorPower", "MegaCrit.Sts2.Core.Models.Powers.MetallicizePower" },
                            "PlatedArmor",
                            "_platedArmor",
                            "Metallicize",
                            "_metallicize"));
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
}
