#nullable disable
using System;
using BBI.Unity.Game;
using HarmonyLib;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class SpeechHooks
{
    private const string P = "[HS-AP]";
    private static int _storyDepth;
    private static int _blocked;

    public static void Apply(Harmony h)
    {
        Wrap(h, typeof(PlayerActionTrackerService), "OnPlayerActionTrackerEvent");
        Wrap(h, typeof(Hab3DController), nameof(Hab3DController.TryStartAfterShiftSequence));
        Wrap(h, typeof(Hab3DController), nameof(Hab3DController.TryStartMorningMode));
        Wrap(h, typeof(Hab3DController), nameof(Hab3DController.TrySetCommsTerminalNotification));
        Wrap(h, typeof(TriggerableSpeechControlBehaviour), nameof(TriggerableSpeechControlBehaviour.OnBehaviourPlay));
        try
        {
            var m = AccessTools.Method(typeof(LynxSpeechEventController), "OnTriggeredSpeechEvent");
            h.Patch(m, prefix: new HarmonyMethod(typeof(SpeechHooks), nameof(TriggeredSpeechPrefix)));
            Plugin.Log.LogInfo($"{P} Patched LynxSpeechEventController.OnTriggeredSpeechEvent");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Patching LynxSpeechEventController.OnTriggeredSpeechEvent failed: {e}");
        }
    }

    private static void Wrap(Harmony h, Type type, string method)
    {
        try
        {
            var m = AccessTools.Method(type, method);
            if (m == null) { Plugin.Log.LogError($"{P} {type.Name}.{method} not found"); return; }
            h.Patch(m, prefix: new HarmonyMethod(typeof(SpeechHooks), nameof(StoryEnter)),
                finalizer: new HarmonyMethod(typeof(SpeechHooks), nameof(StoryExit)));
            Plugin.Log.LogInfo($"{P} Patched {type.Name}.{method} (story speech)");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Patching {type.Name}.{method} failed: {e}");
        }
    }

    private static void StoryEnter() => _storyDepth++;

    private static Exception StoryExit(Exception __exception)
    {
        if (_storyDepth > 0) _storyDepth--;
        return __exception;
    }

    private static bool TriggeredSpeechPrefix()
    {
        try
        {
            if (_storyDepth == 0 || !ShipStripConfig.Enabled.Value || !ApProfiles.CurrentIsAp)
                return true;
            _blocked++;
            Plugin.Log.LogInfo($"{P} Story speech skipped for Archipelago profile ({_blocked} so far).");
            return false;
        }
        catch
        {
            return true;
        }
    }
}
