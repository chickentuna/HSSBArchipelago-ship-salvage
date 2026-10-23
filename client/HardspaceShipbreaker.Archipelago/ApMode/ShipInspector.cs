#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BBI.Unity.Game;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal sealed class SalvageUnit
{
    public Entity Entity;
    public bool IsGroup;
    public bool CountsInTotal;
    public string GameObjectName = "";
    public string ObjectInfoAssetName = "";
    public string ObjectName = "";
    public List<string> CategoryAssetNames = new();
    public List<string> CategoryNames = new();
    public float Mass;
    public float CreditValue;
    public bool HasCreditReward;
    public Entity GroupEntity = Entity.Null;
    public List<Entity> Members = new();
    public Vector3? Position;
    public SalvageableComponent Salvageable;
    public bool CanSeal;
}

internal static class ShipInspector
{
    private const string P = "[HS-STRIP]";

    private static readonly EntityQueryDesc PartsDesc = new()
    {
        All = new[] { ComponentType.ReadOnly<SalvageableComponent>(), ComponentType.ReadOnly<StructurePart>() },
        None = new[] { ComponentType.ReadOnly<PendingDestruction>() }
    };

    private static readonly EntityQueryDesc GroupsDesc = new()
    {
        All = new[] { ComponentType.ReadOnly<SalvageableComponent>(), ComponentType.ReadOnly<StructureGroup>() },
        None = new[] { ComponentType.ReadOnly<PendingDestruction>() }
    };

    private static bool TryGetContext(out EntityManager em, out Entity manifest, out ShipPreview preview)
    {
        em = default;
        manifest = Entity.Null;
        preview = null;
        var world = World.DefaultGameObjectInjectionWorld;
        var ms = ModuleService.Instance;
        if (world == null || !world.IsCreated || ms == null || ms.CurrentShipPreview == null)
            return false;
        em = world.EntityManager;
        preview = ms.CurrentShipPreview;
        manifest = ms.CurrentManifest;
        return manifest != Entity.Null && em.Exists(manifest) && em.HasComponent<ShipCurrentSalvageProgress>(manifest);
    }

    private static AssetTypeID<CurrencyAsset> CreditsId()
    {
        var credits = Main.Instance != null && Main.Instance.MainSettings != null
            ? Main.Instance.MainSettings.PlayerSettings.CreditsCurrencyAsset
            : null;
        return credits != null ? credits.ID : AssetTypeID<CurrencyAsset>.None;
    }

    private static float ComputeCreditValue(EntityManager em, Entity e, AssetTypeID<CurrencyAsset> creditsId, out bool hasCredit)
    {
        hasCredit = false;
        if (!em.HasComponent<SalvageableRewardBuffer>(e))
            return 0f;
        var mass = em.HasComponent<MassComponent>(e) ? em.GetComponentData<MassComponent>(e) : default;
        var rewards = em.GetBuffer<SalvageableRewardBuffer>(e);
        for (int k = 0; k < rewards.Length; k++)
        {
            var reward = rewards[k];
            if (reward.CurrencyAssetID == creditsId)
            {
                hasCredit = true;
                var curve = em.HasComponent<SalvageableMassToValueCurveBuffer>(e)
                    ? em.GetBuffer<SalvageableMassToValueCurveBuffer>(e)
                    : default;
                return SalvageableUtility.GetValueFromEntityData(e, mass, curve, reward);
            }
        }
        return 0f;
    }

