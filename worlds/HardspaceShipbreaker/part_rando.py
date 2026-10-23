from __future__ import annotations

import math
from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Any

from BaseClasses import CollectionState, ItemClassification, Region
from Options import OptionError

from .items import HardspaceShipbreakerItem
from .locations import HardspaceShipbreakerLocation
from .ship_catalog import (
    PART_TYPE_BY_KEY,
    SHIPS,
    CatalogShip,
    marker_location_id,
    marker_location_name,
    part_location_id,
    part_location_name,
)

if TYPE_CHECKING:
    from .world import HardspaceShipbreakerWorld

FILLER_NAME = "Nothing"
SHIP_SALVAGED = "Ship Salvaged"
SHIP_UNLOCK = "Progressive Ship Unlock"
SLACK = 1
ORDER_ATTEMPTS = 200


@dataclass
class PlannedShip:
    index: int
    ship: CatalogShip
    fixed_value: float
    part_values: dict[str, float]
    markers: list[float] = field(default_factory=list)

    @property
    def full_value(self) -> float:
        return self.fixed_value + sum(self.part_values.values())


@dataclass
class ShipPlan:
    ships: list[PlannedShip]
    pool_keys: list[str]
    starting_keys: list[str]
    ship_unlock_copies: int = 0


def _draw_ships(world: HardspaceShipbreakerWorld) -> list[CatalogShip]:
    chosen_ids = list(dict.fromkeys(world.options.part_rando_ships.value))
    if chosen_ids:
        unknown = [i for i in chosen_ids if i not in SHIPS]
        if unknown:
            raise OptionError(f"Hardspace Shipbreaker: unknown ships {unknown}")
        return [SHIPS[i] for i in chosen_ids]

    low, high = sorted((world.options.part_rando_min_ship_grade.value, world.options.part_rando_max_ship_grade.value))
    candidates = [s for s in SHIPS.values() if low <= s.grade <= high]
    world.random.shuffle(candidates)
    seen: set[str] = set()
    unique: list[CatalogShip] = []
    rest: list[CatalogShip] = []
    for ship in candidates:
        (rest if ship.construction in seen else unique).append(ship)
        seen.add(ship.construction)
    return (unique + rest)[: world.options.part_rando_ship_count.value]


def _percent(value: float, total: float, goal: float) -> float:
    return math.floor(min(goal, value / total * 100) * 10) / 10


def _values_by_step(ship: PlannedShip, starting: set[str], order: list[str]) -> list[float]:
    value = ship.fixed_value + sum(v for k, v in ship.part_values.items() if k in starting)
    values = [value]
    for key in order:
        value += ship.part_values.get(key, 0.0)
        values.append(value)
    return values


def _need(ship: PlannedShip, percent: float) -> float:
    return min(percent / 100 * ship.ship.total, ship.full_value) - 0.01


def _marker_steps(world: HardspaceShipbreakerWorld, ship: PlannedShip, values: list[float], goal: float,
                  count: int) -> list[tuple[int, float]]:
    first = _percent(values[0], ship.ship.total, goal)
    spacing = (goal - first) / (count - 1)
    positions = [first]
    for k in range(1, count - 1):
        jitter = world.random.uniform(-0.3, 0.3) * spacing
        positions.append(round(first + spacing * k + jitter, 1))
    positions.append(float(goal))
    positions = sorted(dict.fromkeys(positions))
    return [(next(j for j, v in enumerate(values) if v >= _need(ship, p)), p) for p in positions]


def _unlocks_needed(world: HardspaceShipbreakerWorld, ship: PlannedShip) -> int:
    return ship.index - 1 if world.options.part_rando_ship_unlocks else 0


