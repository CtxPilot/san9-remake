"""Summarize explicitly selected original-game capture sessions without changing raw logs."""
from __future__ import annotations

import argparse
from collections import defaultdict
from datetime import datetime
import json
from pathlib import Path
from statistics import median


# Same source glyph identities as tools/font-s9-generator/generate_font_s9.py.
PRIVATE_NAME_CHARACTERS = dict(zip(range(0xFA40, 0xFA46), "伷璝寗勑𩅦儁"))


def decode_person_name(encoded: str) -> str:
    raw = bytes.fromhex(encoded)
    if len(raw) != 10:
        raise ValueError("Expected two five-byte name fields")
    result = []
    for field in (raw[:5], raw[5:]):
        field = field.split(b"\0", 1)[0]
        index = 0
        while index < len(field):
            width = 2 if field[index] >= 0x81 else 1
            token = field[index:index + width]
            result.append(PRIVATE_NAME_CHARACTERS.get(int.from_bytes(token, "big"))
                          or token.decode("cp950", errors="strict"))
            index += width
    return "".join(result)


def summarize_opening(turn, path):
    if turn["opening_errors"]:
        raise ValueError(f"Opening attribution errors: {path}: {turn['opening_errors']}")
    start = turn["start_date"]["code"]
    actors = [a for a in turn["automatic_actors"] if a["date"]["code"] == start]
    for actor in actors:
        for role in ("leader", "ruler"):
            actor[role + "_name"] = decode_person_name(actor[role + "_name_big5_hex"])
    actors_by_slot = {a["slot"]: a for a in actors}
    if len(actors_by_slot) != len(actors):
        raise ValueError(f"Repeated actor slot: {path}")
    phase = turn["ai_phase_end"]
    if phase["date"]["code"] != start or phase["actors_active"] or phase["actors_completed"] != len(actors):
        raise ValueError(f"AI phase has unmatched task lifetimes: {path}")
    for actor in actors:
        if actor["since_start_ms"] + actor["elapsed_ms"] > phase["since_start_ms"]:
            raise ValueError(f"Automatic actor extends beyond AI phase: {path}: {actor['slot']}")
    events = [e for e in turn["opening_events"] if e["date"]["code"] == start]
    names = ["strategy_work62", "personnel_work22", "combined_corps_preparation"]
    preparation = {name: {e["slot"]: e for e in events if e["name"] == name} for name in names}
    for name in names:
        records = [e for e in events if e["name"] == name]
        if sorted(e["slot"] for e in records) != sorted(a["slot"] for a in actors):
            raise ValueError(f"Preparation ownership mismatch: {path}: {name}")
        for event in records:
            actor = actors_by_slot[event["slot"]]
            if (event["since_start_ms"] < actor["since_start_ms"]
                    or event["since_start_ms"] + event["elapsed_ms"] > actor["since_start_ms"] + actor["elapsed_ms"]):
                raise ValueError(f"Preparation outside automatic actor lifetime: {path}: {event}")
    rows = []
    for actor in actors:
        row = {k: actor[k] for k in ["slot", "root_slot", "is_root", "leader_name", "ruler_name", "elapsed_ms", "thread_cpu_ms"]}
        for name in names:
            row[name + "_ms"] = preparation[name][actor["slot"]]["elapsed_ms"]
        row["after_preparation_wall_ms"] = row["elapsed_ms"] - row["combined_corps_preparation_ms"]
        if row["after_preparation_wall_ms"] < 0:
            raise ValueError(f"Preparation exceeds full actor lifetime: {path}: {row}")
        rows.append(row)
    return {"source": str(path), "run": turn["run"], "date_code": start, "ai_phase": phase, "actors": rows,
            "actor_count": len(rows), "root_count": len({r["root_slot"] for r in rows}),
            "totals": {key: sum(r[key] for r in rows) for key in
                       ["elapsed_ms", "thread_cpu_ms", "strategy_work62_ms", "personnel_work22_ms", "combined_corps_preparation_ms", "after_preparation_wall_ms"]},
            "global_and_world_events": [e for e in turn["opening_events"] if "slot" not in e]}