    private static void FillInfo(SalvageUnit u, ObjectInfoAsset info, Component mono)
    {
        try
        {
            if (mono != null)
            {
                u.GameObjectName = mono.gameObject.name;
                u.Position = mono.transform.position;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug($"{P} GameObject read failed for {u.Entity}: {e.Message}");
        }
        if (info == null)
            return;
        u.ObjectInfoAssetName = info.name;
        u.ObjectName = info.Data.ObjectName ?? "";
        var cats = info.Data.Categories;
        if (cats == null)
            return;
        foreach (var c in cats)
        {
            if (c == null)
                continue;
            u.CategoryAssetNames.Add(c.name);
            u.CategoryNames.Add(c.CategoryName ?? "");
        }
    }

    private static List<SalvageUnit> Collect(EntityManager em)
    {
        var creditsId = CreditsId();
        if (creditsId == AssetTypeID<CurrencyAsset>.None)
            Plugin.Log.LogWarning($"{P} Credits currency asset not available; all values will be 0.");

        var units = new List<SalvageUnit>();

        using (var q = em.CreateEntityQuery(PartsDesc))
        using (var parts = q.ToEntityArray(Allocator.TempJob))
        {
            foreach (var e in parts)
            {
                try
                {
                    var u = new SalvageUnit { Entity = e, IsGroup = false };
                    u.Salvageable = em.GetComponentData<SalvageableComponent>(e);
                    if (em.HasComponent<MassComponent>(e))
                        u.Mass = em.GetComponentData<MassComponent>(e).Mass;
                    bool validMember = false;
                    if (em.HasComponent<StructureGroupMember>(e))
                    {
                        var m = em.GetComponentData<StructureGroupMember>(e);
                        validMember = m.GroupIsValid;
                        if (validMember)
                            u.GroupEntity = m.GroupEntity;
                    }
                    u.CreditValue = ComputeCreditValue(em, e, creditsId, out u.HasCreditReward);
                    u.CountsInTotal = !validMember && u.HasCreditReward;
                    u.CanSeal = CanSeal(em, e);
                    var part = em.GetComponentObject<StructurePart>(e);
                    FillInfo(u, part != null ? part.ObjectInfoAsset : null, part);
                    units.Add(u);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{P} Part {e} skipped: {ex.Message}");
                }
            }
        }

        using (var q = em.CreateEntityQuery(GroupsDesc))
        using (var groups = q.ToEntityArray(Allocator.TempJob))
        {
            foreach (var e in groups)
            {
                try
                {
                    var u = new SalvageUnit { Entity = e, IsGroup = true };
                    u.Salvageable = em.GetComponentData<SalvageableComponent>(e);
                    if (em.HasComponent<MassComponent>(e))
                        u.Mass = em.GetComponentData<MassComponent>(e).Mass;
                    u.CreditValue = ComputeCreditValue(em, e, creditsId, out u.HasCreditReward);
                    u.CountsInTotal = u.HasCreditReward;
                    if (em.HasComponent<PartConnection>(e))
                    {
                        var conns = em.GetBuffer<PartConnection>(e);
                        for (int i = 0; i < conns.Length; i++)
                        {
                            u.Members.Add(conns[i].Value);
                            u.CanSeal |= CanSeal(em, conns[i].Value);
                        }
                    }
                    var group = em.GetComponentObject<StructureGroup>(e);
                    FillInfo(u, group != null ? group.ObjectInfoAsset : null, group);
                    units.Add(u);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{P} Group {e} skipped: {ex.Message}");
                }
            }
        }
        return units;
    }

    private static bool CanSeal(EntityManager em, Entity e) =>
        em.Exists(e) && em.HasComponent<StructurePartData>(e) && (bool)em.GetComponentData<StructurePartData>(e).CanSeal;

    private static ShipCurrentSalvageProgress ReadProgress(EntityManager em, Entity manifest) =>
        em.GetComponentData<ShipCurrentSalvageProgress>(manifest);

    private static string Fmt(ShipCurrentSalvageProgress p) =>
        $"Total={p.TotalCreditValue:F2} Salvaged={p.CurrentSalvagedCreditValue:F2} " +
        $"Destroyed={p.CurrentDestroyedCreditValue:F2} Remaining={p.RemainingCreditValue:F2}";

    public static void DumpCurrentShip(bool manual)
    {
        try
        {
            if (!TryGetContext(out var em, out var manifest, out var preview))
            {
                Plugin.Log.LogWarning($"{P} Dump skipped: no ship manifest is spawned.");
                return;
            }
            var units = Collect(em);
            var progress = ReadProgress(em, manifest);
            var counted = units.Where(u => u.CountsInTotal).ToList();
            float partsTotal = counted.Where(u => !u.IsGroup).Sum(u => u.CreditValue);
            float groupsTotal = counted.Where(u => u.IsGroup).Sum(u => u.CreditValue);
            float recomputed = partsTotal + groupsTotal;

            var distinctCats = units.SelectMany(u => u.CategoryAssetNames).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();

            var json = new JsonWriter();
            json.BeginObject();
            json.Prop("manual", manual);
            json.Prop("timestamp", DateTime.Now.ToString("o"));
            json.Prop("constructionAssetName", preview.ConstructionAssetName);
            json.Prop("shipName", preview.ShipName);
            json.Prop("seed", preview.Seed);
            json.Prop("fixedSeedConfig", ShipStripConfig.FixedSeed.Value);
            json.Key("properties").BeginObject();
            TryProp(json, "company", () => preview.CompanyName);
            TryProp(json, "archetype", () => preview.Archetype);
            TryProp(json, "difficulty", () => preview.Difficulty);
            TryProp(json, "condition", () => preview.Condition);
            TryProp(json, "role", () => preview.Role);
            TryProp(json, "size", () => preview.Size);
            TryProp(json, "theme", () => preview.Theme);
            TryProp(json, "valueRange", () => preview.ValueRange);
            json.EndObject();
            json.Key("progress").BeginObject();
            json.Prop("totalCreditValueStored", progress.TotalCreditValue);
            json.Prop("currentSalvagedCreditValue", progress.CurrentSalvagedCreditValue);
            json.Prop("currentDestroyedCreditValue", progress.CurrentDestroyedCreditValue);
            json.EndObject();
            json.Prop("recomputedTotal", recomputed);
            json.Prop("previewTotalCreditValue", preview.TotalCreditValue);
            json.Prop("recomputedPartsTotal", partsTotal);
            json.Prop("recomputedGroupsTotal", groupsTotal);
            json.Prop("unitCount", units.Count);
            json.Prop("countedUnitCount", counted.Count);
            json.Key("distinctCategories").BeginArray();
            foreach (var c in distinctCats) json.Value(c);
            json.EndArray();

            json.Key("categorySummary").BeginArray();
            foreach (var row in CategorySummary(counted, recomputed))
            {
                json.BeginObject();
                json.Prop("category", row.Name);
                json.Prop("count", row.Count);
                json.Prop("value", row.Value);
                json.Prop("percentOfTotal", row.Percent);
                json.EndObject();
            }
            json.EndArray();
            json.Prop("categorySummaryNote",
                "Counted units only (those in the game's total). A unit with several categories is counted in each.");

            json.Key("units").BeginArray();
            foreach (var u in units.OrderBy(u => u.IsGroup).ThenBy(u => u.Entity.Index))
                WriteUnit(json, u);
            json.EndArray();
            json.EndObject();

            var dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "ship_dumps");
            Directory.CreateDirectory(dir);
            var file = $"{Sanitize(preview.ConstructionAssetName)}_{preview.Seed}_{DateTime.Now:yyyyMMdd-HHmmss}{(manual ? "_manual" : "")}.json";
            var path = Path.Combine(dir, file);
            File.WriteAllText(path, json.ToString(), new UTF8Encoding(false));

            Plugin.Log.LogInfo($"{P} Dump written: {path}");
            Plugin.Log.LogInfo($"{P} Ship '{preview.ShipName}' asset={preview.ConstructionAssetName} seed={preview.Seed} " +
                               $"units={units.Count} counted={counted.Count} stored {Fmt(progress)} recomputed={recomputed:F2} " +
                               $"(parts {partsTotal:F2} + groups {groupsTotal:F2}) diff={progress.TotalCreditValue - recomputed:F2} " +
                               $"previewTotal={preview.TotalCreditValue:F2} previewDiff={progress.TotalCreditValue - preview.TotalCreditValue:F2}");
            Plugin.Log.LogInfo($"{P} Categories on this ship ({distinctCats.Count}): {string.Join(", ", distinctCats)}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Dump failed: {e}");
        }
    }

