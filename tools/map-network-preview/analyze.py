"""Reproduce facility counts, static attack-scope differences and compact storage estimates."""
import argparse
from collections import Counter
import hashlib
import math
from pathlib import Path
import platform
import time

from prepare import CONTENT, OUTPUT, catalog, prepare, read
from travel import MAX_DAYS, estimate_connections


def pairs(edges):
    return {tuple(sorted(edge)) for edge in edges}


def scope_summary(edges, count):
    unordered = pairs(edges)
    mutual = sum((a, b) in edges and (b, a) in edges for a, b in unordered)
    return {"facilities": count, "pairs": len(unordered), "directed": len(edges),
            "bidirectionalPairs": mutual, "oneWayPairs": len(unordered) - mutual}


def static_attack_scope(definition, node_ids):
    """Structural approximation of AttackSources, excluding dynamic eligibility.

    Only identity, kind, activation, durability and hosting eligibility are read
    from settlements. Ownership, troops, morale, officers and forces are unused.
    This does not reproduce actual commands, route redirection or strength gates.
    """
    military = definition["military"]
    width, height = military["width"], military["height"]
    cities = {int(k): v for k, v in definition["regionCityIds"].items()}
    places = {p["id"]: p for p in military["places"]}
    facilities = {f["id"]: f for f in military["facilities"]}
    terrains = {t["id"]: t for t in military["terrains"]}
    blocked = {p["y"] * width + p["x"] for p in military["geography"]["blockedCells"]}
    fields = ["id", "kind", "isDormant", "isDestroyed", "durability", "canHostPersons"]
    sites = [{k: s.get(k) for k in fields} for s in definition["settlements"] if s["id"] in node_ids]

    def region_of(site):
        position = places[site["id"]]["position"]
        return military["regionIds"][(position["y"] // 5) * width + position["x"] // 5]

    def city_of(site):
        return cities.get(site["id"], site["id"])

    def geography_city_of(site):
        return site["id"] if site["kind"] == 0 else cities[region_of(site)]

    harbors = {region_of(s) for s in sites if not s["isDestroyed"] and
               facilities[places[s["id"]]["facilityTypeId"]]["isHarbor"]}
    region_edges = set()
    for i, region in enumerate(military["regionIds"]):
        if region is None or i in blocked:
            continue
        x, y = i % width, i // width
        for dx, dy, bit in [(0, -1, 1), (1, 0, 2), (0, 1, 4), (-1, 0, 8)]:
            nx, ny = x + dx, y + dy
            if not (0 <= nx < width and 0 <= ny < height and military["territoryExits"][i] & bit):
                continue
            j = ny * width + nx
            other = military["regionIds"][j]
            if other is None or other == region or j in blocked:
                continue
            if terrains[military["terrainIds"][i]]["isWater"] and region not in harbors:
                continue
            if terrains[military["terrainIds"][j]]["isWater"] and other not in harbors:
                continue
            region_edges.add((region, other))
    city_edges = {(c["fromCityId"], c["toCityId"]) for c in definition["militaryCityConnections"]}

    def collect(selected):
        edges = set()
        for target in selected:
            home = city_of(target)
            neighbors = {b for a, b in city_edges if a == home}
            for source in selected:
                if source["id"] == target["id"] or not source["canHostPersons"]:
                    continue
                if (city_of(source) == home or source["id"] in neighbors or
                        (region_of(target), region_of(source)) in region_edges and
                        geography_city_of(source) in neighbors):
                    edges.add((source["id"], target["id"]))
        return edges

    active = [s for s in sites if not s["isDormant"] and not s["isDestroyed"] and s["durability"] > 0]
    return {"cityEdges": city_edges, "allEdges": collect(sites), "activeEdges": collect(active),
            "activeIds": {s["id"] for s in active}}


def analyze(scenario_id, cutoff):
    data = read(prepare(scenario_id))
    item = next(s for s in catalog() if s["id"] == scenario_id)
    definition = read(CONTENT / item["package"] / "scenario.json")
    military = definition["military"]
    reference = data["reference"]
    args = (data["width"], data["height"], military["terrainIds"], military["terrains"],
            military["blockedCells"], data["nodes"], reference["landBudget"], reference["waterBudget"])
    statistics = {}
    started = time.perf_counter()
    relations = estimate_connections(*args, maximum_days=cutoff, statistics=statistics)
    seconds = time.perf_counter() - started
    nodes = {n["id"]: n for n in data["nodes"]}
    new_edges = {(e["from"], e["to"]) for e in relations}
    scope = static_attack_scope(definition, nodes)
    old_pairs, new_pairs = pairs(scope["allEdges"]), pairs(new_edges)
    missing, added = old_pairs - new_pairs, new_pairs - old_pairs
    # Unbounded reference costs only explain excluded old candidates. This extra
    # diagnostic pass is excluded from the bounded-search timing above.
    unlimited = estimate_connections(*args, maximum_days=math.inf)
    times = {(e["from"], e["to"]): e["days"] for e in unlimited}
    assert {(a, b) for (a, b), days in times.items() if days <= cutoff} == new_edges

    def pair_time(pair):
        return min(times.get(pair, math.inf), times.get(pair[::-1], math.inf))

    def kind(pair):
        count = sum(nodes[id]["kind"] == "city" for id in pair)
        return ["facility-facility", "city-facility", "city-city"][count]

    def missing_band(pair):
        days = pair_time(pair)
        if not math.isfinite(days):
            return "unreachable"
        if days <= 70:
            return "up-to-70"
        return "70-90" if days <= 90 else "over-90"

    examples = []
    for a, b in [(1, 3), (6, 69), (15, 74), (16, 28), (30, 32),
                 (69, 98), (69, 80), (69, 66), (98, 80), (74, 96)]:
        if a not in nodes or b not in nodes:
            continue
        days = pair_time((a, b))
        examples.append({"from": a, "to": b, "fromName": nodes[a]["name"], "toName": nodes[b]["name"],
                         "days": round(days, 1) if math.isfinite(days) else None,
                         "oldCandidate": (min(a, b), max(a, b)) in old_pairs,
                         "withinCutoff": (min(a, b), max(a, b)) in new_pairs})
    active_new = {e for e in new_edges if all(id in scope["activeIds"] for id in e)}
    n, e = len(nodes), len(relations)
    cells, route_cells = data["width"] * data["height"], statistics["pathCellsSum"]
    script_hash = hashlib.sha256(b"".join(Path(__file__).with_name(name).read_bytes()
        for name in ["analyze.py", "travel.py", "prepare.py"])).hexdigest()
    return {"scenario": data["scenario"], "sourceHash": data["sourceHash"], "scriptHash": script_hash,
            "pythonVersion": platform.python_version(), "cutoffDays": cutoff, "reference": reference,
            "nodes": {"total": n, "cities": sum(v["kind"] == "city" for v in nodes.values()),
                      "otherFacilities": sum(v["kind"] != "city" for v in nodes.values()),
                      "active": len(scope["activeIds"])},
            "relations": scope_summary(new_edges, n),
            "productionStaticScope": {"cities": scope_summary(scope["cityEdges"],
                sum(v["kind"] == "city" for v in nodes.values())),
                "all": scope_summary(scope["allEdges"], n),
                "active": scope_summary(scope["activeEdges"], len(scope["activeIds"])),
                "excludedFilters": ["ownership", "diplomacy", "corps", "readiness", "morale", "troops",
                                    "personnel", "actualRoutes", "strength", "routeTargetRedirection"]},
            "comparison": {"commonPairs": len(old_pairs & new_pairs),
                "commonDirected": len(scope["allEdges"] & new_edges),
                "activeCommonPairs": len(pairs(scope["activeEdges"]) & pairs(active_new)),
                "activeCommonDirected": len(scope["activeEdges"] & active_new),
                "excludedOldPairs": len(missing), "addedPairs": len(added),
                "excludedTimeBands": dict(sorted(Counter(map(missing_band, missing)).items())),
                "excludedKinds": dict(sorted(Counter(map(kind, missing)).items())),
                "addedKinds": dict(sorted(Counter(map(kind, added)).items()))},
            "examples": examples,
            "boundedSearch": {**statistics, "pythonSeconds": round(seconds, 6),
                              "pathCellsMean": round(route_cells / e, 2) if e else 0},
            "compactSizeBytes": {"relationEntries32Bytes": e * 32,
                "allPossibleEntries32Bytes": n * (n - 1) * 32,
                "routeCellIndices": route_cells * 4, "routeOffsets": (e + 1) * 4,
                "cellToRouteIds": route_cells * 4, "cellToRouteOffsets": (cells + 1) * 4,
                "oneDistanceField64Bit": cells * 8, "allDistanceFields64Bit": n * cells * 8,
                "onePredecessorField32Bit": cells * 4}}


def main():
    import json

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scenario", default="19")
    parser.add_argument("--days", type=int, default=MAX_DAYS, choices=range(1, MAX_DAYS + 1))
    args = parser.parse_args()
    report = analyze(args.scenario, args.days)
    output = OUTPUT / f"analysis-{args.scenario}-{args.days}.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{output}\n{report['relations']}\n{report['comparison']}")


if __name__ == "__main__":
    main()
