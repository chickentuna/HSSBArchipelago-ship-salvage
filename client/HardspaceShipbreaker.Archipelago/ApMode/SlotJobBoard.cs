#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BBI.Unity.Game;
using Doozy.Engine;
using HarmonyLib;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class SlotJobBoard
{
    private const string P = "[HS-AP]";

    private static ShipPreview[] _previews = new ShipPreview[0];
    private static bool[] _used = new bool[0];
    private static Task _generation;
    private static JobBoardScreenController _board;

    private static MethodInfo _addCard;
    private static MethodInfo _disableExcessCards;
    private static MethodInfo _disableExcessIndustrialCards;
    private static AccessTools.FieldRef<JobBoardScreenController, List<ShipCardController>> _availableCards;
    private static AccessTools.FieldRef<JobBoardScreenController, Transform> _cardParent;
    private static AccessTools.FieldRef<JobBoardScreenController, ShipCardController> _selectedCard;
    private static AccessTools.FieldRef<JobBoardScreenController, LocalizedTextMeshProUGUI> _coreSystemPrefab;
    private static AccessTools.FieldRef<JobBoardScreenController, Transform> _coreSystemParent;
    private static readonly List<LocalizedTextMeshProUGUI> _partsanityLines = new();

    private static bool Active => PartRandoSlot.Active && ApProfiles.CurrentIsAp;

    public static void Apply(Harmony h)
    {
        try
        {
            var t = typeof(JobBoardScreenController);
            _addCard = AccessTools.Method(t, "AddCard");
            _disableExcessCards = AccessTools.Method(t, "DisableExcessCardControllers");
            _disableExcessIndustrialCards = AccessTools.Method(t, "DisableExcessIndustrialActionCards");
            _availableCards = AccessTools.FieldRefAccess<JobBoardScreenController, List<ShipCardController>>("m_AvailableShipsCardControllers");
            _cardParent = AccessTools.FieldRefAccess<JobBoardScreenController, Transform>("m_ShipCardParent");
            _selectedCard = AccessTools.FieldRefAccess<JobBoardScreenController, ShipCardController>("mCurrentlySelectedShip");
            _coreSystemPrefab = AccessTools.FieldRefAccess<JobBoardScreenController, LocalizedTextMeshProUGUI>("m_CoreSystemTextPrefab");
            _coreSystemParent = AccessTools.FieldRefAccess<JobBoardScreenController, Transform>("m_CoreSystemParent");
            Patch(h, t, nameof(JobBoardScreenController.ShowJobBoard), nameof(ShowJobBoardPrefix));
            Patch(h, t, "DisplayNewlyUnlockedShips", nameof(SkipWhenActive));
            Patch(h, t, "DisplayIndustrialActionShips", nameof(DisplayIndustrialActionShipsPrefix));
            Patch(h, t, "DisplayCatalogueShips", nameof(DisplayCatalogueShipsPrefix));
            Patch(h, t, nameof(JobBoardScreenController.SetupCurrentlySelectedShipForSpawn), nameof(SetupForSpawnPrefix));
            Patch(h, typeof(JobBoardUtils), nameof(JobBoardUtils.RefreshJobBoardShipsAsync), nameof(RefreshPrefix));
            var coreSystems = AccessTools.Method(t, "DisplayShipInfoCoreSystems");
            h.Patch(coreSystems, postfix: new HarmonyMethod(typeof(SlotJobBoard), nameof(CoreSystemsPostfix)));
            Plugin.Log.LogInfo($"{P} Patched {t.Name}.DisplayShipInfoCoreSystems (Part Rando partsanity lines)");
            Patch(h, typeof(JobBoardUtils), nameof(JobBoardUtils.RefreshJobBoardShipsFromLoadedDataAsync), nameof(RefreshFromLoadedDataPrefix));
            Message.AddListener<GameEventMessage>(OnGameEvent);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Slot job board patches failed: {e}");
        }
    }

    private static void Patch(Harmony h, Type type, string method, string prefix)
    {
        var m = AccessTools.Method(type, method);
        if (m == null)
        {
            Plugin.Log.LogError($"{P} {type.Name}.{method} not found");
            return;
        }
        h.Patch(m, prefix: new HarmonyMethod(typeof(SlotJobBoard), prefix));
        Plugin.Log.LogInfo($"{P} Patched {type.Name}.{method} (Part Rando job board)");
    }

    public static void Reset()
    {
        _previews = new ShipPreview[PartRandoSlot.Ships.Count];
        _used = new bool[PartRandoSlot.Ships.Count];
    }

    private static bool IsInBay(SlotShip ship)
    {
        foreach (var pv in new[] { ModuleService.Instance?.CurrentShipPreview, ModuleService.Instance?.NextShipPreview })
            if (pv != null && pv.Seed == ship.Seed && pv.ConstructionAssetName == ship.Construction)
                return true;
        return false;
    }

    private static ShipClassAsset FindClass(string name) =>
        Main.Instance.MainSettings.HabSettings.JobBoardSettings.ShipClasses.FirstOrDefault(c => c != null && c.name == name);

    // Builds the slot's ships on Hab entry so they are ready by the time the job board opens.
    private static void OnGameEvent(GameEventMessage m)
    {
        try
        {
            if (m.EventName == "GoToHab" && Active) EnsurePreviews();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Slot ship generation start failed: {e}");
        }
    }

    public static void OnShipsUnlocked()
    {
        if (_board == null || !_board.isActiveAndEnabled || !Active)
            return;
        EnsurePreviews();
        Redraw();
    }

    private static void Redraw()
    {
        try
        {
            if (_board != null && _board.isActiveAndEnabled && Active)
                _board.ShowJobBoard();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Job board redraw failed: {e}");
        }
    }

    private static void EnsurePreviews() => _ = EnsurePreviewsAsync();

    private static Task EnsurePreviewsAsync()
    {
        if (_previews.Length != PartRandoSlot.Ships.Count)
            Reset();
        if (_generation != null && !_generation.IsCompleted)
            return _generation;
        var missing = PartRandoSlot.Ships
            .Where(PartRandoSlot.IsUnlocked)
            .Where(s => _previews[s.Index - 1] == null || (_used[s.Index - 1] && !IsInBay(s)))
            .ToList();
        if (missing.Count == 0)
            return Task.CompletedTask;
        _generation = GenerateAsync(missing);
        return _generation;
    }

    private static bool RefreshPrefix(ref LynxAsyncHandle<ShipPreview> __result)
    {
        try
        {
            if (!Active) return true;
            ProfileScreenHooks.EnsureAllUpgrades();
            __result = LynxAsyncHandle<ShipPreview>.Start(async handle =>
            {
                await EnsurePreviewsAsync();
                return AsyncOperationStatus.Succeeded;
            });
            AccessTools.Property(typeof(JobBoardUtils), nameof(JobBoardUtils.JobBoardPreviewHandle)).SetValue(null, __result);
            Plugin.Log.LogInfo($"{P} Job board refresh: building the slot's ships in place of the game's board ships.");
            return false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Replacing the job board refresh failed: {e}");
            return true;
        }
    }

    private static bool RefreshFromLoadedDataPrefix(ref Task __result, Action<float> loadProgressCallback)
    {
        try
        {
            if (!Active) return true;
            loadProgressCallback?.Invoke(1f);
            __result = Task.CompletedTask;
            return false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Skipping the saved job board ships failed: {e}");
            return true;
        }
    }

    private static async Task GenerateAsync(List<SlotShip> ships)
    {
        try
        {
            using var cache = new AddressableCache();
            foreach (var ship in ships)
            {
                var cls = FindClass(ship.ShipClass);
                if (cls == null || ship.Hull >= cls.GeneratableShips.Length)
                {
                    Plugin.Log.LogError($"{P} Slot ship {ship.Index} ({ship.Id}): class '{ship.ShipClass}' hull {ship.Hull} not found.");
                    continue;
                }
                var pair = cls.GeneratableShips[ship.Hull];
                var spawnParams = new ShipSpawnParams { SessionType = GameSession.SessionType.Career };
                var opt = new ShipPreview.OptionalCreateParams
                {
                    ShipClassAsset = cls,
                    PropertyOverridesRef = pair.OverrideRef,
                    DaysRemaining = 1
                };
                var preview = await ShipPreview.CreateAsync(ship.Seed, pair.ShipRef, spawnParams, opt, cache);
                if (preview == null)
                {
                    Plugin.Log.LogError($"{P} Slot ship {ship.Index} ({ship.Id}): preview generation failed.");
                    continue;
                }
                _previews[ship.Index - 1] = preview;
                _used[ship.Index - 1] = false;
                Plugin.Log.LogInfo($"{P} Slot ship {ship.Index} ready: '{preview.ShipName}' ({cls.name} hull {ship.Hull} seed {ship.Seed}), " +
                                   $"total {preview.TotalCreditValue:F0} (catalog {ship.Total:F0}).");
                Redraw();
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Slot ship generation failed: {e}");
        }
        Redraw();
    }

    private static void ShowJobBoardPrefix(JobBoardScreenController __instance)
    {
        try
        {
            if (!Active) return;
            _board = __instance;
            EnsurePreviews();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Slot job board setup failed: {e}");
        }
    }

    private static bool SkipWhenActive() => !Active;

    private static bool DisplayIndustrialActionShipsPrefix(JobBoardScreenController __instance)
    {
        try
        {
            if (!Active) return true;
            _disableExcessIndustrialCards.Invoke(__instance, new object[] { 0 });
            return false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Hiding industrial action ships failed: {e}");
            return true;
        }
    }

    private static bool DisplayCatalogueShipsPrefix(JobBoardScreenController __instance)
    {
        try
        {
            if (!Active) return true;
            int shown = 0;
            foreach (var ship in PartRandoSlot.Ships)
            {
                var preview = _previews.Length >= ship.Index ? _previews[ship.Index - 1] : null;
                if (preview == null || IsInBay(ship) || !PartRandoSlot.IsUnlocked(ship)) continue;
                var cls = FindClass(ship.ShipClass);
                SlotMarkers.ApplyMarkers(preview);
                _addCard.Invoke(__instance, new object[] { preview, cls, ShipCardController.ShipCardType.AvailableShip, false });
                shown++;
            }
            _disableExcessCards.Invoke(__instance, null);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_cardParent(__instance));
            foreach (var card in _availableCards(__instance))
                card.UpdateCardDisplayAfterLayoutRebuild();
            Plugin.Log.LogInfo($"{P} Job board: {shown} slot ships shown{(_generation != null && !_generation.IsCompleted ? ", more being generated" : "")}.");
            return false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Showing slot ships failed: {e}");
            return true;
        }
    }

    private static List<string> OpenPartsanityChecks(SlotShip ship)
    {
        var client = Plugin.Instance.Client;
        var locked = PartRandoSlot.LockedPartTypes();
        return ship.Parts.Keys
            .Where(key => !locked.Contains(key) && PartRandoSlot.PartsanityLocations.ContainsKey(key))
            .Select(key => PartRandoSlot.PartsanityLocations[key])
            .Where(location => client?.IsLocationChecked(location) != true)
            .Select(location => client?.LocationName(location) ?? location.ToString())
            .OrderBy(name => name)
            .ToList();
    }

    private static void CoreSystemsPostfix(JobBoardScreenController __instance)
    {
        try
        {
            var ship = Active ? SlotMarkers.FindShip(_selectedCard(__instance)?.ShipPreview) : null;
            var lines = ship != null ? OpenPartsanityChecks(ship) : new List<string>();
            _partsanityLines.RemoveAll(label => label == null);
            while (_partsanityLines.Count < lines.Count)
            {
                var label = UnityEngine.Object.Instantiate(_coreSystemPrefab(__instance), _coreSystemParent(__instance));
                label.enabled = false;
                _partsanityLines.Add(label);
            }
            for (int i = 0; i < _partsanityLines.Count; i++)
            {
                var label = _partsanityLines[i];
                if (i < lines.Count)
                {
                    label.TMProText.SetText(lines[i]);
                    label.transform.SetAsLastSibling();
                }
                label.gameObject.SetActive(i < lines.Count);
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Listing the partsanity checks failed: {e}");
        }
    }

    private static void SetupForSpawnPrefix(JobBoardScreenController __instance)
    {
        try
        {
            if (!Active) return;
            var selected = Traverse.Create(__instance).Field<ShipCardController>("mCurrentlySelectedShip").Value?.ShipPreview;
            int i = Array.IndexOf(_previews, selected);
            if (i >= 0) _used[i] = true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Marking the claimed slot ship failed: {e}");
        }
    }
}
