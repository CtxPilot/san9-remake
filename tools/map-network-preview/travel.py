"""Terrain-only facility travel estimates for the preview, without world state."""
import heapq
import math

MAX_DAYS = 65


def estimate_connections(width, height, terrain_ids, terrains, blocked_cells, nodes,
                         land_budget, water_budget, maximum_days=MAX_DAYS, statistics=None):
    """Keep all terrain-reachable contacts within the reference travel horizon.

    One coarse cell is five fine movement units. Terrain passage costs and the
    reference formation budgets define days. This omits footprint clearance,
    deployment exits, object avoidance, combat and diplomatic restrictions.
    Intermediate facilities do not filter relations or change terrain costs.
    Optional statistics count search work and one tie-selected route's cell count;
    they do not retain distance fields or construct actual movement routes.
    """
    if land_budget <= 0 or water_budget <= 0 or maximum_days <= 0:
        raise ValueError("Travel budgets and horizon must be positive")
    count = width * height
    if len(terrain_ids) != count:
        raise ValueError("Terrain dimensions do not match")
    types = {t["id"]: t for t in terrains}
    ticks_per_day = math.lcm(land_budget, water_budget)
    maximum_cost = maximum_days * ticks_per_day
    blocked = {p["y"] * width + p["x"] for p in blocked_cells}
    # Endpoint cells may intersect a facility footprint. They are relaxed as
    # endpoints only; blocked cells elsewhere stay blocked.
    endpoints = {}
    for node in nodes:
        index = int(node["y"]) * width + int(node["x"])
        endpoints.setdefault(index, []).append(node["id"])
    raw = []
    for terrain_id in terrain_ids:
        t = types[terrain_id]
        cost = t["waterCost"] if t["isWater"] else t["landCost"]
        budget = water_budget if t["isWater"] else land_budget
        raw.append(None if cost is None else cost * 5 * (ticks_per_day // budget))
    neighbors = []
    for index in range(count):
        x, y = index % width, index // width
        neighbors.append([j for j, valid in [(index - width, y > 0), (index + 1, x + 1 < width),
                                            (index + width, y + 1 < height), (index - 1, x > 0)] if valid])
    result = []
    settled_cells, maximum_queue_length = 0, 0
    path_cells = []
    for source in nodes:
        start = int(source["y"]) * width + int(source["x"])
        distance = [math.inf] * count
        distance[start] = 0
        queue = [(0, start)]
        hops = [0] * count if statistics is not None else None
        remaining_targets = len(nodes) - 1

        while queue:
            cost, at = heapq.heappop(queue)
            if cost != distance[at]:
                continue
            if cost > maximum_cost:
                break
            settled_cells += 1
            for destination in endpoints.get(at, []):
                if destination != source["id"]:
                    result.append({"from": source["id"], "to": destination,
                                   "days": round(cost / ticks_per_day, 6)})
                    if hops is not None:
                        path_cells.append(hops[at] + 1)
                    remaining_targets -= 1
            if remaining_targets == 0:
                break
            if at != start and at in blocked:
                continue
            for nxt in neighbors[at]:
                if raw[nxt] is None or nxt in blocked and nxt not in endpoints:
                    continue
                candidate = cost + raw[nxt]
                if candidate > maximum_cost:
                    continue
                if candidate >= distance[nxt]:
                    continue
                distance[nxt] = candidate
                if hops is not None:
                    hops[nxt] = hops[at] + 1
                heapq.heappush(queue, (candidate, nxt))
            maximum_queue_length = max(maximum_queue_length, len(queue))
    if statistics is not None:
        statistics.update({"settledCells": settled_cells, "maximumQueueLength": maximum_queue_length,
                           "pathCellsSum": sum(path_cells), "pathCellsMaximum": max(path_cells, default=0)})
    return sorted(result, key=lambda e: (e["from"], e["to"]))
