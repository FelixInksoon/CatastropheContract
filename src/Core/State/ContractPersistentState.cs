using System;
using System.Collections.Generic;

namespace CatastropheContract.Core.State;

public sealed class ContractPersistentState
{
    public bool LastEnabled { get; set; }
    public List<string> LastSelectedContracts { get; set; } = new();
    public int BestGlobalRisk { get; set; }
    public Dictionary<string, int> BestRiskByCharacter { get; set; } = new(StringComparer.Ordinal);
}
