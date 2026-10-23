#nullable disable
using BepInEx.Configuration;
using HarmonyLib;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class ShipStripConfig
{
    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<bool> UiTrace;
    public static ConfigEntry<bool> BuildCatalog;
    public static ConfigEntry<int> CatalogSeedsPerHull;
    public static ConfigEntry<string> CatalogSeeds;

    private static Harmony _harmony;

    public static void Init(ConfigFile config)
    {
        Enabled = config.Bind("ShipStrip", "Enabled", true, "Master switch for ship dump, strip, fixed seed and Archipelago mode hooks.");
        UiTrace = config.Bind("ArchipelagoMode", "UiTrace", false,
            "Log the UI game events and graph nodes ([HS-AP] [UI] lines).");

        BuildCatalog = config.Bind("Catalog", "BuildCatalog", false,
            "On the main menu, generate every job board hull with CatalogSeedsPerHull seeds in memory and write their parts, categories and values to BepInEx/ship_catalog. Ships already in the folder are skipped. Set back to false when the run finishes.");
        CatalogSeedsPerHull = config.Bind("Catalog", "CatalogSeedsPerHull", 10, "Number of seeds generated per hull.");
        CatalogSeeds = config.Bind("Catalog", "CatalogSeeds", "12345",
            "Comma-separated seeds always included; the rest up to CatalogSeedsPerHull are derived.");

        _harmony = new Harmony(Plugin.PLUGIN_GUID + ".apmode");
        ProfileScreenHooks.Apply(_harmony);
        SpeechHooks.Apply(_harmony);
        PopupHooks.Apply(_harmony);
        if (UiTrace.Value)
            ApMode.UiTrace.Enable();
    }

    public static void Update()
    {
        ShipCatalog.Update();
    }
}
