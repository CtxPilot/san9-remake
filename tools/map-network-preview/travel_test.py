import unittest
from travel import MAX_DAYS, estimate_connections

TERRAINS = [{"id": 0, "landCost": 10, "waterCost": None, "isWater": False},
            {"id": 1, "landCost": None, "waterCost": 10, "isWater": True}]


def node(id, x, y, width=1, height=1):
    return {"id": id, "x": x, "y": y, "footprint": {"width": width * 5, "height": height * 5}}


class TravelTests(unittest.TestCase):
    def test_default_horizon_includes_65_days(self):
        self.assertEqual(MAX_DAYS, 65)
        args = (66, 1, [0] * 66, TERRAINS, [], [node(1, 0, 0), node(2, 65, 0)], 50, 50)
        self.assertEqual(estimate_connections(*args, maximum_days=64), [])
        self.assertEqual(estimate_connections(*args), [
            {"from": 1, "to": 2, "days": 65}, {"from": 2, "to": 1, "days": 65}])

    def test_detour_cost_and_exact_cutoff(self):
        nodes = [node(1, 0, 1), node(2, 4, 1)]
        args = (5, 3, [0] * 15, TERRAINS, [{"x": 2, "y": 1}], nodes, 60, 60)
        self.assertEqual(estimate_connections(*args, maximum_days=4), [])
        edges = estimate_connections(*args, maximum_days=5)
        self.assertEqual(len(edges), 2)
        self.assertEqual(edges[0]["days"], 5)

    def test_no_route_through_complete_barrier(self):
        nodes = [node(1, 0, 1), node(2, 4, 1)]
        blocked = [{"x": 2, "y": y} for y in range(3)]
        self.assertEqual(estimate_connections(5, 3, [0] * 15, TERRAINS, blocked, nodes, 60, 60), [])

    def test_water_and_land_use_their_own_reference_budget(self):
        nodes = [node(1, 0, 0), node(2, 2, 0)]
        result = estimate_connections(3, 1, [0, 1, 0], TERRAINS, [], nodes, 50, 100)
        self.assertEqual(result[0]["days"], 1.5)

    def test_blocked_facility_endpoint_does_not_become_a_through_corridor(self):
        nodes = [node(1, 0, 0), node(2, 1, 0), node(3, 2, 0)]
        result = estimate_connections(3, 1, [0] * 3, TERRAINS, [{"x": 1, "y": 0}], nodes, 60, 60)
        self.assertTrue(any(e["from"] == 1 and e["to"] == 2 for e in result))
        self.assertFalse(any(e["from"] == 1 and e["to"] == 3 for e in result))

    def test_intermediate_facility_preserves_the_full_relation_and_shortest_time(self):
        nodes = [node(1, 0, 1), node(2, 2, 1), node(3, 4, 1)]
        result = estimate_connections(5, 3, [0] * 15, TERRAINS, [], nodes, 60, 60)
        pairs = {(e["from"], e["to"]) for e in result}
        self.assertTrue({(1, 2), (2, 3), (2, 1), (3, 2)} <= pairs)
        self.assertIn((1, 3), pairs)
        self.assertIn((3, 1), pairs)
        self.assertEqual(next(e["days"] for e in result if (e["from"], e["to"]) == (1, 3)), 3.333333)

    def test_adding_or_removing_an_endpoint_preserves_existing_terrain_costs(self):
        original = [node(1, 0, 0), node(3, 2, 0)]
        before = estimate_connections(3, 1, [0] * 3, TERRAINS, [], original, 60, 60)
        after = estimate_connections(3, 1, [0] * 3, TERRAINS, [], original + [node(2, 1, 0)], 60, 60)
        self.assertEqual(before, [e for e in after if e["from"] != 2 and e["to"] != 2])

    def test_distinct_facilities_in_one_cell_keep_both_directions(self):
        nodes = [{"id": 1, "x": 0, "y": 0}, {"id": 2, "x": 0, "y": 0}]
        result = estimate_connections(1, 1, [0], TERRAINS, [], nodes, 60, 60)
        self.assertEqual(result, [{"from": 1, "to": 2, "days": 0}, {"from": 2, "to": 1, "days": 0}])

    def test_third_party_footprints_do_not_remove_terrain_relations(self):
        nodes = [node(1, 0, 0), node(2, 2, 1, width=3, height=3), node(3, 4, 0)]
        result = estimate_connections(5, 4, [0] * 20, TERRAINS, [], nodes, 60, 60)
        self.assertTrue(any(e["from"] == 1 and e["to"] == 3 for e in result))


if __name__ == "__main__":
    unittest.main()
