"""Hab equipment: shop locations are per-upgrade; AP items are Progressive (or single unlocks)."""

from __future__ import annotations

from BaseClasses import ItemClassification

BASE_ID = 2_026_080_100

# Hab shop locations — one check per equipment-screen upgrade.
# (hab_loc_name, loc_offset)
HAB_SHOP_LOCATIONS: list[tuple[str, int]] = [
    ("Hab: Unlock Tethers", 200),
    ("Hab: Grapple Strength 1", 201),
    ("Hab: Grapple Strength 2", 202),
    ("Hab: Grapple Strength 3", 203),
    ("Hab: Grapple Strength 4", 204),
    ("Hab: Grapple Strength 5", 205),
    ("Hab: Tethers Amount 1", 206),
    ("Hab: Tethers Amount 2", 207),
    ("Hab: Tethers Amount 3", 208),
    ("Hab: Tethers Lifetime 1", 209),
    ("Hab: Tethers Lifetime 2", 210),
    ("Hab: Unlock Demo Charge", 211),
    ("Hab: Purchase First Equipment Upgrade", 212),
    ("Hab: Scanner Objects", 213),
    ("Hab: Scanner Systems", 214),
    ("Hab: Suit Integrity 1", 215),
    ("Hab: Suit Integrity 2", 216),
    ("Hab: Cutter Heat 1", 217),
    ("Hab: Demo Charges Capacity 1", 218),
    ("Hab: Tethers Lifetime 3", 219),
    ("Hab: Tethers Lifetime 4", 220),
    ("Hab: Charged Push", 221),
    ("Hab: Scanner Range 1", 222),
    ("Hab: Scanner Range 2", 223),
    ("Hab: Scanner Range 3", 224),
    ("Hab: Scanner Range 4", 225),
    ("Hab: Scanner Range 5", 226),
    ("Hab: Suit Integrity 3", 227),
    ("Hab: Suit Integrity 4", 228),
    ("Hab: Suit Integrity 5", 229),
    ("Hab: Heat Resistance 1", 230),
    ("Hab: Heat Resistance 2", 231),
    ("Hab: Heat Resistance 3", 232),
    ("Hab: Heat Resistance 4", 233),
    ("Hab: Heat Resistance 5", 234),
    ("Hab: Cryo Resistance 1", 235),
    ("Hab: Cryo Resistance 2", 236),
    ("Hab: Cryo Resistance 3", 237),
    ("Hab: Cryo Resistance 4", 238),
    ("Hab: Cryo Resistance 5", 239),
    ("Hab: Electrical Resistance 1", 240),
    ("Hab: Electrical Resistance 2", 241),
    ("Hab: Electrical Resistance 3", 242),
    ("Hab: Electrical Resistance 4", 243),
    ("Hab: Electrical Resistance 5", 244),
    ("Hab: Cutter Heat Capacity 2", 245),
    ("Hab: Cutter Heat Capacity 3", 246),
    ("Hab: Cutter Cooldown 1", 247),
    ("Hab: Cutter Cooldown 2", 248),
    ("Hab: Cutter Cooldown 3", 249),
    ("Hab: Stinger Range 1", 250),
    ("Hab: Stinger Range 2", 251),
    ("Hab: Stinger Range 3", 252),
    ("Hab: Stinger Range 4", 253),
    ("Hab: Stinger Range 5", 254),
    ("Hab: Splitsaw Range 1", 255),
    ("Hab: Splitsaw Range 2", 256),
    ("Hab: Splitsaw Range 3", 257),
    ("Hab: Splitsaw Range 4", 258),
    ("Hab: Splitsaw Range 5", 259),
    ("Hab: Grapple Range 1", 260),
    ("Hab: Grapple Range 2", 261),
    ("Hab: Grapple Range 3", 262),
    ("Hab: Grapple Range 4", 263),
    ("Hab: Grapple Range 5", 264),
    ("Hab: Charged Push Force 1", 265),
    ("Hab: Charged Push Force 2", 266),
    ("Hab: Charged Push Force 3", 267),
    ("Hab: Demo Charges Capacity 2", 268),
    ("Hab: Demo Charges Capacity 3", 269),
    ("Hab: Demo Charges Capacity 4", 270),
    ("Hab: Demo Charges Capacity 5", 271),
    ("Hab: Demo Disarming 1", 272),
    ("Hab: Demo Disarming 2", 273),
    ("Hab: Demo Disarming 3", 274),
    ("Hab: Demo Self Cleanup 1", 275),
    ("Hab: Demo Self Cleanup 2", 276),
    ("Hab: Demo Self Cleanup 3", 277),
    ("Hab: Demo Auto-Deploy", 278),
    ("Hab: O2 Capacity 1", 279),
    ("Hab: O2 Capacity 2", 280),
    ("Hab: O2 Capacity 3", 281),
    ("Hab: O2 Capacity 4", 282),
    ("Hab: O2 Capacity 5", 283),
    ("Hab: O2 Recharge Module", 284),
    ("Hab: O2 Recharge 1", 285),
    ("Hab: O2 Recharge 2", 286),
    ("Hab: O2 Recharge 3", 287),
    ("Hab: Thruster Top Speed 1", 288),
    ("Hab: Thruster Top Speed 2", 289),
    ("Hab: Thruster Top Speed 3", 290),
    ("Hab: Thruster Braking 1", 291),
    ("Hab: Thruster Braking 2", 292),
    ("Hab: Thruster Braking 3", 293),
    ("Hab: Thruster Fuel Capacity 1", 294),
    ("Hab: Thruster Fuel Capacity 2", 295),
    ("Hab: Thruster Fuel Capacity 3", 296),
    ("Hab: Audio Resynth 1", 297),
    ("Hab: Audio Resynth 2", 298),
    ("Hab: Audio Resynth 3", 299),
    # Rental fees + durability (shop-sanity) — IDs 320+; do not renumber.
    ("Hab: Thruster Rental", 320),
    ("Hab: Thruster Durability 1", 321),
    ("Hab: Thruster Durability 2", 322),
    ("Hab: Thruster Durability 3", 323),
    ("Hab: Thruster Durability 4", 324),
    ("Hab: Thruster Durability 5", 325),
    ("Hab: Cutter Durability 1", 327),
    ("Hab: Cutter Durability 2", 328),
    ("Hab: Cutter Durability 3", 329),
    ("Hab: Cutter Durability 4", 330),
    ("Hab: Cutter Durability 5", 331),
    ("Hab: Grapple Rental", 332),
    ("Hab: Grapple Durability 1", 333),
    ("Hab: Grapple Durability 2", 334),
    ("Hab: Grapple Durability 3", 335),
    ("Hab: Grapple Durability 4", 336),
    ("Hab: Grapple Durability 5", 337),
    ("Hab: Scanner Rental", 338),
    ("Hab: Scanner Durability 1", 339),
    ("Hab: Scanner Durability 2", 340),
    ("Hab: Scanner Durability 3", 341),
    ("Hab: Scanner Durability 4", 342),
    ("Hab: Scanner Durability 5", 343),
    ("Hab: Helmet Rental", 344),
    ("Hab: Suit Rental", 345),
    ("Hab: Demo Charge Rental", 349),
    # Was 350–354 (collided with Mackerel Light Cargo Salvage Tier 1–5).
    ("Hab: Demo Durability 1", 444),
    ("Hab: Demo Durability 2", 445),
    ("Hab: Demo Durability 3", 446),
    ("Hab: Demo Durability 4", 447),
    ("Hab: Demo Durability 5", 448),
    ("Hab: Cutter Purchase", 449),
]


