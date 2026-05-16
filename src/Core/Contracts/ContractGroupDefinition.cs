using System.Collections.Generic;

namespace CatastropheContract.Core.Contracts;

public sealed record ContractGroupDefinition(
    string Id,
    string DisplayName,
    string EnglishName,
    ContractCategory Category,
    string Summary,
    ContractNodeMetadata Node,
    bool IsImplemented,
    IReadOnlyList<ContractDefinition> Levels
);
