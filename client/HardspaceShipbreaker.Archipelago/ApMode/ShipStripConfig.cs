#nullable disable
using BepInEx.Configuration;
using HarmonyLib;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class ShipStripConfig
{
    public static ConfigEntry<bool> Enabled;

    private static Harmony _harmony;

    public static void Init(ConfigFile config)
    {
        Enabled = config.Bind("ShipStrip", "Enabled", true, "Master switch for ship dump, strip, fixed seed and Archipelago mode hooks.");

        _harmony = new Harmony(Plugin.PLUGIN_GUID + ".apmode");
        ProfileScreenHooks.Apply(_harmony);
        SpeechHooks.Apply(_harmony);
        PopupHooks.Apply(_harmony);
    }

    public static void Update()
    {
    }
}
