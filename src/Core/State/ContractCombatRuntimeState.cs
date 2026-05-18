using System;
using System.Collections.Generic;
using System.Linq;

namespace CatastropheContract.Core.State;

internal static class ContractCombatRuntimeState
{
    public const string BloodthirstyStatusId = "bloodthirsty";

    private sealed class EnemyRuntimeState
    {
        public int DebrisCoveredStacks { get; set; }
        public Dictionary<string, double> CustomStatusValues { get; } = new(StringComparer.Ordinal);
    }

    private static readonly Dictionary<object, EnemyRuntimeState> EnemyStates = new(ReferenceEqualityComparer.Instance);
    private static string? _lastProcessedDamageFingerprint;
    private static long _lastProcessedDamageTicks;

    public static bool HasTrackedDebrisCovered => EnemyStates.Count > 0;
    public static bool HasBloodthirstyStatuses => EnemyStates.Values.Any(state => state.CustomStatusValues.TryGetValue(BloodthirstyStatusId, out double value) && value > 0.001d);

    public static void ResetCombat()
    {
        EnemyStates.Clear();
        _lastProcessedDamageFingerprint = null;
        _lastProcessedDamageTicks = 0;
    }

    public static void RegisterDebrisCovered(object enemy, int stacks)
    {
        if (stacks <= 0)
        {
            return;
        }

        if (!EnemyStates.TryGetValue(enemy, out EnemyRuntimeState? state))
        {
            state = new EnemyRuntimeState();
            EnemyStates[enemy] = state;
        }

        state.DebrisCoveredStacks = Math.Max(state.DebrisCoveredStacks, stacks);
    }

    public static void SetCustomStatusValue(object enemy, string statusId, double value)
    {
        if (string.IsNullOrWhiteSpace(statusId))
        {
            return;
        }

        if (!EnemyStates.TryGetValue(enemy, out EnemyRuntimeState? state))
        {
            state = new EnemyRuntimeState();
            EnemyStates[enemy] = state;
        }

        if (value <= 0.001d)
        {
            state.CustomStatusValues.Remove(statusId);
            return;
        }

        state.CustomStatusValues[statusId] = value;
    }

    public static double GetCustomStatusValue(object enemy, string statusId)
    {
        return EnemyStates.TryGetValue(enemy, out EnemyRuntimeState? state)
            && state.CustomStatusValues.TryGetValue(statusId, out double value)
            ? value
            : 0d;
    }

    public static IEnumerable<object> FindEnemiesWithStatus(string statusId)
    {
        foreach ((object enemy, EnemyRuntimeState state) in EnemyStates)
        {
            if (state.CustomStatusValues.TryGetValue(statusId, out double value) && value > 0.001d)
            {
                yield return enemy;
            }
        }
    }

    public static int GetDebrisCoveredStacks(object enemy)
    {
        return EnemyStates.TryGetValue(enemy, out EnemyRuntimeState? state) ? state.DebrisCoveredStacks : 0;
    }

    public static bool TryConsumeDebrisCoveredOnHpLoss(object enemy, double hpLost, out int remainingStacks)
    {
        remainingStacks = 0;
        if (hpLost <= 0.001d || !EnemyStates.TryGetValue(enemy, out EnemyRuntimeState? state) || state.DebrisCoveredStacks <= 0)
        {
            return false;
        }

        state.DebrisCoveredStacks = Math.Max(0, state.DebrisCoveredStacks - 1);
        remainingStacks = state.DebrisCoveredStacks;
        return true;
    }

    public static bool MarkDamageEventProcessed(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return true;
        }

        long nowTicks = Environment.TickCount64;
        if (string.Equals(_lastProcessedDamageFingerprint, fingerprint, StringComparison.Ordinal)
            && nowTicks - _lastProcessedDamageTicks <= 50)
        {
            return false;
        }

        _lastProcessedDamageFingerprint = fingerprint;
        _lastProcessedDamageTicks = nowTicks;
        return true;
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