def hab_shop_offset(name: str) -> int | None:
    for loc, off in HAB_SHOP_LOCATIONS:
        if loc == name:
            return off
    return None


def hab_core_shop_location_names() -> list[str]:
    """Gear unlocks / upgrades — excludes rental-fee and durability rows."""
    return [
        name for name, off in HAB_SHOP_LOCATIONS
        if off < 320 or off == 449
    ]


def hab_rental_durability_location_names() -> list[str]:
    """Rental-fee + durability Hab rows (full density when shop-sanity on)."""
    return [
        name for name, off in HAB_SHOP_LOCATIONS
        if 320 <= off <= 349 or 444 <= off <= 448
    ]

# Progressive AP items: (name, item_offset, count, classification, tier_label_prefix)
# Receiving copy N unlocks in-game tier N matching Hab catalog / client needles.
# Stable IDs: reuse classic progressive offsets where they existed.
PROGRESSIVE_EQUIPMENT: list[tuple[str, int, int, ItemClassification, str]] = [
    ("Progressive Grapple Strength", 2, 5, ItemClassification.progression, "Grapple Strength"),
    ("Progressive Tether Amount", 24, 3, ItemClassification.progression, "Tethers Amount"),
    ("Progressive Tether Lifetime", 27, 4, ItemClassification.useful, "Tethers Lifetime"),
    ("Progressive Scanner", 8, 2, ItemClassification.progression, "Scanner"),  # Objects then Systems
    ("Progressive Scanner Range", 33, 5, ItemClassification.useful, "Scanner Range"),
    ("Progressive Suit Integrity", 9, 5, ItemClassification.progression, "Suit Integrity"),
    ("Progressive Heat Resistance", 43, 5, ItemClassification.useful, "Heat Resistance"),
    ("Progressive Cryo Resistance", 48, 5, ItemClassification.useful, "Cryo Resistance"),
    ("Progressive Electrical Resistance", 53, 5, ItemClassification.useful, "Electrical Resistance"),
    ("Progressive Cutter Heat", 58, 3, ItemClassification.useful, "Cutter Heat Capacity"),
    ("Progressive Cutter Cooldown", 61, 3, ItemClassification.useful, "Cutter Cooldown"),
    ("Progressive Stinger Range", 64, 5, ItemClassification.useful, "Stinger Range"),
    ("Progressive Splitsaw Range", 69, 5, ItemClassification.useful, "Splitsaw Range"),
    ("Progressive Grapple Range", 74, 5, ItemClassification.useful, "Grapple Range"),
    ("Progressive Charged Push Force", 79, 3, ItemClassification.useful, "Charged Push Force"),
    ("Progressive Demo Charges", 15, 5, ItemClassification.progression, "Demo Charges Capacity"),
    ("Progressive Demo Disarming", 87, 3, ItemClassification.useful, "Demo Disarming"),
    ("Progressive Demo Self Cleanup", 92, 3, ItemClassification.useful, "Demo Self Cleanup"),
    ("Progressive O2 Capacity", 96, 5, ItemClassification.useful, "O2 Capacity"),
    ("Progressive O2 Recharge", 102, 3, ItemClassification.useful, "O2 Recharge"),
    ("Progressive Thruster Top Speed", 105, 3, ItemClassification.useful, "Thruster Top Speed"),
    ("Progressive Thruster Braking", 108, 3, ItemClassification.useful, "Thruster Braking"),
    # Item offsets 500+ avoid overlapping active location IDs (bay/cert/salvage).
    ("Progressive Thruster Fuel", 511, 3, ItemClassification.useful, "Thruster Fuel Capacity"),
    ("Progressive Audio Resynth", 514, 3, ItemClassification.useful, "Audio Resynth"),
    ("Progressive Thruster Durability", 521, 5, ItemClassification.useful, "Thruster Durability"),
    ("Progressive Cutter Durability", 523, 5, ItemClassification.useful, "Cutter Durability"),
    ("Progressive Grapple Durability", 525, 5, ItemClassification.useful, "Grapple Durability"),
    ("Progressive Scanner Durability", 527, 5, ItemClassification.useful, "Scanner Durability"),
    ("Progressive Demo Durability", 532, 5, ItemClassification.useful, "Demo Durability"),
]

