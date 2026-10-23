from dataclasses import dataclass

from Options import Choice, OptionGroup, OptionList, PerGameCommonOptions, Range, Toggle

from .ship_catalog import MAX_MARKERS, MAX_SHIPS, SHIPS


class GameMode(Choice):
    """
    career = the Career mode checks (upgrades, certification ranks, salvage tiers).
    part_rando = every upgrade unlocked from the start; you salvage a set of ships, and the items are the
    part types those ships spawn with. Locations are markers on each ship's salvage bar.
    """

    display_name = "Game Mode"
    option_career = 0
    option_part_rando = 1
    default = option_career


class ShipCount(Range):
    """Part Rando: how many ships are drawn from the ship catalog."""

    display_name = "Ship Count"
    range_start = 2
    range_end = MAX_SHIPS
    default = 6


class MinShipGrade(Range):
    """Part Rando: lowest job board grade the random draw uses."""

    display_name = "Minimum Ship Grade"
    range_start = 1
    range_end = 18
    default = 1


class MaxShipGrade(Range):
    """Part Rando: highest job board grade the random draw uses."""

    display_name = "Maximum Ship Grade"
    range_start = 1
    range_end = 18
    default = 18


class Ships(OptionList):
    """
    Part Rando, for debugging: catalog ships to use instead of the random draw, for example "G2-H0-12345"
    (grade 2, hull 0, seed 12345). Ship Count and the grade range are ignored when this list is not empty.
    Players should leave it empty: the IDs only appear in data/ship_catalog.json.
    """

    display_name = "Ships"
    valid_keys = sorted(SHIPS)


class MarkersPerShip(Range):
    """Part Rando: how many location markers each ship's salvage bar holds."""

    display_name = "Markers per Ship"
    range_start = 2
    range_end = MAX_MARKERS
    default = 8


class Partsanity(Toggle):
    """Part Rando: salvaging a part type for the first time is a location, for every part type on your ships."""

    display_name = "Partsanity"
    default = True


class ShipUnlocks(Toggle):
    """
    Part Rando: only ship 1 is on the job board at the start. Each Progressive Ship Unlock adds the next
    ship, in grade order.
    """

    display_name = "Ship Unlocks"
    default = False


class ExtraShipUnlocks(Range):
    """Part Rando, with Ship Unlocks: extra Progressive Ship Unlock copies, so the ships arrive sooner."""

    display_name = "Extra Ship Unlocks"
    range_start = 0
    range_end = 10
    default = 0


class GoalPercent(Range):
    """
    Part Rando: share of each ship your parts must reach for its last marker to be in logic. Checking the last marker
    on every ship wins.
    """

    display_name = "Goal Percent"
    range_start = 50
    range_end = 100
    default = 95


class SalvageMargin(Range):
    """
    Part Rando: each marker is in logic once your parts reach a given share of the ship, but it sits this percent lower
    on the salvage bar, so you can lose or destroy a few parts and still check it.
    """

    display_name = "Salvage Margin"
    range_start = 0
    range_end = 20
    default = 8


class Goal(Choice):
    """
    Career mode: how the player completes their seed.
    atlas_scout = reach Atlas Scout Salvage Tier 5 (not a bay Clear-* location).
    """

    display_name = "Goal"
    option_debt_payoff = 0
    option_atlas_scout = 1
    option_rank_20 = 2
    default = option_debt_payoff


class DeathLink(Toggle):
    """Player death (clone) sends and receives Death Link."""

    display_name = "Death Link"
    default = False


class HabShopSanity(Toggle):
    """
    Career mode: Hab equipment upgrades are locations. Buying them sends checks (can contain other items).
    Receiving AP tool items still applies the upgrade in-game.
    """

    display_name = "Hab Shop Sanity"
    default = True


class LocationDensity(Choice):
    """
    Career mode: how many locations to include.
    Sparse ≈ early Career slice + starter ship variants.
    Standard adds meta milestones, per-family salvage tiers, and Hab gear unlocks
    (not every hull variant; Hab rentals/durability omitted).
    Full = all Career checks, all variant salvage tiers, and full Hab shop-sanity.
    """

    display_name = "Location Density"
    option_sparse = 0
    option_standard = 1
    option_full = 2
    default = option_standard


class TrapPercentage(Range):
    """Percent of remaining filler slots that become traps (0–25)."""

    display_name = "Trap Percentage"
    range_start = 0
    range_end = 25
    default = 0


class CreditPackValue(Choice):
    """
    How much LYNX debt each Credit Pack filler pays (Small / Medium / Large).
    low ≈ 250k / 1M / 2.5M — AP credits are a small nudge.
    normal ≈ 1M / 3M / 8M — default; salvage remains the main debt engine.
    high ≈ 5M / 20M / 40M — old generous values.
    """

    display_name = "Credit Pack Value"
    option_low = 0
    option_normal = 1
    option_high = 2
    default = option_normal


