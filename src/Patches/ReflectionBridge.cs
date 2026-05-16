using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CatastropheContract.Core;
using Godot;
using HarmonyLib;

namespace CatastropheContract.Patches;

internal static class ReflectionBridge
{
    private const string EmbarkBlockedPopupName = "CatastropheContractEmbarkBlockedDialog";
    private static readonly string[] CharacterIdPropertyNames = { "CharacterId", "SelectedCharacterId", "CurrentCharacterId" };
    private static readonly string[] CharacterIdFieldNames = { "_characterId", "_selectedCharacterId", "characterId" };
    private static readonly string[] CustomModifierMethodNames = { "GetModifiersTickedOn", "GetSelectedModifiers", "GetEnabledModifiers", "GetActiveModifiers" };
    private static readonly string[] AscensionLevelNames = { "AscensionLevel", "CurrentAscensionLevel", "SelectedAscensionLevel", "Ascension", "Level", "CurrentLevel", "Value" };
    private static readonly string[] AscensionFlagNames = { "IsAscensionEnabled", "AscensionEnabled", "IsAscensionModeEnabled", "AscensionModeEnabled", "Enabled", "IsOn", "ButtonPressed" };

    public static string? TryReadCharacterId(object instance)
    {
        Type type = instance.GetType();

        foreach (string propertyName in CharacterIdPropertyNames)
        {
            PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(instance) is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                return stringValue;
            }
        }

