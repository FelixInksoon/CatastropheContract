using CatastropheContract.Core;
using CatastropheContract.Core.Contracts;
using CatastropheContract.Core.State;
using CatastropheContract.Patches;
using Godot;

namespace CatastropheContract.Bootstrap;

public partial class ModEntry : Node
{
    private static bool _initialized;

    public override void _EnterTree()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        ModLogger.Info("Node entry booting Catastrophe Contract.");
        ContractDatabase.Initialize();
        ContractStateStore.LoadPersistentState();
        HarmonyBootstrap.Apply();
    }

    public override void _ExitTree()
    {
        ContractStateStore.FlushPersistentState();
    }
}