def _place_markers(world: HardspaceShipbreakerWorld, ships: list[PlannedShip], starting: set[str],
                   pool: list[str], unlock_copies: int) -> bool:
    goal = world.options.part_rando_goal_percent.value
    count = world.options.part_rando_markers_per_ship.value
    tokens = list(pool) + [SHIP_UNLOCK] * unlock_copies
    for _ in range(ORDER_ATTEMPTS):
        order = list(tokens)
        world.random.shuffle(order)
        unlock_steps = [0] + [j for j, t in enumerate(order, start=1) if t == SHIP_UNLOCK]
        ship_open = [unlock_steps[min(_unlocks_needed(world, p), len(unlock_steps) - 1)] for p in ships]
        steps = [_marker_steps(world, p, _values_by_step(p, starting, order), goal, count) for p in ships]
        opened = [0] * (len(order) + 1)
        if world.options.part_rando_partsanity:
            received = {k: 0 for k in starting}
            received.update({t: j for j, t in enumerate(order, start=1) if t != SHIP_UNLOCK})
            for key, step in received.items():
                reachable_from = min(o for p, o in zip(ships, ship_open) if key in p.part_values)
                opened[max(step, reachable_from)] += 1
        for marker_steps, ship_step in zip(steps, ship_open):
            for step, _pct in marker_steps:
                opened[max(step, ship_step)] += 1
        reachable = 0
        slack = SLACK if world.options.part_rando_partsanity else SLACK + 1
        if all((reachable := reachable + opened[j]) >= j + 1 + (slack if j else SLACK) for j in range(len(order))):
            for p, marker_steps in zip(ships, steps):
                p.markers = [pct for _step, pct in marker_steps]
            return True
    return False


def plan(world: HardspaceShipbreakerWorld) -> ShipPlan:
    drawn = sorted(_draw_ships(world), key=lambda s: (s.grade, s.name, s.seed))

    planned = []
    for i, ship in enumerate(drawn, start=1):
        parts = {k: v for k, v in ship.parts.items() if k in PART_TYPE_BY_KEY}
        fixed = sum(v for k, v in ship.parts.items() if k not in PART_TYPE_BY_KEY)
        planned.append(PlannedShip(i, ship, fixed, parts))

    unlock_copies = 0
    if world.options.part_rando_ship_unlocks and len(planned) > 1:
        unlock_copies = len(planned) - 1 + world.options.part_rando_extra_ship_unlocks.value

    all_keys = {k for p in planned for k in p.part_values}
    first = planned[0]
    candidates = sorted(k for k, v in first.part_values.items() if v / first.ship.total >= 0.01) or sorted(first.part_values)
    starting = {world.random.choice(candidates)}
    share = {k: sum(p.part_values.get(k, 0.0) / p.ship.total for p in planned) for k in all_keys}
    pool = sorted(all_keys - starting, key=lambda k: -share[k])
    while not _place_markers(world, planned, starting, pool, unlock_copies):
        if not pool:
            raise OptionError("Hardspace Shipbreaker: these Part Rando options leave too few locations.")
        starting.add(pool.pop())

    return ShipPlan(planned, pool, sorted(starting, key=lambda k: -share[k]), unlock_copies)


def restore_options(world: HardspaceShipbreakerWorld, data: dict[str, Any]) -> None:
    world.options.death_link.value = int(data["death_link"])
    world.options.part_rando_goal_percent.value = data["goal_percent"]
    world.options.part_rando_salvage_margin.value = data["salvage_margin"]
    world.options.part_rando_ship_unlocks.value = int(data["ship_unlocks"])
    world.options.part_rando_markers_per_ship.value = data["markers_per_ship"]
    world.options.part_rando_partsanity.value = int(data["partsanity"])


def plan_from_slot_data(world: HardspaceShipbreakerWorld, data: dict[str, Any]) -> ShipPlan:
    ships = []
    for s in data["ships"]:
        catalog_ship = CatalogShip(s["id"], s["shipClass"], s["grade"], s["hull"], s["seed"], s["construction"],
                                   s["name"], s["total"], dict(s["parts"]))
        planned = PlannedShip(s["index"], catalog_ship, s["fixed"], dict(s["parts"]))
        planned.markers = list(s["markers"])
        ships.append(planned)
    starting = list(data["starting_parts"])
    pool = [k for k in data["part_items"] if k not in starting]
    return ShipPlan(ships, pool, starting, data["ship_unlock_copies"])


def _marker_rule(world: HardspaceShipbreakerWorld, ship: PlannedShip, percent: float):
    player = world.player
    need = _need(ship, percent)
    fixed = ship.fixed_value
    parts = [(PART_TYPE_BY_KEY[k].name, v) for k, v in ship.part_values.items()]
    unlocks = _unlocks_needed(world, ship)

    def rule(state: CollectionState) -> bool:
        if unlocks and not state.has(SHIP_UNLOCK, player, unlocks):
            return False
        held = fixed
        for name, value in parts:
            if state.has(name, player):
                held += value
                if held >= need:
                    return True
        return held >= need

    return rule


