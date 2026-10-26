#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BBI.Unity.Game;
using HarmonyLib;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace HardspaceShipbreaker.Archipelago.ApMode;

/// <summary>
/// Pause menu "Reset Ship": respawns the current ship from its preview, removes every part that was gone
/// before the reset (salvaged or destroyed), brings back stripped parts whose type is now unlocked,
/// keeps the salvage progress and puts the player back at the respawn location.
/// </summary>
internal static class ShipReset
{
    private const string P = "[HS-RESET]";
    private const string Label = "RESET SHIP";
    private const string ConfirmLabel = "CONFIRM RESET?";
    private const float FadeTime = 0.5f;

    private sealed class ResetState
    {
        public ShipPreview Preview;
        public HashSet<ulong> Alive;
        public float Salvaged;
        public float HandledMass;
        public List<KeyValuePair<Entity, SalvageableSystem.ProcessedSalvageableInfo>> SalvagedItems;
        public List<KeyValuePair<Entity, SalvageableSystem.ProcessedSalvageableInfo>> DestroyedItems;
        public List<KeyValuePair<Entity, SalvageableSystem.ProcessedSalvageableInfo>> SoldOffItems;
        public List<KeyValuePair<AssetTypeID<ObjectInfoAsset>, int>> SalvagedCores;
        public List<KeyValuePair<AssetTypeID<ObjectInfoAsset>, int>> DestroyedCores;
        public bool Applied;
        public ShipCurrentSalvageProgress Progress;
    }

    private static ResetState _pending;
    private static bool _running;
    private static bool _armed;
    private static GameObject _button;
    private static TextMeshProUGUI _text;
    private static readonly Dictionary<string, HashSet<ulong>> _strippedCache = new();

    private static AccessTools.FieldRef<PauseMenuController, GameObject> _abandonButton;
    private static AccessTools.FieldRef<SalvageableSystem, float> _handledMass;

    public static void Apply(Harmony h)
    {
        try
        {
            _abandonButton = AccessTools.FieldRefAccess<PauseMenuController, GameObject>("m_AbandonShiftButton");
            _handledMass = AccessTools.FieldRefAccess<SalvageableSystem, float>("mHandledMass");
            var m = AccessTools.Method(typeof(PauseMenuController), "ShowPauseMenu");
            if (m == null)
            {
                Plugin.Log.LogError($"{P} PauseMenuController.ShowPauseMenu not found");
                return;
            }
            h.Patch(m, postfix: new HarmonyMethod(typeof(ShipReset), nameof(ShowPauseMenuPostfix)));
            Plugin.Log.LogInfo($"{P} Patched PauseMenuController.ShowPauseMenu (Reset Ship button)");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Reset Ship patch failed: {e}");
        }
    }

    private static bool CanReset()
    {
        var ms = ModuleService.Instance;
        return ShipStripConfig.Enabled.Value && ApProfiles.CurrentIsAp && !_running && ms != null &&
               ms.CurrentShipPreview != null && ms.CurrentShipPreview.IsManifestValid && ms.CurrentSpawnPoint != null &&
               !ms.ShipSpawningInProgress && !ms.ShipDespawningInProgress;
    }

    private static void ShowPauseMenuPostfix(PauseMenuController __instance, bool bHabVersion)
    {
        try
        {
            _armed = false;
            if (_button == null)
            {
                var source = _abandonButton(__instance);
                if (source == null)
                    return;
                _button = CreateButton(source);
                if (_button == null)
                    return;
            }
            _text?.SetText(Label);
            _button.SetActive(!bHabVersion && CanReset());
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Showing the Reset Ship button failed: {e}");
        }
    }

