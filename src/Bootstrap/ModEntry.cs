using System;
using CatastropheContract.Core;
using CatastropheContract.Core.Contracts;
using CatastropheContract.Core.State;
using CatastropheContract.Patches;
using Godot;

namespace CatastropheContract.Bootstrap;

public partial class ModEntry : Node
{
    private const string InitializedKey = "CatastropheContract.Initialized";
    private static bool _initialized;

    public override void _EnterTree()
    {
        if (_initialized || AppDomain.CurrentDomain.GetData(InitializedKey) is true)
        {
            return;
        }

        _initialized = true;
        AppDomain.CurrentDomain.SetData(InitializedKey, true);
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
