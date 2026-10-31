"""Check geographic projection and building differences on real converted inputs."""
import json

from prepare import prepare


def main():
    maps = [json.loads(prepare(id).read_text(encoding="utf-8")) for id in ["0", "19"]]
    forbidden = {"forces", "owner", "ownerId", "soldiers", "officers", "settlements", "morale"}

    def check(value):
        if isinstance(value, dict):
            assert not (set(value) & forbidden), "Runtime force/troop data leaked into projection"
            for v in value.values():
                check(v)
        elif isinstance(value, list):
            for v in value:
                check(v)

    for data in maps:
        check(data)
        assert data["reference"]["landName"] == "井阑"
        assert data["reference"]["waterName"] == "楼船"
        assert data["reference"]["landBudget"] == 45
        assert data["reference"]["waterBudget"] == 50
        assert data["reference"]["maximumDays"] == 65
        nodes = {n["id"]: n for n in data["nodes"]}
        assert len(nodes) == len(data["nodes"])
        assert len(data["grid"]["region"]) == data["width"] * data["height"]
        assert all(e["from"] in nodes and e["to"] in nodes for e in data["connections"])
        assert any(e["from"] == 69 and e["to"] == 98 and 0 < e["days"] <= 30 for e in data["connections"])
        assert all(0 <= e["days"] <= data["reference"]["maximumDays"] for e in data["connections"])
        assert all(n["kind"] != "region" for n in data["nodes"])
        assert nodes[69]["city"] == 5 and nodes[98]["city"] == 15
        assert any(e["from"] == 5 and e["to"] == 69 for e in data["connections"])
        assert any(e["from"] == 5 and e["to"] == 98 for e in data["connections"])
    assert maps[0]["grid"] == maps[1]["grid"], "Shared geography unexpectedly changed"
    assert len(maps[0]["nodes"]) != len(maps[1]["nodes"])
    assert len(maps[1]["connections"]) == 1242
    assert len({tuple(sorted((e["from"], e["to"]))) for e in maps[1]["connections"]}) == 621
    assert len([e for e in maps[1]["connections"] if e["days"] <= 60]) == 1062
    print("Verified: two scenario building configurations, geography, references, Baima/Guandu, no runtime force data.")


if __name__ == "__main__":
    main()
