#!/usr/bin/env python3
"""Pack the in-game ship catalog dump into the apworld data file.

Run from repo root:
  python tools/build_ship_catalog.py [catalog_dir]

catalog_dir defaults to research/ship_catalog (BepInEx/ship_catalog copied from the game).
Output: worlds/HardspaceShipbreaker/data/ship_catalog.json
Part type offsets already in the output file are kept; new part types get the next free offsets.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = ROOT / "research" / "ship_catalog"
OUT = ROOT / "worlds" / "HardspaceShipbreaker" / "data" / "ship_catalog.json"

EXCLUDED_CATEGORIES = {"PickUp", "CutPoint", "Unconfigured", "Interactable"}
NAME_OVERRIDES = {
    "INFO_Nacell": "Nacelle",
    "INFO_NacellDouble": "Double Nacelle",
}


def part_name(key: str) -> str:
    if key in NAME_OVERRIDES:
        return NAME_OVERRIDES[key]
    s = re.sub(r"^(INFO|Info)_", "", key)
    s = re.sub(r"^Prop_", "", s)
    s = s.replace("_", " ")
    s = re.sub(r"(?<=[a-z])(?=[A-Z])", " ", s)
    return re.sub(r"\s+", " ", s).strip()


def is_item_type(key: str, categories: set[str]) -> bool:
    return not (categories & EXCLUDED_CATEGORIES) and not re.match(r"^(INFO|Info)_Pickup", key)


def grade_of(ship_class: str) -> int:
    m = re.search(r"ShipGrade(\d+)", ship_class)
    return int(m.group(1)) if m else 0


def main() -> None:
    source = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_SOURCE
    old_types: dict[str, dict] = {}
    if OUT.exists():
        old_types = {t["key"]: t for t in json.loads(OUT.read_text())["part_types"]}

    ships = []
    type_value: dict[str, float] = {}
    type_ok: dict[str, bool] = {}
    for f in sorted(source.glob("*/*.json")):
        d = json.loads(f.read_text())
        parts: dict[str, float] = {}
        for u in d["units"]:
            key = u["objectInfoAsset"]
            value = u["creditValue"] * u["count"]
            if key is None or value <= 0:
                continue
            cats = {c.replace("_CategoryAsset", "") for c in u["categories"]}
            type_ok[key] = type_ok.get(key, True) and is_item_type(key, cats)
            parts[key] = parts.get(key, 0.0) + value
            type_value[key] = type_value.get(key, 0.0) + value
        grade = grade_of(d["shipClass"])
        ships.append({
            "id": f"G{grade}-H{d['hullIndex']}-{d['seed']}",
            "shipClass": d["shipClass"],
            "grade": grade,
            "hull": d["hullIndex"],
            "seed": d["seed"],
            "construction": d["constructionAssetName"],
            "name": d["shipName"],
            "total": round(d["previewTotalCreditValue"], 2),
            "parts": {k: round(v, 2) for k, v in sorted(parts.items(), key=lambda kv: -kv[1])},
        })

    part_types = [t for k, t in old_types.items()]
    next_offset = max((t["offset"] for t in part_types), default=-1) + 1
    for key in sorted(type_value, key=lambda k: -type_value[k]):
        if key in old_types or not type_ok[key]:
            continue
        part_types.append({"key": key, "name": part_name(key), "offset": next_offset})
        next_offset += 1

    names = [t["name"] for t in part_types]
    dupes = {n for n in names if names.count(n) > 1}
    if dupes:
        raise SystemExit(f"Duplicate part item names: {sorted(dupes)}")

    ships.sort(key=lambda s: (s["grade"], s["hull"], s["seed"]))
    seen: dict[tuple, str] = {}
    unique_ships = []
    for ship in ships:
        signature = (ship["grade"], round(ship["total"]), tuple(sorted((k, round(v)) for k, v in ship["parts"].items())))
        if signature in seen:
            print(f"Dropping {ship['id']}: same parts and value as {seen[signature]}")
            continue
        seen[signature] = ship["id"]
        unique_ships.append(ship)
    ships = unique_ships
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps({"part_types": part_types, "ships": ships}, indent=1) + "\n")
    print(f"{len(ships)} ships, {len(part_types)} part types -> {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
