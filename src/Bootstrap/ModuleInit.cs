using System.Runtime.CompilerServices;
using CatastropheContract.Core;
using CatastropheContract.Core.Contracts;
using CatastropheContract.Core.State;
using CatastropheContract.Patches;

namespace CatastropheContract.Bootstrap;

internal static class ModuleInit
{
    private static bool _initialized;

    [ModuleInitializer]
    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        ModLogger.Info($"Module initializer booting Catastrophe Contract. Build={ModLogger.BuildMarker}");
        ContractDatabase.Initialize();
        ContractStateStore.LoadPersistentState();
        HarmonyBootstrap.Apply();
    }
}
