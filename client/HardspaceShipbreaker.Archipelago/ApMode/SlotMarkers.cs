#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BBI.Unity.Game;
using HarmonyLib;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class SlotMarkers
{
    private const string P = "[HS-AP]";
    private const string LogoObjectName = "ArchipelagoMarkerLogo";

    private static Sprite _logo;
    private static float _nextPoll;
    private static int _shipUnlocksSeen = -1;

    private static AccessTools.FieldRef<TierIndicatorController, TextMeshProUGUI> _tierText;
    private static AccessTools.FieldRef<TierIndicatorController, Image> _tierIcon;
    private static AccessTools.FieldRef<TierIndicatorController, SalvageIndicatorController> _tierBar;

    private static bool Active => PartRandoSlot.Active && ApProfiles.CurrentIsAp;

    public static void Apply(Harmony h)
    {
        try
        {
            _tierText = AccessTools.FieldRefAccess<TierIndicatorController, TextMeshProUGUI>("m_TierText");
            _tierIcon = AccessTools.FieldRefAccess<TierIndicatorController, Image>("m_TierIconBackground");
            _tierBar = AccessTools.FieldRefAccess<TierIndicatorController, SalvageIndicatorController>("mSalvageIndicatorController");
            Patch(h, typeof(SalvageIndicatorController), nameof(SalvageIndicatorController.ConfigureForShipPreview), prefix: nameof(ConfigurePrefix));
            Patch(h, typeof(SalvageIndicatorController), nameof(SalvageIndicatorController.SetSalvageIndicatorPreview), prefix: nameof(PreviewPrefix), postfix: nameof(PreviewPostfix));
            Patch(h, typeof(TierIndicatorController), "RefreshInterface", postfix: nameof(RefreshInterfacePostfix));
            foreach (var volume in new[] { typeof(BargeEntranceVolume), typeof(ProcessorVolume), typeof(FurnaceVolume) })
                Patch(h, volume, "HandlePositiveSalvage", postfix: nameof(DepositPostfix));
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Marker patches failed: {e}");
        }
    }

    private static void Patch(Harmony h, Type type, string method, string prefix = null, string postfix = null)
    {
        var m = AccessTools.Method(type, method);
        if (m == null)
        {
            Plugin.Log.LogError($"{P} {type.Name}.{method} not found");
            return;
        }
        h.Patch(m,
            prefix: prefix == null ? null : new HarmonyMethod(typeof(SlotMarkers), prefix),
            postfix: postfix == null ? null : new HarmonyMethod(typeof(SlotMarkers), postfix));
        Plugin.Log.LogInfo($"{P} Patched {type.Name}.{method} (Part Rando markers)");
    }

    public static SlotShip FindShip(ShipPreview preview) =>
        preview == null ? null : PartRandoSlot.Ships.FirstOrDefault(s => s.Seed == preview.Seed && s.Construction == preview.ConstructionAssetName);

    private static bool IsChecked(long location) => Plugin.Instance.Client?.IsLocationChecked(location) == true;

    public static void ApplyMarkers(ShipPreview preview)
    {
        var ship = FindShip(preview);
        if (ship == null)
            return;
        var rewards = new List<RewardTiersAsset.SelectedRewards>();
        for (int k = 0; k < ship.Markers.Count && k < ship.Locations.Count; k++)
        {
            if (IsChecked(ship.Locations[k]))
                continue;
            rewards.Add(new RewardTiersAsset.SelectedRewards
            {
                Tier = k + 1,
                TierPercent = ship.CheckAt[k],
                RewardData = new List<RewardTiersAsset.SelectedRewards.DataPair>()
            });
        }
        Traverse.Create(preview).Property("SelectedRewards").SetValue(rewards);
    }

    private static void ConfigurePrefix(ShipPreview shipPreview)
    {
        try
        {
            if (Active) ApplyMarkers(shipPreview);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Setting bay markers failed: {e}");
        }
    }

    private static void PreviewPrefix(ShipPreview shipToPreview)
    {
        try
        {
            if (Active) ApplyMarkers(shipToPreview);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Setting job board markers failed: {e}");
        }
    }

    private static void PreviewPostfix(SalvageIndicatorController __instance, ShipPreview shipToPreview)
    {
        try
        {
            if (!Active) return;
            var ship = FindShip(shipToPreview);
            if (ship == null || ship.Total <= 0f) return;
            float locked = Mathf.Clamp01(PartRandoSlot.LockedValue(ship) / ship.Total);
            Traverse.Create(__instance).Field<SalvageBarController>("m_InnerBarDestroyedPreviously").Value?.SetFillAmount(locked, animateOverride: false);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Drawing the locked share on the job board failed: {e}");
        }
    }

    private static Sprite Logo()
    {
        if (_logo != null)
            return _logo;
        using var stream = typeof(SlotMarkers).Assembly.GetManifestResourceStream("archipelago.png");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(buffer.ToArray());
        _logo = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return _logo;
    }

    private static void RefreshInterfacePostfix(TierIndicatorController __instance)
    {
        try
        {
            var text = _tierText(__instance);
            if (text == null)
                return;
            var existing = text.transform.Find(LogoObjectName);
            var bar = _tierBar(__instance);
            bool show = Active && bar != null && bar.CurrentIndicatorMode != SalvageIndicatorController.IndicatorMode.NPC;
            if (!show)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }
            text.SetText("");
            var logo = existing;
            if (logo == null)
            {
                var go = new GameObject(LogoObjectName, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(text.transform, false);
                var image = go.GetComponent<Image>();
                image.sprite = Logo();
                image.preserveAspect = true;
                image.raycastTarget = false;
                logo = go.transform;
            }
            var rt = (RectTransform)logo;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            var icon = _tierIcon(__instance);
            float size = icon != null ? Mathf.Min(icon.rectTransform.rect.width, icon.rectTransform.rect.height) * 0.6f : 0f;
            if (size <= 0f) size = 16f;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
            logo.gameObject.SetActive(true);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Drawing the marker logo failed: {e}");
        }
    }

    private static void DepositPostfix(StructurePart part)
    {
        try
        {
            if (!Active || part == null)
                return;
            var key = part.ObjectInfoAsset != null ? part.ObjectInfoAsset.name : null;
            if (key == null || !PartRandoSlot.PartsanityLocations.TryGetValue(key, out var location))
                return;
            if (Plugin.Instance.Client?.TryCheckLocation(location, $"First {key} Salvaged", "Part Rando") == true)
                Plugin.Log.LogInfo($"{P} First salvage of {key}: location {location} checked.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} First salvage check failed: {e}");
        }
    }

    public static void Update()
    {
        if (!Active || Time.unscaledTime < _nextPoll)
            return;
        _nextPoll = Time.unscaledTime + 0.5f;
        try
        {
            var client = Plugin.Instance.Client;
            if (client == null || !client.IsConnected)
                return;
            int unlocks = PartRandoSlot.ShipUnlocksReceived();
            if (unlocks != _shipUnlocksSeen)
            {
                if (_shipUnlocksSeen >= 0)
                    Plugin.Log.LogInfo($"{P} Ship unlocks: {unlocks}.");
                _shipUnlocksSeen = unlocks;
                SlotJobBoard.OnShipsUnlocked();
            }
            var preview = ModuleService.Instance?.CurrentShipPreview;
            var ship = FindShip(preview);
            if (ship != null && preview.IsManifestValid &&
                World.DefaultGameObjectInjectionWorld.EntityManager.TryGetComponent<ShipCurrentSalvageProgress>(preview.SpawnedManifest, out var progress) &&
                progress.TotalCreditValue > 0f)
            {
                float percent = progress.CurrentSalvagedCreditValue / progress.TotalCreditValue * 100f;
                for (int k = 0; k < ship.Markers.Count && k < ship.Locations.Count; k++)
                {
                    if (percent >= ship.CheckAt[k] && client.TryCheckLocation(ship.Locations[k], $"Ship {ship.Index} Marker {k + 1}", "Part Rando"))
                        Plugin.Log.LogInfo($"{P} Ship {ship.Index} marker {k + 1} ({ship.Markers[k]}% in logic, checked at {ship.CheckAt[k]:F1}%) reached at {percent:F1}%.");
                }
            }
            int salvaged = PartRandoSlot.Ships.Count(s => s.Locations.Count > 0 && client.IsLocationChecked(s.Locations[s.Locations.Count - 1]));
            if (!client.GoalSent && PartRandoSlot.Ships.Count > 0 && salvaged >= PartRandoSlot.Ships.Count)
            {
                Plugin.Log.LogInfo($"{P} {salvaged} ships reached their last marker: sending the goal.");
                client.SendGoal();
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Marker check failed: {e}");
        }
    }
}
