"""Compare bounded and all-facility travel tables using the same search implementation."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import platform
import statistics
import time

from prepare import CONTENT, OUTPUT, catalog, prepare, read
from travel import MAX_DAYS, estimate_connections


def load_case(scenario_id):
    data = read(prepare(scenario_id))
    entry = next(s for s in catalog() if s["id"] == scenario_id)
    military = read(CONTENT / entry["package"] / "scenario.json")["military"]
    args = (data["width"], data["height"], military["terrainIds"], military["terrains"],
            military["blockedCells"], data["nodes"], data["reference"]["landBudget"], data["reference"]["waterBudget"])
    return data, args


def export_csharp_fixture(scenario_id):
    data, args = load_case(scenario_id)
    width, height, terrain_ids, terrains, blocked, nodes, land, water = args
    full = estimate_connections(*args, maximum_days=math.inf)
    bounded = estimate_connections(*args)
    assert bounded == data["connections"]
    directory = Path(__file__).resolve().parent
    output = OUTPUT / f"csharp-benchmark-input-{scenario_id}.json"
    payload = {"scenario": data["scenario"], "sourceHash": data["sourceHash"],
        "pythonModelHash": hashlib.sha256(b"".join((directory / f).read_bytes()
            for f in ["benchmark.py", "travel.py", "prepare.py"])).hexdigest(),
        "width": width, "height": height, "terrainIds": terrain_ids, "terrains": terrains,
        "blockedCells": blocked, "nodes": [{k: n[k] for k in ["id", "x", "y"]} for n in nodes],
        "landBudget": land, "waterBudget": water, "boundedDays": MAX_DAYS,
        "expected": {"bounded": bounded, "allFacilities": full}}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    return output


def benchmark(scenario_id, runs):
    data, args = load_case(scenario_id)
    modes = {"bounded": MAX_DAYS, "allFacilities": math.inf}
    records, timings, work = {}, {mode: [] for mode in modes}, {}
    # One warmup per mode; timing includes graph/workspace construction, all source
    # searches and result sorting. Loading, JSON serialization and validation are outside.
    for mode, days in modes.items():
        records[mode] = estimate_connections(*args, maximum_days=days)
    bounded, full = records["bounded"], records["allFacilities"]
    assert bounded == data["connections"], "Bounded results changed after target termination"
    assert bounded == [e for e in full if e["days"] <= MAX_DAYS], "Full costs disagree with bounded search"
    for iteration in range(runs):
        # Alternate execution order to reduce systematic warmup/order effects.
        order = list(modes) if iteration % 2 == 0 else list(reversed(modes))
        for mode in order:
            started = time.perf_counter()
            result = estimate_connections(*args, maximum_days=modes[mode])
            timings[mode].append(time.perf_counter() - started)
            assert result == records[mode], "Non-deterministic relation table"
    # Work counters collect hop lengths and add overhead; measure them separately.
    for mode, days in modes.items():
        work[mode] = {}
        assert estimate_connections(*args, maximum_days=days, statistics=work[mode]) == records[mode]
    sizes = {mode: len(edges) for mode, edges in records.items()}
    count = len(data["nodes"])
    directory = Path(__file__).resolve().parent
    return {"scenario": data["scenario"], "sourceHash": data["sourceHash"],
            "scriptHash": hashlib.sha256(b"".join((directory / f).read_bytes()
                for f in ["benchmark.py", "travel.py", "prepare.py"])).hexdigest(),
            "environment": {"python": platform.python_version(), "platform": platform.platform()},
            "facilities": count, "gridCells": data["width"] * data["height"],
            "reference": data["reference"], "runsPerMode": runs, "warmupsPerMode": 1,
            "boundedDays": MAX_DAYS, "unreachableDirectedPairs": count * (count - 1) - sizes["allFacilities"],
            "results": {mode: {"seconds": [round(v, 6) for v in values],
                "medianSeconds": round(statistics.median(values), 6),
                "minimumSeconds": round(min(values), 6), "maximumSeconds": round(max(values), 6),
                "directedRelations": sizes[mode], "work": work[mode]}
                for mode, values in timings.items()},
            "medianTimeRatio": round(statistics.median(timings["allFacilities"]) /
                statistics.median(timings["bounded"]), 3),
            "checks": {"boundedMatchesPreparedTable": True, "boundedIsFullCostSubset": True,
                       "allRunsIdentical": True},
            "excludes": ["fileIO", "jsonSerialization", "routeConstruction", "aiRules", "csharpRuntime"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scenario", default="19")
    parser.add_argument("--runs", type=int, default=5, choices=range(2, 21))
    parser.add_argument("--export-csharp", action="store_true", help="Export inputs and Python expected costs without timing")
    args = parser.parse_args()
    if args.export_csharp:
        print(export_csharp_fixture(args.scenario))
        return
    report = benchmark(args.scenario, args.runs)
    output = OUTPUT / f"full-table-benchmark-{args.scenario}.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
