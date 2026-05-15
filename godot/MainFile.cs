using CatastropheContract.Core;
using CatastropheContract.Core.Contracts;
using CatastropheContract.Core.State;
using CatastropheContract.Patches;
using Godot;

namespace CatastropheContract;

public partial class MainFile : Node
{
    private static bool _initialized;
    private double _scanCooldown;
    private Label? _debugOverlay;
    private string _lastOverlayText = "CC: boot";

    public override void _EnterTree()
    {
        EnsureInitialized();
        SetProcess(true);
        ModLogger.Info("MainFile entered tree.");
        EnsureDebugOverlay();
        TryInjectIntoTree();
    }

    public override void _ExitTree()
    {
        ModLogger.Info("MainFile exiting tree.");
        ContractStateStore.FlushPersistentState();
    }

    public override void _Process(double delta)
    {
        _scanCooldown -= delta;
        if (_scanCooldown > 0)
        {
            return;
        }

        _scanCooldown = 0.5d;
        EnsureDebugOverlay();
        TryInjectIntoTree();
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        ModLogger.Info("MainFile booting Catastrophe Contract.");
        ContractDatabase.Initialize();
        ContractStateStore.LoadPersistentState();
        HarmonyBootstrap.Apply();
    }

    private void TryInjectIntoTree()
    {
        SceneTree? tree = GetTree();
        if (tree == null)
        {
            UpdateOverlay("CC: no tree");
            return;
        }

        string currentSceneName = tree.CurrentScene?.GetType().FullName ?? "<null>";
        UpdateOverlay($"CC: scene {currentSceneName}");

        if (tree.CurrentScene != null)
        {
            CustomRunScreenPatch.TryInjectIntoTree(tree.CurrentScene);
        }

        CustomRunScreenPatch.TryInjectIntoTree(tree.Root);
    }

    private void EnsureDebugOverlay()
    {
        if (_debugOverlay != null && IsInstanceValid(_debugOverlay))
        {
            if (_debugOverlay.GetParent() == null && GetTree()?.Root != null)
            {
                GetTree().Root.AddChild(_debugOverlay);
            }

            return;
        }

        SceneTree? tree = GetTree();
        if (tree?.Root == null)
        {
            return;
        }

        _debugOverlay = new Label
        {
            Name = "CatastropheContractDebugOverlay",
            Text = _lastOverlayText,
            Visible = true,
            ZIndex = 10000,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = new Vector2(12, 12)
        };
        _debugOverlay.AddThemeFontSizeOverride("font_size", 22);
        _debugOverlay.AddThemeColorOverride("font_color", new Color(1.0f, 0.88f, 0.45f));
        _debugOverlay.AddThemeColorOverride("font_outline_color", Colors.Black);
        _debugOverlay.AddThemeConstantOverride("outline_size", 4);
        tree.Root.AddChild(_debugOverlay);
        ModLogger.Info("Debug overlay attached to scene root.");
    }

    private void UpdateOverlay(string text)
    {
        _lastOverlayText = text;
        if (_debugOverlay != null && IsInstanceValid(_debugOverlay))
        {
            _debugOverlay.Text = text;
        }
    }
}
