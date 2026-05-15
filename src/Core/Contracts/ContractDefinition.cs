using System.Collections.Generic;

namespace CatastropheContract.Core.Contracts;

public sealed record ContractDefinition(
    string Id,
    string GroupId,
    string InternalName,
    string DisplayName,
    string EnglishName,
    string LevelLabel,
    ContractCategory Category,
    int RiskValue,
    string Summary,
    ContractApplyPhase ApplyPhase,
    bool IsImplemented,
    IReadOnlyList<ContractEffect> Effects,
    string DesignNote
);
