-- Visibility + small helpers for access/visibility $rules.

function part_rando_mode()
    return SLOT_DATA ~= nil and tonumber(SLOT_DATA["game_mode"]) == 1
end

--- Return true if location id is in the active pool for current seed options.
function loc_visible(loc_id)
    if part_rando_mode() then
        return false
    end
    local id = tonumber(loc_id)
    if not id then
        return true
    end
    local m = LOCATION_VISIBILITY[id]
    if not m then
        return true
    end

    local density = 1
    if Tracker:ProviderCountForCode("setting_density_sparse") > 0 then
        density = 0
    elseif Tracker:ProviderCountForCode("setting_density_full") > 0 then
        density = 2
    end

    local hab_shop = Tracker:ProviderCountForCode("setting_hab_shop") > 0

    local in_density = false
    if density == 0 then
        in_density = m.sparse
    elseif density == 1 then
        in_density = m.standard
    else
        in_density = m.full
    end

    if not in_density then
        return false
    end
    if m.shop and not hab_shop then
        return false
    end
    return true
end

function has_tether()
    return Tracker:ProviderCountForCode("tether_module") > 0
        or Tracker:ProviderCountForCode("progressive_tether_amount") > 0
        or Tracker:ProviderCountForCode("progressive_tether_lifetime") > 0
        or Tracker:ProviderCountForCode("progressive_tethers") > 0
end

function has_demo()
    return Tracker:ProviderCountForCode("demo_charge_license") > 0
        or Tracker:ProviderCountForCode("progressive_demo_charges") > 0
end

function has_atlas()
    return Tracker:ProviderCountForCode("progressive_certification_rank:1") > 0
        or Tracker:ProviderCountForCode("unlock_atlas") > 0
        or Tracker:ProviderCountForCode("progressive_ship_unlock:1") > 0
end

function has_javelin()
    return Tracker:ProviderCountForCode("progressive_certification_rank:1") > 0
        or Tracker:ProviderCountForCode("unlock_javelin") > 0
        or Tracker:ProviderCountForCode("progressive_ship_unlock:2") > 0
end

function has_gecko()
    return Tracker:ProviderCountForCode("progressive_certification_rank:2") > 0
        or Tracker:ProviderCountForCode("unlock_gecko") > 0
        or Tracker:ProviderCountForCode("progressive_ship_unlock:3") > 0
end

local function slot_ship(i)
    if not part_rando_mode() or not SLOT_DATA["ships"] then
        return nil
    end
    return SLOT_DATA["ships"][tonumber(i)]
end

local function has_part(key)
    local code = PART_CODES[key]
    return code ~= nil and Tracker:ProviderCountForCode(code) > 0
end

local function ship_unlocked(ship)
    return (tonumber(ship["unlocks_needed"]) or 0) <= Tracker:ProviderCountForCode("ship_unlocks")
end

function ship_visible(i)
    return slot_ship(i) ~= nil
end

function ship_marker_visible(i, k)
    local ship = slot_ship(i)
    return ship ~= nil and ship["markers"][tonumber(k)] ~= nil
end

function ship_marker(i, k)
    local ship = slot_ship(i)
    if not ship or not ship_unlocked(ship) then
        return false
    end
    local marker = ship["markers"][tonumber(k)]
    if not marker then
        return false
    end
    local held = tonumber(ship["fixed"]) or 0
    local full = held
    for key, value in pairs(ship["parts"]) do
        full = full + value
        if has_part(key) then
            held = held + value
        end
    end
    local need = math.min(marker / 100 * ship["total"], full) - 0.01
    return held >= need
end

function partsanity_visible(key)
    local locations = part_rando_mode() and SLOT_DATA["partsanity_locations"]
    return locations ~= nil and locations ~= false and locations[key] ~= nil
end

function partsanity(key)
    if not part_rando_mode() or not has_part(key) then
        return false
    end
    for _, ship in ipairs(SLOT_DATA["ships"]) do
        if ship["parts"][key] ~= nil and ship_unlocked(ship) then
            return true
        end
    end
    return false
end
