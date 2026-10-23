#nullable disable
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class ShipStripConfig
{
    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<int> FixedSeed;
    public static ConfigEntry<bool> DumpOnSpawn;
    public static ConfigEntry<bool> StripOnSpawn;
    public static ConfigEntry<string> StripCategories;
    public static ConfigEntry<string> StripNameContains;
    public static ConfigEntry<bool> ShowStrippedAsDestroyed;
    public static ConfigEntry<bool> VentBeforeStrip;
    public static ConfigEntry<bool> UiTrace;
    public static ConfigEntry<bool> BuildCatalog;
    public static ConfigEntry<int> CatalogSeedsPerHull;
    public static ConfigEntry<string> CatalogSeeds;

    private static Harmony _harmony;

    public static void Init(ConfigFile config)
    {
        Enabled = config.Bind("ShipStrip", "Enabled", true, "Master switch for ship dump, strip, fixed seed and Archipelago mode hooks.");
        FixedSeed = config.Bind("ShipStrip", "FixedSeed", 0,
            "When non-zero, every ShipPreview.CreateAsync call uses this seed (job board, level start, debug spawns). Ignored for Archipelago mode profiles.");
        DumpOnSpawn = config.Bind("ShipStrip", "DumpOnSpawn", false,
            "Write a JSON dump of every salvageable part/group to BepInEx/ship_dumps right after the game computes the ship's total salvage value.");
        StripOnSpawn = config.Bind("ShipStrip", "StripOnSpawn", false,
            "Remove the units matching the filters right after the total salvage value is computed.");
        StripCategories = config.Bind("ShipStrip", "StripCategories", "",
            "Comma-separated category names (CategoryAsset asset name or CategoryName, case-insensitive).");
        StripNameContains = config.Bind("ShipStrip", "StripNameContains", "",
            "Comma-separated substrings matched against the ObjectInfo ObjectName, the ObjectInfo asset name (e.g. INFO_Panel_Nanocarbon) and the GameObject name (case-insensitive). When both filters are set, a unit must match both.");
        ShowStrippedAsDestroyed = config.Bind("ShipStrip", "ShowStrippedAsDestroyed", true,
            "Add the stripped value to CurrentDestroyedCreditValue so the salvage bar shows it in red and it cannot be salvaged.");
        VentBeforeStrip = config.Bind("ShipStrip", "VentBeforeStrip", true,
            "Before stripping, set the air pressure of every room group bounded by a stripped part to 0, so no explosive decompression happens.");
        UiTrace = config.Bind("ArchipelagoMode", "UiTrace", false,
            "Log the UI game events and graph nodes ([HS-AP] [UI] lines).");

        BuildCatalog = config.Bind("Catalog", "BuildCatalog", false,
            "On the main menu, generate every job board hull with CatalogSeedsPerHull seeds in memory and write their parts, categories and values to BepInEx/ship_catalog. Ships already in the folder are skipped. Set back to false when the run finishes.");
        CatalogSeedsPerHull = config.Bind("Catalog", "CatalogSeedsPerHull", 10, "Number of seeds generated per hull.");
        CatalogSeeds = config.Bind("Catalog", "CatalogSeeds", "12345",
            "Comma-separated seeds always included; the rest up to CatalogSeedsPerHull are derived.");

        _harmony = new Harmony(Plugin.PLUGIN_GUID + ".apmode");
        ShipStripHooks.Apply(_harmony);
        ProfileScreenHooks.Apply(_harmony);
        SpeechHooks.Apply(_harmony);
        PopupHooks.Apply(_harmony);
        SlotJobBoard.Apply(_harmony);
        if (UiTrace.Value)
            ApMode.UiTrace.Enable();
    }

    public static void Update()
    {
        ShipCatalog.Update();
        if (!Enabled.Value || !Input.GetKeyDown(KeyCode.F5))
            return;
        Plugin.Log.LogInfo("[HS-STRIP] F5: manual dump requested.");
        ShipInspector.DumpCurrentShip(manual: true);
    }
}