# Single (non-progressive) equipment unlocks / licenses.
# (name, item_offset, classification)
SINGLE_EQUIPMENT: list[tuple[str, int, ItemClassification]] = [
    ("Tether Module", 1, ItemClassification.progression),
    ("Demo Charge License", 7, ItemClassification.progression),
    ("Charged Push", 18, ItemClassification.progression),
    ("Demo Auto-Deploy", 95, ItemClassification.useful),
    ("O2 Recharge Module", 501, ItemClassification.progression),
    ("Thruster Rental", 520, ItemClassification.useful),
    ("Cutter Purchase", 540, ItemClassification.useful),
    ("Grapple Rental", 524, ItemClassification.useful),
    ("Scanner Rental", 526, ItemClassification.useful),
    ("Helmet Rental", 528, ItemClassification.useful),
    ("Suit Rental", 529, ItemClassification.useful),
    ("Demo Charge Rental", 531, ItemClassification.useful),
]

# Progressive / single AP items that must not apply until their license is owned.
ITEM_LICENSE_GATES: dict[str, str] = {
    "Progressive Tether Amount": "Tether Module",
    "Progressive Tether Lifetime": "Tether Module",
    "Progressive Tethers": "Tether Module",
    "Progressive Demo Charges": "Demo Charge License",
    "Progressive Demo Disarming": "Demo Charge License",
    "Progressive Demo Self Cleanup": "Demo Charge License",
    "Progressive Demo Durability": "Demo Charge License",
    "Demo Auto-Deploy": "Demo Charge License",
    "Demo Charge Rental": "Demo Charge License",
    "Progressive Charged Push Force": "Charged Push",
    "Progressive O2 Recharge": "O2 Recharge Module",
}

