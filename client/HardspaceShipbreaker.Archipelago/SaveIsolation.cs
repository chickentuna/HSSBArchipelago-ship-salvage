using System;
using System.IO;
using BBI.Unity.Game;
using HarmonyLib;

namespace HardspaceShipbreaker.Archipelago;

internal static class SaveIsolation
{
    private const string FolderName = "Archipelago";

    public static void Apply()
    {
        try
        {
            string path = Path.Combine(PlatformSettings.SaveDataPath, FolderName);
            AccessTools.PropertySetter(typeof(PlatformSettings), nameof(PlatformSettings.SaveDataPath))
                .Invoke(null, new object[] { path });
            Plugin.Log.LogInfo($"[HS-AP] Save data path: {path}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[HS-AP] Moving the save files failed: {e}");
        }
    }
}
