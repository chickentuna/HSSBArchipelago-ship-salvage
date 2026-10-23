#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BBI.Unity.Game;
using HarmonyLib;
using UnityEngine;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class ShipCatalog
{
    private const string P = "[HS-CATALOG]";

    private static readonly AccessTools.FieldRef<StructurePart, ObjectInfoAsset> PartInfoOverride =
        AccessTools.FieldRefAccess<StructurePart, ObjectInfoAsset>("m_ObjectInfoAssetOverride");

    public static bool Running { get; private set; }
    private static bool _started;
    private static float _readySince = -1f;

    private sealed class CatRow
    {
        public int Count;
        public float Value;
    }

    public static void Update()
    {
        if (_started || !ShipStripConfig.BuildCatalog.Value)
            return;
        bool ready = Main.Instance != null && Main.Instance.MainSettings?.HabSettings?.JobBoardSettings != null &&
                     PlayerProfileService.Instance != null && GameSession.CurrentSessionType == GameSession.SessionType.None;
        if (!ready)
        {
            _readySince = -1f;
            return;
        }
        if (_readySince < 0f)
            _readySince = Time.realtimeSinceStartup;
        if (Time.realtimeSinceStartup - _readySince < 5f)
            return;
        _started = true;
        _ = RunSafe();
    }

    private static async Task RunSafe()
    {
        Running = true;
        try
        {
            await Run();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Catalog build failed: {e}");
        }
        finally
        {
            Running = false;
        }
    }

    private static List<int> Seeds()
    {
        var seeds = new List<int>();
        foreach (var s in (ShipStripConfig.CatalogSeeds.Value ?? "").Split(new[] { ',' }))
            if (int.TryParse(s.Trim(), out var v) && !seeds.Contains(v))
                seeds.Add(v);
        int k = 0;
        while (seeds.Count < ShipStripConfig.CatalogSeedsPerHull.Value)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (var ch in $"catalog|{k++}")
                    h = (h ^ ch) * 16777619;
                int v = (int)(h & 0x7FFFFFFF);
                if (!seeds.Contains(v)) seeds.Add(v);
            }
        }
        return seeds.Take(Math.Max(ShipStripConfig.CatalogSeedsPerHull.Value, 0)).ToList();
    }

    private static async Task Run()
    {
        var credits = Main.Instance.MainSettings.PlayerSettings.CreditsCurrencyAsset;
        var currencies = new List<CurrencyAsset> { credits };
        var classes = Main.Instance.MainSettings.HabSettings.JobBoardSettings.ShipClasses;
        var seeds = Seeds();
        var root = Path.Combine(BepInEx.Paths.BepInExRootPath, "ship_catalog");
        Directory.CreateDirectory(root);
        var summaryPath = Path.Combine(root, "catalog.jsonl");

        int total = classes.Sum(c => c?.GeneratableShips?.Length ?? 0) * seeds.Count;
        int done = 0, written = 0, skipped = 0, failed = 0;
        Plugin.Log.LogInfo($"{P} Building catalog: {classes.Length} ship classes, {total} ships at most, seeds [{string.Join(",", seeds)}] -> {root}");

        using var cache = new AddressableCache();
        foreach (var cls in classes)
        {
            if (cls?.GeneratableShips == null) continue;
            for (int hull = 0; hull < cls.GeneratableShips.Length; hull++)
            {
                var pair = cls.GeneratableShips[hull];
                foreach (var seed in seeds)
                {
                    done++;
                    var file = Path.Combine(root, Sanitize(cls.name), $"hull{hull}_{seed}.json");
                    if (File.Exists(file)) { skipped++; continue; }
                    string json = null, summary = null;
                    try
                    {
                        var spawnParams = new ShipSpawnParams { SessionType = GameSession.SessionType.Career };
                        var opt = new ShipPreview.OptionalCreateParams
                        {
                            ShipClassAsset = cls,
                            PropertyOverridesRef = pair.OverrideRef,
                            DaysRemaining = 1
                        };
                        var preview = await ShipPreview.CreateAsync(seed, pair.ShipRef, spawnParams, opt, cache,
                            (pv, groupSummary) =>
                            {
                                try { (json, summary) = Describe(cls, hull, pv, groupSummary, currencies); }
                                catch (Exception ex) { Plugin.Log.LogError($"{P} Reading {cls.name} hull {hull} seed {seed} failed: {ex}"); }
                                return Task.CompletedTask;
                            });
                        if (preview == null || json == null)
                        {
                            failed++;
                            Plugin.Log.LogWarning($"{P} {cls.name} hull {hull} seed {seed}: no ship generated.");
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(file));
                            File.WriteAllText(file, json, new UTF8Encoding(false));
                            File.AppendAllText(summaryPath, summary + "\n", new UTF8Encoding(false));
                            written++;
                        }
                        preview?.UnloadModuleAssets();
                    }
                    catch (Exception e)
                    {
                        failed++;
                        Plugin.Log.LogError($"{P} {cls.name} hull {hull} seed {seed} failed: {e}");
                    }
                    if (done % 10 == 0)
                        Plugin.Log.LogInfo($"{P} {done}/{total} (written {written}, already there {skipped}, failed {failed})");
                }
            }
        }
        Plugin.Log.LogInfo($"{P} Catalog done: written {written}, already there {skipped}, failed {failed}. Files in {root}");
        ShipStripConfig.BuildCatalog.Value = false;
    }

    private static (string json, string summary) Describe(ShipClassAsset cls, int hull, ShipPreview pv,
        ModuleGroupSummary groupSummary, List<CurrencyAsset> currencies)
    {
        var cats = new Dictionary<string, CatRow>(StringComparer.OrdinalIgnoreCase);
        float total = 0f, mass = 0f;
        int unitCount = 0;

        var units = new JsonWriter();
        units.BeginArray();
        foreach (var module in groupSummary.moduleSummaries ?? new ModuleSummary[0])
        {
            int count = Math.Max(module.ModuleCount, 1);
            if (module.Parts != null)
                foreach (var part in module.Parts)
                {
                    if (part == null || part.GetComponentInParent<StructureGroup>() != null) continue;
                    SalvageableUtility.GetTotalMassAndSimulatedValueForStructures(currencies, new List<StructurePart> { part }, null, out var m, out var vals);
                    var info = PartInfoOverride(part) ?? part.StructurePartAsset?.Data.ObjectInfoAsset;
                    AddUnit(units, cats, false, part.gameObject.name, info, m, vals[0].Value, count, ref total, ref mass, ref unitCount);
                }
            if (module.Groups != null)
                foreach (var group in module.Groups)
                {
                    if (group == null) continue;
                    SalvageableUtility.GetTotalMassAndSimulatedValueForStructures(currencies, null, new List<StructureGroup> { group }, out var m, out var vals);
                    AddUnit(units, cats, true, group.gameObject.name, group.ObjectInfoAsset, m, vals[0].Value, count, ref total, ref mass, ref unitCount);
                }
        }
        units.EndArray();

        var j = new JsonWriter();
        j.BeginObject();
        WriteHeader(j, cls, hull, pv, total, mass, unitCount);
        WriteCategories(j, cats, total);
        j.Key("units");
        var s = j.ToString() + units.ToString().Replace("\n", "\n  ") + "\n}";

        var sum = new JsonWriter();
        sum.BeginObject();
        WriteHeader(sum, cls, hull, pv, total, mass, unitCount);
        WriteCategories(sum, cats, total);
        sum.EndObject();
        return (s, sum.ToString().Replace("\n", "").Replace("  ", ""));
    }

    private static void AddUnit(JsonWriter units, Dictionary<string, CatRow> cats, bool isGroup, string name, ObjectInfoAsset info,
        float m, float value, int count, ref float total, ref float mass, ref int unitCount)
    {
        var names = info?.Data.Categories?.Where(c => c != null).Select(c => c.name).Distinct().ToList() ?? new List<string>();
        total += value * count;
        mass += m * count;
        unitCount += count;
        foreach (var n in names.Count > 0 ? names : new List<string> { "(none)" })
        {
            if (!cats.TryGetValue(n, out var row)) cats[n] = row = new CatRow();
            row.Count += count;
            row.Value += value * count;
        }
        units.BeginObject();
        units.Prop("isGroup", isGroup);
        units.Prop("gameObjectName", name);
        units.Prop("objectInfoAsset", info != null ? info.name : null);
        units.Key("categories").BeginArray();
        foreach (var n in names) units.Value(n);
        units.EndArray();
        units.Prop("mass", m);
        units.Prop("creditValue", value);
        units.Prop("count", count);
        units.EndObject();
    }

    private static void WriteHeader(JsonWriter j, ShipClassAsset cls, int hull, ShipPreview pv, float total, float mass, int unitCount)
    {
        j.Prop("shipClass", cls.name);
        j.Prop("hullIndex", hull);
        j.Prop("constructionAssetName", pv.ConstructionAssetName);
        j.Prop("seed", pv.Seed);
        j.Prop("sessionType", "Career");
        j.Prop("propertyOverrides", pv.PropertyOverridesAsset != null ? pv.PropertyOverridesAsset.name : null);
        j.Prop("shipName", pv.ShipName);
        j.Prop("difficulty", pv.Difficulty);
        j.Prop("previewTotalCreditValue", pv.TotalCreditValue);
        j.Prop("unitTotalCreditValue", total);
        j.Prop("mass", mass);
        j.Prop("unitCount", unitCount);
    }

    private static void WriteCategories(JsonWriter j, Dictionary<string, CatRow> cats, float total)
    {
        j.Key("categories").BeginArray();
        foreach (var kv in cats.OrderByDescending(kv => kv.Value.Value))
        {
            j.BeginObject();
            j.Prop("category", kv.Key);
            j.Prop("count", kv.Value.Count);
            j.Prop("value", kv.Value.Value);
            j.Prop("percentOfTotal", total > 0 ? kv.Value.Value / total * 100f : 0f);
            j.EndObject();
        }
        j.EndArray();
    }

    private static string Sanitize(string s)
    {
        var bad = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var ch in s ?? "unknown") sb.Append(Array.IndexOf(bad, ch) >= 0 || ch == ' ' ? '_' : ch);
        return sb.ToString();
    }
}
