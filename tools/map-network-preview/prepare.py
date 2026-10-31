"""Prepare a map/building projection from converted content; no force or troop fields."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path

from travel import MAX_DAYS, estimate_connections

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "local/generated/content"
OUTPUT = ROOT / "local/generated/map-network-preview"
SCHEMA = 8
# Explicit content references for the preview's chosen movement profile.
REFERENCE_LAND_ID = 8
REFERENCE_WATER_ID = 22


def read(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def catalog(content: Path = CONTENT):
    result = []
    for item in read(content / "catalog.json")["scenarios"]:
        package = content / item["package"]
        info = read(package / "content.json")["preview"]
        names = read(package / "messages/text.json")["entries"]
        result.append({"id": item["id"], "package": item["package"],
                       "title": names["scenario.title"], "year": info["year"],
                       "category": "历史" if info["categoryId"] == "historical" else "虚构"})
    return result


def prepare(scenario_id: str, content: Path = CONTENT, output: Path = OUTPUT):
    item = next((s for s in catalog(content) if s["id"] == scenario_id), None)
    if item is None:
        raise ValueError("Unknown converted scenario")
    package = content / item["package"]
    paths = [package / "scenario.json", package / "map.json", package / "messages/text.json"]
    digest = hashlib.sha256(b"".join(p.read_bytes() for p in paths)).hexdigest()
    target = output / f"map-{scenario_id}.json"
    if target.exists():
        old = read(target)
        if old.get("sourceHash") == digest and old.get("schema") == SCHEMA:
            return target
    # The current converted contract keeps geography in scenario.json. Project only
    # static map definitions/references; never access settlements, forces or officers.
    source = read(paths[0])
    geography = source["military"]
    region_cities = source["regionCityIds"]
    region_keys = source["regionNameKeys"]
    del source
    names = read(paths[2])["entries"]
    buildings = read(paths[1])["buildings"]
    width, height = geography["width"], geography["height"]
    region_ids = geography["regionIds"]
    types = {f["id"]: f for f in geography["facilities"]}
    nodes = []
    for building in buildings:
        region = region_ids[building["y"] * width + building["x"]]
        facility = types[building["fixedType"]]
        kind = ("city" if building["fixedType"] == 0 else "port" if facility["isHarbor"]
                else "fort" if not facility["buildable"] else "facility")
        region_name = names[region_keys[str(region)]]
        label = names.get(f"settlement.{building['id']}", region_name)
        nodes.append({"id": building["id"], "name": label, "region": region,
                      "regionName": region_name, "city": region_cities.get(str(region)),
                      "x": building["x"], "y": building["y"], "type": building["fixedType"], "kind": kind})
    regions = [{"id": int(key), "name": names[name], "city": region_cities.get(key)}
               for key, name in region_keys.items()]
    land = next(f for f in geography["formations"] if f["id"] == REFERENCE_LAND_ID)
    naval = next(f for f in geography["formations"] if f["id"] == REFERENCE_WATER_ID)
    if land["travelMode"] != 0 or naval["travelMode"] != 1:
        raise ValueError("Reference formations must use land and water travel respectively")
    connections = estimate_connections(width, height, geography["terrainIds"], geography["terrains"],
                                       geography["blockedCells"], nodes, land["movementBudget"], naval["movementBudget"])
    blocked = {p["y"] * width + p["x"] for p in geography["geography"]["blockedCells"]}
    payload = {"schema": SCHEMA, "sourceHash": digest, "scenario": {k: item[k] for k in ["id", "title", "year", "category"]},
               "width": width, "height": height, "nodes": nodes, "regions": regions,
               "connections": connections,
               "reference": {"landName": names[f"formation.{land['id']}"],
                             "waterName": names[f"formation.{naval['id']}"],
                             "landBudget": land["movementBudget"], "waterBudget": naval["movementBudget"],
                             "maximumDays": MAX_DAYS, "unitsPerCell": 5},
               "grid": {"terrain": geography["terrainIds"], "elevation": geography["elevations"],
                        "region": region_ids, "blocked": sorted(blocked)},
               "terrains": geography["terrains"]}
    output.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    return target


if __name__ == "__main__":
    print(prepare("19"))
