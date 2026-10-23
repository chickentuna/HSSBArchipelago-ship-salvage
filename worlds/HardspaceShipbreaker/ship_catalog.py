from __future__ import annotations

import json
import pkgutil
from dataclasses import dataclass

from .equipment import BASE_ID

PART_ITEM_BASE = 1000
PART_LOCATION_BASE = 1500
SHIP_LOCATION_BASE = 2000
MAX_SHIPS = 30
MAX_MARKERS = 20


@dataclass(frozen=True)
class PartType:
    key: str
    name: str
    item_id: int


@dataclass(frozen=True)
class CatalogShip:
    id: str
    ship_class: str
    grade: int
    hull: int
    seed: int
    construction: str
    name: str
    total: float
    parts: dict[str, float]


def _load() -> tuple[list[PartType], dict[str, CatalogShip]]:
    raw = json.loads(pkgutil.get_data(__name__, "data/ship_catalog.json").decode("utf-8"))
    part_types = [PartType(t["key"], t["name"], BASE_ID + PART_ITEM_BASE + t["offset"]) for t in raw["part_types"]]
    ships = {
        s["id"]: CatalogShip(s["id"], s["shipClass"], s["grade"], s["hull"], s["seed"], s["construction"],
                             s["name"], s["total"], s["parts"])
        for s in raw["ships"]
    }
    return part_types, ships


PART_TYPES, SHIPS = _load()
PART_TYPE_BY_KEY = {t.key: t for t in PART_TYPES}


def marker_location_name(ship_index: int, marker: int) -> str:
    return f"Ship {ship_index} Marker {marker}"


def marker_location_id(ship_index: int, marker: int) -> int:
    return BASE_ID + SHIP_LOCATION_BASE + (ship_index - 1) * MAX_MARKERS + (marker - 1)


def part_location_name(part: PartType) -> str:
    return f"First {part.name} Salvaged"


def part_location_id(part: PartType) -> int:
    return part.item_id - PART_ITEM_BASE + PART_LOCATION_BASE


def part_item_name_to_id() -> dict[str, int]:
    return {t.name: t.item_id for t in PART_TYPES}


def ship_location_name_to_id() -> dict[str, int]:
    return {part_location_name(t): part_location_id(t) for t in PART_TYPES} | {
        marker_location_name(i, k): marker_location_id(i, k)
        for i in range(1, MAX_SHIPS + 1)
        for k in range(1, MAX_MARKERS + 1)
    }
