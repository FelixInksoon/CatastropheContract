using System;
using System.Collections.Generic;
using System.Linq;
using CatastropheContract.Core;
using CatastropheContract.Core.UI;
using Godot;
using HarmonyLib;

namespace CatastropheContract.Patches;

[HarmonyPatch]
public static class CustomRunScreenPatch
{
    private static readonly ContractPanelViewModel ViewModel = new();
    private const string PanelNodeName = "CatastropheContractPanel";
    private static readonly HashSet<string> LoggedNodeNames = new(StringComparer.Ordinal);

    static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        Type? type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.CustomRun.NCustomRunScreen");
        if (type == null)
        {
            yield break;
        }

        System.Reflection.MethodInfo? ready = AccessTools.Method(type, "_Ready");
        if (ready != null)
        {
            yield return ready;
        }
    }

    static void Postfix(object __instance)
    {
        if (__instance is Node node)
        {
            TryInjectPanel(node);
        }
    }

    public static void TryInjectIntoTree(Node root)
    {
        foreach (Node node in Traverse(root))
        {
            LogInterestingNode(node);
            TryInjectPanel(node);
        }
    }

    public static void TryInjectPanel(Node node)
    {
        try
        {
            string? fullName = node.GetType().FullName;
            if (fullName == null || !fullName.Contains("NCustomRunScreen", StringComparison.Ordinal))
            {
                return;
            }

            if (node.HasNode(PanelNodeName))
            {
                return;
            }

            ContractPanelNode panel = new();
            panel.Name = PanelNodeName;
            node.AddChild(panel);

            panel.Position = new Vector2(24, 104);
            panel.ZIndex = 100;
            panel.Bind(ViewModel);

            ModLogger.Info("Injected Catastrophe Contract panel into custom run screen.");
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Failed to inject Catastrophe Contract panel: {ex.Message}");
        }
    }

    private static void LogInterestingNode(Node node)
    {
        string? fullName = node.GetType().FullName;
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return;
        }

        if (!fullName.Contains("CustomRun", StringComparison.OrdinalIgnoreCase) &&
            !fullName.Contains("CharacterSelect", StringComparison.OrdinalIgnoreCase) &&
            !fullName.Contains("MainMenu", StringComparison.OrdinalIgnoreCase) &&
            !fullName.Contains("Screen", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!LoggedNodeNames.Add(fullName))
        {
            return;
        }

        ModLogger.Debug($"Observed scene node type: {fullName}");
    }

    private static IEnumerable<Node> Traverse(Node root)
    {
        yield return root;

        foreach (Node child in root.GetChildren().OfType<Node>())
        {
            foreach (Node nested in Traverse(child))
            {
                yield return nested;
            }
        }
    }
}
