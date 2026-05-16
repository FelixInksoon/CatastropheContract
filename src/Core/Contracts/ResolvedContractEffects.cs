namespace CatastropheContract.Core.Contracts;

public sealed class ResolvedContractEffects
{
    public double EnemyUnblockedDamageLifestealPercent { get; set; }
    public int EnemyStartWithThorns { get; set; }
    public int EnemyStartWithStrength { get; set; }
    public double EnemyMaxHpPercent { get; set; }
    public int EnemyStartWithBlock { get; set; }
    public int EnemyStartWithArtifact { get; set; }
    public int EnemyStartWithPlatedArmor { get; set; }
    public double EliteEncounterRatio { get; set; } = -1;
    public int BossesRequiredPerAct { get; set; } = 1;
    public double PlayerMaxHpLossPercent { get; set; }
    public int PlayerStartWithWeak { get; set; }
    public int PlayerStartWithFrail { get; set; }
    public int PlayerStartWithDexterityPenalty { get; set; }
    public int PlayerStartWithStrengthPenalty { get; set; }
    public int XCostCardReduction { get; set; }
    public bool AllGainedCardsAreEthereal { get; set; }
    public int DeckSizeCap { get; set; }
    public int LimitedTurnOneCardOnlyFrequency { get; set; }
    public bool HideEnemyIntent { get; set; }
    public bool LinearMap { get; set; }
    public double GoldGainPercentPenalty { get; set; }
    public bool NoHealing { get; set; }
    public int? PotionSlotCap { get; set; }
    public bool MaxHpLockedToOne { get; set; }
    public int DeathCountdownTurn { get; set; }
}
