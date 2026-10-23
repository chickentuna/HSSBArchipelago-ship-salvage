#nullable disable
using System;
using System.Reflection;
using BBI.Unity.Game;
using HarmonyLib;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class PopupHooks
{
    private const string P = "[HS-AP]";

    public static void Apply(Harmony h)
    {
        Block(h, typeof(NarrativeItemSystem), "OnNarrativeMessageEvent", nameof(SkipVoid));
        Block(h, typeof(HabNarrativeMessageButtonController), "OnNarrativeMessageReceived", nameof(SkipVoid));
        Block(h, typeof(TriggerableTutorialUIPopup), nameof(TriggerableTutorialUIPopup.ShowPopup), nameof(SkipVoid));
        Block(h, typeof(CertificationService), "TryIncreaseCertification", nameof(SkipBool));
        Block(h, typeof(CustomGreetingPopup), nameof(CustomGreetingPopup.DisplayRemainingGreetingMessages), nameof(SkipGreetingMessages));
        Block(h, typeof(HabScreenController), nameof(HabScreenController.TriggerNextGreetingFlow), nameof(ClearGreetingFlow));
    }

    private static void Block(Harmony h, Type type, string method, string prefix)
    {
        try
        {
            var m = AccessTools.Method(type, method);
            if (m == null) { Plugin.Log.LogError($"{P} {type.Name}.{method} not found"); return; }
            h.Patch(m, prefix: new HarmonyMethod(typeof(PopupHooks), prefix));
            Plugin.Log.LogInfo($"{P} Patched {type.Name}.{method} (Archipelago profiles)");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Patching {type.Name}.{method} failed: {e}");
        }
    }

    private static bool Active()
    {
        try { return ShipStripConfig.Enabled.Value && ApProfiles.CurrentIsAp; }
        catch { return false; }
    }

    private static bool SkipVoid(MethodBase __originalMethod)
    {
        if (!Active()) return true;
        Plugin.Log.LogInfo($"{P} Skipped {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name} for Archipelago profile.");
        return false;
    }

    private static bool SkipGreetingMessages(CustomGreetingPopup __instance)
    {
        if (!Active()) return true;
        Traverse.Create(__instance).Field<DoozyEventSender>("m_ContinueHabSequence").Value?.SendEvent();
        Plugin.Log.LogInfo($"{P} Skipped the Hab greeting messages for Archipelago profile.");
        return false;
    }

    private static bool ClearGreetingFlow()
    {
        if (!Active()) return true;
        var inventory = PlayerProfileService.Instance?.Profile?.NarrativeInventory;
        if (inventory != null) inventory.NewMessagesCount = 0;
        StickerProgressService.Instance?.BrandNewStickersCollectedThisShift?.Clear();
        Plugin.Log.LogInfo($"{P} Cleared new messages and new stickers from the Hab greeting for Archipelago profile.");
        return true;
    }

    private static bool SkipBool(MethodBase __originalMethod, ref bool __result)
    {
        if (!Active()) return true;
        __result = false;
        Plugin.Log.LogInfo($"{P} Skipped {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name} for Archipelago profile.");
        return false;
    }
}
