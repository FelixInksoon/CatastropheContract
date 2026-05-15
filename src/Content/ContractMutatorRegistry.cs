using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CatastropheContract.Core;
using CatastropheContract.Core.Contracts;
using CatastropheContract.Core.State;

namespace CatastropheContract.Content;

public static class ContractMutatorRegistry
{
    public static void ApplyRunStartMutators(object runContext)
    {
        if (!ContractStateStore.CurrentRun.Enabled)
        {
            return;
        }

        object? player = ContractRuntimeReflection.TryGetPlayer(runContext);
        foreach (ContractDefinition contract in GetActiveContracts(ContractApplyPhase.RunStart))
        {
            ApplyContract(contract, player, runContext);
        }
    }

    public static void ApplyPreCombatMutators(object combatContext)
    {
        if (!ContractStateStore.CurrentRun.Enabled)
        {
            ModLogger.Info("ApplyPreCombatMutators skipped because CurrentRun.Enabled is false.");
            return;
        }

        object? player = ContractRuntimeReflection.TryGetPlayer(combatContext);
        List<object> enemies = ContractRuntimeReflection.TryGetEnemies(combatContext).ToList();
        ModLogger.Info($"PreCombat start. enemies={enemies.Count}, contracts={string.Join(", ", ContractStateStore.CurrentRun.SelectedContracts)}");

        foreach (ContractDefinition contract in GetActiveContracts(ContractApplyPhase.PreCombat))
        {
            ApplyContract(contract, player, combatContext);
        }
    }