def create_regions(world: HardspaceShipbreakerWorld) -> None:
    sp = world.ship_plan
    hab = Region("Hab", world.player, world.multiworld)
    bay = Region("Bay", world.player, world.multiworld)
    world.multiworld.regions += [hab, bay]
    hab.connect(bay, "Hab to Bay")

    for ship in sp.ships:
        for k, percent in enumerate(ship.markers, start=1):
            loc = HardspaceShipbreakerLocation(
                world.player, marker_location_name(ship.index, k), marker_location_id(ship.index, k), bay)
            loc.access_rule = _marker_rule(world, ship, percent)
            bay.locations.append(loc)
        event = HardspaceShipbreakerLocation(world.player, f"Ship {ship.index} Salvaged", None, bay)
        event.access_rule = _marker_rule(world, ship, ship.markers[-1])
        event.place_locked_item(
            HardspaceShipbreakerItem(SHIP_SALVAGED, ItemClassification.progression, None, world.player))
        bay.locations.append(event)

    if world.options.part_rando_partsanity:
        for key in sp.starting_keys + sp.pool_keys:
            part = PART_TYPE_BY_KEY[key]
            unlocks = min(_unlocks_needed(world, p) for p in sp.ships if key in p.part_values)
            loc = HardspaceShipbreakerLocation(world.player, part_location_name(part), part_location_id(part), bay)
            loc.access_rule = lambda state, name=part.name, n=unlocks: (
                state.has(name, world.player) and (n == 0 or state.has(SHIP_UNLOCK, world.player, n)))
            bay.locations.append(loc)

    world.multiworld.completion_condition[world.player] = (
        lambda state: state.has(SHIP_SALVAGED, world.player, len(sp.ships)))


def create_items(world: HardspaceShipbreakerWorld) -> None:
    sp = world.ship_plan
    for key in sp.starting_keys:
        world.multiworld.push_precollected(world.create_item(PART_TYPE_BY_KEY[key].name))
    pool = [world.create_item(PART_TYPE_BY_KEY[k].name) for k in sp.pool_keys]
    pool += [world.create_item(SHIP_UNLOCK) for _ in range(sp.ship_unlock_copies)]
    unfilled = len(world.multiworld.get_unfilled_locations(world.player))
    pool += [world.create_item(FILLER_NAME) for _ in range(unfilled - len(pool))]
    world.multiworld.itempool += pool


def slot_data(world: HardspaceShipbreakerWorld) -> dict[str, Any]:
    sp = world.ship_plan
    keys = sp.starting_keys + sp.pool_keys
    return {
        "game_mode": int(world.options.game_mode.value),
        "death_link": bool(world.options.death_link.value),
        "goal_percent": int(world.options.part_rando_goal_percent.value),
        "salvage_margin": int(world.options.part_rando_salvage_margin.value),
        "part_items": {k: PART_TYPE_BY_KEY[k].item_id for k in keys},
        "starting_parts": sp.starting_keys,
        "ship_unlock_item": world.item_name_to_id[SHIP_UNLOCK] if sp.ship_unlock_copies else None,
        "ship_unlocks": bool(world.options.part_rando_ship_unlocks.value),
        "ship_unlock_copies": sp.ship_unlock_copies,
        "markers_per_ship": int(world.options.part_rando_markers_per_ship.value),
        "partsanity": bool(world.options.part_rando_partsanity.value),
        "partsanity_locations": (
            {k: part_location_id(PART_TYPE_BY_KEY[k]) for k in keys} if world.options.part_rando_partsanity else {}),
        "ships": [
            {
                "index": s.index,
                "id": s.ship.id,
                "shipClass": s.ship.ship_class,
                "grade": s.ship.grade,
                "hull": s.ship.hull,
                "seed": s.ship.seed,
                "construction": s.ship.construction,
                "name": s.ship.name,
                "total": s.ship.total,
                "markers": s.markers,
                "unlocks_needed": _unlocks_needed(world, s),
                "parts": dict(s.part_values),
                "fixed": s.fixed_value,
                "locations": [marker_location_id(s.index, k) for k in range(1, len(s.markers) + 1)],
            }
            for s in sp.ships
        ],
    }
