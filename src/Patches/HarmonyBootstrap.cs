using HarmonyLib;

namespace CatastropheContract.Patches;

public static class HarmonyBootstrap
{
    private static bool _applied;

    public static void Apply()
    {
        if (_applied)
        {
            return;
        }

        _applied = true;
        Harmony harmony = new("CatastropheContract");
        harmony.PatchAll(typeof(HarmonyBootstrap).Assembly);
    }
}
