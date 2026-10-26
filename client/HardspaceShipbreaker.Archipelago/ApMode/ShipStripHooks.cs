#nullable disable
using System;
using System.Reflection;
using BBI.Unity.Game;
using HarmonyLib;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class ShipStripHooks
{
    public static void Apply(Harmony harmony)
    {
        PatchSafe(harmony, "ShipPreview.CreateAsync",
            () => AccessTools.Method(typeof(ShipPreview), nameof(ShipPreview.CreateAsync)),
            prefix: nameof(CreateAsyncPrefix));
        PatchSafe(harmony, "TotalSalvageValueCalculationSystem.OnUpdate",
            () => typeof(TotalSalvageValueCalculationSystem).GetMethod("OnUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly),
            postfix: nameof(TotalSalvageOnUpdatePostfix));
    }

    private static void PatchSafe(Harmony harmony, string label, Func<MethodInfo> find, string prefix = null, string postfix = null)
    {
        try
        {
            var target = find();
            if (target == null)
            {
                Plugin.Log.LogError($"[HS-STRIP] Patch target not found: {label}");
                return;
            }
            harmony.Patch(target,
                prefix: prefix == null ? null : new HarmonyMethod(typeof(ShipStripHooks), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(ShipStripHooks), postfix));
            Plugin.Log.LogInfo($"[HS-STRIP] Patched {label}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[HS-STRIP] Failed to patch {label}: {e}");
        }
    }

    private static void CreateAsyncPrefix(ref int seed)
    {
        try
        {
            if (!ShipStripConfig.Enabled.Value || ShipStripConfig.FixedSeed.Value == 0 || ApProfiles.CurrentIsAp || ShipCatalog.Running)
                return;
            int original = seed;
            seed = ShipStripConfig.FixedSeed.Value;
            Plugin.Log.LogInfo($"[HS-STRIP] ShipPreview.CreateAsync seed {original} -> {seed}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[HS-STRIP] CreateAsync prefix failed: {e}");
        }
    }

    private static void TotalSalvageOnUpdatePostfix()
    {
        try
        {
            if (!ShipStripConfig.Enabled.Value)
                return;
            Plugin.Log.LogInfo("[HS-STRIP] TotalSalvageValueCalculationSystem.OnUpdate ran (new ship spawn).");
            if (ShipStripConfig.DumpOnSpawn.Value)
                ShipInspector.DumpCurrentShip(manual: false);
            if (ShipReset.TryFinish())
                return;
            if (ApProfiles.CurrentIsAp && PartRandoSlot.Active)
                ShipInspector.StripPartTypes(PartRandoSlot.LockedPartTypes());
            else if (ShipStripConfig.StripOnSpawn.Value)
                ShipInspector.StripCurrentShip();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[HS-STRIP] TotalSalvage postfix failed: {e}");
        }
    }
}