# Vanilla Career shop (DefaultUpgradeSettingsAsset upgrade trees): location → (RequiredTier, PreviousUpgrade).
# The Hab only sells a row once the previous row was bought there; received AP items don't count.
# Client PCR ceilings: ×0→4, ×1→9, ×2→14, ×3→19, ×4→20.
HAB_SHOP_VANILLA: dict[str, tuple[int, str | None]] = {
    "Hab: Purchase First Equipment Upgrade": (1, None),
    "Hab: Unlock Tethers": (4, None),
    "Hab: Grapple Strength 1": (6, None),
    "Hab: Grapple Strength 2": (8, "Hab: Grapple Strength 1"),
    "Hab: Grapple Strength 3": (10, "Hab: Grapple Strength 2"),
    "Hab: Grapple Strength 4": (12, "Hab: Grapple Strength 3"),
    "Hab: Grapple Strength 5": (15, "Hab: Grapple Strength 4"),
    "Hab: Tethers Amount 1": (5, "Hab: Unlock Tethers"),
    "Hab: Tethers Amount 2": (8, "Hab: Tethers Amount 1"),
    "Hab: Tethers Amount 3": (13, "Hab: Tethers Amount 2"),
    "Hab: Tethers Lifetime 1": (7, "Hab: Unlock Tethers"),
    "Hab: Tethers Lifetime 2": (10, "Hab: Tethers Lifetime 1"),
    "Hab: Unlock Demo Charge": (11, None),
    "Hab: Scanner Objects": (7, None),
    "Hab: Scanner Systems": (5, None),
    "Hab: Suit Integrity 1": (5, None),
    "Hab: Suit Integrity 2": (9, "Hab: Suit Integrity 1"),
    "Hab: Cutter Heat 1": (7, None),
    "Hab: Demo Charges Capacity 1": (12, "Hab: Unlock Demo Charge"),
    "Hab: Tethers Lifetime 3": (14, "Hab: Tethers Lifetime 2"),
    "Hab: Tethers Lifetime 4": (17, "Hab: Tethers Lifetime 3"),
    "Hab: Charged Push": (6, None),
    "Hab: Scanner Range 1": (7, None),
    "Hab: Scanner Range 2": (10, "Hab: Scanner Range 1"),
    "Hab: Scanner Range 3": (13, "Hab: Scanner Range 2"),
    "Hab: Scanner Range 4": (15, "Hab: Scanner Range 3"),
    "Hab: Scanner Range 5": (17, "Hab: Scanner Range 4"),
    "Hab: Suit Integrity 3": (12, "Hab: Suit Integrity 2"),
    "Hab: Suit Integrity 4": (15, "Hab: Suit Integrity 3"),
    "Hab: Suit Integrity 5": (18, "Hab: Suit Integrity 4"),
    "Hab: Heat Resistance 1": (6, "Hab: Suit Integrity 1"),
    "Hab: Heat Resistance 2": (8, "Hab: Heat Resistance 1"),
    "Hab: Heat Resistance 3": (11, "Hab: Heat Resistance 2"),
    "Hab: Heat Resistance 4": (14, "Hab: Heat Resistance 3"),
    "Hab: Heat Resistance 5": (17, "Hab: Heat Resistance 4"),
    "Hab: Cryo Resistance 1": (11, "Hab: Suit Integrity 1"),
    "Hab: Cryo Resistance 2": (13, "Hab: Cryo Resistance 1"),
    "Hab: Cryo Resistance 3": (15, "Hab: Cryo Resistance 2"),
    "Hab: Cryo Resistance 4": (17, "Hab: Cryo Resistance 3"),
    "Hab: Cryo Resistance 5": (20, "Hab: Cryo Resistance 4"),
    "Hab: Electrical Resistance 1": (6, "Hab: Suit Integrity 1"),
    "Hab: Electrical Resistance 2": (9, "Hab: Electrical Resistance 1"),
    "Hab: Electrical Resistance 3": (12, "Hab: Electrical Resistance 2"),
    "Hab: Electrical Resistance 4": (15, "Hab: Electrical Resistance 3"),
    "Hab: Electrical Resistance 5": (18, "Hab: Electrical Resistance 4"),
    "Hab: Cutter Heat Capacity 2": (11, "Hab: Cutter Heat 1"),
    "Hab: Cutter Heat Capacity 3": (16, "Hab: Cutter Heat Capacity 2"),
    "Hab: Cutter Cooldown 1": (6, None),
    "Hab: Cutter Cooldown 2": (9, "Hab: Cutter Cooldown 1"),
    "Hab: Cutter Cooldown 3": (14, "Hab: Cutter Cooldown 2"),
    "Hab: Stinger Range 1": (5, None),
    "Hab: Stinger Range 2": (7, "Hab: Stinger Range 1"),
    "Hab: Stinger Range 3": (10, "Hab: Stinger Range 2"),
    "Hab: Stinger Range 4": (13, "Hab: Stinger Range 3"),
    "Hab: Stinger Range 5": (16, "Hab: Stinger Range 4"),
    "Hab: Splitsaw Range 1": (6, None),
    "Hab: Splitsaw Range 2": (9, "Hab: Splitsaw Range 1"),
    "Hab: Splitsaw Range 3": (13, "Hab: Splitsaw Range 2"),
    "Hab: Splitsaw Range 4": (16, "Hab: Splitsaw Range 3"),
    "Hab: Splitsaw Range 5": (19, "Hab: Splitsaw Range 4"),
    "Hab: Grapple Range 1": (5, None),
    "Hab: Grapple Range 2": (6, "Hab: Grapple Range 1"),
    "Hab: Grapple Range 3": (8, "Hab: Grapple Range 2"),
    "Hab: Grapple Range 4": (11, "Hab: Grapple Range 3"),
    "Hab: Grapple Range 5": (14, "Hab: Grapple Range 4"),
    "Hab: Charged Push Force 1": (10, "Hab: Charged Push"),
    "Hab: Charged Push Force 2": (14, "Hab: Charged Push Force 1"),
    "Hab: Charged Push Force 3": (16, "Hab: Charged Push Force 2"),
    "Hab: Demo Charges Capacity 2": (14, "Hab: Demo Charges Capacity 1"),
    "Hab: Demo Charges Capacity 3": (16, "Hab: Demo Charges Capacity 2"),
    "Hab: Demo Charges Capacity 4": (18, "Hab: Demo Charges Capacity 3"),
    "Hab: Demo Charges Capacity 5": (20, "Hab: Demo Charges Capacity 4"),
    "Hab: Demo Disarming 1": (12, "Hab: Unlock Demo Charge"),
    "Hab: Demo Disarming 2": (14, "Hab: Demo Disarming 1"),
    "Hab: Demo Disarming 3": (17, "Hab: Demo Disarming 2"),
    "Hab: Demo Self Cleanup 1": (12, "Hab: Unlock Demo Charge"),
    "Hab: Demo Self Cleanup 2": (15, "Hab: Demo Self Cleanup 1"),
    "Hab: Demo Self Cleanup 3": (19, "Hab: Demo Self Cleanup 2"),
    "Hab: Demo Auto-Deploy": (16, "Hab: Unlock Demo Charge"),
    "Hab: O2 Capacity 1": (5, None),
    "Hab: O2 Capacity 2": (8, "Hab: O2 Capacity 1"),
    "Hab: O2 Capacity 3": (10, "Hab: O2 Capacity 2"),
    "Hab: O2 Capacity 4": (13, "Hab: O2 Capacity 3"),
    "Hab: O2 Capacity 5": (16, "Hab: O2 Capacity 4"),
    "Hab: O2 Recharge Module": (7, None),
    "Hab: O2 Recharge 1": (9, "Hab: O2 Recharge Module"),
    "Hab: O2 Recharge 2": (11, "Hab: O2 Recharge 1"),
    "Hab: O2 Recharge 3": (13, "Hab: O2 Recharge 2"),
    "Hab: Thruster Top Speed 1": (5, None),
    "Hab: Thruster Top Speed 2": (7, "Hab: Thruster Top Speed 1"),
    "Hab: Thruster Top Speed 3": (10, "Hab: Thruster Top Speed 2"),
    "Hab: Thruster Braking 1": (6, "Hab: Thruster Top Speed 1"),
    "Hab: Thruster Braking 2": (8, "Hab: Thruster Braking 1"),
    "Hab: Thruster Braking 3": (10, "Hab: Thruster Braking 2"),
    "Hab: Thruster Fuel Capacity 1": (7, None),
    "Hab: Thruster Fuel Capacity 2": (9, "Hab: Thruster Fuel Capacity 1"),
    "Hab: Thruster Fuel Capacity 3": (13, "Hab: Thruster Fuel Capacity 2"),
    "Hab: Audio Resynth 1": (5, None),
    "Hab: Audio Resynth 2": (7, "Hab: Audio Resynth 1"),
    "Hab: Audio Resynth 3": (9, "Hab: Audio Resynth 2"),
    "Hab: Thruster Rental": (18, None),
    "Hab: Thruster Durability 1": (6, None),
    "Hab: Thruster Durability 2": (9, "Hab: Thruster Durability 1"),
    "Hab: Thruster Durability 3": (12, "Hab: Thruster Durability 2"),
    "Hab: Thruster Durability 4": (15, "Hab: Thruster Durability 3"),
    "Hab: Thruster Durability 5": (18, "Hab: Thruster Durability 4"),
    "Hab: Cutter Durability 1": (6, None),
    "Hab: Cutter Durability 2": (9, "Hab: Cutter Durability 1"),
    "Hab: Cutter Durability 3": (12, "Hab: Cutter Durability 2"),
    "Hab: Cutter Durability 4": (15, "Hab: Cutter Durability 3"),
    "Hab: Cutter Durability 5": (18, "Hab: Cutter Durability 4"),
    "Hab: Grapple Rental": (18, None),
    "Hab: Grapple Durability 1": (5, None),
    "Hab: Grapple Durability 2": (8, "Hab: Grapple Durability 1"),
    "Hab: Grapple Durability 3": (11, "Hab: Grapple Durability 2"),
    "Hab: Grapple Durability 4": (14, "Hab: Grapple Durability 3"),
    "Hab: Grapple Durability 5": (17, "Hab: Grapple Durability 4"),
    "Hab: Scanner Rental": (20, None),
    "Hab: Scanner Durability 1": (8, None),
    "Hab: Scanner Durability 2": (11, "Hab: Scanner Durability 1"),
    "Hab: Scanner Durability 3": (13, "Hab: Scanner Durability 2"),
    "Hab: Scanner Durability 4": (16, "Hab: Scanner Durability 3"),
    "Hab: Scanner Durability 5": (19, "Hab: Scanner Durability 4"),
    "Hab: Helmet Rental": (19, None),
    "Hab: Suit Rental": (19, None),
    "Hab: Demo Charge Rental": (20, "Hab: Unlock Demo Charge"),
    "Hab: Demo Durability 1": (12, "Hab: Unlock Demo Charge"),
    "Hab: Demo Durability 2": (14, "Hab: Demo Durability 1"),
    "Hab: Demo Durability 3": (16, "Hab: Demo Durability 2"),
    "Hab: Demo Durability 4": (18, "Hab: Demo Durability 3"),
    "Hab: Demo Durability 5": (20, "Hab: Demo Durability 4"),
    "Hab: Cutter Purchase": (19, None),
}


