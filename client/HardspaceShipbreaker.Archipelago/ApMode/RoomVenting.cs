#nullable disable
using System;
using System.Collections.Generic;
using BBI.Unity.Game;
using Unity.Collections;
using Unity.Entities;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal static class RoomVenting
{
    private const string P = "[HS-STRIP]";

    public static void VentRoomsTouching(EntityManager em, HashSet<Entity> stripped)
    {
        try
        {
            int sealing = 0;
            foreach (var e in stripped)
                if (em.HasComponent<StructurePartData>(e) && (bool)em.GetComponentData<StructurePartData>(e).CanSeal)
                    sealing++;

            var rooms = new HashSet<Entity>();
            int linkedRooms = 0, linkedOpenings = 0;

            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<RoomComponent>(), ComponentType.ReadOnly<PanelConnection>()))
            using (var arr = q.ToEntityArray(Allocator.TempJob))
                foreach (var room in arr)
                    if (BufferTouches(em.GetBuffer<PanelConnection>(room), stripped)) { rooms.Add(room); linkedRooms++; }

            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<OpeningComponent>(), ComponentType.ReadOnly<PanelConnection>()))
            using (var arr = q.ToEntityArray(Allocator.TempJob))
                foreach (var opening in arr)
                {
                    if (!BufferTouches(em.GetBuffer<PanelConnection>(opening), stripped)) continue;
                    linkedOpenings++;
                    var oc = em.GetComponentData<OpeningComponent>(opening);
                    if (oc.RoomA.IsValid) rooms.Add(oc.RoomA.Entity);
                    if (oc.RoomB.IsValid) rooms.Add(oc.RoomB.Entity);
                }

            bool fallback = false;
            if (rooms.Count == 0 && sealing > 0)
            {
                fallback = true;
                using var q = em.CreateEntityQuery(ComponentType.ReadOnly<RoomComponent>());
                using var arr = q.ToEntityArray(Allocator.TempJob);
                foreach (var room in arr) rooms.Add(room);
            }

            var groups = new HashSet<Entity>();
            foreach (var room in rooms)
                if (em.Exists(room) && em.HasComponent<RoomComponent>(room))
                {
                    var g = em.GetComponentData<RoomComponent>(room).RoomGroupEntity;
                    if (g != Entity.Null && em.Exists(g)) groups.Add(g);
                }
            if (groups.Count > 0)
            {
                using var q = em.CreateEntityQuery(ComponentType.ReadOnly<RoomComponent>());
                using var arr = q.ToEntityArray(Allocator.TempJob);
                foreach (var room in arr)
                    if (groups.Contains(em.GetComponentData<RoomComponent>(room).RoomGroupEntity)) rooms.Add(room);
            }

            int ventedRooms = 0, wasPressurized = 0;
            foreach (var room in rooms)
            {
                try
                {
                    if (!em.Exists(room) || !em.HasComponent<RoomComponent>(room)) continue;
                    var rc = em.GetComponentData<RoomComponent>(room);
                    if (rc.AirPressure > 0f) wasPressurized++;
                    rc.AirPressure = 0f;
                    rc.AirPressureState = RoomPressurization.Decompressed;
                    rc.AirPressureChangeRequest = 0;
                    em.SetComponentData(room, rc);
                    SetPressureComponents(em, room);
                    ventedRooms++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{P} Vent room {room} failed: {ex.Message}");
                }
            }
            foreach (var g in groups)
            {
                try
                {
                    if (!em.HasComponent<RoomGroup>(g)) continue;
                    var rg = em.GetComponentData<RoomGroup>(g);
                    rg.AirPressure = 0f;
                    rg.AirPressureState = RoomPressurization.Decompressed;
                    rg.AirPressureChangeRequest = 0;
                    em.SetComponentData(g, rg);
                    SetPressureComponents(em, g);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{P} Vent room group {g} failed: {ex.Message}");
                }
            }

            Plugin.Log.LogInfo($"{P} VentBeforeStrip: {sealing}/{stripped.Count} stripped entities can seal; " +
                               $"panel links hit {linkedRooms} rooms + {linkedOpenings} openings{(fallback ? " (no links found: venting all rooms)" : "")}; " +
                               $"vented {ventedRooms} rooms in {groups.Count} room groups ({wasPressurized} had pressure).");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{P} VentBeforeStrip failed: {e}");
        }
    }

    private static void SetPressureComponents(EntityManager em, Entity e)
    {
        if (em.HasComponent<AirPressure>(e)) em.SetComponentData(e, new AirPressure { Value = 0f });
        if (em.HasComponent<PreviousAirPressure>(e)) em.SetComponentData(e, new PreviousAirPressure { Value = 0f });
        if (em.HasComponent<AirPressureState>(e)) em.SetComponentData(e, new AirPressureState { Value = RoomPressurization.Decompressed });
        if (em.HasComponent<PreviousAirPressureState>(e)) em.SetComponentData(e, new PreviousAirPressureState { Value = RoomPressurization.Decompressed });
    }

    private static bool BufferTouches(DynamicBuffer<PanelConnection> buf, HashSet<Entity> set)
    {
        for (int i = 0; i < buf.Length; i++)
            if (set.Contains(buf[i].Value)) return true;
        return false;
    }
}
