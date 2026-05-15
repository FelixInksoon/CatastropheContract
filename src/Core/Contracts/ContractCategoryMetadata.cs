namespace CatastropheContract.Core.Contracts;

public sealed record ContractCategoryMetadata(
    ContractCategory Category,
    string DisplayName,
    string EnglishName,
    string Summary,
    int SortOrder
);
