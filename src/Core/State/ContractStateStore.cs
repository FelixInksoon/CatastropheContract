using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CatastropheContract.Core.Contracts;
using Godot;

namespace CatastropheContract.Core.State;

public static class ContractStateStore
{
    private const string SavePath = "user://catastrophe_contract_state.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static ContractPersistentState _persistent = new();
    private static ContractRunState _currentRun = new();

    public static ContractPersistentState Persistent => _persistent;
    public static ContractRunState CurrentRun => _currentRun;

    public static void LoadPersistentState()
    {
        try
        {
            if (!FileAccess.FileExists(SavePath))
            {
                _persistent = new ContractPersistentState();
                return;
            }

            using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            string json = file.GetAsText();
            _persistent = JsonSerializer.Deserialize<ContractPersistentState>(json, JsonOptions) ?? new ContractPersistentState();
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Failed to load contract state: {ex.Message}");
            _persistent = new ContractPersistentState();
        }
    }

    public static void FlushPersistentState()
    {
        try
        {
            string json = JsonSerializer.Serialize(_persistent, JsonOptions);
            using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            file.StoreString(json);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Failed to save contract state: {ex.Message}");
        }
    }

    public static void SetSelection(bool enabled, IEnumerable<string> selectedContracts, string characterId)
    {
        List<string> selected = selectedContracts.Distinct(StringComparer.Ordinal).ToList();
        _persistent.LastEnabled = enabled;
        _persistent.LastSelectedContracts = selected;
        _currentRun = new ContractRunState
        {
            Enabled = enabled,
            CharacterId = characterId,
            SelectedContracts = selected,
            RiskLevel = ContractSelectionEvaluator.CalculateRisk(selected),
            Effects = ContractEffectResolver.Resolve(selected)
        };

        ModLogger.Info(
            $"Selection updated. Enabled={enabled}, Character={characterId}, Risk={_currentRun.RiskLevel}, Contracts=[{string.Join(", ", selected)}]");
    }

    public static void ResetRun()
    {
        _currentRun = new ContractRunState();
    }

    public static void OnCombatStarted()
    {
        _currentRun.CurrentCombatTurn = 0;
        _currentRun.CardsPlayedThisTurn = 0;
        _currentRun.PreCombatAppliedThisFight = false;
    }

    public static void OnTurnStarted()
    {
        _currentRun.CurrentCombatTurn += 1;
        _currentRun.CardsPlayedThisTurn = 0;
    }

    public static void OnCardPlayed()
    {
        _currentRun.CardsPlayedThisTurn += 1;
    }

    public static bool MarkPreCombatApplied()
    {
        if (_currentRun.PreCombatAppliedThisFight)
        {
            return false;
        }

        _currentRun.PreCombatAppliedThisFight = true;
        return true;
    }

    public static void CommitRunResult(bool victory)
    {
        if (!_currentRun.Enabled)
        {
            return;
        }

        if (_currentRun.RiskLevel > _persistent.BestGlobalRisk)
        {
            _persistent.BestGlobalRisk = _currentRun.RiskLevel;
        }

        if (!_persistent.BestRiskByCharacter.TryGetValue(_currentRun.CharacterId, out int currentBest) || _currentRun.RiskLevel > currentBest)
        {
            _persistent.BestRiskByCharacter[_currentRun.CharacterId] = _currentRun.RiskLevel;
        }

        ModLogger.Info($"Run result committed. Victory={victory}, Risk={_currentRun.RiskLevel}, Character={_currentRun.CharacterId}");
        FlushPersistentState();
    }
}
