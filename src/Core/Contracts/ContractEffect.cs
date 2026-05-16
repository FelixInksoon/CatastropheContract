namespace CatastropheContract.Core.Contracts;

public sealed record ContractEffect(
    ContractEffectKind Kind,
    double Value,
    string Detail
);