        foreach (string fieldName in CharacterIdFieldNames)
        {
            FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(instance) is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                return stringValue;
            }
        }

        if (instance is Node node)
        {
            foreach (Node candidate in EnumerateAncestors(node).Prepend(node))
            {
                string? nestedCharacterId = TryReadCharacterIdFromObject(candidate);
                if (!string.IsNullOrWhiteSpace(nestedCharacterId))
                {
                    return nestedCharacterId;
                }
            }
        }

        return null;
    }

    public static bool TryBuildEmbarkConflictMessage(object instance, out string message)
    {
        List<string> conflicts = new();
        object contextRoot = ResolveEmbarkValidationContext(instance);

        int? ascensionLevel = TryReadAscensionLevel(contextRoot);
        if (ascensionLevel is int concreteAscensionLevel && concreteAscensionLevel > 0)
        {
            conflicts.Add($"已启用 Ascension {concreteAscensionLevel}");
        }

        IReadOnlyList<string> vanillaModifiers = TryReadEnabledVanillaModifiers(contextRoot);
        if (vanillaModifiers.Count > 0)
        {
            conflicts.Add($"已启用原版自定义词条：{string.Join("、", vanillaModifiers)}");
        }

        if (conflicts.Count == 0)
        {
            message = string.Empty;
            return false;
        }

        message = "天灾合约不能与以下选项同时启用：\n- "
            + string.Join("\n- ", conflicts)
            + "\n\n请先关闭这些选项后再开始。";
        return true;
    }

    public static void ShowEmbarkBlockedPopup(object instance, string message)
    {
        Node? root = instance as Node;
        if (root == null && instance is not Node)
        {
            ModLogger.Warn($"Embark validation blocked start but could not show popup: instance type was {instance.GetType().FullName}.");
            return;
        }

        try
        {
            Node popupRoot = (root?.GetTree()?.Root) ?? root!;
            AcceptDialog dialog;
            if (popupRoot.FindChild(EmbarkBlockedPopupName, true, false) is AcceptDialog existingDialog)
            {
                dialog = existingDialog;
            }
            else
            {
                dialog = new AcceptDialog
                {
                    Name = EmbarkBlockedPopupName,
                    Title = "Catastrophe Contract"
                };
                popupRoot.AddChild(dialog);
            }

            dialog.DialogText = message;
            dialog.PopupCentered(new Vector2I(760, 240));
            dialog.GrabFocus();

            string flattenedMessage = message.Replace(System.Environment.NewLine, " ").Replace('\n', ' ');
            ModLogger.Warn($"Blocked run start due to conflict: {flattenedMessage}");
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Failed to show embark conflict popup: {ex.Message}");
        }
    }

    private static string? TryReadCharacterIdFromObject(object instance)
    {
        Type type = instance.GetType();

        foreach (string propertyName in CharacterIdPropertyNames)
        {
            PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(instance) is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                return stringValue;
            }
        }

        foreach (string fieldName in CharacterIdFieldNames)
        {
            FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(instance) is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                return stringValue;
            }
        }

        return null;
    }

    private static int? TryReadAscensionLevel(object instance)
    {
        foreach (object candidate in EnumerateCandidates(instance))
        {
            string typeName = candidate.GetType().FullName ?? string.Empty;
            if (!typeName.Contains("Ascension", StringComparison.OrdinalIgnoreCase))
            {
                int? directLevel = TryGetIntByNames(candidate, "AscensionLevel", "CurrentAscensionLevel", "SelectedAscensionLevel");
                if (directLevel.GetValueOrDefault() > 0)
                {
                    return directLevel;
                }

                continue;
            }

            int? level = TryGetIntByNames(candidate, AscensionLevelNames);
            if (level.GetValueOrDefault() > 0)
            {
                return level;
            }

            bool? enabled = TryGetBoolByNames(candidate, AscensionFlagNames);
            if (enabled == true)
            {
                return 1;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> TryReadEnabledVanillaModifiers(object instance)
    {
        HashSet<string> modifiers = new(StringComparer.Ordinal);

        foreach (object candidate in EnumerateCandidates(instance))
        {
            IEnumerable<object> selectedModifiers = TryInvokeSelectedModifierMethod(candidate);
            foreach (object modifier in selectedModifiers)
            {
                modifiers.Add(DescribeModifier(modifier));
            }
        }

        if (modifiers.Count > 0)
        {
            return modifiers.OrderBy(name => name, StringComparer.Ordinal).ToList();
        }

        if (ResolveEmbarkValidationContext(instance) is not Node rootNode)
        {
            return Array.Empty<string>();
        }

        foreach (Node node in TraverseNodes(rootNode))
        {
            string typeName = node.GetType().FullName ?? string.Empty;
            if (!typeName.Contains("NRunModifierTickbox", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool isSelected = TryGetBoolByNames(node, "ButtonPressed", "Pressed", "TickedOn", "Selected", "IsSelected") == true;
            if (!isSelected)
            {
                continue;
            }

            modifiers.Add(DescribeModifier(node));
        }

        return modifiers.OrderBy(name => name, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<object> TryInvokeSelectedModifierMethod(object candidate)
    {
        Type type = candidate.GetType();
        foreach (string methodName in CustomModifierMethodNames)
        {
            MethodInfo? method = AccessTools.Method(type, methodName, Type.EmptyTypes);
            if (method == null)
            {
                continue;
            }

            object? result;
            try
            {
                result = method.Invoke(candidate, null);
            }
            catch
            {
                continue;
            }

            if (result is not IEnumerable enumerable)
            {
                continue;
            }

            foreach (object? item in enumerable)
            {
                if (item != null)
                {
                    yield return item;
                }
            }
        }
    }

    private static IEnumerable<object> EnumerateCandidates(object root)
    {
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        foreach (object anchor in EnumerateAnchors(root))
        {
            foreach (object candidate in EnumerateCandidatesRecursive(anchor, visited, 0))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<object> EnumerateAnchors(object root)
    {
        yield return root;

        if (root is not Node node)
        {
            yield break;
        }

        foreach (Node ancestor in EnumerateAncestors(node))
        {
            yield return ancestor;
        }
    }

    private static IEnumerable<object> EnumerateCandidatesRecursive(object? current, HashSet<object> visited, int depth)
    {
        if (current == null || depth > 4 || !visited.Add(current))
        {
            yield break;
        }

        yield return current;

        if (current is Node node)
        {
            foreach (Node child in node.GetChildren().OfType<Node>())
            {
                foreach (object candidate in EnumerateCandidatesRecursive(child, visited, depth + 1))
                {
                    yield return candidate;
                }
            }
        }

        foreach (string memberName in new[]
        {
            "CustomRunScreen", "_customRunScreen", "AscensionPanel", "_ascensionPanel", "CustomRunModifiersList", "_customRunModifiersList",
            "Screen", "_screen", "State", "_state", "ViewModel", "_viewModel"
        })
        {
            object? nested = TryGetMemberValue(current, memberName);
            foreach (object candidate in EnumerateCandidatesRecursive(nested, visited, depth + 1))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<Node> TraverseNodes(Node root)
    {
        yield return root;
        foreach (Node child in root.GetChildren().OfType<Node>())
        {
            foreach (Node nested in TraverseNodes(child))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<Node> EnumerateAncestors(Node node)
    {
        Node? current = node.GetParent();
        while (current != null)
        {
            yield return current;
            current = current.GetParent();
        }
    }

    private static object ResolveEmbarkValidationContext(object instance)
    {
        if (instance is not Node node)
        {
            return instance;
        }

        foreach (Node candidate in EnumerateAncestors(node).Prepend(node))
        {
            string fullName = candidate.GetType().FullName ?? string.Empty;
            if (fullName.Contains("NCustomRunScreen", StringComparison.Ordinal) ||
                fullName.Contains("NCharacterSelectScreen", StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return node.GetTree()?.CurrentScene ?? node.GetTree()?.Root ?? node;
    }

    private static int? TryGetIntByNames(object candidate, params string[] names)
    {
        foreach (string name in names)
        {
            object? value = TryGetMemberValue(candidate, name);
            if (value == null)
            {
                continue;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
            }
        }

        return null;
    }

    private static bool? TryGetBoolByNames(object candidate, params string[] names)
    {
        foreach (string name in names)
        {
            object? value = TryGetMemberValue(candidate, name);
            if (value == null)
            {
                continue;
            }

            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
            }
        }

        return null;
    }

    private static object? TryGetMemberValue(object candidate, string name)
    {
        Type type = candidate.GetType();

        PropertyInfo? property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            try
            {
                return property.GetValue(candidate);
            }
            catch
            {
            }
        }

        FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            try
            {
                return field.GetValue(candidate);
            }
            catch
            {
            }
        }

        return null;
    }

    private static string DescribeModifier(object modifier)
    {
        foreach (string name in new[] { "DisplayName", "Name", "EnglishName", "Title", "Text", "ButtonText" })
        {
            object? value = TryGetMemberValue(modifier, name);
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        if (modifier is Node node && !string.IsNullOrWhiteSpace(node.Name))
        {
            return node.Name;
        }

        string typeName = modifier.GetType().Name;
        return typeName.StartsWith("N", StringComparison.Ordinal) ? typeName[1..] : typeName;
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