    public static void ApplyTurnRuleMutators(object combatContext)
    {
        if (!ContractStateStore.CurrentRun.Enabled)
        {
            return;
        }

        ContractStateStore.OnTurnStarted();
        object? player = ContractRuntimeReflection.TryGetPlayer(combatContext);
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

    private static void ApplyContract(ContractDefinition contract, object? primaryTarget, object? context)
    {
        if (!contract.IsImplemented)
        {
            LogUnsupported(contract, "Marked as not implemented.");
            return;
        }

        foreach (ContractEffect effect in contract.Effects)
        {
            switch (effect.Kind)
            {
                case ContractEffectKind.PlayerMaxHpLossPercent:
                    ApplyPlayerMaxHpPercentLoss(contract, primaryTarget, effect.Value);
                    break;
                case ContractEffectKind.PotionSlotCap:
                    ApplySetNumber(contract, primaryTarget, effect.Value, "PotionSlots", "PotionSlotCount", "MaxPotionSlots", "_potionSlots");
                    break;
                case ContractEffectKind.MaxHpLockedToOne:
                    ApplySetNumber(contract, primaryTarget, 1, "MaxHealth", "MaxHp", "_maxHealth", "_maxHp");
                    ApplySetNumber(contract, primaryTarget, 1, "Health", "Hp", "_health", "_hp");
                    break;
                case ContractEffectKind.PlayerStartWithWeak:
                    ApplyPowerOrFallback(
                        contract,
                        primaryTarget,
                        primaryTarget,
                        effect.Value,
                        new[] { "MegaCrit.Sts2.Core.Models.Powers.WeakPower" },
                        "Weak",
                        "_weak");
                    break;
                case ContractEffectKind.PlayerStartWithFrail:
                    ApplyPowerOrFallback(
                        contract,
                        primaryTarget,
                        primaryTarget,
                        effect.Value,
                        new[] { "MegaCrit.Sts2.Core.Models.Powers.VulnerablePower" },
                        "Vulnerable",
                        "_vulnerable");
                    break;
                case ContractEffectKind.PlayerStartWithDexterityPenalty:
                    ApplyPowerOrFallback(
                        contract,
                        primaryTarget,
                        primaryTarget,
                        -effect.Value,
                        new[] { "MegaCrit.Sts2.Core.Models.Powers.DexterityPower" },
                        "Dexterity",
                        "_dexterity");
                    break;
                case ContractEffectKind.PlayerStartWithStrengthPenalty:
                    ApplyPowerOrFallback(
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

    private static void ApplyPlayerMaxHpPercentLoss(ContractDefinition contract, object? player, double percentLoss)
    {
        if (player == null)
        {
            LogMissingTarget(contract, "player");
            return;
        }

        double? currentMax = ContractRuntimeReflection.TryGetNumber(player, "MaxHealth", "MaxHp", "_maxHealth", "_maxHp");
        if (!currentMax.HasValue)
        {
            LogMemberMiss(contract, "MaxHealth/MaxHp");
            return;
        }

        double reduced = currentMax.Value * (1.0 - (percentLoss / 100.0));
        bool maxApplied = ContractRuntimeReflection.TrySetNumber(player, reduced, "MaxHealth", "MaxHp", "_maxHealth", "_maxHp");
        bool hpApplied = ContractRuntimeReflection.TrySetNumber(player, reduced, "Health", "Hp", "_health", "_hp");

        if (maxApplied && hpApplied)
        {
            ModLogger.Info($"Applied {contract.Id}: reduced player max HP by {percentLoss}% to {reduced:0.##}.");
            return;
        }

        LogMemberMiss(contract, "Health/MaxHealth");
    }

    private static void ApplyEnemyMaxHpBoost(ContractDefinition contract, object enemy, double percentBoost)
    {
        double? currentMax = ContractRuntimeReflection.TryGetNumber(enemy, "MaxHealth", "MaxHp", "_maxHealth", "_maxHp");
        if (!currentMax.HasValue)
        {
            LogMemberMiss(contract, "Enemy MaxHealth/MaxHp");
            return;
        }

        double boosted = currentMax.Value * (1.0 + (percentBoost / 100.0));
        bool maxApplied = ContractRuntimeReflection.TrySetNumber(enemy, boosted, "MaxHealth", "MaxHp", "_maxHealth", "_maxHp");
        bool hpApplied = ContractRuntimeReflection.TrySetNumber(enemy, boosted, "Health", "Hp", "_health", "_hp");
        if (maxApplied && hpApplied)
        {
            ModLogger.Info($"Applied {contract.Id}: enemy max HP boosted by {percentBoost}% to {boosted:0.##}.");
            return;
        }

        LogMemberMiss(contract, "Enemy Health/MaxHealth");
    }

    private static void ApplyDelta(ContractDefinition contract, object? target, double delta, params string[] names)
    {
        if (target == null)
        {
            LogMissingTarget(contract, string.Join("/", names));
            return;
        }

        double current = ContractRuntimeReflection.TryGetNumber(target, names) ?? 0;
        bool applied = ContractRuntimeReflection.TrySetNumber(target, current + delta, names);
        if (applied)
        {
            ModLogger.Info($"Applied {contract.Id}: {string.Join("/", names)} {(delta >= 0 ? "+" : string.Empty)}{delta:0.##}.");
            return;
        }

        LogMemberMiss(contract, string.Join("/", names));
    }

    private static void ApplySetNumber(ContractDefinition contract, object? target, double value, params string[] names)
    {
        if (target == null)
        {
            LogMissingTarget(contract, string.Join("/", names));
            return;
        }

        bool applied = ContractRuntimeReflection.TrySetNumber(target, value, names);
        if (applied)
        {
            ModLogger.Info($"Applied {contract.Id}: set {string.Join("/", names)} to {value:0.##}.");
            return;
        }

        LogMemberMiss(contract, string.Join("/", names));
    }

    private static void ApplyPowerOrFallback(
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
            return;
        }

        ModLogger.Info(
            $"ApplyPowerOrFallback start for {contract.Id}. powerTypes=[{string.Join(", ", powerTypeNames)}], fallback=[{string.Join(", ", fallbackNames)}], amount={amount:0.##}, targetType={target.GetType().FullName}");

        bool appliedViaPower = ContractRuntimeReflection.TryApplyPower(target, source, amount, powerTypeNames);
        ModLogger.Info($"ApplyPowerOrFallback result for {contract.Id}: TryApplyPower={appliedViaPower}.");

        if (appliedViaPower)
        {
            ModLogger.Info($"Applied {contract.Id} via PowerCmd ({string.Join(", ", powerTypeNames)}) amount={amount:0.##}.");
            return;
        }

        ApplyDelta(contract, target, amount, fallbackNames);
    }

    private static void ApplyDeathCountdown(ContractDefinition contract, object? player, int countdownTurn)
    {
        if (ContractStateStore.CurrentRun.CurrentCombatTurn < countdownTurn)
        {
            return;
        }

        ApplySetNumber(contract, player, 0, "Health", "Hp", "_health", "_hp");
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
}
