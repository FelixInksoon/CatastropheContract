using System;
using System.Collections.Generic;
using System.Linq;

namespace CatastropheContract.Core.Contracts;

public static class ContractSelectionEvaluator
{
    public static int CalculateRisk(IEnumerable<string> ids)
    {
        int risk = 0;

        foreach (string id in ids.Distinct(StringComparer.Ordinal))
        {
            ContractDefinition? contract = ContractDatabase.TryGet(id);
            if (contract != null)
            {
                risk += contract.RiskValue;
            }
        }

        return risk;
    }

    public static IReadOnlyList<string> GetConflicts(IEnumerable<string> ids)
    {
        HashSet<string> selected = ids.ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> seenGroups = new(StringComparer.Ordinal);
        List<string> conflicts = new();

        foreach (string id in selected)
        {
            ContractDefinition? definition = ContractDatabase.TryGet(id);
            if (definition == null)
            {
                continue;
            }

            if (!seenGroups.TryAdd(definition.GroupId, definition.Id))
            {
                conflicts.Add($"{definition.DisplayName} 只能选择一个等级。");
            }
        }

        return conflicts;
    }

    public static IReadOnlyList<string> GetMissingRequirements(IEnumerable<string> ids)
    {
        HashSet<string> selectedGroups = ids
            .Select(ContractDatabase.TryGet)
            .Where(definition => definition != null)
            .Select(definition => definition!.GroupId)
            .ToHashSet(StringComparer.Ordinal);

        List<string> missing = new();

        foreach (string id in ids.Distinct(StringComparer.Ordinal))
        {
            ContractDefinition? definition = ContractDatabase.TryGet(id);
            if (definition == null)
            {
                continue;
            }

            ContractGroupDefinition? group = ContractDatabase.TryGetGroup(definition.GroupId);
            if (group == null)
            {
                continue;
            }

            foreach (string prerequisite in group.Node.PrerequisiteGroupIds)
            {
                if (selectedGroups.Contains(prerequisite))
                {
                    continue;
                }

                ContractGroupDefinition? prerequisiteGroup = ContractDatabase.TryGetGroup(prerequisite);
                string prerequisiteName = prerequisiteGroup?.DisplayName ?? prerequisite;
                missing.Add($"{group.DisplayName} 需要前置词条 {prerequisiteName}。");
            }
        }

        return missing;
    }
}
