#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal sealed class SlotShip
{
    public int Index;
    public string Id;
    public string ShipClass;
    public int Grade;
    public int Hull;
    public int Seed;
    public string Construction;
    public string Name;
    public float Total;
    public List<float> Markers = new();
    public List<float> CheckAt = new();
    public List<long> Locations = new();
    public Dictionary<string, float> Parts = new();
    public int UnlocksNeeded;
}

internal static class PartRandoSlot
{
    public const int GameModePartRando = 1;
    private const string P = "[HS-AP]";

    public static bool Active { get; private set; }
    public static int GoalPercent { get; private set; }
    public static int SalvageMargin { get; private set; }
    public static List<SlotShip> Ships { get; private set; } = new();
    public static Dictionary<string, long> PartItems { get; private set; } = new();
    public static Dictionary<string, long> PartsanityLocations { get; private set; } = new();
    public static long? ShipUnlockItem { get; private set; }

    public static bool IsPartRando(Dictionary<string, object> slotData) =>
        slotData != null && slotData.TryGetValue("game_mode", out var mode) && Convert.ToInt32(mode) == GameModePartRando;

    public static void Load(Dictionary<string, object> slotData)
    {
        Active = false;
        if (!IsPartRando(slotData))
            return;
        try
        {
            GoalPercent = Convert.ToInt32(slotData["goal_percent"]);
            SalvageMargin = Convert.ToInt32(slotData["salvage_margin"]);
            PartItems = JToken.FromObject(slotData["part_items"]).ToObject<Dictionary<string, long>>();
            PartsanityLocations = JToken.FromObject(slotData["partsanity_locations"]).ToObject<Dictionary<string, long>>();
            ShipUnlockItem = slotData.TryGetValue("ship_unlock_item", out var unlockItem) && unlockItem != null
                ? Convert.ToInt64(unlockItem)
                : null;
            var ships = JToken.FromObject(slotData["ships"]).Select(s => new SlotShip
            {
                Index = s.Value<int>("index"),
                Id = s.Value<string>("id"),
                ShipClass = s.Value<string>("shipClass"),
                Grade = s.Value<int>("grade"),
                Hull = s.Value<int>("hull"),
                Seed = s.Value<int>("seed"),
                Construction = s.Value<string>("construction"),
                Name = s.Value<string>("name"),
                Total = s.Value<float>("total"),
                Markers = s["markers"].ToObject<List<float>>(),
                Locations = s["locations"].ToObject<List<long>>(),
                Parts = s["parts"]?.ToObject<Dictionary<string, float>>() ?? new Dictionary<string, float>(),
                UnlocksNeeded = s["unlocks_needed"]?.ToObject<int>() ?? 0,
            }).ToList();
            foreach (var ship in ships)
                ship.CheckAt = ship.Markers.Select(m => m * (100 - SalvageMargin) / 100f).ToList();
            Ships = ships;
            Active = true;
            Plugin.Log.LogInfo($"{P} Part Rando slot: {Ships.Count} ships, {PartItems.Count} part types, " +
                               $"{PartsanityLocations.Count} partsanity checks, goal {GoalPercent}% on every ship, margin {SalvageMargin}%.");
            foreach (var s in Ships)
                Plugin.Log.LogInfo($"{P}   ship {s.Index}: {s.Id} '{s.Name}' ({s.ShipClass} hull {s.Hull} seed {s.Seed}), markers [{string.Join(", ", s.Markers)}] checked at [{string.Join(", ", s.CheckAt.Select(c => c.ToString("F1")))}]");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} Reading the Part Rando slot data failed: {e}");
        }
    }

    public static int ShipUnlocksReceived() =>
        ShipUnlockItem is long id ? Plugin.Instance.Client?.ReceivedItemCount(id) ?? 0 : 0;

    public static bool IsUnlocked(SlotShip ship) => ship.UnlocksNeeded <= ShipUnlocksReceived();

    public static float LockedValue(SlotShip ship)
    {
        var locked = LockedPartTypes();
        return ship.Parts.Where(kv => locked.Contains(kv.Key)).Sum(kv => kv.Value);
    }

    public static HashSet<string> LockedPartTypes()
    {
        var received = Plugin.Instance.Client?.ReceivedItemIds() ?? new HashSet<long>();
        return new HashSet<string>(PartItems.Where(kv => !received.Contains(kv.Value)).Select(kv => kv.Key));
    }
}
