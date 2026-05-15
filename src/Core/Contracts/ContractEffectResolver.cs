using System.Collections.Generic;

namespace CatastropheContract.Core.Contracts;

public static class ContractEffectResolver
{
    public static ResolvedContractEffects Resolve(IEnumerable<string> selectedIds)
    {
        ResolvedContractEffects resolved = new();

        foreach (string id in selectedIds)
        {
            ContractDefinition? definition = ContractDatabase.TryGet(id);
            if (definition == null)
            {
                continue;
            }

            foreach (ContractEffect effect in definition.Effects)
            {
                switch (effect.Kind)
                {
                    case ContractEffectKind.EnemyUnblockedDamageLifestealPercent:
                        resolved.EnemyUnblockedDamageLifestealPercent = effect.Value;
                        break;
                    case ContractEffectKind.EnemyStartWithThorns:
                        resolved.EnemyStartWithThorns = (int)effect.Value;
                        break;
                    case ContractEffectKind.EnemyStartWithStrength:
                        resolved.EnemyStartWithStrength = (int)effect.Value;
                        break;
                    case ContractEffectKind.EnemyMaxHpPercent:
                        resolved.EnemyMaxHpPercent = effect.Value;
                        break;
                    case ContractEffectKind.EnemyStartWithBlock:
                        resolved.EnemyStartWithBlock = (int)effect.Value;
                        break;
                    case ContractEffectKind.EnemyStartWithArtifact:
                        resolved.EnemyStartWithArtifact = (int)effect.Value;
                        break;
                    case ContractEffectKind.EnemyStartWithPlatedArmor:
                        resolved.EnemyStartWithPlatedArmor = (int)effect.Value;
                        break;
                    case ContractEffectKind.EliteEncounterRatio:
                        resolved.EliteEncounterRatio = effect.Value;
                        break;
                    case ContractEffectKind.BossesRequiredPerAct:
                        resolved.BossesRequiredPerAct = (int)effect.Value;
                        break;
                    case ContractEffectKind.PlayerMaxHpLossPercent:
                        resolved.PlayerMaxHpLossPercent = effect.Value;
                        break;
                    case ContractEffectKind.PlayerStartWithWeak:
                        resolved.PlayerStartWithWeak = (int)effect.Value;
                        break;
                    case ContractEffectKind.PlayerStartWithFrail:
                        resolved.PlayerStartWithFrail = (int)effect.Value;
                        break;
                    case ContractEffectKind.PlayerStartWithDexterityPenalty:
                        resolved.PlayerStartWithDexterityPenalty = (int)effect.Value;
                        break;
                    case ContractEffectKind.PlayerStartWithStrengthPenalty:
                        resolved.PlayerStartWithStrengthPenalty = (int)effect.Value;
                        break;
                    case ContractEffectKind.XCostCardReduction:
                        resolved.XCostCardReduction = (int)effect.Value;
                        break;
                    case ContractEffectKind.AllGainedCardsAreEthereal:
                        resolved.AllGainedCardsAreEthereal = true;
                        break;
                    case ContractEffectKind.DeckSizeCap:
                        resolved.DeckSizeCap = (int)effect.Value;
                        break;
                    case ContractEffectKind.LimitedTurnOneCardOnlyFrequency:
                        resolved.LimitedTurnOneCardOnlyFrequency = (int)effect.Value;
                        break;
                    case ContractEffectKind.HideEnemyIntent:
                        resolved.HideEnemyIntent = true;
                        break;
                    case ContractEffectKind.LinearMap:
                        resolved.LinearMap = true;
                        break;
                    case ContractEffectKind.GoldGainPercentPenalty:
                        resolved.GoldGainPercentPenalty = effect.Value;
                        break;
                    case ContractEffectKind.NoHealing:
                        resolved.NoHealing = true;
                        break;
                    case ContractEffectKind.PotionSlotCap:
                        resolved.PotionSlotCap = (int)effect.Value;
                        break;
                    case ContractEffectKind.MaxHpLockedToOne:
                        resolved.MaxHpLockedToOne = true;
                        break;
                    case ContractEffectKind.DeathCountdownTurn:
                        resolved.DeathCountdownTurn = (int)effect.Value;
                        break;
                }
            }
        }

        return resolved;
    }
}
