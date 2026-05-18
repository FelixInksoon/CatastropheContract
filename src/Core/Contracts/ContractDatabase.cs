using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CatastropheContract.Core.Contracts;

public static class ContractDatabase
{
    private static readonly Dictionary<string, ContractDefinition> DefinitionsById = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ContractGroupDefinition> GroupsById = new(StringComparer.Ordinal);
    private static readonly List<ContractCategoryMetadata> Categories = new();
    private static bool _initialized;

    public static ReadOnlyCollection<ContractDefinition> AllContracts =>
        DefinitionsById.Values
            .OrderBy(c => (int)c.Category)
            .ThenBy(c => c.GroupId)
            .ThenBy(c => c.RiskValue)
            .ToList()
            .AsReadOnly();

    public static ReadOnlyCollection<ContractGroupDefinition> AllGroups =>
        GroupsById.Values
            .OrderBy(c => Categories.First(meta => meta.Category == c.Category).SortOrder)
            .ThenBy(c => c.Node.Column)
            .ThenBy(c => c.Node.Row)
            .ThenBy(c => c.Id)
            .ToList()
            .AsReadOnly();

    public static ReadOnlyCollection<ContractCategoryMetadata> AllCategories =>
        Categories
            .OrderBy(category => category.SortOrder)
            .ToList()
            .AsReadOnly();

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        SeedCategories();
        SeedGroups();
        ModLogger.Info($"Loaded {DefinitionsById.Count} contract levels across {GroupsById.Count} groups.");
    }

    public static ContractDefinition? TryGet(string id)
    {
        DefinitionsById.TryGetValue(id, out ContractDefinition? value);
        return value;
    }

    public static ContractGroupDefinition? TryGetGroup(string groupId)
    {
        GroupsById.TryGetValue(groupId, out ContractGroupDefinition? value);
        return value;
    }

    public static IReadOnlyList<ContractGroupDefinition> GetGroupsByCategory(ContractCategory category)
    {
        return GroupsById.Values
            .Where(group => group.Category == category)
            .OrderBy(group => group.Node.Column)
            .ThenBy(group => group.Node.Row)
            .ThenBy(group => group.Id)
            .ToList();
    }

    private static void SeedCategories()
    {
        Categories.Add(new ContractCategoryMetadata(ContractCategory.EnemyForce, "腐化", "Corruption", "提升全体敌方属性", 0));
        Categories.Add(new ContractCategoryMetadata(ContractCategory.BossPressure, "觉醒", "Awakening", "提升特化精英敌人", 1));
        Categories.Add(new ContractCategoryMetadata(ContractCategory.PlayerConstraint, "侵蚀", "Erosion", "削弱全体己方属性", 2));
        Categories.Add(new ContractCategoryMetadata(ContractCategory.RuleMutation, "裂痕", "Crack", "削弱特化己方选择", 3));
        Categories.Add(new ContractCategoryMetadata(ContractCategory.EconomyRoute, "坍缩", "Collapse", "改变全局作战环境", 4));
        Categories.Add(new ContractCategoryMetadata(ContractCategory.SpecialRule, "悖论", "Paradox", "改变作战核心规则", 5));
    }

    private static void SeedGroups()
    {
        AddGroup("bloodthirsty", "嗜血", "Bloodthirsty", ContractCategory.EnemyForce, "敌人造成未被格挡的伤害时，回复生命。", Node(0, 0), true, ContractApplyPhase.PreCombat,
            Tier("I", 1, "敌人造成未被格挡的伤害时，回复该伤害值 25% 的生命值", true, Fx(ContractEffectKind.EnemyUnblockedDamageLifestealPercent, 25)),
            Tier("II", 2, "敌人造成未被格挡的伤害时，回复该伤害值 50% 的生命值", true, Fx(ContractEffectKind.EnemyUnblockedDamageLifestealPercent, 50)),
            Tier("III", 3, "敌人造成未被格挡的伤害时，回复该伤害值 100% 的生命值", true, Fx(ContractEffectKind.EnemyUnblockedDamageLifestealPercent, 100)));

        AddGroup("thorn", "荆棘", "Thorn", ContractCategory.EnemyForce, "每场战斗开始时，所有敌人获得荆棘。", Node(0, 1), true, ContractApplyPhase.PreCombat,
            Tier("I", 1, "每场战斗开始时，所有敌人获得 2 层荆棘", true, Fx(ContractEffectKind.EnemyStartWithThorns, 2)),
            Tier("II", 2, "每场战斗开始时，所有敌人获得 4 层荆棘", true, Fx(ContractEffectKind.EnemyStartWithThorns, 4)),
            Tier("III", 3, "每场战斗开始时，所有敌人获得 6 层荆棘", true, Fx(ContractEffectKind.EnemyStartWithThorns, 6)));

        AddGroup("great_awakening", "大觉醒", "Great Awakening", ContractCategory.EnemyForce, "每场战斗开始时，所有敌人获得力量。", Node(0, 2), true, ContractApplyPhase.PreCombat,
            Tier("I", 1, "每场战斗开始时，所有敌人获得 2 点力量", true, Fx(ContractEffectKind.EnemyStartWithStrength, 2)),
            Tier("II", 2, "每场战斗开始时，所有敌人获得 4 点力量", true, Fx(ContractEffectKind.EnemyStartWithStrength, 4)),
            Tier("III", 3, "每场战斗开始时，所有敌人获得 6 点力量", true, Fx(ContractEffectKind.EnemyStartWithStrength, 6)));

        AddGroup("activating", "活性化", "Activating", ContractCategory.EnemyForce, "所有敌人生命值上限提升。", Node(1, 0), true, ContractApplyPhase.PreCombat,
            Tier("I", 1, "所有敌人生命值上限提升 30%", true, Fx(ContractEffectKind.EnemyMaxHpPercent, 30)),
            Tier("II", 2, "所有敌人生命值上限提升 50%", true, Fx(ContractEffectKind.EnemyMaxHpPercent, 50)),
            Tier("III", 3, "所有敌人生命值上限提升 100%", true, Fx(ContractEffectKind.EnemyMaxHpPercent, 100)));

        AddGroup("metallization", "金属化", "Metallization", ContractCategory.EnemyForce, "每场战斗开始时，所有敌人获得格挡。", Node(1, 1), true, ContractApplyPhase.PreCombat,
            Tier("I", 1, "每场战斗开始时，所有敌人获得 6 点格挡", true, Fx(ContractEffectKind.EnemyStartWithBlock, 6)),
            Tier("II", 2, "每场战斗开始时，所有敌人获得 12 点格挡", true, Fx(ContractEffectKind.EnemyStartWithBlock, 12)),
            Tier("III", 3, "每场战斗开始时，所有敌人获得 18 点格挡", true, Fx(ContractEffectKind.EnemyStartWithBlock, 18)));

        AddGroup("debris_covered", "残骸裹身", "Debris Covered", ContractCategory.EnemyForce, "每场战斗开始时，所有敌人获得覆甲。", Node(1, 2), true, ContractApplyPhase.PreCombat,
            Tier("I", 1, "每场战斗开始时，所有敌人获得 6 层覆甲", true, Fx(ContractEffectKind.EnemyStartWithPlatedArmor, 6)),
            Tier("II", 2, "每场战斗开始时，所有敌人获得 12 层覆甲", true, Fx(ContractEffectKind.EnemyStartWithPlatedArmor, 12)),
            Tier("III", 3, "每场战斗开始时，所有敌人获得 18 层覆甲", true, Fx(ContractEffectKind.EnemyStartWithPlatedArmor, 18)));

        AddGroup("industrialization", "工业化", "Industrialization", ContractCategory.EnemyForce, "每场战斗开始时，所有敌人获得人工制品。", Node(1, 3), true, ContractApplyPhase.PreCombat,
            Tier("I", 1, "每场战斗开始时，所有敌人获得 2 层人工制品", true, Fx(ContractEffectKind.EnemyStartWithArtifact, 2)),
            Tier("II", 2, "每场战斗开始时，所有敌人获得 4 层人工制品", true, Fx(ContractEffectKind.EnemyStartWithArtifact, 4)),
            Tier("III", 3, "每场战斗开始时，所有敌人获得 6 层人工制品", true, Fx(ContractEffectKind.EnemyStartWithArtifact, 6)));

        AddGroup("swarming_elites", "精英蜂拥", "Swarming Elites", ContractCategory.BossPressure, "精英出现频率大幅提升。", Node(2, 0), false, ContractApplyPhase.MapGeneration,
            Tier("I", 2, "精英出现频率大幅提升（普通:精英 = 2:1）", false, Fx(ContractEffectKind.EliteEncounterRatio, 2)),
            Tier("II", 3, "精英出现频率大幅提升（普通:精英 = 1:1）", false, Fx(ContractEffectKind.EliteEncounterRatio, 1)),
            Tier("III", 4, "精英出现频率大幅提升（仅精英）", false, Fx(ContractEffectKind.EliteEncounterRatio, 0)));

        AddGroup("congregating_bosses", "群贤毕至", "Congregating Bosses", ContractCategory.BossPressure, "每个阶段需要击败更多 Boss。", Node(2, 1), false, ContractApplyPhase.BossOnly,
            Tier("I", 3, "每个阶段需要击败 2 个 Boss", false, Fx(ContractEffectKind.BossesRequiredPerAct, 2)),
            Tier("II", 5, "每个阶段需要击败 3 个 Boss", false, Fx(ContractEffectKind.BossesRequiredPerAct, 3)));

        AddGroup("erosion", "侵蚀", "Erosion", ContractCategory.PlayerConstraint, "游戏开始时减少最大生命值。", Node(3, 0), true, ContractApplyPhase.RunStart,
            Tier("I", 1, "游戏开始时减少 15% 最大生命值", true, Fx(ContractEffectKind.PlayerMaxHpLossPercent, 15)),
            Tier("II", 2, "游戏开始时减少 30% 最大生命值", true, Fx(ContractEffectKind.PlayerMaxHpLossPercent, 30)),
            Tier("III", 4, "游戏开始时减少 60% 最大生命值", true, Fx(ContractEffectKind.PlayerMaxHpLossPercent, 60)));

        AddGroup("burning", "灼伤", "Burning", ContractCategory.PlayerConstraint, "每场战斗开始时，获得脆弱和虚弱。", Node(3, 1), true, ContractApplyPhase.PreCombat,
            Tier("I", 1, "每场战斗开始时，获得 2 层脆弱和虚弱", true, Fx(ContractEffectKind.PlayerStartWithWeak, 2), Fx(ContractEffectKind.PlayerStartWithFrail, 2)),
            Tier("II", 2, "每场战斗开始时，获得 4 层脆弱和虚弱", true, Fx(ContractEffectKind.PlayerStartWithWeak, 4), Fx(ContractEffectKind.PlayerStartWithFrail, 4)),
            Tier("III", 3, "每场战斗开始时，获得 6 层脆弱和虚弱", true, Fx(ContractEffectKind.PlayerStartWithWeak, 6), Fx(ContractEffectKind.PlayerStartWithFrail, 6)));

        AddGroup("secret_battle", "隐秘作战", "Secret Battle", ContractCategory.PlayerConstraint, "每场战斗开始时获得负敏捷。", Node(3, 2), true, ContractApplyPhase.PreCombat,
            Tier("I", 1, "每场战斗开始时获得 -2 敏捷", true, Fx(ContractEffectKind.PlayerStartWithDexterityPenalty, 2)),
            Tier("II", 2, "每场战斗开始时获得 -4 敏捷", true, Fx(ContractEffectKind.PlayerStartWithDexterityPenalty, 4)),
            Tier("III", 3, "每场战斗开始时获得 -6 敏捷", true, Fx(ContractEffectKind.PlayerStartWithDexterityPenalty, 6)));

        AddGroup("high_valued_object", "高价值目标", "High Valued Object", ContractCategory.PlayerConstraint, "每场战斗开始时获得负力量。", Node(3, 3), true, ContractApplyPhase.PreCombat,
            Tier("I", 1, "每场战斗开始时获得 -2 力量", true, Fx(ContractEffectKind.PlayerStartWithStrengthPenalty, 2)),
            Tier("II", 2, "每场战斗开始时获得 -4 力量", true, Fx(ContractEffectKind.PlayerStartWithStrengthPenalty, 4)),
            Tier("III", 3, "每场战斗开始时获得 -6 力量", true, Fx(ContractEffectKind.PlayerStartWithStrengthPenalty, 6)));

        AddGroup("malaise", "萎靡", "Malaise", ContractCategory.RuleMutation, "所有 X 费效果牌变为 X-1 / X-2 / X-3。", Node(4, 0), false, ContractApplyPhase.TurnRule,
            Tier("I", 2, "所有 X 费效果牌变为 X-1", false, Fx(ContractEffectKind.XCostCardReduction, 1)),
            Tier("II", 3, "所有 X 费效果牌变为 X-2", false, Fx(ContractEffectKind.XCostCardReduction, 2)),
            Tier("III", 4, "所有 X 费效果牌变为 X-3", false, Fx(ContractEffectKind.XCostCardReduction, 3)));

        AddGroup("restriction", "束缚", "Restriction", ContractCategory.RuleMutation, "你获得的所有牌附带永恒。", Node(4, 1), false, ContractApplyPhase.TurnRule,
            Tier("I", 3, "你获得的所有牌附带永恒", false, Fx(ContractEffectKind.AllGainedCardsAreEthereal, 1)));

        AddGroup("secret_action", "隐秘行动", "Secret Action", ContractCategory.RuleMutation, "牌库上限锁定。", Node(4, 2), false, ContractApplyPhase.RunStart,
            Tier("I", 1, "牌库上限锁定为 40（多余部分不会进入牌库）", false, Fx(ContractEffectKind.DeckSizeCap, 40)),
            Tier("II", 2, "牌库上限锁定为 30（多余部分不会进入牌库）", false, Fx(ContractEffectKind.DeckSizeCap, 30)),
            Tier("III", 3, "牌库上限锁定为 20（多余部分不会进入牌库）", false, Fx(ContractEffectKind.DeckSizeCap, 20)));

        AddGroup("inefficiency", "效率低下", "Inefficiency", ContractCategory.RuleMutation, "特定回合只能打出一张牌。", Node(4, 3), false, ContractApplyPhase.TurnRule,
            Tier("I", 2, "每 4 回合时，该回合只能打出一张牌", false, Fx(ContractEffectKind.LimitedTurnOneCardOnlyFrequency, 4)),
            Tier("II", 3, "每 3 回合时，该回合只能打出一张牌", false, Fx(ContractEffectKind.LimitedTurnOneCardOnlyFrequency, 3)),
            Tier("III", 4, "每 2 回合时，该回合只能打出一张牌", false, Fx(ContractEffectKind.LimitedTurnOneCardOnlyFrequency, 2)));

        AddGroup("antidetection", "反侦察", "Antidetection", ContractCategory.EconomyRoute, "你无法再看见敌人的意图。", Node(5, 0), false, ContractApplyPhase.PreCombat,
            Tier("I", 3, "你无法再看见敌人的意图", false, Fx(ContractEffectKind.HideEnemyIntent, 1)));

        AddGroup("linear_battlefield", "线性战场", "Linear Battlefield", ContractCategory.EconomyRoute, "每阶段地图变为一条直线。", Node(5, 1), false, ContractApplyPhase.MapGeneration,
            Tier("I", 3, "每阶段地图变为一条直线", false, Fx(ContractEffectKind.LinearMap, 1)));

        AddGroup("economic_crisis", "经济危机", "Economic Crisis", ContractCategory.EconomyRoute, "金币获取减少。", Node(5, 2), true, ContractApplyPhase.RewardEconomy,
            Tier("I", 1, "金币获取减少 25%", true, Fx(ContractEffectKind.GoldGainPercentPenalty, 25)),
            Tier("II", 2, "金币获取减少 50%", true, Fx(ContractEffectKind.GoldGainPercentPenalty, 50)),
            Tier("III", 3, "金币获取减少 75%", true, Fx(ContractEffectKind.GoldGainPercentPenalty, 75)));

        AddGroup("run_out", "弹尽粮绝", "Run Out", ContractCategory.EconomyRoute, "你无法通过任何手段恢复生命值。", Node(5, 3), true, ContractApplyPhase.RunStart,
            Tier("I", 4, "你无法通过任何手段恢复生命值", true, Fx(ContractEffectKind.NoHealing, 1)));

        AddGroup("tightened_belt", "收紧腰带", "Tightened Belt", ContractCategory.EconomyRoute, "药水槽位锁定，且无法增加。", Node(5, 4), true, ContractApplyPhase.RunStart,
            Tier("I", 1, "药水槽位锁定为 2，无法增加", true, Fx(ContractEffectKind.PotionSlotCap, 2)),
            Tier("II", 2, "药水槽位锁定为 1，无法增加", true, Fx(ContractEffectKind.PotionSlotCap, 1)),
            Tier("III", 3, "药水槽位锁定为 0，无法增加", true, Fx(ContractEffectKind.PotionSlotCap, 0)));

        AddGroup("ultimate_defense", "最终防线", "Ultimate Defense", ContractCategory.SpecialRule, "最大生命值锁定为 1。", Node(6, 0), true, ContractApplyPhase.RunStart,
            Tier("I", 6, "最大生命值锁定为 1，无法通过任何手段提升最大生命值", true, Fx(ContractEffectKind.MaxHpLockedToOne, 1)));

        AddGroup("countdown", "倒计时", "Countdown", ContractCategory.SpecialRule, "达到指定回合后强制死亡。", Node(6, 1), true, ContractApplyPhase.TurnRule,
            Tier("I", 3, "第 8 回合及之后强制死亡", true, Fx(ContractEffectKind.DeathCountdownTurn, 8)),
            Tier("II", 5, "第 6 回合及之后强制死亡", true, Fx(ContractEffectKind.DeathCountdownTurn, 6)),
            Tier("III", 7, "第 4 回合及之后强制死亡", true, Fx(ContractEffectKind.DeathCountdownTurn, 4)));

        AddGroup("counterforce", "反作用力", "Counterforce", ContractCategory.SpecialRule, "待补充文本。", Node(6, 2), false, ContractApplyPhase.TurnRule,
            Tier("I", 4, "待补充文本", false));
    }

    private static ContractNodeMetadata Node(int column, int row, string? parentGroupId = null, bool isLeaf = true, bool showConnector = false, params string[] prerequisites)
    {
        return new ContractNodeMetadata(column, row, parentGroupId, isLeaf, showConnector, prerequisites);
    }

    private static ContractDefinition Tier(string levelLabel, int riskValue, string summary, bool isImplemented, params ContractEffect[] effects)
    {
        return new ContractDefinition(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            levelLabel,
            default,
            riskValue,
            summary,
            default,
            isImplemented,
            effects,
            isImplemented ? "已实现" : "未实装");
    }

    private static ContractEffect Fx(ContractEffectKind kind, double value)
    {
        return new ContractEffect(kind, value, string.Empty);
    }

    private static void AddGroup(
        string groupId,
        string displayName,
        string englishName,
        ContractCategory category,
        string summary,
        ContractNodeMetadata node,
        bool isImplemented,
        ContractApplyPhase applyPhase,
        params ContractDefinition[] tiers)
    {
        List<ContractDefinition> resolved = new();

        foreach (ContractDefinition tier in tiers)
        {
            ContractDefinition definition = tier with
            {
                Id = $"{groupId}_{tier.LevelLabel.ToLowerInvariant()}",
                GroupId = groupId,
                InternalName = $"{groupId}_{tier.LevelLabel.ToLowerInvariant()}",
                DisplayName = displayName,
                EnglishName = englishName,
                Category = category,
                ApplyPhase = applyPhase
            };

            DefinitionsById.Add(definition.Id, definition);
            resolved.Add(definition);
        }

        GroupsById.Add(groupId, new ContractGroupDefinition(groupId, displayName, englishName, category, summary, node, isImplemented, resolved));
    }
}