    private struct CatRow
    {
        public string Name;
        public int Count;
        public float Value;
        public float Percent;
    }

    private static IEnumerable<CatRow> CategorySummary(List<SalvageUnit> counted, float total)
    {
        var rows = new Dictionary<string, CatRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in counted)
        {
            var names = u.CategoryAssetNames.Count > 0 ? u.CategoryAssetNames.Distinct() : new[] { "(none)" };
            foreach (var n in names)
            {
                rows.TryGetValue(n, out var r);
                r.Name = n;
                r.Count++;
                r.Value += u.CreditValue;
                rows[n] = r;
            }
        }
        return rows.Values
            .Select(r => { r.Percent = total > 0 ? r.Value / total * 100f : 0f; return r; })
            .OrderByDescending(r => r.Value);
    }

    private static void WriteUnit(JsonWriter json, SalvageUnit u)
    {
        json.BeginObject();
        json.Prop("entityIndex", u.Entity.Index);
        json.Prop("entityVersion", u.Entity.Version);
        json.Prop("isGroup", u.IsGroup);
        json.Prop("countsInTotal", u.CountsInTotal);
        json.Prop("gameObjectName", u.GameObjectName);
        json.Prop("objectInfoAsset", u.ObjectInfoAssetName);
        json.Prop("objectName", u.ObjectName);
        json.Key("categories").BeginArray();
        foreach (var c in u.CategoryAssetNames) json.Value(c);
        json.EndArray();
        json.Key("categoryDisplayNames").BeginArray();
        foreach (var c in u.CategoryNames) json.Value(c);
        json.EndArray();
        json.Prop("mass", u.Mass);
        json.Prop("creditValue", u.CreditValue);
        json.Prop("hasCreditReward", u.HasCreditReward);
        json.Prop("canSeal", u.CanSeal);
        if (u.GroupEntity != Entity.Null)
            json.Prop("groupEntityIndex", u.GroupEntity.Index);
        if (u.IsGroup)
            json.Prop("memberCount", u.Members.Count);
        if (u.Position.HasValue)
        {
            var p = u.Position.Value;
            json.Key("position").BeginArray().Value(p.x).Value(p.y).Value(p.z).EndArray();
        }
        json.Key("destinations").BeginObject();
        json.Prop("barge", (bool)u.Salvageable.CanBeBargeSalvaged);
        json.Prop("pickup", (bool)u.Salvageable.CanBePickupSalvaged);
        json.Prop("furnace", (bool)u.Salvageable.CanBeFurnaceSalvaged);
        json.Prop("processor", (bool)u.Salvageable.CanBeProcessorSalvaged);
        json.Prop("destroy", (bool)u.Salvageable.CanBeDestroySalvaged);
        json.EndObject();
        json.EndObject();
    }

    private static void TryProp(JsonWriter json, string key, Func<string> get)
    {
        try { json.Prop(key, get()); }
        catch { json.Prop(key, (string)null); }
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "unknown";
        var bad = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) sb.Append(Array.IndexOf(bad, ch) >= 0 || ch == ' ' ? '_' : ch);
        return sb.ToString();
    }

    private static List<string> SplitList(string s) =>
        (s ?? "").Split(new[] { ',' }).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

    private static bool Matches(SalvageUnit u, List<string> cats, List<string> names)
    {
        if (cats.Count > 0)
        {
            bool hit = cats.Any(c =>
                u.CategoryAssetNames.Any(n => string.Equals(n, c, StringComparison.OrdinalIgnoreCase)) ||
                u.CategoryNames.Any(n => string.Equals(n, c, StringComparison.OrdinalIgnoreCase)));
            if (!hit) return false;
        }
        if (names.Count > 0)
        {
            bool hit = names.Any(s =>
                u.ObjectName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 ||
                u.ObjectInfoAssetName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0 ||
                u.GameObjectName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hit) return false;
        }
        return true;
    }

    public static void StripCurrentShip()
    {
        var cats = SplitList(ShipStripConfig.StripCategories.Value);
        var names = SplitList(ShipStripConfig.StripNameContains.Value);
        if (cats.Count == 0 && names.Count == 0)
        {
            Plugin.Log.LogWarning($"{P} Strip skipped: StripCategories and StripNameContains are both empty.");
            return;
        }
        StripUnits(u => Matches(u, cats, names), $"cats=[{string.Join(",", cats)}] names=[{string.Join(",", names)}]");
    }

    public static void StripPartTypes(ICollection<string> objectInfoAssetNames)
    {
        StripUnits(u => objectInfoAssetNames.Contains(u.ObjectInfoAssetName),
            $"{objectInfoAssetNames.Count} locked part types");
    }

    private static void StripUnits(Func<SalvageUnit, bool> match, string label)
    {
        try
        {
            if (!TryGetContext(out var em, out var manifest, out var preview))
            {
                Plugin.Log.LogWarning($"{P} Strip skipped: no ship manifest is spawned.");
                return;
            }

            var targets = Collect(em).Where(u => u.CountsInTotal && match(u)).ToList();
            float targetValue = targets.Sum(u => u.CreditValue);
            var before = ReadProgress(em, manifest);
            Plugin.Log.LogInfo($"{P} Strip {label}: {targets.Count} units " +
                               $"({targets.Count(u => u.IsGroup)} groups), value {targetValue:F2}. Before: {Fmt(before)}");

            if (ShipStripConfig.VentBeforeStrip.Value && targets.Count > 0)
            {
                var strippedSet = new HashSet<Entity>();
                foreach (var u in targets)
                {
                    strippedSet.Add(u.Entity);
                    foreach (var m in u.Members) strippedSet.Add(m);
                }
                RoomVenting.VentRoomsTouching(em, strippedSet);
            }

            int marked = 0, failed = 0;
            float strippedValue = 0f;
            foreach (var u in targets)
            {
                try
                {
                    foreach (var m in u.Members)
                        if (DestroyObjectsSystem.TryMarkForDestruction(em, m)) marked++; else failed++;
                    if (DestroyObjectsSystem.TryMarkForDestruction(em, u.Entity)) { marked++; strippedValue += u.CreditValue; }
                    else failed++;
                    Plugin.Log.LogDebug($"{P}   stripped {(u.IsGroup ? "group" : "part")} {u.Entity} '{u.GameObjectName}' " +
                                        $"[{string.Join(",", u.CategoryAssetNames)}] value {u.CreditValue:F2}");
                }
                catch (Exception ex)
                {
                    failed++;
                    Plugin.Log.LogWarning($"{P} Strip of {u.Entity} failed: {ex.Message}");
                }
            }
            var after = ReadProgress(em, manifest);
            Plugin.Log.LogInfo($"{P} Strip marked {marked} entities for destruction ({failed} failed). Immediately: {Fmt(after)}");
            if (ShipStripConfig.ShowStrippedAsDestroyed.Value && strippedValue > 0f)
                ShowAsDestroyed(em, manifest, strippedValue);
            Plugin.Instance.StartCoroutine(FollowUp(manifest, before.CurrentDestroyedCreditValue, before.TotalCreditValue));
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Strip failed: {e}");
        }
    }

    private static void ShowAsDestroyed(EntityManager em, Entity manifest, float strippedValue)
    {
        try
        {
            var p = ReadProgress(em, manifest);
            float max = p.TotalCreditValue - p.CurrentSalvagedCreditValue;
            p.CurrentDestroyedCreditValue = Mathf.Clamp(p.CurrentDestroyedCreditValue + strippedValue, 0f, max);
            em.SetComponentData(manifest, p);
            Main.EventSystem.Post(TotalSalvageValueCalculatedEvent.GetEvent(p.TotalCreditValue, 0f));
            Plugin.Log.LogInfo($"{P} ShowStrippedAsDestroyed: added {strippedValue:F2} to Destroyed " +
                               $"({strippedValue / p.TotalCreditValue * 100f:F1}% of Total). Now: {Fmt(ReadProgress(em, manifest))}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} ShowStrippedAsDestroyed failed: {e}");
        }
    }

    private static IEnumerator FollowUp(Entity manifest, float destroyedBefore, float totalBefore)
    {
        float elapsed = 0f;
        foreach (var at in new[] { 1f, 3f, 10f })
        {
            yield return new WaitForSecondsRealtime(at - elapsed);
            elapsed = at;
            LogFollowUp(manifest, $"+{at:0}s", destroyedBefore, totalBefore);
        }
    }

    private static void LogFollowUp(Entity manifest, string label, float destroyedBefore, float totalBefore)
    {
        try
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            var em = world.EntityManager;
            if (!em.Exists(manifest) || !em.HasComponent<ShipCurrentSalvageProgress>(manifest))
            {
                Plugin.Log.LogWarning($"{P} Strip {label}: manifest is gone.");
                return;
            }
            var p = ReadProgress(em, manifest);
            Plugin.Log.LogInfo($"{P} Strip {label}: {Fmt(p)} | Total changed by {p.TotalCreditValue - totalBefore:F2}, " +
                               $"Destroyed rose by {p.CurrentDestroyedCreditValue - destroyedBefore:F2}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Strip {label} check failed: {e}");
        }
    }
}
