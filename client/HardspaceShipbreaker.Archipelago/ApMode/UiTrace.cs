#nullable disable
using System;
using BBI.Unity.Game;
using Doozy.Engine;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class UiTrace
{
    private const string P = "[HS-AP] [UI]";
    private static bool _on;

    public static void Enable()
    {
        if (_on) return;
        try
        {
            Message.AddListener<GameEventMessage>(OnGameEvent);
            Main.EventSystem.AddHandler<DoozyGraphNodeUpdateEvent>(OnNode);
            _on = true;
            Plugin.Log.LogInfo($"{P} UI trace on.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} UI trace setup failed: {e}");
        }
    }

    private static void OnGameEvent(GameEventMessage m)
    {
        try { Plugin.Log.LogInfo($"{P} game event '{m.EventName}'"); }
        catch { }
    }

    private static void OnNode(DoozyGraphNodeUpdateEvent ev)
    {
        try { Plugin.Log.LogInfo($"{P} graph '{ev.ControllerName}': '{ev.PrevNode}' -> '{ev.ActiveNode}'"); }
        catch { }
    }
}
