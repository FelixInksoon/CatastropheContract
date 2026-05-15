using System;
using System.Collections.Generic;
using System.Linq;
using CatastropheContract.Core.Contracts;
using CatastropheContract.Core.State;

namespace CatastropheContract.Core.UI;

public sealed class ContractPanelViewModel
{
    public string CharacterId { get; set; } = "*";
    public bool IsEnabled { get; set; }
    public ContractCategory SelectedCategory { get; private set; } = ContractCategory.EnemyForce;
    public string? FocusedGroupId { get; private set; }
    public HashSet<string> SelectedContracts { get; } = new(StringComparer.Ordinal);

    public int RiskLevel => ContractSelectionEvaluator.CalculateRisk(SelectedContracts);
    public IReadOnlyList<string> Conflicts => ContractSelectionEvaluator.GetConflicts(SelectedContracts);
    public IReadOnlyList<string> MissingRequirements => ContractSelectionEvaluator.GetMissingRequirements(SelectedContracts);

    public IEnumerable<ContractGroupDefinition> VisibleGroups => ContractDatabase.GetGroupsByCategory(SelectedCategory);

    public ContractGroupDefinition? FocusedGroup =>
        FocusedGroupId == null ? VisibleGroups.FirstOrDefault() : ContractDatabase.TryGetGroup(FocusedGroupId);

    public ContractDefinition? FocusedDefinition
    {
        get
        {
            ContractGroupDefinition? group = FocusedGroup;
            if (group == null)
            {
                return null;
            }

            string? selectedTier = GetSelectedTierForGroup(group.Id);
            if (selectedTier != null)
            {
                return ContractDatabase.TryGet(selectedTier);
            }

            return group.Levels.OrderBy(level => level.RiskValue).FirstOrDefault();
        }
    }

    public string? GetSelectedTierForGroup(string groupId)
    {
        return SelectedContracts
            .Select(ContractDatabase.TryGet)
            .Where(definition => definition != null && definition.GroupId == groupId)
            .Select(definition => definition!.Id)
            .FirstOrDefault();
    }

    public void SetSelectedCategory(ContractCategory category)
    {
        SelectedCategory = category;
        FocusedGroupId = VisibleGroups.FirstOrDefault()?.Id;
    }

    public void FocusGroup(string groupId)
    {
        FocusedGroupId = groupId;
    }

    public void SelectTier(string contractId)
    {
        ContractDefinition? target = ContractDatabase.TryGet(contractId);
        if (target == null)
        {
            return;
        }

        foreach (string existingId in SelectedContracts.ToList())
        {
            ContractDefinition? existing = ContractDatabase.TryGet(existingId);
            if (existing != null && existing.GroupId == target.GroupId)
            {
                SelectedContracts.Remove(existingId);
            }
        }

        SelectedContracts.Add(contractId);
        FocusedGroupId = target.GroupId;
        SavePreset();
    }

    public void DeselectTier(string contractId)
    {
        if (!SelectedContracts.Remove(contractId))
        {
            return;
        }

        SavePreset();
    }

    public void Clear()
    {
        SelectedContracts.Clear();
        SavePreset();
    }

    public void LoadLastPreset()
    {
        IsEnabled = ContractStateStore.Persistent.LastEnabled;
        SelectedContracts.Clear();
        foreach (string contractId in ContractStateStore.Persistent.LastSelectedContracts)
        {
            SelectedContracts.Add(contractId);
        }

        SelectedCategory = ContractDatabase.AllCategories.FirstOrDefault()?.Category ?? ContractCategory.EnemyForce;
        FocusedGroupId = VisibleGroups.FirstOrDefault()?.Id;
    }

    public void SavePreset()
    {
        ContractStateStore.SetSelection(IsEnabled, SelectedContracts, CharacterId);
        ContractStateStore.FlushPersistentState();
    }
}