def summarize_owners(runs):
    identities = [{(r["slot"], r["root_slot"], r["leader_name"], r["ruler_name"]) for r in run["actors"]} for run in runs]
    if any(identity != identities[0] for identity in identities):
        raise ValueError("Actor identities changed across repeated baseline loads")
    by_root = defaultdict(list)
    for run in runs:
        grouped = defaultdict(list)
        for actor in run["actors"]:
            grouped[actor["root_slot"]].append(actor)
        for root, actors in grouped.items():
            by_root[root].append({"ruler_name": actors[0]["ruler_name"], "corps_count": len(actors),
                                  **{key: sum(a[key] for a in actors) for key in run["totals"]}})
    roots = [{"root_slot": root, "ruler_name": samples[0]["ruler_name"], "corps_count": samples[0]["corps_count"],
              **{key: {"values": [s[key] for s in samples], "median": median(s[key] for s in samples)} for key in runs[0]["totals"]}}
             for root, samples in by_root.items()]
    return {"runs": runs, "by_root": sorted(roots, key=lambda r: -r["elapsed_ms"]["median"])}


def distribution(values):
    return {"samples": len(values), "minimum": min(values), "median": median(values), "maximum": max(values)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sessions", nargs="+", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    runs = []
    opening_runs = []
    for directory in args.sessions:
        ready = json.loads((directory / "ready.json").read_text(encoding="utf-8-sig"))
        events = [json.loads(line) for line in (directory / "events.jsonl").read_text(encoding="utf-8-sig").splitlines()]
        for path in sorted(directory.glob("turn-*.json")):
            turn = json.loads(path.read_text(encoding="utf-8-sig"))
            if not turn["completed"] or turn["stack_overflow"] or turn["truncated_outer_callbacks"]:
                raise ValueError(f"Incomplete or unreliable turn: {path}")
            days = turn["days"]
            start = turn["start_date"]["code"]
            if [d["code"] for d in days] != list(range(start + 1, start + 11)) or turn["end_date"]["code"] != start + 10:
                raise ValueError(f"Not a full ten-day turn: {path}")
            if ready["mode"] == "opening":
                opening_runs.append(summarize_opening(turn, path))
            if days[-1].get("since_start_ms") is not None:
                date_ms = days[-1]["since_start_ms"]
                precision = "qpc"
            else:
                last = [e for e in events if e.get("kind") == "day" and e["run"] == turn["run"]][-1]
                date_ms = (datetime.fromisoformat(last["utc"]) - datetime.fromisoformat(turn["started_utc"])).total_seconds() * 1000
                precision = "utc_millisecond"
            runs.append({"source": str(path), "mode": ready["mode"], "label": turn["label"],
                         "elapsed_ms": turn["elapsed_ms"], "main_thread_cpu_ms": turn["main_thread_cpu_ms"],
                         "date_advanced_ten_days_ms": date_ms, "date_timer": precision,
                         "date_advanced_thread_cpu_ms": days[-1].get("thread_cpu_ms"),
                         "probes": turn["probes"], "date_code_start": start,
                         "start_date": {"year": start // 360 + 130, "month": start // 30 % 12 + 1, "day": start % 30 + 1}})
    if not runs:
        raise ValueError("No completed turns")
    result = {"runs": runs, "modes": {}}
    if opening_runs:
        result["opening"] = summarize_owners(opening_runs)
    for mode in sorted({r["mode"] for r in runs}):
        group = [r for r in runs if r["mode"] == mode]
        stats = {metric: distribution([r[metric] for r in group]) for metric in
                 ["elapsed_ms", "main_thread_cpu_ms", "date_advanced_ten_days_ms"]}
        names = sorted({p["name"] for r in group for p in r["probes"]})
        stats["probes"] = {name: {metric: distribution([p[metric] for r in group for p in r["probes"] if p["name"] == name])
                                  for metric in ["calls", "inclusive_ms", "exclusive_ms", "maximum_ms"]} for name in names}
        result["modes"][mode] = stats
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"runs": len(runs), "modes": {k: {m: v[m] for m in ["elapsed_ms", "main_thread_cpu_ms", "date_advanced_ten_days_ms"]}
                                               for k, v in result["modes"].items()}}, ensure_ascii=False))


if __name__ == "__main__":
    main()
