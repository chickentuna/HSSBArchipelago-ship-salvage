#nullable disable
using System;
using BBI.Unity.Game;
using HarmonyLib;
using UnityEngine;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class ProfileScreenHooks
{
    private const string P = "[HS-AP]";
    public static void Apply(Harmony h)
    {
        try
        {
            var goToNext = AccessTools.Method(typeof(FrontEndProfileUIController), nameof(FrontEndProfileUIController.GoToNext));
            h.Patch(goToNext, prefix: new HarmonyMethod(typeof(ProfileScreenHooks), nameof(GoToNextPrefix)));
            Plugin.Log.LogInfo($"{P} Patched FrontEndProfileUIController.GoToNext");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Profile screen hooks failed: {e}");
        }
    }

    private static bool GoToNextPrefix(FrontEndProfileUIController __instance, bool ignoreOxygenDrain)
    {
        try
        {
            if (!ShipStripConfig.Enabled.Value || !PartRandoSlot.Active)
                return true;
            var button = __instance.CurrentlySelectedProfileButton;
            if (button == null || button.PlayerProfile != null)
                return true;
            var name = CreateApProfile(button.DifficultyModeAsset, ignoreOxygenDrain);
            if (name == null)
                return true;
            _ = SceneLoader.Instance.TearDownAndLoadLevelWithPlayerProfile(name, bLoadWithHabStart: true);
            return false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Creating the Archipelago profile failed: {e}");
            return true;
        }
    }

    private static string CreateApProfile(DifficultyModeAsset difficulty, bool ignoreOxygenDrain)
    {
        var service = PlayerProfileService.Instance;
        string name = "Archipelago";
        for (int i = 2; !PlayerProfileSaveLoadManager.Instance.IsProfileNameUnique(name, out _); i++)
            name = $"Archipelago {i}";

        if (!service.TryCreateNewPlayerProfile(name, difficulty, out var err))
        {
            Plugin.Log.LogError($"{P} TryCreateNewPlayerProfile('{name}') failed: {err}");
            return null;
        }
        if (!service.TryLoadPlayerProfile(name, out var loadErr))
        {
            Plugin.Log.LogError($"{P} TryLoadPlayerProfile('{name}') failed: {loadErr}");
            return null;
        }
        ApProfiles.Add(name);
        var profile = service.Profile;
        profile.IgnoreOxygenDrain = ignoreOxygenDrain && difficulty.AllowsOxygenDrainToBeIgnored;
        service.DoSkipTutorial(profile);
        int unlocked = UnlockAllUpgrades(profile);
        if (!service.TrySavePlayerProfile(out var saveErr))
            Plugin.Log.LogError($"{P} Saving '{name}' failed: {saveErr}");
        PlayerMetaProgressService.Instance.TrySaveMetaProgress(out _);
        Plugin.Log.LogInfo($"{P} Archipelago profile '{name}' created: difficulty '{difficulty.name}', " +
                           $"rank {profile.CurrentCertificationRank}, {unlocked} upgrades unlocked, ignore oxygen drain {profile.IgnoreOxygenDrain}.");
        return name;
    }

    public static void EnsureAllUpgrades()
    {
        try
        {
            if (!ApProfiles.CurrentIsAp)
                return;
            var service = PlayerProfileService.Instance;
            int unlocked = UnlockAllUpgrades(service.Profile);
            if (unlocked == 0)
                return;
            if (!service.TrySavePlayerProfile(out var err))
                Plugin.Log.LogError($"{P} Saving the profile after restoring upgrades failed: {err}");
            Plugin.Log.LogInfo($"{P} Restored {unlocked} missing upgrades on the Archipelago profile.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Restoring upgrades failed: {e}");
        }
    }

    private static int UnlockAllUpgrades(PlayerProfile profile)
    {
        int n = 0;
        foreach (var equipment in Main.Instance.MainSettings.UpgradeSettings.EquipmentInfoSettingsAssets)
            foreach (var tree in equipment.Data.UpgradeTrees)
                n += UnlockTree(profile, tree);
        return n;
    }

    private static int UnlockTree(PlayerProfile profile, UpgradeTreeAsset tree)
    {
        if (tree == null) return 0;
        int n = 0;
        foreach (var upgrade in tree.Upgrades)
        {
            if (upgrade == null || profile.Upgrades.Contains(upgrade)) continue;
            upgrade.UnlockUpgrade();
            n++;
        }
        foreach (var branch in tree.Branches)
            n += UnlockTree(profile, branch);
        return n;
    }
}