def hab_shop_required_rank(name: str) -> int:
    """Highest RequiredTier along the row's purchase chain."""
    rank = 0
    while name:
        row_rank, name = HAB_SHOP_VANILLA[name]
        rank = max(rank, row_rank)
    return rank


def pcr_copies_for_cert_rank(rank: int) -> int:
    if rank <= 4:
        return 0
    if rank <= 9:
        return 1
    if rank <= 14:
        return 2
    if rank <= 19:
        return 3
    return 4


# Legacy alias kept in ID map for old seeds (not placed in new pools).
LEGACY_PROGRESSIVE_ALIASES = {
    "Progressive Tethers": BASE_ID + 4,  # old combined tether progressive
    "Progressive Cutter": BASE_ID + 14,  # old combined cutter progressive
}


def equipment_location_name_to_id() -> dict[str, int]:
    return {name: BASE_ID + off for name, off in HAB_SHOP_LOCATIONS}


def equipment_item_name_to_id() -> dict[str, int]:
    out = {name: BASE_ID + off for name, off, _, _, _ in PROGRESSIVE_EQUIPMENT}
    out.update({name: BASE_ID + off for name, off, _ in SINGLE_EQUIPMENT})
    out.update(LEGACY_PROGRESSIVE_ALIASES)
    return out


def equipment_classifications() -> dict[str, ItemClassification]:
    out = {name: cls for name, _, _, cls, _ in PROGRESSIVE_EQUIPMENT}
    out.update({name: cls for name, _, cls in SINGLE_EQUIPMENT})
    out["Progressive Tethers"] = ItemClassification.progression
    out["Progressive Cutter"] = ItemClassification.useful
    return out


def equipment_item_counts() -> dict[str, int]:
    out = {name: count for name, _, count, _, _ in PROGRESSIVE_EQUIPMENT}
    out.update({name: 1 for name, _, _ in SINGLE_EQUIPMENT})
    return out


def all_hab_equipment_location_names() -> list[str]:
    return [name for name, _ in HAB_SHOP_LOCATIONS]


def progressive_tier_prefix(item_name: str) -> str | None:
    for name, _, _, _, prefix in PROGRESSIVE_EQUIPMENT:
        if name == item_name:
            return prefix
    return None
