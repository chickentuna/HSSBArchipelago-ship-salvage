from collections.abc import Mapping
from typing import Any

from worlds.AutoWorld import World

from . import items, locations, regions, rules, part_rando, web_world
from . import options as hs_options


class HardspaceShipbreakerWorld(World):
    """
    Hardspace: Shipbreaker — Career salvage multiworld (public RC 0.6.0).
    """

    game = "Hardspace Shipbreaker"
    web = web_world.HardspaceShipbreakerWebWorld()
    options_dataclass = hs_options.HardspaceShipbreakerOptions
    options: hs_options.HardspaceShipbreakerOptions

    location_name_to_id = locations.ALL_LOCATION_NAME_TO_ID
    item_name_to_id = items.ALL_ITEM_NAME_TO_ID

    origin_region_name = "Hab"
    ship_plan: part_rando.ShipPlan | None = None
    ut_can_gen_without_yaml = True

    @property
    def is_part_rando(self) -> bool:
        return self.options.game_mode == hs_options.GameMode.option_part_rando

    @staticmethod
    def interpret_slot_data(slot_data: dict[str, Any]) -> dict[str, Any]:
        return slot_data

    def generate_early(self) -> None:
        tracker_data = getattr(self.multiworld, "re_gen_passthrough", {}).get(self.game)
        if tracker_data:
            self.options.game_mode.value = tracker_data["game_mode"]
            if self.is_part_rando:
                part_rando.restore_options(self, tracker_data)
                self.ship_plan = part_rando.plan_from_slot_data(self, tracker_data)
                return
            self.options.goal.value = tracker_data["goal"]
            self.options.death_link.value = int(tracker_data["death_link"])
            self.options.hab_shop_sanity.value = int(tracker_data["hab_shop_sanity"])
            self.options.location_density.value = tracker_data["location_density"]
            self.options.credit_pack_value.value = tracker_data["credit_pack_value"]
            return
        if self.is_part_rando:
            self.ship_plan = part_rando.plan(self)

    def create_regions(self) -> None:
        if self.is_part_rando:
            part_rando.create_regions(self)
            return
        regions.create_and_connect_regions(self)
        locations.create_all_locations(self)

    def set_rules(self) -> None:
        if self.is_part_rando:
            return
        rules.set_all_rules(self)

    def create_items(self) -> None:
        if self.is_part_rando:
            part_rando.create_items(self)
            return
        # Starting Unlock Mackerel so early Career is always reachable.
        self.multiworld.push_precollected(self.create_item("Unlock Mackerel"))
        items.create_all_items(self)

    def create_item(self, name: str) -> items.HardspaceShipbreakerItem:
        return items.create_item_with_correct_classification(self, name)

    def get_filler_item_name(self) -> str:
        if self.is_part_rando:
            return part_rando.FILLER_NAME
        return items.get_filler_item_name(self)

    def fill_slot_data(self) -> Mapping[str, Any]:
        if self.is_part_rando:
            return part_rando.slot_data(self)
        small, medium, large = hs_options.credit_pack_amounts_for(self.options.credit_pack_value.value)
        return {
            "game_mode": int(self.options.game_mode.value),
            "goal": int(self.options.goal.value),
            "death_link": bool(self.options.death_link.value),
            "hab_shop_sanity": bool(self.options.hab_shop_sanity.value),
            "location_density": int(self.options.location_density.value),
            "credit_pack_value": int(self.options.credit_pack_value.value),
            "credit_pack_small": small,
            "credit_pack_medium": medium,
            "credit_pack_large": large,
        }