    // Clones the Abandon Shift button and strips everything but the uGUI button, its visuals and its text.
    private static GameObject CreateButton(GameObject source)
    {
        bool wasActive = source.activeSelf;
        source.SetActive(false);
        GameObject clone;
        try
        {
            clone = UnityEngine.Object.Instantiate(source, source.transform.parent);
        }
        finally
        {
            source.SetActive(wasActive);
        }
        clone.name = "ArchipelagoResetShipButton";
        clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

        foreach (var label in clone.GetComponentsInChildren<LocalizedTextMeshProUGUI>(true))
            label.enabled = false;
        foreach (var c in clone.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (c == null || c is LocalizedTextMeshProUGUI || c is ButtonTextSelectionOptions)
                continue;
            var ns = c.GetType().Namespace ?? "";
            if (ns.StartsWith("UnityEngine.UI") || ns.StartsWith("TMPro"))
                continue;
            try { UnityEngine.Object.DestroyImmediate(c); }
            catch (Exception e) { Plugin.Log.LogWarning($"{P} Could not remove {c.GetType().Name} from the button: {e.Message}"); }
        }

        var button = clone.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            Plugin.Log.LogError($"{P} The Abandon Shift button has no uGUI Button; no Reset Ship button.");
            UnityEngine.Object.Destroy(clone);
            return null;
        }
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(OnClick);
        _text = clone.GetComponentInChildren<TextMeshProUGUI>(true);
        Plugin.Log.LogInfo($"{P} Reset Ship button added to the pause menu.");
        return clone;
    }

    private static void OnClick()
    {
        if (!CanReset())
        {
            _button?.SetActive(false);
            return;
        }
        if (!_armed)
        {
            _armed = true;
            _text?.SetText(ConfirmLabel);
            return;
        }
        _armed = false;
        PauseMenuController.Instance?.HidePauseMenu();
        Plugin.Instance.StartCoroutine(Run(ModuleService.Instance.CurrentShipPreview));
    }

    private static IEnumerator Run(ShipPreview preview)
    {
        _running = true;
        float waited = 0f;
        while (GameSession.CurrentGameState != GameSession.GameState.Gameplay && waited < 5f)
        {
            yield return null;
            waited += Time.unscaledDeltaTime;
        }

        var state = Capture(preview);
        if (state == null)
        {
            _running = false;
            yield break;
        }

        Main.EventSystem.Post(ForceReleaseEvent.GetEvent(ForceReleaseEvent.ReleaseState.All, null));
        FadeScreen.CrossFade(Color.clear, Color.black, FadeTime);
        yield return new WaitForSecondsRealtime(FadeTime);
        yield return TeleportPlayer();

        var despawn = StartDespawn(preview);
        yield return WaitFor(despawn, 30f);
        bool spawned = false;
        if (despawn != null && despawn.Status == TaskStatus.RanToCompletion && despawn.Result)
        {
            yield return null;
            yield return null;
            _pending = state;
            var task = StartSpawn(preview);
            yield return WaitFor(task, 60f);
            spawned = task != null && task.Status == TaskStatus.RanToCompletion && task.Result;
        }
        else
        {
            Plugin.Log.LogError($"{P} Despawning '{preview.ShipName}' failed or timed out.");
        }

        float t = 0f;
        while (spawned && !state.Applied && t < 10f)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        _pending = null;

        if (!spawned)
            Plugin.Log.LogError($"{P} Respawning '{preview.ShipName}' failed.");
        else if (!state.Applied)
            Plugin.Log.LogError($"{P} '{preview.ShipName}' respawned but the reset was not applied (total salvage value never computed).");
        else
            Reapply(state);

        FadeScreen.CrossFade(Color.black, Color.clear, FadeTime);
        _running = false;
    }

    private static IEnumerator WaitFor(Task task, float timeout)
    {
        float t = 0f;
        while (task != null && !task.IsCompleted && t < timeout)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
        }
    }

    private static Task<bool> StartDespawn(ShipPreview preview)
    {
        try
        {
            return preview.DespawnModuleAsync(unloadModuleAssets: false);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Starting the despawn failed: {e}");
            return null;
        }
    }

    private static Task<bool> StartSpawn(ShipPreview preview)
    {
        try
        {
            Plugin.Log.LogInfo($"{P} Respawning '{preview.ShipName}' ({preview.ConstructionAssetName} seed {preview.Seed}).");
            return preview.SpawnModuleWithoutSaveDataAsync(ModuleService.Instance.CurrentSpawnPoint.ToRigidTransform());
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Starting the respawn failed: {e}");
            return null;
        }
    }

    private static ResetState Capture(ShipPreview preview)
    {
        try
        {
            if (!ShipInspector.TryGetContext(out var em, out var manifest, out var current) || current != preview)
            {
                Plugin.Log.LogWarning($"{P} Reset skipped: the ship is no longer spawned.");
                return null;
            }
            var units = ShipInspector.Collect(em);
            var sys = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<SalvageableSystem>();
            var state = new ResetState
            {
                Preview = preview,
                Alive = new HashSet<ulong>(units.Where(u => u.MemberId != 0).Select(u => u.MemberId)),
                Salvaged = ShipInspector.ReadProgress(em, manifest).CurrentSalvagedCreditValue,
                HandledMass = _handledMass(sys),
                SalvagedItems = sys.SalvagedSalvageables.ToList(),
                DestroyedItems = sys.DestroyedSalvageables.ToList(),
                SoldOffItems = sys.SoldOffSalvageables.ToList(),
                SalvagedCores = sys.SalvagedCoreSystems.ToList(),
                DestroyedCores = sys.DestroyedCoreSystems.ToList(),
            };
            Plugin.Log.LogInfo($"{P} Before reset: {units.Count} units left, {ShipInspector.Fmt(ShipInspector.ReadProgress(em, manifest))}");
            return state;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Reading the ship before the reset failed: {e}");
            return null;
        }
    }

    /// <summary>Called when the respawned ship's total salvage value is computed. Returns true when it handled the spawn.</summary>
    public static bool TryFinish()
    {
        var state = _pending;
        if (state == null || state.Applied)
            return false;
        try
        {
            if (!ShipInspector.TryGetContext(out var em, out var manifest, out var preview) || preview != state.Preview)
                return false;
            var locked = PartRandoSlot.Active ? PartRandoSlot.LockedPartTypes() : new HashSet<string>();
            var stripped = Stripped(preview);
            bool known(ulong id) => id == 0 || state.Alive.Contains(id) || stripped.Contains(id);

            var remove = new List<SalvageUnit>();
            var restripped = new List<SalvageUnit>();
            var removeMembers = new List<Entity>();
            var restored = new List<SalvageUnit>();
            float keptValue = 0f;
            foreach (var u in ShipInspector.Collect(em))
            {
                if (!u.IsGroup && u.GroupEntity != Entity.Null)
                    continue;
                if (u.CountsInTotal && locked.Contains(u.ObjectInfoAssetName))
                {
                    remove.Add(u);
                    restripped.Add(u);
                    continue;
                }
                if (!known(u.MemberId))
                {
                    remove.Add(u);
                    continue;
                }
                if (u.MemberId != 0 && !state.Alive.Contains(u.MemberId))
                    restored.Add(u);
                if (u.CountsInTotal)
                    keptValue += u.CreditValue;
                foreach (var m in u.Members)
                    if (!known(ShipInspector.MemberId(em, m)))
                        removeMembers.Add(m);
            }

            var removedEntities = new HashSet<Entity>(removeMembers);
            foreach (var u in remove)
            {
                removedEntities.Add(u.Entity);
                foreach (var m in u.Members) removedEntities.Add(m);
            }
            if (ShipStripConfig.VentBeforeStrip.Value && removedEntities.Count > 0)
                RoomVenting.VentRoomsTouching(em, removedEntities);
            foreach (var u in remove)
                foreach (var m in u.Members)
                    DestroyObjectsSystem.TryMarkForDestruction(em, m);
            foreach (var e in removeMembers)
                DestroyObjectsSystem.TryMarkForDestruction(em, e);
            foreach (var u in remove)
                DestroyObjectsSystem.TryMarkForDestruction(em, u.Entity);
            RecordStripped(em, preview, restripped);
            ForgetStripped(em, preview, restored);

            var p = ShipInspector.ReadProgress(em, manifest);
            p.CurrentSalvagedCreditValue = Mathf.Min(state.Salvaged, p.TotalCreditValue);
            float max = p.TotalCreditValue - p.CurrentSalvagedCreditValue;
            p.CurrentDestroyedCreditValue = Mathf.Clamp(max - keptValue, 0f, max);
            em.SetComponentData(manifest, p);
            state.Progress = p;
            state.Applied = true;
            RestoreSalvageSystem(state);
            Main.EventSystem.Post(TotalSalvageValueCalculatedEvent.GetEvent(p.TotalCreditValue, 0f));
            Plugin.Log.LogInfo($"{P} Reset applied: removed {remove.Count} units ({restripped.Count} still locked) and {removeMembers.Count} group members, " +
                               $"restored {restored.Count} unlocked units. Now: {ShipInspector.Fmt(p)}");
        }
        catch (Exception e)
        {
            state.Applied = true;
            Plugin.Log.LogError($"{P} Applying the reset failed: {e}");
        }
        return true;
    }

    // Spawn listeners may run after TryFinish and clear the salvage bookkeeping again.
    private static void Reapply(ResetState state)
    {
        try
        {
            RestoreSalvageSystem(state);
            if (!ShipInspector.TryGetContext(out var em, out var manifest, out var preview) || preview != state.Preview)
                return;
            var p = ShipInspector.ReadProgress(em, manifest);
            if (p.CurrentSalvagedCreditValue >= state.Progress.CurrentSalvagedCreditValue)
                return;
            p.CurrentSalvagedCreditValue = state.Progress.CurrentSalvagedCreditValue;
            p.CurrentDestroyedCreditValue = state.Progress.CurrentDestroyedCreditValue;
            em.SetComponentData(manifest, p);
            Main.EventSystem.Post(TotalSalvageValueCalculatedEvent.GetEvent(p.TotalCreditValue, 0f));
            Plugin.Log.LogInfo($"{P} Salvage progress set again after the spawn: {ShipInspector.Fmt(p)}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Restoring the salvage progress failed: {e}");
        }
    }

    private static void RestoreSalvageSystem(ResetState state)
    {
        var sys = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<SalvageableSystem>();
        if (_handledMass(sys) < state.HandledMass)
            _handledMass(sys) = state.HandledMass;
        foreach (var kv in state.SalvagedItems) sys.SalvagedSalvageables[kv.Key] = kv.Value;
        foreach (var kv in state.DestroyedItems) sys.DestroyedSalvageables[kv.Key] = kv.Value;
        foreach (var kv in state.SoldOffItems) sys.SoldOffSalvageables[kv.Key] = kv.Value;
        foreach (var kv in state.SalvagedCores) sys.SalvagedCoreSystems[kv.Key] = kv.Value;
        foreach (var kv in state.DestroyedCores) sys.DestroyedCoreSystems[kv.Key] = kv.Value;
    }

    private static IEnumerator TeleportPlayer()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var player = PlayerProfileService.Instance?.Profile?.PlayerEntity ?? Entity.Null;
        if (!em.Exists(player) || !em.HasComponent<RespawnLocation>(player) || !em.HasComponent<Transform>(player))
        {
            Plugin.Log.LogWarning($"{P} Player respawn location not found; the player stays in place.");
            yield break;
        }
        var location = em.GetComponentData<RespawnLocation>(player);
        var transform = em.GetComponentObject<Transform>(player);
        var body = em.HasComponent<Rigidbody>(player) ? em.GetComponentObject<Rigidbody>(player) : null;
        transform.position = location.Position;
        transform.rotation = location.Rotation;
        if (body != null)
        {
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
        }
        yield return new WaitForFixedUpdate();
        if (body != null)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    private static string StrippedPath(ShipPreview preview) =>
        Path.Combine(BepInEx.Paths.BepInExRootPath, "ap_stripped_parts", $"{preview.ConstructionAssetName ?? "unknown"}_{preview.Seed}.txt");

    // Ship member ids of the parts stripped from this ship, kept on disk so a reset after a reload can bring them back.
    private static HashSet<ulong> Stripped(ShipPreview preview)
    {
        var path = StrippedPath(preview);
        if (_strippedCache.TryGetValue(path, out var ids))
            return ids;
        ids = new HashSet<ulong>();
        try
        {
            if (File.Exists(path))
                foreach (var line in File.ReadAllLines(path))
                    if (ulong.TryParse(line.Trim(), out var id)) ids.Add(id);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Reading {path} failed: {e.Message}");
        }
        _strippedCache[path] = ids;
        return ids;
    }

    public static void RecordStripped(EntityManager em, ShipPreview preview, List<SalvageUnit> units) =>
        UpdateStripped(em, preview, units, add: true);

    // A restored part is on the ship again, so salvaging it later must not bring it back.
    private static void ForgetStripped(EntityManager em, ShipPreview preview, List<SalvageUnit> units) =>
        UpdateStripped(em, preview, units, add: false);

    private static void UpdateStripped(EntityManager em, ShipPreview preview, List<SalvageUnit> units, bool add)
    {
        try
        {
            var ids = Stripped(preview);
            int before = ids.Count;
            foreach (var u in units)
            {
                var unitIds = u.Members.Select(m => ShipInspector.MemberId(em, m)).ToList();
                unitIds.Add(u.MemberId);
                unitIds.RemoveAll(id => id == 0);
                foreach (var id in unitIds)
                    if (add) ids.Add(id); else ids.Remove(id);
            }
            if (ids.Count == before)
                return;
            var path = StrippedPath(preview);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, ids.Select(i => i.ToString()).ToArray());
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Saving the stripped parts failed: {e.Message}");
        }
    }
}
