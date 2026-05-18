using System.Collections.Generic;
using CatastropheContract.Core.Contracts;

namespace CatastropheContract.Core.State;

public sealed class ContractRunState
{
    public bool Enabled { get; set; }
    public string CharacterId { get; set; } = string.Empty;
    public int RiskLevel { get; set; }
    public List<string> SelectedContracts { get; set; } = new();
    public ResolvedContractEffects Effects { get; set; } = new();
    public int CurrentCombatTurn { get; set; }
    public int CardsPlayedThisTurn { get; set; }
    public bool PreCombatAppliedThisFight { get; set; }
    public bool CountdownTriggeredThisCombat { get; set; }
    public List<string> AppliedRunStartContracts { get; set; } = new();
}
