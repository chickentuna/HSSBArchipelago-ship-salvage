#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BBI.Unity.Game;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class ApProfiles
{
    private const string P = "[HS-AP]";
    private static HashSet<string> _names;

    private static string FilePath => Path.Combine(BepInEx.Paths.ConfigPath, "hssb_ap_profiles.txt");

    private static HashSet<string> Names
    {
        get
        {
            if (_names != null) return _names;
            _names = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                if (File.Exists(FilePath))
                    foreach (var line in File.ReadAllLines(FilePath))
                        if (line.Trim().Length > 0) _names.Add(line.Trim());
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"{P} Reading {FilePath} failed: {e.Message}");
            }
            return _names;
        }
    }

    public static bool IsAp(string profileName) => !string.IsNullOrEmpty(profileName) && Names.Contains(profileName);

    public static bool CurrentIsAp
    {
        get
        {
            try { return IsAp(PlayerProfileService.Instance?.Profile?.ProfileName); }
            catch { return false; }
        }
    }

    public static void Add(string profileName)
    {
        try
        {
            if (!Names.Add(profileName)) return;
            File.WriteAllLines(FilePath, Names.OrderBy(n => n).ToArray());
            Plugin.Log.LogInfo($"{P} Profile '{profileName}' marked as Archipelago profile ({FilePath}).");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Saving {FilePath} failed: {e.Message}");
        }
    }
}