# Small / Medium / Large debt payments matching CreditPackValue.
CREDIT_PACK_AMOUNTS: dict[int, tuple[int, int, int]] = {
    CreditPackValue.option_low: (250_000, 1_000_000, 2_500_000),
    CreditPackValue.option_normal: (1_000_000, 3_000_000, 8_000_000),
    CreditPackValue.option_high: (5_000_000, 20_000_000, 40_000_000),
}


def credit_pack_amounts_for(option_value: int) -> tuple[int, int, int]:
    return CREDIT_PACK_AMOUNTS.get(int(option_value), CREDIT_PACK_AMOUNTS[CreditPackValue.option_normal])


@dataclass
class HardspaceShipbreakerOptions(PerGameCommonOptions):
    game_mode: GameMode
    part_rando_ship_count: ShipCount
    part_rando_min_ship_grade: MinShipGrade
    part_rando_max_ship_grade: MaxShipGrade
    part_rando_ships: Ships
    part_rando_markers_per_ship: MarkersPerShip
    part_rando_partsanity: Partsanity
    part_rando_ship_unlocks: ShipUnlocks
    part_rando_extra_ship_unlocks: ExtraShipUnlocks
    part_rando_goal_percent: GoalPercent
    part_rando_salvage_margin: SalvageMargin
    goal: Goal
    death_link: DeathLink
    hab_shop_sanity: HabShopSanity
    location_density: LocationDensity
    trap_percentage: TrapPercentage
    credit_pack_value: CreditPackValue


option_groups = [
    OptionGroup("Game Mode", [GameMode]),
    OptionGroup("Part Rando", [ShipCount, MinShipGrade, MaxShipGrade, Ships, MarkersPerShip, Partsanity,
                                 ShipUnlocks, ExtraShipUnlocks, GoalPercent, SalvageMargin]),
    OptionGroup("Goal", [Goal]),
    OptionGroup("Locations", [LocationDensity, HabShopSanity]),
    OptionGroup("Multiplayer", [DeathLink]),
    OptionGroup("Items", [TrapPercentage, CreditPackValue]),
]

option_presets = {
    "phase1_smoke": {
        "game_mode": GameMode.option_career,
        "goal": Goal.option_debt_payoff,
        "death_link": False,
        "hab_shop_sanity": True,
        "location_density": LocationDensity.option_sparse,
        "trap_percentage": 0,
        "credit_pack_value": CreditPackValue.option_normal,
    },
    "standard": {
        "game_mode": GameMode.option_career,
        "goal": Goal.option_debt_payoff,
        "death_link": False,
        "hab_shop_sanity": True,
        "location_density": LocationDensity.option_standard,
        "trap_percentage": 5,
        "credit_pack_value": CreditPackValue.option_normal,
    },
    "death_link": {
        "game_mode": GameMode.option_career,
        "goal": Goal.option_debt_payoff,
        "death_link": True,
        "hab_shop_sanity": True,
        "location_density": LocationDensity.option_standard,
        "trap_percentage": 5,
        "credit_pack_value": CreditPackValue.option_normal,
    },
    "Part Rando - Short": {
        "game_mode": GameMode.option_part_rando,
        "part_rando_ship_count": 2,
        "part_rando_min_ship_grade": 1,
        "part_rando_max_ship_grade": 4,
        "part_rando_markers_per_ship": 20,
        "part_rando_partsanity": False,
        "part_rando_ship_unlocks": False,
    },
    "Part Rando - Medium": {
        "game_mode": GameMode.option_part_rando,
        "part_rando_ship_count": 4,
        "part_rando_min_ship_grade": 1,
        "part_rando_max_ship_grade": 9,
        "part_rando_markers_per_ship": 20,
        "part_rando_partsanity": False,
        "part_rando_ship_unlocks": False,
    },
    "Part Rando - Long": {
        "game_mode": GameMode.option_part_rando,
        "part_rando_ship_count": 6,
        "part_rando_min_ship_grade": 1,
        "part_rando_max_ship_grade": 18,
        "part_rando_markers_per_ship": 8,
        "part_rando_ship_unlocks": True,
        "part_rando_extra_ship_unlocks": 2,
    },
    "Part Rando - Marathon": {
        "game_mode": GameMode.option_part_rando,
        "part_rando_ship_count": 10,
        "part_rando_min_ship_grade": 8,
        "part_rando_max_ship_grade": 18,
        "part_rando_markers_per_ship": 10,
        "part_rando_ship_unlocks": True,
        "part_rando_extra_ship_unlocks": 3,
    },
}
