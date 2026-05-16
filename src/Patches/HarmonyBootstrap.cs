using System;
using HarmonyLib;

namespace CatastropheContract.Patches;

public static class HarmonyBootstrap
{
    private const string AppliedKey = "CatastropheContract.HarmonyApplied";
    private static bool _applied;

    public static void Apply()
    {
        if (_applied || AppDomain.CurrentDomain.GetData(AppliedKey) is true)
        {
            return;
        }

        _applied = true;
        AppDomain.CurrentDomain.SetData(AppliedKey, true);
        Harmony harmony = new("CatastropheContract");
        harmony.PatchAll(typeof(HarmonyBootstrap).Assembly);
    }
}
