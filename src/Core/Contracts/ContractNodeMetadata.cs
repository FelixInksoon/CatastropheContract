using System.Collections.Generic;

namespace CatastropheContract.Core.Contracts;

public sealed record ContractNodeMetadata(
    int Column,
    int Row,
    string? ParentGroupId,
    bool IsLeaf,
    bool ShowConnector,
    IReadOnlyList<string> PrerequisiteGroupIds
);
