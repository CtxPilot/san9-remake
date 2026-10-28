"""Verify actual Godot captures and package a continuous preview animation."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageOps

from run_experiment import ROOT


def verify(capture: Path) -> dict:
    errors = (capture / "engine.errors.log").read_text(encoding="utf-8-sig").strip()
    if errors:
        raise AssertionError(errors)
    log = (capture / "engine.log").read_text(encoding="utf-8-sig")
    if "FLAG_DIRECTION_CAPTURE_OK" not in log:
        raise AssertionError("Godot did not complete the capture sequence")
    checks = {}
    original_box = (120, 310, 740, 715)
    final_box = (860, 310, 1480, 715)
    for first, second in [("repeat-a", "repeat-b"), ("cycle-start", "cycle-end")]:
        with Image.open(capture / f"{first}.png") as a, Image.open(capture / f"{second}.png") as b:
            np.testing.assert_array_equal(np.asarray(a), np.asarray(b))
        checks[f"{first}/{second}"] = "pixel-identical"
    with Image.open(capture / "zero-wind-a.png") as a, Image.open(capture / "zero-wind-b.png") as b:
        np.testing.assert_array_equal(np.asarray(a.crop(final_box)), np.asarray(b.crop(final_box)))
        assert a.crop(original_box).tobytes() != b.crop(original_box).tobytes(), "Original reference must retain its independent four-frame animation"
    checks["zero_wind_final_panel"] = "pixel-identical"
    checks["original_animation_independent_of_wind"] = "different source frames"
    audit = json.loads((capture / "direction-audit.json").read_text(encoding="utf-8"))
    assert [entry["direction"] for entry in audit] == list(range(8))
    assert [entry["show_emblem"] for entry in audit] == [True, True, True, False, True, True, True, False]
    direction_hashes = {"original": [], "final": []}
    tiles = Image.new("RGB", (1600, 680), (32, 43, 48))
    draw = ImageDraw.Draw(tiles)
    for direction in range(8):
        with Image.open(capture / f"direction-{direction}.png") as frame:
            for name, box in [("original", original_box), ("final", final_box)]:
                crop = frame.crop(box).convert("RGB")
                direction_hashes[name].append(hashlib.sha256(crop.tobytes()).hexdigest())
                column = 0 if name == "original" else 1
                bounds = ImageChops.difference(crop, Image.new("RGB", crop.size, (32, 43, 48))).getbbox()
                assert bounds is not None
                crop = crop.crop((max(0, bounds[0] - 8), max(0, bounds[1] - 8), min(crop.width, bounds[2] + 8), min(crop.height, bounds[3] + 8)))
                thumbnail = ImageOps.contain(crop, (198, 270), Image.Resampling.LANCZOS)
                tiles.paste(thumbnail, ((direction % 4) * 400 + column * 200 + (198 - thumbnail.width) // 2, (direction // 4) * 340 + 40))
        draw.text(((direction % 4) * 400 + 14, (direction // 4) * 340 + 12), f"direction {direction}: original / final", fill="white")
    assert all(len(set(values)) == 8 for values in direction_hashes.values()), "Both flag panels must actually show eight distinct poses"
    tiles.save(capture / "all-directions.png")
    checks["direction_buttons_and_rendered_poses"] = "8 original + 8 final"
    dark_hashes = {"original": [], "final": []}
    for direction in range(8):
        with Image.open(capture / f"dark-direction-{direction}.png") as frame:
            for name, box in [("original", original_box), ("final", final_box)]:
                dark_hashes[name].append(hashlib.sha256(frame.crop(box).tobytes()).hexdigest())
    assert all(len(set(values)) == 8 for values in dark_hashes.values())
    assert all(a != b for name in direction_hashes for a, b in zip(direction_hashes[name], dark_hashes[name]))
    checks["gold_and_dark_styles"] = "both panels change across all eight directions"
    palette_audit = json.loads((capture / "palette-audit.json").read_text(encoding="utf-8"))
    assert [entry["color"] for entry in palette_audit["colors"]] == list(range(51))
    assert [entry["show_emblem"] for entry in palette_audit["colors"]] == [True] * 50 + [False]
    assert {entry["glyph_ink"] for entry in palette_audit["colors"]} == {0, 1}
    for key in ("automatic_wrap", "pause_stops_both_clocks", "manual_selection_stops_auto"):
        assert palette_audit[key] is True
    color_hashes = {"original": [], "final": []}
    samples = [0, 2, 3, 5, 6, 50]
    color_tiles = Image.new("RGB", (1200, 660), (32, 43, 48))
    color_draw = ImageDraw.Draw(color_tiles)
    for color in range(51):
        with Image.open(capture / f"color-{color:02d}.png") as frame:
            for name, box in [("original", original_box), ("final", final_box)]:
                crop = frame.crop(box).convert("RGB")
                color_hashes[name].append(hashlib.sha256(crop.tobytes()).hexdigest())
                if color in samples:
                    index = samples.index(color)
                    thumbnail = crop.resize((300, 196), Image.Resampling.LANCZOS)
                    color_tiles.paste(thumbnail, ((index % 2) * 600 + (0 if name == "original" else 300), (index // 2) * 220 + 24))
        if color in samples:
            index = samples.index(color)
            color_draw.text(((index % 2) * 600 + 12, (index // 2) * 220 + 6), f"color {color:02d}: original / final", fill="white")
    assert all(len(set(values)) == 51 for values in color_hashes.values()), "Both panels must render all 51 distinct palettes"
    color_tiles.save(capture / "color-comparison.png")
    checks["palette_selection_and_rendering"] = "51 original + 51 final, both ink styles and neutral no-glyph"
    checks["automatic_color_controls"] = "wrap, pause and manual override passed"
    motion = json.loads((capture / "motion-settings.json").read_text(encoding="utf-8"))
    wind_spans = {}
    for direction in (1, 3, 5, 7):
        samples = []
        for strength in range(3):
            centroids = []
            bounds = []
            for phase in range(12):
                with Image.open(capture / f"wind-{direction}-{strength}-{phase:02d}.png") as image:
                    rgb = np.asarray(image).astype(float)[..., :3]
                weights = np.maximum(rgb[..., 2] - np.maximum(rgb[..., 0], rgb[..., 1]) - 8, 0)
                yy, xx = np.mgrid[:weights.shape[0], :weights.shape[1]]
                assert weights.sum() > 0
                centroids.append([(xx * weights).sum() / weights.sum(), (yy * weights).sum() / weights.sum()])
                by, bx = np.where(weights > 32)
                bounds.append([bx.min(), by.min(), bx.max(), by.max()])
            span = np.ptp(centroids, axis=0) / 8  # Eight display pixels per reference unit.
            edge_span = np.ptp(bounds, axis=0) / 8
            # The center of visible coverage jumps when a pole occludes a thin
            # edge-on flag. Corner/edge excursions measure the observed swing.
            samples.append({"strength": [0.5, 1.0, 2.0][strength], "centroid_span": span.tolist(), "edge_span": edge_span.tolist(), "edge_excursion": float(edge_span.max())})
        lengths = [sample["edge_excursion"] for sample in samples]
        assert lengths[1] > lengths[0] * 1.15 and lengths[2] > lengths[1] * 1.15, (direction, lengths)
        wind_spans[str(direction)] = samples
    frontal = np.mean([wind_spans[str(d)][1]["edge_excursion"] for d in (1, 5)])
    edge_on = np.mean([wind_spans[str(d)][1]["edge_excursion"] for d in (3, 7)])
    assert frontal > 0.3, "Default frontal motion must be visibly present"
    assert edge_on / frontal < 4, "Edge-on calibration must not multiply wind amplitude"
    checks["wind_strength_response"] = "weak < default < strong in front/back/edge views"
    checks["edge_to_front_motion_ratio"] = float(edge_on / frontal)
    images = []
    hashes = []
    for index in range(36):
        with Image.open(capture / f"frame-{index:03d}.png") as image:
            hashes.append(hashlib.sha256(image.tobytes()).hexdigest())
            images.append(image.convert("RGB").crop((0, 0, 1600, 820)).resize((1200, 615), Image.Resampling.LANCZOS))
    if len(set(hashes)) != 36:
        raise AssertionError("Expected 36 distinct continuously generated poses")
    cycle_ms = round(motion["period_seconds"] * 1000)
    durations = [round((i + 1) * cycle_ms / 36) - round(i * cycle_ms / 36) for i in range(36)]
    images[0].save(capture / "preview.png", save_all=True, append_images=images[1:], duration=durations, loop=0, disposal=1, blend=0)
    # GIF timing is quantized to 10ms. Keep total period close to the APNG.
    gif_units = round(cycle_ms / 10)
    gif_durations = [round((i + 1) * gif_units / 36) * 10 - round(i * gif_units / 36) * 10 for i in range(36)]
    images[0].save(capture / "preview.gif", save_all=True, append_images=images[1:], duration=gif_durations, loop=0, disposal=2)
    images[0].save(capture / "comparison.png")
    report = {
        "renderer": log.splitlines()[1],
        "capture_frames": 36,
        "distinct_poses": len(set(hashes)),
        "cycle_milliseconds": sum(durations),
        "gif_cycle_milliseconds": sum(gif_durations),
        "checks": checks,
        "frame_hashes": hashes,
        "direction_hashes": direction_hashes,
        "dark_style_hashes": dark_hashes,
        "color_hashes": color_hashes,
        "motion": motion,
        "wind_spans": wind_spans,
    }
    (capture / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture", type=Path, default=ROOT / "local/generated/research/flag-generation/alignment-capture")
    args = parser.parse_args()
    if not args.capture.resolve().is_relative_to((ROOT / "local/generated").resolve()):
        parser.error("Capture directory must stay under local/generated")
    report = verify(args.capture)
    print(json.dumps({key: report[key] for key in ("capture_frames", "distinct_poses", "checks")}, indent=2))
